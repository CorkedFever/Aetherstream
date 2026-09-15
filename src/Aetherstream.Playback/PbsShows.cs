using System.Net;
using System.Text.RegularExpressions;

namespace Aetherstream.Playback;

/// <summary>
/// PBS's shows, read from their show pages. PBS keeps no open catalogue service, but each
/// show's page lists its episodes with a title, a still, a length and a line of
/// description, and a video page plays through yt-dlp. A short list of the shows worth a shelf.
/// </summary>
public sealed partial class PbsShows(HttpClient http)
{
    public readonly record struct Show(string Slug, string Name);

    public readonly record struct Episode(string Url, string Title, string Still, string Kind, string Length, string Description);

    public static readonly Show[] Shows =
    [
        new("nova", "NOVA"),
        new("nature", "Nature"),
        new("frontline", "FRONTLINE"),
        new("american-experience", "American Experience"),
        new("american-masters", "American Masters"),
        new("independent-lens", "Independent Lens"),
        new("pov", "POV"),
        new("secrets-of-the-dead", "Secrets of the Dead"),
        new("finding-your-roots", "Finding Your Roots"),
        new("antiques-roadshow", "Antiques Roadshow"),
        new("great-performances", "Great Performances"),
        new("masterpiece", "Masterpiece"),
        new("newshour", "PBS News Hour"),
        new("washington-week", "Washington Week"),
    ];

    /// <summary>The episodes on a show's page that anyone can watch, in the order PBS lists them. Passport titles are left out.</summary>
    public async Task<List<Episode>> EpisodesAsync(string slug, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.pbs.org/show/{slug}/");
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Aetherstream");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);

        // Each card: a title link, then a description line "Kind | length | words", then a still.
        var titles = new Dictionary<string, string>();
        foreach (Match m in TitleLink().Matches(html))
        {
            var path = m.Groups[1].Value;
            var title = WebUtility.HtmlDecode(m.Groups[2].Value).Trim();
            if (title.Length > 0 && !titles.ContainsKey(path))
                titles[path] = title;
        }

        var stills = new Dictionary<string, string>();
        var passport = new HashSet<string>();
        foreach (Match m in StillLink().Matches(html))
        {
            var path = m.Groups[1].Value;
            if (!stills.ContainsKey(path))
                stills[path] = WebUtility.HtmlDecode(m.Groups[2].Value);

            // Members-only titles wear a Passport badge right after the still; they need a login this cannot give.
            var tail = html.AsSpan(m.Index + m.Length, Math.Min(600, html.Length - m.Index - m.Length));
            if (tail.IndexOf("passport_badge", StringComparison.Ordinal) >= 0)
                passport.Add(path);
        }

        var descriptions = new Dictionary<string, (string Kind, string Length, string Words)>();
        var order = new List<string>();
        foreach (Match m in Card().Matches(html))
        {
            var path = m.Groups[1].Value;
            if (!descriptions.ContainsKey(path))
            {
                descriptions[path] = (WebUtility.HtmlDecode(m.Groups[2].Value).Trim(), m.Groups[3].Value.Trim(), WebUtility.HtmlDecode(m.Groups[4].Value).Trim());
                order.Add(path);
            }
        }

        var list = new List<Episode>();
        foreach (var path in order.Count > 0 ? order : titles.Keys.ToList())
        {
            var title = titles.GetValueOrDefault(path, string.Empty);
            if (title.StartsWith("Preview: ", StringComparison.OrdinalIgnoreCase) || passport.Contains(path))
                continue;
            var d = descriptions.GetValueOrDefault(path);
            list.Add(new Episode("https://www.pbs.org" + path, title.Length > 0 ? title : path.Trim('/'), stills.GetValueOrDefault(path, string.Empty), d.Kind ?? string.Empty, d.Length ?? string.Empty, d.Words ?? string.Empty));
        }

        return list;
    }

    [GeneratedRegex(@"<a href=""(/video/[a-z0-9-]+/)""[^>]*>([^<]{2,200})</a>")]
    private static partial Regex TitleLink();

    [GeneratedRegex(@"<a href=""(/video/[a-z0-9-]+/)""><img src=""([^""?]+)")]
    private static partial Regex StillLink();

    [GeneratedRegex(@"<a href=""(/video/[a-z0-9-]+/)""[^>]*>[^<]{2,200}</a></p>.*?video_description"">([^|<]+)\|([^|<]+)\|([^<]*)</p>", RegexOptions.Singleline)]
    private static partial Regex Card();
}
