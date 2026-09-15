using System.Net;
using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// NASA+: the agency's own streaming service, free and worldwide. It is a WordPress site, so its
/// videos are posts on the standard REST API, each carrying a plain HLS address in its meta,
/// with topics as a taxonomy and a full-text search. No account, no keys, no restrictions.
/// </summary>
public sealed class NasaPlus(HttpClient http)
{
    public const int MaxItems = 40;

    private const string Api = "https://plus.nasa.gov/wp-json/wp/v2";

    public readonly record struct Topic(int Id, string Name, int Count);

    /// <summary>A video: its title, still, series, length, rating, and the HLS address to play.</summary>
    public readonly record struct Item(int Id, string Title, string Image, string Series, string Kind, double Seconds, string Rating, string Summary, string StreamUrl);

    public async Task<List<Topic>> TopicsAsync(CancellationToken ct)
    {
        var text = await this.GetAsync($"{Api}/topic?per_page=50&orderby=count&order=desc&_fields=id,name,count", ct);
        var list = new List<Topic>();
        using var doc = JsonDocument.Parse(text);
        foreach (var t in doc.RootElement.EnumerateArray())
        {
            var count = t.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;
            if (count > 0)
                list.Add(new Topic(t.GetProperty("id").GetInt32(), WebUtility.HtmlDecode(Str(t, "name")), count));
        }

        return list;
    }

    /// <summary>The newest videos, in a topic when one is given, matching a search when one is given.</summary>
    public async Task<List<Item>> VideosAsync(int topic, string search, CancellationToken ct)
    {
        var url = $"{Api}/video?per_page={MaxItems}&_fields=id,title,featured_image,meta,excerpt";
        if (topic > 0)
            url += $"&topic={topic}";
        if (search.Trim().Length > 0)
            url += "&search=" + Uri.EscapeDataString(search.Trim());

        var text = await this.GetAsync(url, ct);
        var list = new List<Item>();
        using var doc = JsonDocument.Parse(text);
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            if (!e.TryGetProperty("meta", out var meta))
                continue;
            var stream = Str(meta, "video-url");
            if (!stream.StartsWith("https://", StringComparison.Ordinal))
                continue;

            var title = WebUtility.HtmlDecode(e.TryGetProperty("title", out var t) ? Str(t, "rendered") : string.Empty).Trim();
            var summary = e.TryGetProperty("excerpt", out var x) ? StripTags(WebUtility.HtmlDecode(Str(x, "rendered"))) : string.Empty;
            double.TryParse(Str(meta, "runtime"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var seconds);
            list.Add(new Item(
                e.GetProperty("id").GetInt32(),
                title,
                Str(e, "featured_image"),
                Str(meta, "series"),
                Str(meta, "content-type"),
                seconds,
                Str(meta, "rating"),
                summary,
                stream));
        }

        return list;
    }

    private async Task<string> GetAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 Aetherstream");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string StripTags(string html)
    {
        var sb = new System.Text.StringBuilder(html.Length);
        var inside = false;
        foreach (var ch in html)
        {
            if (ch == '<') inside = true;
            else if (ch == '>') inside = false;
            else if (!inside) sb.Append(ch);
        }

        return sb.ToString().Replace("[…]", "…").Trim();
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
