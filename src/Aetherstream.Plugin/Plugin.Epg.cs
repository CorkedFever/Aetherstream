using Aetherstream.Playback;
using Aetherstream.Plugin.Video;

namespace Aetherstream.Plugin;

/// <summary>
/// Listings for the guide's live channels, from an XMLTV file: the playlist's own, or one set
/// for the playlist in the Live TV tab. Fetched twice a day into the config folder and parsed
/// from there for the pinned channels, so pinning something new is a re-parse, not a download.
/// </summary>
public sealed partial class Plugin
{
    private static readonly TimeSpan GuideFreshFor = TimeSpan.FromHours(12);

    private XmltvGuide? guide;
    private string playlistGuideUrl = string.Empty;
    private string guideKey = string.Empty;
    private long guideLoadedAtMs = -1;
    private bool guideLoading;

    /// <summary>The guide URL in force: the playlist's override first, then what its header said.</summary>
    private string GuideUrl()
    {
        var current = this.config.LiveTvPlaylists.FirstOrDefault(p => p.Url == this.config.LiveTvPlaylistUrl);
        return current?.EpgUrl is { Length: > 0 } set ? set : this.playlistGuideUrl;
    }

    private string GuideCachePath(string url) =>
        Path.Combine(
            this.pluginInterface.GetPluginConfigDirectory(),
            $"guide-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url)))[..12].ToLowerInvariant()}.xml");

    /// <summary>
    /// Programmes for a channel over the guide's window, as slots. Null when there is no guide, or
    /// the guide does not know the channel — the row then shows its group, as before.
    /// </summary>
    private IReadOnlyList<GuideSlot>? ListingsFor(M3uPlaylist.Channel channel, DateTime utc)
    {
        this.EnsureGuide();

        if (this.guide is not { } g)
            return null;

        var id = g.IdFor(channel.TvgId, channel.Name);
        if (id is null)
            return null;

        var slots = new List<GuideSlot>();
        foreach (var p in g.ProgrammesFor(id))
        {
            if (p.StopUtc <= utc.AddHours(-1) || p.StartUtc >= utc.AddHours(3))
                continue;

            slots.Add(new GuideSlot(p.StartUtc, p.StopUtc, p.Title, p.StartUtc <= utc && p.StopUtc > utc));
        }

        return slots.Count > 0 ? slots : null;
    }

    /// <summary>The programme on now for a channel, for the on-screen display. Null when unknown.</summary>
    private string? OnNow(M3uPlaylist.Channel channel)
    {
        if (this.guide is not { } g || g.IdFor(channel.TvgId, channel.Name) is not { } id)
            return null;

        return g.At(id, DateTime.UtcNow)?.Title;
    }

    /// <summary>
    /// Loads or re-parses the guide when the URL, the pins or the age call for it. Cheap to call
    /// often: it only does work when the key changes or the last load is stale.
    /// </summary>
    private void EnsureGuide()
    {
        var url = this.GuideUrl();
        if (url.Length == 0)
        {
            if (this.guide is not null)
            {
                this.guide = null;
                this.window.LiveTv.SetGuideStatus("no guide for this playlist");
            }

            return;
        }

        var pins = this.config.LiveTvFavourites;
        var key = $"{url}|{string.Join('|', pins)}|{this.config.Source}";
        var now = Environment.TickCount64;
        var stale = now - this.guideLoadedAtMs > GuideFreshFor.TotalMilliseconds;

        if (this.guideLoading || (key == this.guideKey && !stale))
            return;

        this.guideKey = key;
        this.guideLoadedAtMs = now;
        this.guideLoading = true;

        // The channels to keep: the pinned ones and whatever is playing, by id and by name.
        var dial = this.window.Dial;
        var wantedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wantedNames = new List<string>();
        foreach (var (_, channel) in dial.Pinned())
        {
            if (channel.TvgId.Length > 0) wantedIds.Add(channel.TvgId);
            wantedNames.Add(channel.Name);
        }

        if (dial.Find(this.config.Source) is { } current)
        {
            if (current.TvgId.Length > 0) wantedIds.Add(current.TvgId);
            wantedNames.Add(current.Name);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var path = this.GuideCachePath(url);
                var cached = new FileInfo(path);
                if (!cached.Exists || DateTime.UtcNow - cached.LastWriteTimeUtc > GuideFreshFor)
                {
                    this.window.LiveTv.SetGuideStatus("fetching listings…");
                    using var response = await this.http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await using var file = File.Create(path);
                    await response.Content.CopyToAsync(file);
                }

                var utc = DateTime.UtcNow;
                XmltvGuide parsed;
                await using (var stream = File.OpenRead(path))
                    parsed = XmltvGuide.Parse(stream, wantedIds, wantedNames, utc.AddHours(-3), utc.AddHours(36));

                this.guide = parsed;

                var matched = dial.Pinned().Count(p => parsed.IdFor(p.Channel.TvgId, p.Channel.Name) is not null);
                var pinned = dial.Pinned().Count();
                this.window.LiveTv.SetGuideStatus(
                    parsed.ChannelCount == 0 ? "the guide file lists no channels"
                    : $"listings for {matched} of {pinned} pinned channels ({parsed.ChannelCount:N0} in the guide)");
                this.log.Information($"[guide] {parsed.ChannelCount} channels, {parsed.ProgrammeCount} programmes kept, {matched}/{pinned} pins matched");
            }
            catch (Exception ex)
            {
                this.log.Warning($"[guide] could not load listings: {ex.Message}");
                this.window.LiveTv.SetGuideStatus($"could not load the guide: {ex.Message}");

                // Try again sooner than twelve hours, but not every second.
                this.guideLoadedAtMs = Environment.TickCount64 - (long)GuideFreshFor.TotalMilliseconds + 300_000;
            }
            finally
            {
                this.guideLoading = false;
            }
        });
    }
}
