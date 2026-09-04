using System.Diagnostics;
using System.Globalization;

namespace Aetherstream.Playback;

/// <summary>Where a broadcast is pushed, and where the room watches it.</summary>
/// <param name="Server">Host running MediaMTX.</param>
/// <param name="Path">The stream path — also the read secret, so it is long and random.</param>
/// <param name="PublishPass">Publish credential.</param>
/// <param name="SrtPassphrase">Encrypts the SRT ingest.</param>
/// <param name="WatchHost">
/// Host and port the room plays from. Carries the port because the stream is served over an
/// HTTP/1.1-only listener — libvlc cannot fetch HLS through HTTP/2.
/// </param>
public sealed record BroadcastTarget(
    string Server,
    string Path,
    string PublishPass,
    string SrtPassphrase,
    string WatchHost)
{
    /// <summary>
    /// <paramref name="PublishPass"/> is this install's own key. The relay asks the party service
    /// whether that user owns the group this path belongs to, so a stranger holding the path -- or
    /// a member of the same party -- still cannot publish to it.
    /// </summary>
    public static BroadcastTarget ForGroup(string relay, string path, string key, string srtPassphrase, string watchHost) =>
        new(relay, path, key, srtPassphrase, watchHost);

    public bool IsComplete =>
        this.Server.Length > 0 && this.Path.Length > 0 && this.PublishPass.Length > 0;

    /// <summary>Contains the publish credentials, so it is never shown or logged.</summary>
    public string IngestUrl =>
        $"srt://{this.Server}:8890?streamid=publish:{this.Path}:user:{this.PublishPass}"
        + (this.SrtPassphrase.Length > 0 ? $"&passphrase={this.SrtPassphrase}" : string.Empty)
        + "&pkt_size=1316";

    /// <summary>Safe to share: the path is the only secret in it, and sharing it is the point.</summary>
    public string WatchUrl => $"https://{this.WatchHost}/{this.Path}/index.m3u8";

}

/// <summary>What ffmpeg found in the source, and whether it can be sent without re-encoding.</summary>
public readonly record struct SourceProbe(string VideoCodec, string AudioCodec, double KeyframeGapSeconds)
{
    /// <summary>
    /// HLS cuts segments only on keyframes. A source whose keyframes are further apart than the
    /// segment duration cannot fill a segment, and the result is not an error — the player reports
    /// itself as playing and then delivers no frames at all. Re-encoding is the only way through.
    /// </summary>
    public const double MaxKeyframeGapSeconds = 4d;

    public bool CanCopy =>
        this.VideoCodec == "h264"
        && (this.AudioCodec is "aac" or "mp3")
        && (this.KeyframeGapSeconds <= 0 || this.KeyframeGapSeconds <= MaxKeyframeGapSeconds);

    public bool KeyframesTooFarApart =>
        this.KeyframeGapSeconds > MaxKeyframeGapSeconds;
}

/// <summary>
/// Pushes one file or URL to the party relay, as a child ffmpeg process.
/// <para>
/// Nothing here touches the render thread: starting is a process spawn, and progress arrives on a
/// reader task that only ever writes to volatile fields the UI reads.
/// </para>
/// </summary>
public sealed class BroadcastSession : IDisposable
{
    private readonly object gate = new();
    private Process? process;
    private volatile string status = string.Empty;
    private volatile string? error;
    private volatile bool copying;
    private DateTime startedUtc;
    private bool disposed;

    /// <summary>Where progress and failures are written, so a failed push can be diagnosed.</summary>
    public Action<string>? Log { get; set; }

    public bool IsRunning
    {
        get
        {
            lock (this.gate)
                return this.process is { HasExited: false };
        }
    }

    /// <summary>The most recent progress line from ffmpeg, already tidied for display.</summary>
    public string Status => this.status;

    public string? Error => this.error;

    /// <summary>True when the streams are being copied rather than re-encoded.</summary>
    public bool IsCopying => this.copying;

    public TimeSpan Elapsed =>
        this.startedUtc == default ? TimeSpan.Zero : DateTime.UtcNow - this.startedUtc;

    /// <summary>
    /// Asks ffprobe what is in the source. Returns a probe with empty codecs when ffprobe is absent
    /// or the source cannot be read — the caller then falls back to re-encoding, which always works.
    /// </summary>
    public static async Task<SourceProbe> ProbeAsync(string input, CancellationToken ct)
    {
        var video = await RunProbeAsync(
            $"-v error -select_streams v:0 -show_entries stream=codec_name -of csv=p=0 -- \"{input}\"", ct);

        var audio = await RunProbeAsync(
            $"-v error -select_streams a:0 -show_entries stream=codec_name -of csv=p=0 -- \"{input}\"", ct);

        // Only the first seconds are inspected, so this stays cheap on a feature-length file.
        var keyframes = await RunProbeAsync(
            "-v error -select_streams v:0 -skip_frame nokey -read_intervals \"%+12\" " +
            $"-show_entries frame=pts_time -of csv=p=0 -- \"{input}\"", ct);

        return new SourceProbe(
            video.Trim().ToLowerInvariant(),
            audio.Trim().ToLowerInvariant(),
            KeyframeGap(keyframes));
    }

    /// <summary>
    /// Mean spacing of the keyframes ffprobe reported. The CSV writer leaves a trailing comma on the
    /// first row, which has to be trimmed — otherwise the timestamp that anchors the interval is
    /// discarded and the check silently never fires.
    /// </summary>
    private static double KeyframeGap(string csv)
    {
        var times = new List<double>();

        foreach (var line in csv.Split('\n'))
        {
            var text = line.Trim().TrimEnd(',');
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                times.Add(value);
        }

        return times.Count >= 2 ? (times[^1] - times[0]) / (times.Count - 1) : 0d;
    }

    private static async Task<string> RunProbeAsync(string arguments, CancellationToken ct)
    {
        try
        {
            using var probe = Process.Start(new ProcessStartInfo("ffprobe", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (probe is null)
                return string.Empty;

            // Both pipes are drained concurrently. Reading one to completion while the other fills
            // deadlocks the child — the same trap the yt-dlp resolver had to be fixed for.
            var output = probe.StandardOutput.ReadToEndAsync(ct);
            var errors = probe.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(output, errors);
            await probe.WaitForExitAsync(ct);

            return await output;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Starts pushing. Replaces any push already running, because two publishers on one path is not
    /// something the relay can make sense of.
    /// </summary>
    public void Start(BroadcastTarget target, string input, SourceProbe probe, TimeSpan? startAt = null)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (!target.IsComplete)
        {
            this.error = "The party server is not configured yet.";
            return;
        }

        if (input.Trim().Length == 0)
        {
            this.error = "Nothing to broadcast — give it a file or a URL.";
            return;
        }

        this.Stop();

        var copy = probe.CanCopy;
        var arguments = BuildArguments(target, input.Trim(), probe, startAt);

        try
        {
            var started = Process.Start(new ProcessStartInfo("ffmpeg", arguments)
            {
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (started is null)
            {
                this.error = "Could not start ffmpeg. Is it on PATH?";
                return;
            }

            lock (this.gate)
                this.process = started;

            this.error = null;
            this.copying = copy;
            this.startedUtc = DateTime.UtcNow;
            this.status = copy ? "starting — copying streams" : "starting — re-encoding";

            _ = Task.Run(() => this.ReadProgressAsync(started));
        }
        catch (Exception ex)
        {
            this.error = $"Could not start ffmpeg: {ex.Message}";
            this.Log?.Invoke($"[broadcast] start failed: {ex}");
        }
    }

    /// <summary>
    /// ffmpeg writes progress to stderr, so it has to be drained whether or not anyone is reading —
    /// a full pipe stops the encode dead.
    /// </summary>
    private async Task ReadProgressAsync(Process running)
    {
        try
        {
            while (await running.StandardError.ReadLineAsync() is { } line)
            {
                if (line.StartsWith("frame=", StringComparison.Ordinal))
                {
                    this.status = Tidy(line);
                }
                else if (line.Length > 0)
                {
                    this.Log?.Invoke($"[broadcast] {line}");

                    // ffmpeg reports fatal problems on the same stream as progress; surfacing them
                    // is the difference between "it stopped" and knowing why.
                    if (line.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("No such file", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("Invalid data", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("Server error", StringComparison.OrdinalIgnoreCase))
                    {
                        this.error = line.Trim();
                    }
                }
            }

            await running.WaitForExitAsync();

            if (running.ExitCode != 0 && this.error is null)
                this.error = $"ffmpeg stopped with code {running.ExitCode}.";

            this.status = string.Empty;
        }
        catch (Exception ex)
        {
            this.Log?.Invoke($"[broadcast] reader ended: {ex.Message}");
        }
    }

    /// <summary>Turns ffmpeg's progress line into the two numbers worth showing.</summary>
    private static string Tidy(string line)
    {
        var time = Field(line, "time=");
        var speed = Field(line, "speed=");
        var bitrate = Field(line, "bitrate=");

        return time.Length == 0
            ? line.Trim()
            : $"sent {time}  ·  {bitrate}  ·  {speed}";

        static string Field(string text, string key)
        {
            var start = text.IndexOf(key, StringComparison.Ordinal);
            if (start < 0)
                return string.Empty;

            start += key.Length;
            while (start < text.Length && text[start] == ' ')
                start++;

            var end = start;
            while (end < text.Length && text[end] != ' ')
                end++;

            return text[start..end];
        }
    }

    private static string BuildArguments(
        BroadcastTarget target,
        string input,
        SourceProbe probe,
        TimeSpan? startAt)
    {
        // -re paces the file at real time. Without it ffmpeg sends the whole film upstream as fast
        // as the link allows, and viewers get nothing usable.
        var arguments = "-hide_banner -loglevel warning -stats -re";

        if (startAt is { } from && from > TimeSpan.Zero)
            arguments += $" -ss {from.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}";

        // Survive the source connection dropping. A remote Plex file is pulled over HTTPS for the
        // length of a whole film, and that connection does not reliably last: observed ending at
        // 998 MB of a 3.07 GB file, after which ffmpeg reads corrupt packets, gives up, and every
        // viewer freezes because the relay has lost its publisher.
        //
        // Without these ffmpeg does not retry at all — a truncated stream simply looks like the end
        // of the file to it. They have to precede -i to apply to the input.
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            arguments += " -reconnect 1 -reconnect_on_network_error 1"
                + " -reconnect_streamed 1 -reconnect_delay_max 10";
        }

        arguments += $" -i \"{input}\"";

        arguments += probe.CanCopy
            ? " -c copy"

            // A keyframe every two seconds, matching the relay's segment duration. Without it the
            // muxer cannot close a segment on time and playback never starts.
            : " -c:v libx264 -preset veryfast -b:v 3M -pix_fmt yuv420p"

                // A keyframe every two seconds, matching the relay's segment duration. Without it
                // the muxer cannot close a segment on time and playback never starts.
                + " -g 60 -keyint_min 60 -sc_threshold 0"

                // No B-frames. They reorder, and the relay's MPEG-TS muxer gives up past a dozen
                // reordered frames - "unable to extract DTS: too many reordered frames" - then
                // destroys itself, which drops every viewer mid-film. Costs a little efficiency and
                // buys a stream that does not die.
                + " -bf 0"

                + " -c:a aac -b:a 160k -ac 2";

        return arguments + $" -f mpegts \"{target.IngestUrl}\"";
    }

    /// <summary>Ends the broadcast. Ending it for the host ends it for the room; that is the design.</summary>
    public void Stop()
    {
        Process? running;
        lock (this.gate)
        {
            running = this.process;
            this.process = null;
        }

        if (running is null)
            return;

        try
        {
            if (!running.HasExited)
            {
                // entireProcessTree: ffmpeg spawns nothing today, but a child left holding the SRT
                // socket would keep the relay believing it still has a publisher.
                running.Kill(entireProcessTree: true);
                running.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            this.Log?.Invoke($"[broadcast] stop: {ex.Message}");
        }
        finally
        {
            running.Dispose();
            this.status = string.Empty;
            this.startedUtc = default;
        }
    }

    /// <summary>
    /// Kills the push on unload. On Windows a child outlives its parent by default, so without this
    /// closing the game would leave ffmpeg broadcasting with nothing left to stop it.
    /// </summary>
    public void Dispose()
    {
        this.disposed = true;
        this.Stop();
    }
}
