using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aetherstream.Playback;

/// <summary>
/// South Park Studios: Paramount's own site for the show, every episode free with ads, as plain
/// HLS. The season list is read from a season page, each season's episodes from the site's own
/// paging API, and an episode plays through yt-dlp, whose extractor for the site is current.
/// An episode the site has locked, which happens to a few at a time, is listed but not offered.
/// </summary>
public sealed partial class SouthParkStudios(HttpClient http)
{
    private const string Site = "https://www.southparkstudios.com";

    public readonly record struct Season(int Number, string Url);

    public readonly record struct Episode(string Url, string Title, string Code, string Description, string Image, bool Locked)
    {
        /// <summary>The still at tile width, from the site's image service.</summary>
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
            list.Add(new Episode(Site + url, WebUtility.HtmlDecode(title), code, meta.ValueKind == JsonValueKind.Object ? WebUtility.HtmlDecode(Str(meta, "description")) : string.Empty, image, locked));
        }

        return list;
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
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Aetherstream");
        request.Headers.TryAddWithoutValidation("Accept", url.Contains("/api/") ? "application/json" : "text/html");
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
}
