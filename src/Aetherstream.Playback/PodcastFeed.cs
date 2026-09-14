using System.Text.Json;
using System.Xml.Linq;

namespace Aetherstream.Playback;

/// <summary>
/// Podcasts: a feed is RSS with an enclosure per episode, and the directory that finds feeds by
/// name is Apple's search, which is free and needs no key. An episode is an audio URL the jukebox
/// plays like any track.
/// </summary>
public sealed class PodcastFeed(HttpClient http)
{
    private static readonly XNamespace Itunes = "http://www.itunes.com/dtds/podcast-1.0.dtd";

    public readonly record struct Show(string Title, string FeedUrl, string Image, string Author, int Episodes);

    public readonly record struct Episode(string Title, string Url, DateTime PublishedUtc, double Seconds, string Description);

    public sealed record Feed(string Title, string Image, string Description, List<Episode> Episodes);

    /// <summary>Shows matching a name, through Apple's podcast directory.</summary>
    public async Task<List<Show>> SearchAsync(string term, CancellationToken ct)
    {
        var text = await http.GetStringAsync($"https://itunes.apple.com/search?media=podcast&limit=25&term={Uri.EscapeDataString(term)}", ct);
        var list = new List<Show>();
        using var doc = JsonDocument.Parse(text);
        foreach (var e in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            var feed = Str(e, "feedUrl");
            var title = Str(e, "collectionName");
            if (feed.Length == 0 || title.Length == 0)
                continue;

            list.Add(new Show(title, feed, Str(e, "artworkUrl600"), Str(e, "artistName"), e.TryGetProperty("trackCount", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0));
        }

        return list;
    }

    /// <summary>A feed's episodes, newest first, with the show's own title and picture.</summary>
    public async Task<Feed> FetchAsync(string feedUrl, CancellationToken ct)
    {
        using var stream = await http.GetStreamAsync(feedUrl, ct);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        var channel = doc.Root?.Element("channel") ?? throw new InvalidOperationException("not an RSS feed");

        var title = (string?)channel.Element("title") ?? feedUrl;
        var image = (string?)channel.Element(Itunes + "image")?.Attribute("href")
            ?? (string?)channel.Element("image")?.Element("url")
            ?? string.Empty;
        var description = Plain((string?)channel.Element(Itunes + "summary") ?? (string?)channel.Element("description") ?? string.Empty);

        var episodes = new List<Episode>();
        foreach (var item in channel.Elements("item"))
        {
            var enclosure = item.Element("enclosure");
            var url = (string?)enclosure?.Attribute("url") ?? string.Empty;
            if (url.Length == 0)
                continue;

            var when = DateTime.TryParse((string?)item.Element("pubDate"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d : DateTime.MinValue;
            episodes.Add(new Episode(
                ((string?)item.Element("title") ?? "Untitled").Trim(),
                url,
                when,
                Duration((string?)item.Element(Itunes + "duration")),
                Plain((string?)item.Element(Itunes + "summary") ?? (string?)item.Element("description") ?? string.Empty)));
        }

        episodes.Sort((a, b) => b.PublishedUtc.CompareTo(a.PublishedUtc));
        return new Feed(title.Trim(), image, description, episodes);
    }

    private static double Duration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return seconds;
        var parts = text.Split(':');
        var total = 0.0;
        foreach (var p in parts)
        {
            if (!double.TryParse(p, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                return 0;
            total = (total * 60) + v;
        }

        return total;
    }

    private static string Plain(string html)
    {
        var sb = new System.Text.StringBuilder();
        var inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<')
                inTag = true;
            else if (ch == '>')
                inTag = false;
            else if (!inTag)
                sb.Append(ch);
        }

        var text = sb.ToString().Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ").Trim();
        return text.Length > 400 ? text[..400] + "…" : text;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
