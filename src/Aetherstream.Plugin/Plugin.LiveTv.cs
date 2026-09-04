using Aetherstream.Core;
using Aetherstream.Playback;

namespace Aetherstream.Plugin;

/// <summary>
/// Live TV: fetching the channel playlist, caching it, and starting a channel.
/// <para>
/// Kept apart from <c>Plugin.cs</c> for the same reason the party code is — none of it touches
/// rendering or decode. It is one HTTP fetch, a parse, and a hand-off.
/// </para>
/// </summary>
public sealed partial class Plugin
{
    /// <summary>
    /// How long a cached playlist is used before it is re-fetched.
    /// <para>
    /// The default list is about three megabytes and changes slowly, so downloading it every time
    /// the tab opens would be rude to a volunteer-run project and slow for no benefit.
    /// </para>
    /// </summary>
    private static readonly TimeSpan PlaylistFreshFor = TimeSpan.FromHours(12);

    private void WireLiveTv()
    {
        this.window.LiveTv.LoadPlaylist = this.LoadPlaylist;

        // Loaded from the cache on startup so the tab has something the first time it is opened,
        // without anyone having to press a button. A missing or stale cache simply does nothing
        // here; the tab offers to fetch.
        _ = Task.Run(() => this.LoadPlaylistAsync(force: false, CancellationToken.None));
    }

    /// <summary>
    /// One cache file per playlist, so switching lists does not re-download the one just left. The
    /// default list keeps the name it always had, so nobody's existing cache is orphaned.
    /// </summary>
    private string PlaylistCachePath
    {
        get
        {
            var url = this.config.LiveTvPlaylistUrl;
            var name = url == Configuration.DefaultPlaylistUrl
                ? "livetv.m3u"
                : $"livetv-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url)))[..12].ToLowerInvariant()}.m3u";

            return Path.Combine(this.pluginInterface.GetPluginConfigDirectory(), name);
        }
    }

    private void LoadPlaylist(bool force) =>
        _ = Task.Run(() => this.LoadPlaylistAsync(force, CancellationToken.None));

    private async Task LoadPlaylistAsync(bool force, CancellationToken ct)
    {
        try
        {
            var path = this.PlaylistCachePath;
            var cached = new FileInfo(path);
            var fresh = cached.Exists && DateTime.UtcNow - cached.LastWriteTimeUtc < PlaylistFreshFor;

            string text;
            if (!force && fresh)
            {
                text = await File.ReadAllTextAsync(path, ct);
            }
            else if (this.config.LiveTvPlaylistUrl.Length == 0)
            {
                this.window.LiveTv.SetStatus("No playlist address set.");
                return;
            }
            else
            {
                this.window.LiveTv.SetStatus("Downloading the channel list…", busy: true);
                text = await this.http.GetStringAsync(this.config.LiveTvPlaylistUrl, ct);

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, text, ct);
            }

            var channels = M3uPlaylist.Parse(text);
            if (channels.Count == 0)
            {
                this.window.LiveTv.SetStatus("That playlist had no channels in it.");
                return;
            }

            this.log.Information(
                $"[livetv] {channels.Count} channels, {M3uPlaylist.GroupsOf(channels).Count} groups " +
                $"({(force || !fresh ? "downloaded" : "from cache")})");

            this.window.LiveTv.SetChannels(channels, string.Empty);
        }
        catch (OperationCanceledException)
        {
            // Shutting down; nothing to report.
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Could not load the channel playlist.");
            this.window.LiveTv.SetStatus($"Could not load the playlist: {ex.Message}");

            // A cached copy is better than nothing when the fetch fails, so fall back to it rather
            // than leaving the tab empty because a volunteer-run host had a bad minute.
            await this.TryCachedAsync(ct);
        }
    }

    private async Task TryCachedAsync(CancellationToken ct)
    {
        try
        {
            var path = this.PlaylistCachePath;
            if (!File.Exists(path))
                return;

            var channels = M3uPlaylist.Parse(await File.ReadAllTextAsync(path, ct));
            if (channels.Count > 0)
                this.window.LiveTv.SetChannels(channels, "using the cached list");
        }
        catch (Exception ex)
        {
            this.log.Debug($"[livetv] cached copy unusable: {ex.Message}");
        }
    }

    /// <summary>
    /// Restarts a stalled channel through the local relay.
    /// <para>
    /// The common reason a public-playlist channel dies after twenty seconds is an upstream that
    /// redirects to a tokenised playlist valid for a few seconds: the decoder resolves the redirect
    /// once, and every refresh afterwards is refused. Re-resolving mints a fresh token, which is
    /// exactly what the relay does behind an address that never expires.
    /// </para>
    /// <para>
    /// Done automatically, and only once — <see cref="ResolvedStream.Relayed"/> stops a channel that
    /// is genuinely offline from bouncing between direct and relayed playback forever.
    /// </para>
    /// </summary>
    private void RetryStalledThroughRelay()
    {
        if (!this.session.ConsumeStall())
            return;

        if (this.session.Current is not { } current)
            return;

        if (current is { Relayable: true, Relayed: false })
        {
            this.log.Information($"[relay] '{current.DisplayName}' stalled; retrying through the relay.");
            this.PlayResolved(this.relay.Publish(current));
            return;
        }

        // Stalled and nothing left to try: the guide marks it so nobody keeps clicking it. The
        // origin, not the relay's loopback address, is what the guide knows the channel by.
        this.window.Dial.MarkOffline(current.Origin ?? current.PlaylistUrl);
    }

    /// <summary>Starts something already resolved, bypassing resolution.</summary>
    private void PlayResolved(ResolvedStream stream)
    {
        // A relayed stream's address is a loopback port valid only for this session, so remembering
        // it would restore a dead URL on the next launch. The original source is already saved.
        if (!stream.Relayed)
            this.config.Source = stream.PlaylistUrl;

        // Any in-flight resolution is for a source we are replacing.
        this.resolving?.Cancel();

        this.log.Information($"Playing '{stream.DisplayName}' directly.");
        this.session.RequestStart(stream);
    }
}
