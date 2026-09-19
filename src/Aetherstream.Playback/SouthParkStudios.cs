using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// South Park Studios: Paramount's own site for the show, free with ads, as plain HLS, though by
/// now most episodes have moved to the paid service and only a few per season still play. The
/// season list is read from a season page and each season's episodes from the site's paging
/// API, which marks nothing; whether an episode still plays is asked of the player service by
/// id, the one place that knows. An episode plays from that service's stitched master playlist,
/// whose sound is a rendition libvlc keeps in step itself.
/// </summary>
public sealed partial class SouthParkStudios(HttpClient http)
{
    private const string Site = "https://www.southparkstudios.com";

    /// <summary>The player service, asked the way the site's desktop player asks it.</summary>
    private const string Service = "https://topaz.paramount.tech/topaz/api/mgid:arc:episode:shared.southpark.us.en:";

    /// <summary>The user agent the site and its CDN are asked with; libvlc is given the same one.</summary>
    public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Aetherstream";

    public readonly record struct Season(int Number, string Url);

    /// <summary>One episode as the listing gives it; <paramref name="Id"/> is what the player service is asked by.</summary>
    public sealed record Episode(string Url, string Title, string Code, string Description, string Image, bool Locked, string Id, int Season, int Number)
    {
        /// <summary>The still at tile width, from the site's image service.</summary>
        [JsonIgnore]
        public string Still => this.Image.Length > 0 ? this.Image + "&width=480&height=270&crop=true" : string.Empty;
    }

    private readonly Dictionary<string, string> seasonIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every season, from the season picker on the first season's page.</summary>
    public async Task<List<Season>> SeasonsAsync(CancellationToken ct)
    {
        var html = await this.GetAsync($"{Site}/seasons/south-park/yjy8n9/season-1", ct);
        var seasons = new List<Season>();
        // The picker's links appear both as anchors and inside the page's data, where the
        // slashes are escaped; both forms are read, and the address rebuilt from the parts.
        foreach (Match m in SeasonLink().Matches(html))
        {
            var number = int.Parse(m.Groups[2].Value);
            if (seasons.All(s => s.Number != number))
                seasons.Add(new Season(number, $"/seasons/south-park/{m.Groups[1].Value}/season-{number}"));
        }

        if (seasons.All(s => s.Number != 1))
            seasons.Add(new Season(1, "/seasons/south-park/yjy8n9/season-1"));
        seasons.Sort((a, b) => a.Number.CompareTo(b.Number));
        this.NoteSeasonId(html);
        return seasons;
    }

    /// <summary>A season's episodes in order: the season page names its id, the API lists the rest.</summary>
    public async Task<List<Episode>> EpisodesAsync(Season season, CancellationToken ct)
    {
        if (!this.seasonIds.TryGetValue(season.Url, out var id))
        {
            var html = await this.GetAsync(Site + season.Url, ct);
            id = this.NoteSeasonId(html, season.Url);
            if (id is null)
                throw new InvalidOperationException("The season's page does not name its listing.");
        }

        var text = await this.GetAsync($"{Site}/api/context/{Uri.EscapeDataString("mgid:arc:season:southparkstudios.com:" + id)}/episode/1/40", ct);
        var list = new List<Episode>();
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("items", out var items))
            return list;

        foreach (var e in items.EnumerateArray())
        {
            var url = Str(e, "url");
            if (!url.StartsWith("/episodes/", StringComparison.Ordinal))
                continue;
            var meta = e.TryGetProperty("meta", out var m) ? m : default;
            var media = e.TryGetProperty("media", out var md) ? md : default;
            // The number sits three deep: meta.header.title.text, as "S1 • E1".
            var code = meta.ValueKind == JsonValueKind.Object && meta.TryGetProperty("header", out var h) && h.ValueKind == JsonValueKind.Object && h.TryGetProperty("title", out var ht)
                ? (ht.ValueKind == JsonValueKind.Object ? Str(ht, "text") : ht.ValueKind == JsonValueKind.String ? ht.GetString() ?? string.Empty : string.Empty).Replace("•", "·").Replace("  ", " ")
                : string.Empty;
            var title = meta.ValueKind == JsonValueKind.Object ? Str(meta, "subHeader") : string.Empty;
            var image = media.ValueKind == JsonValueKind.Object && media.TryGetProperty("image", out var img) ? Str(img, "url") : string.Empty;
            var locked = media.ValueKind == JsonValueKind.Object && Str(media, "lockedLabel").Length > 0;
            if (title.Length == 0)
                continue;
            var number = EpisodeNumber().Match(code) is { Success: true } n ? int.Parse(n.Groups[1].Value) : list.Count + 1;
            list.Add(new Episode(Site + url, WebUtility.HtmlDecode(title), code, meta.ValueKind == JsonValueKind.Object ? WebUtility.HtmlDecode(Str(meta, "description")) : string.Empty, image, locked, Str(e, "id"), season.Number, number));
        }

        return list;
    }

    /// <summary>Whether the player service still has a stream for an episode: it answers with one, or with an error slate.</summary>
    public async Task<bool> PlaysAsync(Episode episode, CancellationToken ct)
    {
        if (episode.Locked || episode.Id.Length == 0)
            return false;
        var text = await this.GetAsync($"{Service}{episode.Id}/mica.json?clientPlatform=desktop", ct);
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.TryGetProperty("stitchedstream", out var stitched) && Str(stitched, "source").Length > 0;
    }

    /// <summary>
    /// Every episode the site still plays, newest season first, episodes in order. Seasons are
    /// listed a few at a time and their episodes asked about eight at a time, so the whole run
    /// is a few seconds; <paramref name="progress"/> hears after each season, with the seasons
    /// done, the seasons in all, and what plays so far. The count of episodes that could not be
    /// asked about comes back too, so a run with the network falling over is not remembered as
    /// the truth.
    /// </summary>
    public async Task<(List<Episode> Playable, int Unasked)> CatalogueAsync(Action<int, int, List<Episode>> progress, CancellationToken ct)
    {
        var seasons = await this.SeasonsAsync(ct);
        using var seasonGate = new SemaphoreSlim(3);
        using var checkGate = new SemaphoreSlim(8);
        var playable = new List<Episode>();
        var unasked = 0;
        var done = 0;

        await Task.WhenAll(seasons.Select(async season =>
        {
            var episodes = new List<Episode>();
            await seasonGate.WaitAsync(ct);
            try
            {
                episodes = await this.EpisodesAsync(season, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                Interlocked.Increment(ref unasked);
            }
            finally
            {
                seasonGate.Release();
            }

            var plays = new bool[episodes.Count];
            await Task.WhenAll(episodes.Select(async (episode, i) =>
            {
                if (episode.Locked || episode.Id.Length == 0)
                    return;
                await checkGate.WaitAsync(ct);
                try
                {
                    plays[i] = await this.PlaysAsync(episode, ct);
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    Interlocked.Increment(ref unasked);
                }
                finally
                {
                    checkGate.Release();
                }
            }));

            List<Episode> snapshot;
            int finished;
            lock (playable)
            {
                for (var i = 0; i < episodes.Count; i++)
                {
                    if (plays[i])
                        playable.Add(episodes[i]);
                }

                snapshot = Sorted(playable);
                finished = ++done;
            }

            progress(finished, seasons.Count, snapshot);
        }));

        lock (playable)
            return (Sorted(playable), unasked);
    }

    /// <summary>
    /// The stream behind an episode page: the page as JSON carries the player's detail, which
    /// names the service address, and the service, asked as the desktop player would, names the
    /// stitched master playlist. That master carries the sound as a rendition of the picture,
    /// which libvlc keeps in step itself; yt-dlp would hand over the two as separate playlists,
    /// and on this site's encrypted, ad-stitched segments libvlc's second input never caught the
    /// clock and the picture froze on its first frame.
    /// </summary>
    public async Task<(string Source, string Title)> StreamAsync(string episodeUrl, CancellationToken ct)
    {
        var page = await this.GetAsync(episodeUrl + "?json=true", ct);
        var detailText = ObjectAfter(page, "\"videoDetail\":");
        if (detailText is null)
            throw new InvalidOperationException("The page has no player detail; is it an episode?");

        using var detail = JsonDocument.Parse(detailText);
        var root = detail.RootElement;
        var serviceUrl = Str(root, "videoServiceUrl");
        if (serviceUrl.Length == 0)
            throw new InvalidOperationException("The site no longer serves this episode; it moved to the paid service.");
        var cut = serviceUrl.IndexOf('?');
        if (cut >= 0)
            serviceUrl = serviceUrl[..cut];

        var name = "South Park";
        if (root.TryGetProperty("seasonNumber", out var s) && s.ValueKind == JsonValueKind.Number
            && root.TryGetProperty("episodeAiringOrder", out var e) && e.ValueKind == JsonValueKind.Number)
            name += $" S{s.GetInt32()} · E{e.GetInt32()}";
        var title = Str(root, "title");
        if (title.Length > 0)
            name += " · " + WebUtility.HtmlDecode(title);

        var text = await this.GetAsync(serviceUrl + "?clientPlatform=desktop", ct);
        using var doc = JsonDocument.Parse(text);
        var source = doc.RootElement.TryGetProperty("stitchedstream", out var stitched) ? Str(stitched, "source") : string.Empty;
        if (source.Length == 0)
            throw new InvalidOperationException("The site's player service named no stream.");
        return (source, name);
    }

    private static List<Episode> Sorted(List<Episode> episodes) =>
        episodes.OrderByDescending(e => e.Season).ThenBy(e => e.Number).ToList();

    /// <summary>The JSON object that follows a key in a JSON text, braces balanced and strings skipped.</summary>
    private static string? ObjectAfter(string text, string key)
    {
        var at = text.IndexOf(key, StringComparison.Ordinal);
        if (at < 0)
            return null;
        var start = text.IndexOf('{', at + key.Length);
        if (start < 0)
            return null;

        var depth = 0;
        var inString = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (c == '\\')
                    i++;
                else if (c == '"')
                    inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return text[start..(i + 1)];
            }
        }

        return null;
    }

    private string? NoteSeasonId(string html, string? forUrl = null)
    {
        var m = SeasonId().Match(html);
        if (!m.Success)
            return null;
        var id = m.Groups[1].Value;
        var url = forUrl ?? CanonicalSeason().Match(html).Groups[1].Value;
        if (url.Length > 0)
            this.seasonIds[url] = id;
        return id;
    }

    private async Task<string> GetAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        var json = url.Contains("/api/") || url.Contains("json=true") || url.Contains("clientPlatform=");
        request.Headers.TryAddWithoutValidation("Accept", json ? "application/json" : "text/html");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    [GeneratedRegex(@"seasons/south-park/([a-z0-9]{6})/season-(\d+)")]
    private static partial Regex SeasonLink();

    [GeneratedRegex(@"mgid%3Aarc%3Aseason%3Asouthparkstudios\.com%3A([0-9a-f-]{36})")]
    private static partial Regex SeasonId();

    [GeneratedRegex(@"southparkstudios\.com(/seasons/south-park/[a-z0-9]{6}/season-\d+)")]
    private static partial Regex CanonicalSeason();

    [GeneratedRegex(@"E(\d+)")]
    private static partial Regex EpisodeNumber();
}

/// <summary>An episode page on South Park Studios, played from the site's own master playlist.</summary>
public sealed class SouthParkResolver(HttpClient http) : IStreamResolver
{
    public static bool Matches(string input) =>
        Uri.TryCreate(input, UriKind.Absolute, out var uri)
        && (uri.Host.Equals("www.southparkstudios.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("southparkstudios.com", StringComparison.OrdinalIgnoreCase))
        && uri.AbsolutePath.StartsWith("/episodes/", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Length > "/episodes/".Length;

    public async Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        var (source, title) = await new SouthParkStudios(http).StreamAsync(input.Split('?')[0], ct);
        return new ResolvedStream(source, title, new Dictionary<string, string> { ["User-Agent"] = SouthParkStudios.UserAgent });
    }
}
