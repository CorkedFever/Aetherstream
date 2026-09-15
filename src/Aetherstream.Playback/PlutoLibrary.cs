using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// Pluto TV's on-demand side: free films and series with Pluto's own ad breaks in the stream.
/// A session comes from its boot endpoint, the catalogue from its VOD service in categories,
/// a search from its search service, and a film or episode plays as an ordinary HLS playlist
/// from its stitcher, with the session's parameters and token on the address. No account.
/// </summary>
public sealed class PlutoLibrary(HttpClient http)
{
    public const int MaxItems = 40;

    public sealed record Session(string Token, string StitcherParams, string Stitcher, string Vod, string Search, DateTime MadeUtc);

    /// <summary>A film, a series, or an episode: enough for a tile, and the path to play it.</summary>
    public readonly record struct Item(string Id, string Name, string Type, string Summary, string Genre, string Rating, string Poster, string Wide, string HlsPath, double Seconds, int Number, int Season);

    public readonly record struct Category(string Name, List<Item> Items);

    public readonly record struct SeasonOf(int Number, string Name, List<Item> Episodes);

    /// <summary>A session for a few hours: the token, the stitcher parameters, and where the services are.</summary>
    public async Task<Session> BootAsync(CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N")[..16];
        var url = "https://boot.pluto.tv/v4/start?appName=web&appVersion=9.0.0&deviceVersion=120.0.0&deviceModel=web&deviceMake=chrome&deviceType=web"
            + $"&clientID=aetherstream-{id}&clientModelNumber=1.0.0&serverSideAds=false&drmCapabilities=widevine%3AL3";
        var text = await http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var servers = root.GetProperty("servers");
        return new Session(
            Str(root, "sessionToken"),
            Str(root, "stitcherParams"),
            Str(servers, "stitcher").TrimEnd('/'),
            Str(servers, "vod").TrimEnd('/'),
            Str(servers, "search").TrimEnd('/'),
            DateTime.UtcNow);
    }

    /// <summary>The shelves as Pluto arranges them, each with its first films and series.</summary>
    public async Task<List<Category>> CategoriesAsync(Session s, CancellationToken ct)
    {
        var text = await GetAsync(s, $"{s.Vod}/v4/vod/categories?offset=0&page=1&includeItems=true&itemsPerCategory={MaxItems}", ct);
        var list = new List<Category>();
        using var doc = JsonDocument.Parse(text);
        foreach (var c in doc.RootElement.GetProperty("categories").EnumerateArray())
        {
            var items = new List<Item>();
            if (c.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                {
                    if (Parse(e) is { } item)
                        items.Add(item);
                }
            }

            if (items.Count > 0)
                list.Add(new Category(Str(c, "name"), items));
        }

        return list;
    }

    public async Task<List<Item>> SearchAsync(Session s, string query, CancellationToken ct)
    {
        var text = await GetAsync(s, $"{s.Search}/v1/search?q={Uri.EscapeDataString(query.Trim())}&limit={MaxItems}", ct);
        var list = new List<Item>();
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in data.EnumerateArray())
            {
                if (Parse(e) is { } item && item.Type is "movie" or "series")
                    list.Add(item);
            }
        }

        return list;
    }

    /// <summary>A series' seasons with their episodes, in order.</summary>
    public async Task<List<SeasonOf>> SeasonsAsync(Session s, string seriesId, CancellationToken ct)
    {
        var text = await GetAsync(s, $"{s.Vod}/v4/vod/series/{Uri.EscapeDataString(seriesId)}/seasons?includeItems=true&offset=1000&page=1", ct);
        var list = new List<SeasonOf>();
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("seasons", out var seasons))
            return list;
        foreach (var season in seasons.EnumerateArray())
        {
            var number = season.TryGetProperty("number", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : list.Count + 1;
            var episodes = new List<Item>();
            if (season.TryGetProperty("episodes", out var eps) && eps.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in eps.EnumerateArray())
                {
                    if (Parse(e) is { } item)
                        episodes.Add(item);
                }
            }

            episodes.Sort((a, b) => a.Number.CompareTo(b.Number));
            list.Add(new SeasonOf(number, Str(season, "name").Length > 0 ? Str(season, "name") : $"Season {number}", episodes));
        }

        list.Sort((a, b) => a.Number.CompareTo(b.Number));
        return list;
    }

    /// <summary>The playable address for a film or an episode: the stitcher's HLS path with the session on it.</summary>
    public static string StreamUrl(Session s, Item item) =>
        s.Stitcher + item.HlsPath + (item.HlsPath.Contains('?') ? "&" : "?") + s.StitcherParams + "&jwt=" + Uri.EscapeDataString(s.Token);

    private async Task<string> GetAsync(Session s, string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", "Bearer " + s.Token);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static Item? Parse(JsonElement e)
    {
        var id = Str(e, "_id").Length > 0 ? Str(e, "_id") : Str(e, "id");
        var name = Str(e, "name");
        var type = Str(e, "type");
        if (id.Length == 0 || name.Length == 0)
            return null;

        var poster = string.Empty;
        var wide = string.Empty;
        if (e.TryGetProperty("covers", out var covers) && covers.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in covers.EnumerateArray())
            {
                var ratio = Str(c, "aspectRatio");
                var url = Str(c, "url");
                if (ratio == "347:500" && poster.Length == 0)
                    poster = url;
                if (ratio == "16:9" && wide.Length == 0)
                    wide = url;
            }
        }

        if (poster.Length == 0 && e.TryGetProperty("featuredImage", out var fi) && fi.ValueKind == JsonValueKind.Object)
            poster = Str(fi, "path");
        if (wide.Length == 0)
            wide = Str(e, "poster16_9");

        var hls = string.Empty;
        if (e.TryGetProperty("stitched", out var st) && st.ValueKind == JsonValueKind.Object && st.TryGetProperty("paths", out var paths) && paths.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in paths.EnumerateArray())
            {
                if (Str(p, "type") == "hls")
                    hls = Str(p, "path");
            }
        }

        var ms = e.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0;
        return new Item(
            id, name, type,
            Str(e, "summary").Length > 0 ? Str(e, "summary") : Str(e, "description"),
            Str(e, "genre"), Str(e, "rating"), poster, wide, hls, ms / 1000.0,
            e.TryGetProperty("number", out var num) && num.ValueKind == JsonValueKind.Number ? num.GetInt32() : 0,
            e.TryGetProperty("season", out var sea) && sea.ValueKind == JsonValueKind.Number ? sea.GetInt32() : 0);
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
