using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// The Internet Archive's films: search and collections through its advanced search API, and a
/// film's files through its metadata API, so the best MP4 can be played straight from
/// archive.org's download links. No account, no key; the files are plain URLs that redirect to
/// a storage node and support ranges, which is all libvlc needs to play and seek.
/// </summary>
public sealed class ArchiveLibrary(HttpClient http)
{
    public const int MaxItems = 60;

    /// <summary>A film in a listing: enough for a tile, and the identifier to fetch the rest.</summary>
    public readonly record struct Film(string Identifier, string Title, string Year, string Description, long Downloads)
    {
        public string Poster => $"https://archive.org/services/img/{this.Identifier}";
    }

    /// <summary>What to play for a film: the best MP4's URL, and the runtime when the archive knows it.</summary>
    public readonly record struct Pick(string Url, string FileName, long Bytes, int Width, int Height, double Seconds);

    private static readonly string[] Lurid = ["sex", "nude", "naked", "molest", "erotic", "porn", "strip", "virgin", "orgy", "reefer", "madness"];

    /// <summary>The curated shelves: a name for the chip, and the collection identifier behind it.</summary>
    public static readonly (string Title, string Collection)[] Shelves =
    [
        ("Feature Films", "feature_films"),
        ("Sci-Fi & Horror", "SciFi_Horror"),
        ("Comedy", "Comedy_Films"),
        ("Film Noir", "Film_Noir"),
        ("Silent", "silent_films"),
        ("Cartoons", "animationandcartoons"),
        ("Classic TV", "classic_tv"),
        ("Prelinger", "prelinger"),
    ];

    /// <summary>
    /// Films matching a query in a collection, most downloaded first: what the archive's own
    /// visitors pick, which is as good a guide as any to what plays and is worth playing.
    /// </summary>
    public async Task<List<Film>> SearchAsync(string collection, string query, CancellationToken ct)
    {
        var terms = new List<string> { "mediatype:movies" };
        if (collection.Length > 0)
            terms.Add($"collection:{collection}");
        if (query.Trim().Length > 0)
            terms.Add($"({Escape(query.Trim())})");

        var url = "https://archive.org/advancedsearch.php?q=" + Uri.EscapeDataString(string.Join(" AND ", terms))
            + "&fl[]=identifier&fl[]=title&fl[]=year&fl[]=description&fl[]=downloads"
            + "&sort[]=downloads+desc&rows=" + MaxItems + "&output=json";

        var text = await http.GetStringAsync(url, ct);
        var list = new List<Film>();
        using var doc = JsonDocument.Parse(text);
        foreach (var e in doc.RootElement.GetProperty("response").GetProperty("docs").EnumerateArray())
        {
            var id = Str(e, "identifier");
            var title = Str(e, "title");
            if (id.Length == 0 || title.Length == 0)
                continue;

            // The most-downloaded lists are padded with old exploitation pictures whose titles
            // nobody wants on a set in their living room. They are still there by search.
            if (query.Trim().Length == 0 && Lurid.Any(w => title.Contains(w, StringComparison.OrdinalIgnoreCase)))
                continue;

            var downloads = e.TryGetProperty("downloads", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt64() : 0;
            list.Add(new Film(id, title, Str(e, "year"), Str(e, "description"), downloads));
        }

        return list;
    }

    /// <summary>
    /// The file to play for a film: the largest H.264 MP4 the item carries, which is the original
    /// or a good derivative; the 512kb ones are the fallback when nothing better exists. Null
    /// when the item has no MP4 at all (some are only Ogg or MPEG-2, which libvlc plays but
    /// which do not seek well over HTTP).
    /// </summary>
    public async Task<Pick?> PickAsync(string identifier, CancellationToken ct)
    {
        var text = await http.GetStringAsync($"https://archive.org/metadata/{Uri.EscapeDataString(identifier)}", ct);
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("files", out var files))
            return null;

        Pick? best = null;
        foreach (var f in files.EnumerateArray())
        {
            var name = Str(f, "name");
            if (!name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                continue;

            var bytes = Num(f, "size");
            var width = (int)Num(f, "width");
            var height = (int)Num(f, "height");
            var seconds = Dbl(f, "length");
            var candidate = new Pick(
                $"https://archive.org/download/{Uri.EscapeDataString(identifier)}/{Uri.EscapeDataString(name)}",
                name, bytes, width, height, seconds);

            // Bigger is better here: the archive's small derivatives are 320 wide and the
            // originals are what people uploaded. A trailer or a sample is short; prefer the long one.
            if (best is null || Score(candidate) > Score(best.Value))
                best = candidate;
        }

        return best;
    }

    private static double Score(Pick p) => (p.Width * (double)p.Height) + (p.Seconds * 100) + (p.Bytes / 1e6);

    private static string Str(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
            return string.Empty;

        // The search API returns some fields as arrays when an item has several values.
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? string.Empty,
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.Array => v.GetArrayLength() > 0 ? Str(v[0], out var s) ? s : string.Empty : string.Empty,
            _ => string.Empty,
        };
    }

    private static bool Str(JsonElement v, out string s)
    {
        s = v.ValueKind switch { JsonValueKind.String => v.GetString() ?? string.Empty, JsonValueKind.Number => v.GetRawText(), _ => string.Empty };
        return s.Length > 0;
    }

    private static long Num(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
            return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetInt64(out var n) ? n : (long)v.GetDouble(),
            JsonValueKind.String => long.TryParse(v.GetString(), out var n) ? n : 0,
            _ => 0,
        };
    }

    private static double Dbl(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v))
            return 0;
        if (v.ValueKind == JsonValueKind.Number)
            return v.GetDouble();
        var s = v.GetString() ?? string.Empty;
        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;

        // Some lengths are "h:mm:ss".
        var parts = s.Split(':');
        if (parts.Length is 2 or 3 && parts.All(p => int.TryParse(p, out _)))
        {
            var total = 0.0;
            foreach (var p in parts)
                total = (total * 60) + int.Parse(p);
            return total;
        }

        return 0;
    }

    /// <summary>Quotes the query's special characters so a title with a colon or a dash searches as words.</summary>
    private static string Escape(string q)
    {
        var chars = "+-&|!(){}[]^\"~*?:\\/";
        var sb = new System.Text.StringBuilder();
        foreach (var ch in q)
        {
            if (chars.Contains(ch))
                sb.Append(' ');
            else
                sb.Append(ch);
        }

        return sb.ToString();
    }
}
