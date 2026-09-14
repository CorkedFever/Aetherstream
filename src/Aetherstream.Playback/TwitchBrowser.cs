using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// Twitch, browsed: who is live at the top, who is live in a game, a search for channels and
/// games, and with a signed-in session, who you follow that is live. Through the same GraphQL
/// endpoint and web client id the resolver already uses for playback tokens; the session is the
/// browser's auth cookie, borrowed the way YouTube's is.
/// </summary>
public sealed class TwitchBrowser(HttpClient http)
{
    private const string ClientId = "kimne78kx3ncx6brgo4mv6wki5h1ko";
    private const string Gql = "https://gql.twitch.tv/gql";
    private const string StreamFields = "title viewersCount game { name displayName } previewImageURL(width: 320, height: 180)";

    public readonly record struct Stream(string Login, string DisplayName, string Title, long Viewers, string Game, string Preview, string Avatar);

    public readonly record struct Game(string Name, string DisplayName, long Viewers, string BoxArt);

    public sealed record SearchResult(List<Stream> Channels, List<Game> Games);

    /// <summary>The most watched streams right now.</summary>
    public async Task<List<Stream>> TopAsync(CancellationToken ct)
    {
        var doc = await QueryAsync("{ streams(first: 30) { edges { node { " + StreamFields + " broadcaster { login displayName profileImageURL(width: 70) } } } } }", null, ct);
        var streams = doc.RootElement.GetProperty("data").GetProperty("streams");
        return streams.ValueKind == JsonValueKind.Object ? Edges(streams) : [];
    }

    /// <summary>The most watched streams in a game, by its exact name.</summary>
    public async Task<List<Stream>> ByGameAsync(string game, CancellationToken ct)
    {
        var doc = await QueryAsync("{ game(name: " + Quote(game) + ") { streams(first: 30) { edges { node { " + StreamFields + " broadcaster { login displayName profileImageURL(width: 70) } } } } } }", null, ct);
        var g = doc.RootElement.GetProperty("data").GetProperty("game");
        return g.ValueKind == JsonValueKind.Object && g.TryGetProperty("streams", out var st) && st.ValueKind == JsonValueKind.Object ? Edges(st) : [];
    }

    /// <summary>Channels (live ones first) and games matching a search.</summary>
    public async Task<SearchResult> SearchAsync(string query, CancellationToken ct)
    {
        var doc = await QueryAsync("{ searchFor(userQuery: " + Quote(query) + ", platform: \"web\") { channels { edges { item { ... on User { login displayName profileImageURL(width: 70) stream { " + StreamFields + " } } } } } games { edges { item { ... on Game { name displayName viewersCount boxArtURL } } } } } }", null, ct);
        var s = doc.RootElement.GetProperty("data").GetProperty("searchFor");
        var channels = new List<Stream>();
        foreach (var e in s.GetProperty("channels").GetProperty("edges").EnumerateArray())
        {
            var u = e.GetProperty("item");
            var stream = u.TryGetProperty("stream", out var st) && st.ValueKind == JsonValueKind.Object ? st : (JsonElement?)null;
            channels.Add(new Stream(
                Str(u, "login"), Str(u, "displayName"),
                stream is { } sv ? Str(sv, "title") : string.Empty,
                stream is { } sv2 && sv2.TryGetProperty("viewersCount", out var vc) && vc.ValueKind == JsonValueKind.Number ? vc.GetInt64() : -1,
                stream is { } sv3 ? GameName(sv3) : string.Empty,
                stream is { } sv4 ? Str(sv4, "previewImageURL") : string.Empty,
                Str(u, "profileImageURL")));
        }

        var games = new List<Game>();
        foreach (var e in s.GetProperty("games").GetProperty("edges").EnumerateArray())
        {
            var g = e.GetProperty("item");
            games.Add(new Game(Str(g, "name"), Str(g, "displayName"), g.TryGetProperty("viewersCount", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0, Str(g, "boxArtURL").Replace("{width}", "285").Replace("{height}", "380")));
        }

        channels.Sort((a, b) => b.Viewers.CompareTo(a.Viewers));
        return new SearchResult(channels, games);
    }

    /// <summary>
    /// Who you follow that is live, through a session's auth token. The follows list only comes
    /// out of the persisted query Twitch's own site uses; a plain query is refused. The logins
    /// then go into an ordinary batch query for whether each is live.
    /// </summary>
    public async Task<List<Stream>> FollowedAsync(string login, string token, CancellationToken ct)
    {
        var logins = new List<string>();
        string? cursor = null;
        for (var page = 0; page < 5; page++)
        {
            var variables = cursor is null ? "{\"limit\":100,\"order\":\"DESC\"}" : "{\"limit\":100,\"order\":\"DESC\",\"cursor\":" + Quote(cursor) + "}";
            var body = "[{\"operationName\":\"ChannelFollows\",\"variables\":" + variables + ",\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"eecf815273d3d949e5cf0085cc5084cd8a1b5b7b6f7990cf43cb0beadf546907\"}}}]";
            using var doc = await PostAsync(body, token, ct);
            var first = doc.RootElement[0];
            if (!first.TryGetProperty("data", out var data) || !data.TryGetProperty("user", out var u) || u.ValueKind != JsonValueKind.Object)
                break;
            if (!u.TryGetProperty("follows", out var follows) || follows.ValueKind != JsonValueKind.Object || !follows.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
                break;
            cursor = null;
            var count = 0;
            foreach (var e in edges.EnumerateArray())
            {
                count++;
                var l = Str(e.GetProperty("node"), "login");
                if (l.Length > 0)
                    logins.Add(l);
                if (e.TryGetProperty("cursor", out var c) && c.ValueKind == JsonValueKind.String)
                    cursor = c.GetString();
            }

            if (cursor is null || count < 100 || !follows.TryGetProperty("pageInfo", out var pi) || !pi.TryGetProperty("hasNextPage", out var hn) || hn.ValueKind != JsonValueKind.True)
                break;
        }

        var live = new List<Stream>();
        foreach (var chunk in logins.Chunk(100))
        {
            var list = string.Join(",", chunk.Select(Quote));
            var doc = await QueryAsync("{ users(logins: [" + list + "]) { login displayName profileImageURL(width: 70) stream { " + StreamFields + " } } }", null, ct);
            foreach (var u in doc.RootElement.GetProperty("data").GetProperty("users").EnumerateArray())
            {
                if (u.ValueKind != JsonValueKind.Object || !u.TryGetProperty("stream", out var st) || st.ValueKind != JsonValueKind.Object)
                    continue;
                live.Add(new Stream(Str(u, "login"), Str(u, "displayName"), Str(st, "title"), st.GetProperty("viewersCount").GetInt64(), GameName(st), Str(st, "previewImageURL"), Str(u, "profileImageURL")));
            }
        }

        live.Sort((a, b) => b.Viewers.CompareTo(a.Viewers));
        return live;
    }

    private async Task<JsonDocument> PostAsync(string body, string? token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Gql);
        request.Headers.Add("Client-ID", ClientId);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Add("Authorization", "OAuth " + token);
        request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>The login the session belongs to, or null when the token is not accepted.</summary>
    public async Task<string?> WhoAmIAsync(string token, CancellationToken ct)
    {
        var doc = await QueryAsync("{ currentUser { login } }", token, ct);
        var u = doc.RootElement.GetProperty("data").GetProperty("currentUser");
        return u.ValueKind == JsonValueKind.Object ? Str(u, "login") : null;
    }

    private static List<Stream> Edges(JsonElement connection)
    {
        var list = new List<Stream>();
        foreach (var e in connection.GetProperty("edges").EnumerateArray())
        {
            if (!e.TryGetProperty("node", out var n) || n.ValueKind != JsonValueKind.Object || !n.TryGetProperty("broadcaster", out var b) || b.ValueKind != JsonValueKind.Object)
                continue;
            list.Add(new Stream(Str(b, "login"), Str(b, "displayName"), Str(n, "title"), n.TryGetProperty("viewersCount", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0, GameName(n), Str(n, "previewImageURL"), Str(b, "profileImageURL")));
        }

        return list;
    }

    private async Task<JsonDocument> QueryAsync(string query, string? token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Gql);
        request.Headers.Add("Client-ID", ClientId);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Add("Authorization", "OAuth " + token);
        request.Content = new StringContent(JsonSerializer.Serialize(new { query }), System.Text.Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            throw new InvalidOperationException(errors[0].TryGetProperty("message", out var m) ? m.GetString() ?? "Twitch refused" : "Twitch refused");
        return doc;
    }

    private static string GameName(JsonElement stream) =>
        stream.TryGetProperty("game", out var g) && g.ValueKind == JsonValueKind.Object ? (Str(g, "displayName").Length > 0 ? Str(g, "displayName") : Str(g, "name")) : string.Empty;

    private static string Quote(string s) => JsonSerializer.Serialize(s);

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
