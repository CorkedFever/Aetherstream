using Aetherstream.Core;
using Aetherstream.Playback;
using Aetherstream.Plugin.Audio;

using Dalamud.Plugin.Services;

using LibVLCSharp.Shared;

namespace Aetherstream.Plugin.Playback;

/// <summary>
/// Background music for the drawn channels: the smooth jazz under the forecast.
/// <para>
/// A second, audio-only decoder with its own output, shuffling through a list and looping. It
/// runs beside the main picture rather than instead of it, so a film keeps decoding under the
/// guide — the session simply turns its sound down while this plays. Tracks are whatever the
/// plugin hands over: the bundled ones, a folder, or a Plex playlist; this does not care which.
/// </para>
/// </summary>
internal sealed class Jukebox(LibVLC vlc, IPluginLog log) : IDisposable
{
    private VlcStreamSource? source;
    private AudioOutput? output;
    private StereoRingBuffer? ring;
    private int sampleRate;
    private string deviceId = string.Empty;

    private IReadOnlyList<string>? tracks;
    private List<string> queue = [];
    private int index = -1;
    private int failures;

    private volatile bool endedPending;
    private volatile bool failedPending;
    private bool disposed;

    public bool Playing => this.source is not null;

    /// <summary>The file name or title of what is on, for anyone who wants to show it.</summary>
    public string NowPlaying { get; private set; } = string.Empty;

    /// <summary>The track after this one, by name, or empty.</summary>
    public string UpNext => this.queue.Count > 1 ? this.TitleOf(this.queue[(this.index + 1) % this.queue.Count]) : string.Empty;

    /// <summary>A better name for a track than its file name, when the plugin has one.</summary>
    public Func<string, string?>? Titles { get; set; }

    /// <summary>What the stream itself says is on, for a radio station: the song, from its metadata.</summary>
    public string LiveTitle { get; private set; } = string.Empty;

    private long liveCheckedAtMs;

    /// <summary>Which track of how many, one-based.</summary>
    public (int Index, int Count) Position => (this.index + 1, this.queue.Count);

    /// <summary>The most recent samples of the sound, mono, oldest first; silence when nothing plays.</summary>
    public void CopyTap(Span<float> into)
    {
        if (this.source is { } s)
            s.CopyTap(into);
        else
            into.Clear();
    }

    private string TitleOf(string track) => this.Titles?.Invoke(track) ?? Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(track.Split('?')[0]));

    /// <summary>
    /// Makes sure this list is playing. The same list twice is a no-op; a different one starts
    /// over, shuffled. Render thread.
    /// </summary>
    public void Ensure(IReadOnlyList<string> list, string device, bool shuffle = true)
    {
        if (this.disposed)
            return;

        if (ReferenceEquals(list, this.tracks) && (this.Playing || list.Count == 0))
            return;

        this.tracks = list;
        this.deviceId = device;
        this.failures = 0;

        if (list.Count == 0)
        {
            this.Stop();
            return;
        }

        // Shuffled once per list, then looped in that order, so the same track never follows itself.
        this.queue = [.. list];
        for (var i = shuffle ? this.queue.Count - 1 : 0; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (this.queue[i], this.queue[j]) = (this.queue[j], this.queue[i]);
        }

        this.index = -1;
        this.Next();
    }

    /// <summary>Moves on when a track ends or fails. Render thread, once a frame.</summary>
    public void Update()
    {
        if (this.disposed || !this.Playing)
            return;

        var now = Environment.TickCount64;
        if (now - this.liveCheckedAtMs > 2000)
        {
            this.liveCheckedAtMs = now;
            this.LiveTitle = this.source?.NowPlayingMeta?.Trim() ?? string.Empty;
        }

        if (this.failedPending)
        {
            this.failedPending = false;
            this.failures++;
            log.Warning($"[music] could not play '{this.NowPlaying}'; skipping.");

            // Every track failing means the list is wrong, not one file; stop rather than spin.
            if (this.failures >= this.queue.Count)
            {
                log.Warning("[music] nothing in the list would play; music off until it changes.");
                this.Stop();
                return;
            }

            this.Next();
            return;
        }

        if (this.endedPending)
        {
            this.endedPending = false;
            this.failures = 0;
            this.Next();
        }
    }

    private void Next()
    {
        if (this.queue.Count == 0)
            return;

        this.index = (this.index + 1) % this.queue.Count;
        var track = this.queue[this.index];

        try
        {
            this.Open(track);
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"[music] could not open '{track}'.");
            this.failedPending = true;
        }
    }

    /// <summary>
    /// Plays one track. The decoder and output are made once and kept: a new track is a new
    /// media on the same player, so the sound card is not reopened between songs.
    /// </summary>
    private void Open(string track)
    {
        var mrl = ToMrl(track);
        this.NowPlaying = this.TitleOf(track);
        this.LiveTitle = string.Empty;

        if (this.source is null)
        {
            this.sampleRate = AudioOutput.MixRateOf(this.deviceId);

            // Audio only; the tiny picture is the smallest the decoder will accept and is never read.
            var created = new VlcStreamSource(vlc, this.sampleRate, 16, 16, callbackAudio: true);
            created.PlaybackEnded += (_, _) => this.endedPending = true;
            created.PlaybackFailed += (_, _) => this.failedPending = true;

            this.ring = created.Audio!;
            this.output = new AudioOutput(this.ring, 0, this.deviceId);
            this.source = created;
        }

        this.source.Play(new ResolvedStream(mrl, this.NowPlaying), hardwareDecode: false, networkCachingMs: 3000);
    }

    /// <summary>Loudness and where it sits, applied by the session with the picture's own rules.</summary>
    public void ApplyVolume(float volume, float pan)
    {
        if (this.output is null)
            return;

        this.output.Volume = volume;
        this.output.Pan = pan;
    }

    /// <summary>Moves the sound to another device mid-track. The track restarts if the rate differs.</summary>
    public void Reopen(string device)
    {
        if (!this.Playing || this.ring is null)
            return;

        this.deviceId = device;

        try
        {
            if (AudioOutput.MixRateOf(device) != this.sampleRate)
            {
                // The decoder is locked to the old rate; the cheap fix is to start the track again.
                var track = this.queue[Math.Max(0, this.index)];
                this.Stop();
                this.Open(track);
                return;
            }

            var replacement = new AudioOutput(this.ring, 0, device);
            this.output?.Dispose();
            this.output = replacement;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[music] could not move the sound; keeping the current device.");
        }
    }

    public void Stop()
    {
        this.source?.Dispose();
        this.source = null;
        this.output?.Dispose();
        this.output = null;
        this.ring = null;
        this.NowPlaying = string.Empty;
        this.endedPending = false;
        this.failedPending = false;
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.Stop();
    }

    /// <summary>A local path becomes a file URI; anything already a URL is left alone.</summary>
    private static string ToMrl(string track) =>
        track.Contains("://", StringComparison.Ordinal) ? track : new Uri(track).AbsoluteUri;
}
