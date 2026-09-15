using System.Text;
using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// A Jellyfin or Emby server: yours, on your network or wherever you put it. The two speak the
/// same API, Emby's, which Jellyfin kept when it forked, so one client serves both; the server
/// says which it is when asked. A sign-in with a username and password gives back a token that
/// is kept in place of the password, and everything after that is the token: the libraries, a
/// search, a series' episodes, what you were in the middle of, the art, and the file itself,
/// streamed as it sits on disk.
/// </summary>
public sealed class MediaServer(HttpClient http, string server, string token, string userId, string deviceId)
{
    public const int MaxItems = 60;

    /// <summary>What the server says it is, before any sign-in.</summary>
    public readonly record struct Identity(string Product, string Name, string Version)
    {
        /// <summary>"Emby" or "Jellyfin", from the product name; Jellyfin when unsure, being the more common of the two.</summary>
        public string Kind => this.Product.Contains("Emby", StringComparison.OrdinalIgnoreCase) ? "Emby" : "Jellyfin";
    }

    public readonly record struct SignedIn(string Token, string UserId, string UserName);

    /// <summary>A library: films, shows, home videos. Music and photos are left to other tools.</summary>
    public readonly record struct View(string Id, string Name, string Kind);

    /// <summary>A film, a series, or an episode; enough for a tile and to play it.</summary>
    public readonly record struct Item(
        string Id, string Name, string Type, int Year, double Seconds, string Overview,
        string Series, int Season, int Episode, double ResumeSeconds)
    {
        public bool IsSeries => this.Type == "Series";

        public bool IsEpisode => this.Type == "Episode";

        /// <summary>A label with the series and numbering when it is an episode.</summary>
        public string Label => this.IsEpisode && this.Series.Length > 0
            ? $"{this.Series} S{this.Season}E{this.Episode} · {this.Name}"
            : this.Name;
    }

    public string Server => server;

    public static string Normalise(string address)
    {
        var a = address.Trim().TrimEnd('/');
        if (a.Length > 0 && !a.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !a.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            a = "http://" + a;
        return a;
    }

    public static async Task<Identity> IdentifyAsync(HttpClient http, string server, CancellationToken ct)
    {
        var text = await http.GetStringAsync(Normalise(server) + "/System/Info/Public", ct);
        using var doc = JsonDocument.Parse(text);
        return new Identity(Str(doc.RootElement, "ProductName"), Str(doc.RootElement, "ServerName"), Str(doc.RootElement, "Version"));
    }

    /// <summary>Signs in by name. The password goes to the server once and is not kept; the token it answers with is.</summary>
    public static async Task<SignedIn> SignInAsync(HttpClient http, string server, string user, string password, string deviceId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Normalise(server) + "/Users/AuthenticateByName");
        Authorise(request, deviceId, null);
        request.Content = new StringContent(JsonSerializer.Serialize(new { Username = user, Pw = password }), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("The server did not accept that name and password.");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var u = root.GetProperty("User");
        return new SignedIn(Str(root, "AccessToken"), Str(u, "Id"), Str(u, "Name"));
    }

    public async Task<List<View>> ViewsAsync(CancellationToken ct)
    {
        var text = await this.GetAsync($"/Users/{userId}/Views", ct);
        var list = new List<View>();
        using var doc = JsonDocument.Parse(text);
        foreach (var v in doc.RootElement.GetProperty("Items").EnumerateArray())
        {
            var kind = Str(v, "CollectionType");
            if (kind is "music" or "playlists" or "books" or "photos" or "musicvideos" or "livetv" or "boxsets")
                continue;
            list.Add(new View(Str(v, "Id"), Str(v, "Name"), kind));
        }

        return list;
    }

    /// <summary>The newest films and series in a library, or whatever matches a search across all of them.</summary>
    public async Task<List<Item>> ItemsAsync(string viewId, string search, CancellationToken ct)
    {
        var url = $"/Users/{userId}/Items?Recursive=true&Limit={MaxItems}&Fields=Overview,ProductionYear,SeriesName";
        if (search.Trim().Length > 0)
            url += "&IncludeItemTypes=Movie,Series,Episode&SearchTerm=" + Uri.EscapeDataString(search.Trim());
        else
            url += "&IncludeItemTypes=Movie,Series&SortBy=DateCreated&SortOrder=Descending";
        if (viewId.Length > 0)
            url += "&ParentId=" + Uri.EscapeDataString(viewId);
        return await this.ListAsync(url, ct);
    }

    /// <summary>What was left part-way through, newest first.</summary>
    public async Task<List<Item>> ContinueAsync(CancellationToken ct) =>
        await this.ListAsync($"/Users/{userId}/Items/Resume?MediaTypes=Video&Limit={MaxItems}&Fields=Overview,ProductionYear,SeriesName", ct);

    /// <summary>Every episode of a series, in order.</summary>
    public async Task<List<Item>> EpisodesAsync(string seriesId, CancellationToken ct)
    {
        var list = await this.ListAsync($"/Shows/{Uri.EscapeDataString(seriesId)}/Episodes?UserId={userId}&Fields=Overview,SeriesName", ct);
        list.Sort((a, b) => a.Season != b.Season ? a.Season.CompareTo(b.Season) : a.Episode.CompareTo(b.Episode));
        return list;
    }

    /// <summary>The primary image at tile width. The token rides on the address, as Plex's does, since art is fetched outside this client.</summary>
    public string ImageUrl(Item item) =>
        $"{server}/Items/{item.Id}/Images/Primary?maxWidth=400&api_key={Uri.EscapeDataString(token)}";

    /// <summary>The file as it sits on disk, streamed whole; the decoder handles the container.</summary>
    public string StreamUrl(Item item) =>
        $"{server}/Videos/{item.Id}/stream?static=true&api_key={Uri.EscapeDataString(token)}";

    private async Task<List<Item>> ListAsync(string path, CancellationToken ct)
    {
        var text = await this.GetAsync(path, ct);
        var list = new List<Item>();
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var items = root.ValueKind == JsonValueKind.Array ? root : root.GetProperty("Items");
        foreach (var e in items.EnumerateArray())
        {
            if (Parse(e) is { } item)
                list.Add(item);
        }

        return list;
    }

    private static Item? Parse(JsonElement e)
    {
        var id = Str(e, "Id");
        var name = Str(e, "Name");
        var type = Str(e, "Type");
        if (id.Length == 0 || name.Length == 0 || type is not ("Movie" or "Series" or "Episode" or "Video"))
            return null;

        var ticks = e.TryGetProperty("RunTimeTicks", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt64() : 0;
        var resume = e.TryGetProperty("UserData", out var ud) && ud.TryGetProperty("PlaybackPositionTicks", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : 0;
        return new Item(
            id,
            name,
            type == "Video" ? "Movie" : type,
            Int(e, "ProductionYear"),
            ticks / 10_000_000.0,
            Str(e, "Overview"),
            Str(e, "SeriesName"),
            Int(e, "ParentIndexNumber"),
            Int(e, "IndexNumber"),
            resume / 10_000_000.0);
    }

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, server + path);
        Authorise(request, deviceId, token);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("The server no longer accepts the sign-in; sign in again.");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>The authorisation both servers read: Emby's header, which Jellyfin still honours, and the token on its own as well.</summary>
    private static void Authorise(HttpRequestMessage request, string deviceId, string? token)
    {
        var value = $"MediaBrowser Client=\"Aetherstream\", Device=\"Final Fantasy XIV\", DeviceId=\"{deviceId}\", Version=\"1.0\"";
        if (token is { Length: > 0 })
        {
            value += $", Token=\"{token}\"";
            request.Headers.TryAddWithoutValidation("X-Emby-Token", token);
        }

        request.Headers.TryAddWithoutValidation("X-Emby-Authorization", value);
        request.Headers.TryAddWithoutValidation("Authorization", value);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
    }

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
