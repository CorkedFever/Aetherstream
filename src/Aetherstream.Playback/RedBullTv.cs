using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// Red Bull TV: films, documentaries, shows and replays, free and worldwide. A session token
/// from its API, the shelves from its discover page, each collection on its own, a search, and
/// a film's stream straight from its media service as an HLS playlist. The same route yt-dlp
/// takes, done here so nothing has to be installed.
/// </summary>
public sealed class RedBullTv(HttpClient http)
{
    public const int MaxItems = 40;

    public sealed record Session(string Token, DateTime MadeUtc);

    /// <summary>Something on a shelf: a film, a video, an episode, or a show that opens into one.</summary>
    public readonly record struct Item(string Id, string Title, string Subheading, string Kind, double Seconds, string Description)
    {
        /// <summary>The wide cover art, at a tile's width.</summary>
        public string Image => $"https://resources.redbull.tv/{this.Id}/rbtv_cover_art_landscape/im:i:w_400";

        public bool IsShow => this.Kind == "show";
    }

    public readonly record struct Shelf(string Label, string Id);

    public async Task<Session> SessionAsync(CancellationToken ct)
    {
        var text = await http.GetStringAsync("https://api.redbull.tv/v3/session?category=personal_computer&os_family=http", ct);
        using var doc = JsonDocument.Parse(text);
        var token = doc.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("no token");
        return new Session(token, DateTime.UtcNow);
    }

    /// <summary>The shelves on the discover page, by label, without their items.</summary>
    public async Task<List<Shelf>> ShelvesAsync(Session s, CancellationToken ct)
    {
        var text = await GetAsync(s, "https://api.redbull.tv/v3/products/discover", ct);
        var list = new List<Shelf>();
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("collections", out var cols))
        {
            foreach (var c in cols.EnumerateArray())
            {
                var label = Str(c, "label").Trim();
                var id = Str(c, "id");
                if (label.Length > 0 && id.Length > 0 && id != "playlist")
                    list.Add(new Shelf(label, id));
            }
        }

        return list;
    }

    public async Task<List<Item>> ShelfAsync(Session s, string id, CancellationToken ct)
    {
        var text = await GetAsync(s, $"https://api.redbull.tv/v3/collections/{Uri.EscapeDataString(id)}?limit={MaxItems}", ct);
        using var doc = JsonDocument.Parse(text);
        return Items(doc.RootElement);
    }

    public async Task<List<Item>> SearchAsync(Session s, string query, CancellationToken ct)
    {
        var text = await GetAsync(s, $"https://api.redbull.tv/v3/search?q={Uri.EscapeDataString(query.Trim())}&limit={MaxItems}", ct);
        var list = new List<Item>();
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("collections", out var cols))
        {
            foreach (var c in cols.EnumerateArray())
                list.AddRange(Items(c));
        }

        return list;
    }

    /// <summary>What a show plays when pressed: the id its play button carries, or null.</summary>
    public async Task<string?> PlayIdOfAsync(Session s, string id, CancellationToken ct)
    {
        var text = await GetAsync(s, $"https://api.redbull.tv/v3/products/{Uri.EscapeDataString(id)}", ct);
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("links", out var links))
            return null;
        foreach (var l in links.EnumerateArray())
        {
            if (Str(l, "action") == "play" && Str(l, "id").Length > 0)
                return Str(l, "id");
        }

        return null;
    }

    /// <summary>The HLS playlist for a film, video or episode id.</summary>
    public static string StreamUrl(Session s, string id) => $"https://dms.redbull.tv/v3/{id}/{s.Token}/playlist.m3u8";

    private static List<Item> Items(JsonElement collection)
    {
        var list = new List<Item>();
        if (!collection.TryGetProperty("items", out var items))
            return list;
        foreach (var e in items.EnumerateArray())
        {
            var id = Str(e, "id");
            if (id.StartsWith("page:", StringComparison.Ordinal))
                id = id[5..];
            var title = Str(e, "title");
            var kind = Str(e, "content_type");
            if (id.Length == 0 || title.Length == 0 || !id.StartsWith("rrn:content:", StringComparison.Ordinal))
                continue;
            if (kind is not ("film" or "video" or "episode" or "show" or "live_video"))
                continue;
            var ms = e.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0;
            list.Add(new Item(id, title, Str(e, "subheading"), kind == "live_video" ? "video" : kind, ms / 1000.0, Str(e, "short_description")));
        }

        return list;
    }

    private async Task<string> GetAsync(Session s, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", s.Token);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
