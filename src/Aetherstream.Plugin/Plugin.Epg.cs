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

    /// <summary>
    /// The guide files in force. An override set for the playlist stands alone. Otherwise the
    /// playlist's own guide, if it names one, plus the community guides for the character's
    /// region — a public list's own file rarely covers much, and these fill in the big networks.
    /// </summary>
    private List<string> GuideUrls()
    {
        var current = this.config.LiveTvPlaylists.FirstOrDefault(p => p.Url == this.config.LiveTvPlaylistUrl);
        if (current?.EpgUrl is { Length: > 0 } set)
            return set.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var urls = new List<string>();
        if (this.playlistGuideUrl.Length > 0)
            urls.Add(this.playlistGuideUrl);

        const string Share = "https://epgshare01.online/epgshare01/";
        urls.AddRange(this.CurrentRegion() switch
        {
            1 => [Share + "epg_ripper_JP1.xml.gz"],
            2 => [Share + "epg_ripper_US2.xml.gz", Share + "epg_ripper_US_SPORTS1.xml.gz", Share + "epg_ripper_CA2.xml.gz"],
            3 => [Share + "epg_ripper_UK1.xml.gz", Share + "epg_ripper_DE1.xml.gz", Share + "epg_ripper_FR1.xml.gz"],
            4 => [Share + "epg_ripper_AU1.xml.gz", Share + "epg_ripper_NZ1.xml.gz"],
            _ => Array.Empty<string>(),
        });

        return urls;
    }

    private UI.ChannelDial dial0() => this.window.Dial;

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
        var urls = this.GuideUrls();
        var url = string.Join(',', urls);
        if (urls.Count == 0)
        {
            if (this.guide is not null)
            {
                this.guide = null;
                this.window.LiveTv.SetGuideStatus("no guide for this playlist");
            }

            return;
        }

        var pins = this.config.LiveTvFavourites;
        var lineup = dial0().Numbered();
        var key = $"{url}|{string.Join('|', lineup.Take(40).Select(n => n.Channel.Url))}|{this.config.Source}";
        var now = Environment.TickCount64;
        var stale = now - this.guideLoadedAtMs > GuideFreshFor.TotalMilliseconds;

        if (this.guideLoading || (key == this.guideKey && !stale))
            return;

        this.guideKey = key;
        this.guideLoadedAtMs = now;
        this.guideLoading = true;

        // The channels to keep: the numbered lineup's first forty and whatever is playing, by id and by name.
        var dial = this.window.Dial;
        var wantedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wantedNames = new List<string>();
        foreach (var (_, channel) in lineup.Take(40))
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
                var utc = DateTime.UtcNow;
                var parsedGuides = new List<XmltvGuide>();
                foreach (var one in urls)
                {
                    try
                    {
                        var path = this.GuideCachePath(one);
                        var cached = new FileInfo(path);
                        if (!cached.Exists || DateTime.UtcNow - cached.LastWriteTimeUtc > GuideFreshFor)
                        {
                            this.window.LiveTv.SetGuideStatus($"fetching listings ({parsedGuides.Count + 1} of {urls.Count})…");
                            using var response = await this.http.GetAsync(one, HttpCompletionOption.ResponseHeadersRead);
                            response.EnsureSuccessStatusCode();
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            await using var file = File.Create(path);
                            await response.Content.CopyToAsync(file);
                        }

                        await using var stream = File.OpenRead(path);
                        parsedGuides.Add(XmltvGuide.Parse(stream, wantedIds, wantedNames, utc.AddHours(-3), utc.AddHours(36)));
                    }
                    catch (Exception ex)
                    {
                        // One guide down does not lose the others.
                        this.log.Warning($"[guide] {one}: {ex.Message}");
                    }
                }

                var parsed = XmltvGuide.Merge(parsedGuides);
                this.guide = parsed;

                var considered = lineup.Take(40).ToList();
                var matched = considered.Count(p => parsed.IdFor(p.Channel.TvgId, p.Channel.Name) is not null);
                this.window.LiveTv.SetGuideStatus(
                    parsed.ChannelCount == 0 ? "no guide could be read"
                    : $"listings for {matched} of the first {considered.Count} channels ({parsed.ChannelCount:N0} in {parsedGuides.Count} guide{(parsedGuides.Count == 1 ? string.Empty : "s")})");
                this.log.Information($"[guide] {parsed.ChannelCount} channels, {parsed.ProgrammeCount} programmes kept, {matched}/{considered.Count} lineup matched");
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
