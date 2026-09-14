using System.Net.Http.Headers;

using Aetherstream.Playback;

namespace Aetherstream.Plugin;

/// <summary>
/// Which live channels are dead, so the guide and the dial can skip them.
/// <para>
/// Two ways to find out. A link check asks each lineup channel's address for its headers in the
/// background, a few at a time, and marks the ones that answer with an error or nothing. And
/// while a channel plays, the picture is watched: black for a quarter of a minute with frames
/// still arriving is a feed carrying nothing, and it is marked the same way. Marks last a day and
/// are kept across sessions; a channel that plays again clears its own.
/// </para>
/// </summary>
public sealed partial class Plugin
{
    private static readonly TimeSpan DeadFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan BlackFor = TimeSpan.FromSeconds(15);

    private bool healthCheckRunning;
    private string healthKey = string.Empty;
    private long blackSinceMs = -1;
    private string blackSource = string.Empty;

    /// <summary>What the Setup tab shows about the last check.</summary>
    private string healthStatus = string.Empty;

    private bool IsDead(string url) =>
        this.config.LiveTvDead.TryGetValue(url, out var until) && until > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private void MarkDead(string url, string why)
    {
        if (url.Length == 0)
            return;

        this.config.LiveTvDead[url] = (DateTimeOffset.UtcNow + DeadFor).ToUnixTimeSeconds();
        this.configDirty = true;
        this.log.Information($"[health] {why}: {url}");
    }

    private void MarkAlive(string url)
    {
        if (this.config.LiveTvDead.Remove(url))
            this.configDirty = true;
    }

    /// <summary>Forgets expired marks. Called with the once-a-second housekeeping.</summary>
    private void PruneDead()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expired = this.config.LiveTvDead.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList();
        foreach (var url in expired)
            this.config.LiveTvDead.Remove(url);

        if (expired.Count > 0)
            this.configDirty = true;
    }

    /// <summary>
    /// Watches the picture of a playing live channel. Every frame is too often for a full scan; a
    /// sampled brightness once a second is plenty to tell a black feed from a dark scene, given
    /// that a dark scene changes and a dead feed does not.
    /// </summary>
    private void WatchForBlack()
    {
        var source = this.config.Source;
        if (!this.session.IsPlaying || this.window.Dial.Find(source) is null || this.session.IsPaused)
        {
            this.blackSinceMs = -1;
            return;
        }

        if (source != this.blackSource)
        {
            this.blackSource = source;
            this.blackSinceMs = -1;
        }

        var luma = this.session.SampledBrightness;
        if (luma < 0)
            return;

        var now = Environment.TickCount64;
        if (luma > 6f)
        {
            // A real picture: the channel is alive, whatever an earlier check said.
            this.blackSinceMs = -1;
            if (this.session.FramesPresented > 90)
                this.MarkAlive(source);
            return;
        }

        if (this.blackSinceMs < 0)
        {
            this.blackSinceMs = now;
            return;
        }

        if (now - this.blackSinceMs > BlackFor.TotalMilliseconds && !this.IsDead(source))
        {
            this.MarkDead(source, "black picture");
            this.window.Dial.MarkOffline(source);
        }
    }

    /// <summary>
    /// Asks the lineup's channels for their headers, a few at a time, and marks the ones that
    /// fail. Runs once per lineup per session, or when asked from Setup.
    /// </summary>
    private void CheckLineupHealth(bool force)
    {
        var lineup = this.window.Dial.Numbered();
        var key = string.Join('|', lineup.Select(n => n.Channel.Url));
        if (this.healthCheckRunning || (!force && key == this.healthKey) || lineup.Count == 0)
            return;

        this.healthKey = key;
        this.healthCheckRunning = true;
        this.healthStatus = $"checking {lineup.Count} channels…";

        _ = Task.Run(async () =>
        {
            var dead = 0;
            var checkedCount = 0;
            using var gate = new SemaphoreSlim(4);

            var tasks = lineup.Select(async entry =>
            {
                var channel = entry.Channel;
                await gate.WaitAsync();
                try
                {
                    var alive = await ProbeAsync(channel);
                    Interlocked.Increment(ref checkedCount);
                    if (!alive)
                    {
                        Interlocked.Increment(ref dead);
                        this.MarkDead(channel.Url, "no answer");
                    }
                    else if (!this.IsDead(channel.Url))
                    {
                        this.MarkAlive(channel.Url);
                    }

                    this.healthStatus = $"checking… {checkedCount} of {lineup.Count}, {dead} dead";
                }
                finally
                {
                    gate.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);
            this.healthStatus = $"checked {lineup.Count} channels at {DateTime.Now:h:mm tt}: {dead} not answering";
            this.log.Information($"[health] {lineup.Count} checked, {dead} dead");
            this.healthCheckRunning = false;
        });
    }

    /// <summary>
    /// One channel: a GET with the channel's own headers, reading only the first bytes. A
    /// playlist that answers 200 with something that looks like HLS is alive; anything else is
    /// not. Eight seconds is the budget — a channel slower than that is dead to a viewer anyway.
    /// </summary>
    private async Task<bool> ProbeAsync(M3uPlaylist.Channel channel)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, channel.Url);
            if (channel.UserAgent.Length > 0)
                request.Headers.TryAddWithoutValidation("User-Agent", channel.UserAgent);
            if (channel.Referrer.Length > 0)
                request.Headers.TryAddWithoutValidation("Referer", channel.Referrer);
            request.Headers.Range = new RangeHeaderValue(0, 4095);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var response = await this.http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
                return false;

            // Only HTTP playlists can be judged by their first bytes; other schemes never get here.
            if (channel.Url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) || channel.Url.Contains(".m3u", StringComparison.OrdinalIgnoreCase))
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                var buffer = new byte[512];
                var n = await stream.ReadAsync(buffer, cts.Token);
                var head = System.Text.Encoding.ASCII.GetString(buffer, 0, n);
                return head.Contains("#EXTM3U", StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
