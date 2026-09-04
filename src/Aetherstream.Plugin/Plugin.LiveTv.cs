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
        this.window.LiveTv.PlayChannel = this.PlayChannel;

        // Loaded from the cache on startup so the tab has something the first time it is opened,
        // without anyone having to press a button. A missing or stale cache simply does nothing
        // here; the tab offers to fetch.
        _ = Task.Run(() => this.LoadPlaylistAsync(force: false, CancellationToken.None));
    }

    private string PlaylistCachePath =>
        Path.Combine(this.pluginInterface.GetPluginConfigDirectory(), "livetv.m3u");

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
    /// Starts a channel with its own headers intact.
    /// <para>
    /// This skips the resolver chain deliberately. Sending the URL back through as a bare string
    /// would drop the user agent and referrer the playlist supplied, and several hundred channels in
    /// the default list refuse to serve without them.
    /// </para>
    /// </summary>
    private void PlayChannel(M3uPlaylist.Channel channel)
    {
        this.config.Remember(channel.Url, channel.Name);
        this.configDirty = true;

        this.PlayResolved(channel.ToStream());
    }

    /// <summary>Starts something already resolved, bypassing resolution.</summary>
    private void PlayResolved(ResolvedStream stream)
    {
        this.config.Source = stream.PlaylistUrl;

        // Any in-flight resolution is for a source we are replacing.
        this.resolving?.Cancel();

        this.log.Information($"Playing '{stream.DisplayName}' directly.");
        this.session.RequestStart(stream);
    }
}
