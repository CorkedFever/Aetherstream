using System.Text.Json;

using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// TED: every talk, free and worldwide. TED's site has an open GraphQL endpoint that lists the
/// newest talks, its curated playlists, and a search; a talk's player data names a plain HLS
/// manifest. yt-dlp's TED support is broken at the moment, so this resolves the talks itself.
/// </summary>
public sealed class TedTalks(HttpClient http)
{
    public const int MaxItems = 40;

    private const string Api = "https://www.ted.com/graphql?query=";

    private const string Fields = "id slug title duration presenterDisplayName primaryImageSet{url}";

    public readonly record struct Playlist(string Id, string Slug, string Title, int Count);

    public readonly record struct Item(string Id, string Slug, string Title, string Speaker, double Seconds, string Image)
    {
        public string Url => "https://www.ted.com/talks/" + this.Slug;
    }

    public async Task<List<Playlist>> PlaylistsAsync(CancellationToken ct)
    {
        var text = await this.QueryAsync("{playlists(first:24){nodes{id slug title videos{totalCount}}}}", ct);
        var list = new List<Playlist>();
        using var doc = JsonDocument.Parse(text);
        foreach (var p in doc.RootElement.GetProperty("data").GetProperty("playlists").GetProperty("nodes").EnumerateArray())
        {
            var count = p.TryGetProperty("videos", out var v) && v.TryGetProperty("totalCount", out var c) ? c.GetInt32() : 0;
            if (count > 0)
                list.Add(new Playlist(Str(p, "id"), Str(p, "slug"), Str(p, "title").Trim(), count));
        }

        return list;
    }

    public async Task<List<Item>> NewestAsync(CancellationToken ct)
    {
        var text = await this.QueryAsync($"{{videos(first:{MaxItems}){{nodes{{{Fields}}}}}}}", ct);
        using var doc = JsonDocument.Parse(text);
        return Items(doc.RootElement.GetProperty("data").GetProperty("videos").GetProperty("nodes"));
    }

    public async Task<List<Item>> PlaylistAsync(Playlist playlist, CancellationToken ct)
    {
        var text = await this.QueryAsync($"{{playlists(first:60){{nodes{{id videos(first:{MaxItems}){{nodes{{{Fields}}}}}}}}}}}", ct);
        using var doc = JsonDocument.Parse(text);
        foreach (var p in doc.RootElement.GetProperty("data").GetProperty("playlists").GetProperty("nodes").EnumerateArray())
        {
            if (Str(p, "id") == playlist.Id && p.TryGetProperty("videos", out var v))
                return Items(v.GetProperty("nodes"));
        }

        return [];
    }

    /// <summary>A search names talks by slug only, so the talks are then fetched in one aliased query.</summary>
    public async Task<List<Item>> SearchAsync(string query, CancellationToken ct)
    {
        var q = query.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"");
        var text = await this.QueryAsync($"{{search(q:\"{q}\"){{results{{... on SearchTalk{{slug}}}}}}}}", ct);
        var slugs = new List<string>();
        using (var doc = JsonDocument.Parse(text))
        {
            foreach (var r in doc.RootElement.GetProperty("data").GetProperty("search").GetProperty("results").EnumerateArray())
            {
                var slug = Str(r, "slug");
                if (slug.Length > 0 && !slugs.Contains(slug))
                    slugs.Add(slug);
            }
        }

        if (slugs.Count == 0)
            return [];

        var parts = slugs.Take(MaxItems).Select((s, i) => $"t{i}:video(slug:\"{s}\"){{{Fields}}}");
        var batch = await this.QueryAsync("{" + string.Join(" ", parts) + "}", ct);
        var list = new List<Item>();
        using (var doc = JsonDocument.Parse(batch))
        {
            foreach (var prop in doc.RootElement.GetProperty("data").EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object && Parse(prop.Value) is { } item)
                    list.Add(item);
            }
        }

        return list;
    }

    /// <summary>The HLS manifest and title of a talk, from its player data.</summary>
    public async Task<(string Stream, string Title)> StreamAsync(string slug, CancellationToken ct)
    {
        var text = await this.QueryAsync($"{{video(slug:\"{slug}\"){{title playerData}}}}", ct);
        using var doc = JsonDocument.Parse(text);
        var video = doc.RootElement.GetProperty("data").GetProperty("video");
        if (video.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("TED has no talk by that name.");

        using var player = JsonDocument.Parse(Str(video, "playerData"));
        var stream = player.RootElement.TryGetProperty("resources", out var r) && r.TryGetProperty("hls", out var hls) ? Str(hls, "stream") : string.Empty;
        if (stream.Length == 0)
            throw new InvalidOperationException("TED lists no stream for that talk.");

        return (stream, Str(video, "title"));
    }

    private static List<Item> Items(JsonElement nodes)
    {
        var list = new List<Item>();
        foreach (var n in nodes.EnumerateArray())
        {
            if (Parse(n) is { } item)
                list.Add(item);
        }

        return list;
    }

    private static Item? Parse(JsonElement n)
    {
        var slug = Str(n, "slug");
        var title = Str(n, "title");
        if (slug.Length == 0 || title.Length == 0)
            return null;
        var image = string.Empty;
        if (n.TryGetProperty("primaryImageSet", out var set) && set.ValueKind == JsonValueKind.Array)
        {
            foreach (var i in set.EnumerateArray())
            {
                image = Str(i, "url");
                if (image.Length > 0)
                    break;
            }
        }

        var seconds = n.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0;
        return new Item(Str(n, "id"), slug, title, Str(n, "presenterDisplayName"), seconds, image);
    }

    private async Task<string> QueryAsync(string query, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Api + Uri.EscapeDataString(query));
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 Aetherstream");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}

/// <summary>A talk's page address, resolved to its HLS manifest through TED's own player data.</summary>
public sealed class TedResolver(HttpClient http) : IStreamResolver
{
    public static bool Matches(string input) =>
        Uri.TryCreate(input, UriKind.Absolute, out var uri)
        && (uri.Host.Equals("www.ted.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("ted.com", StringComparison.OrdinalIgnoreCase))
        && uri.AbsolutePath.StartsWith("/talks/", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Length > "/talks/".Length;

    public async Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        var slug = new Uri(input).AbsolutePath["/talks/".Length..].Trim('/');
        var (stream, title) = await new TedTalks(http).StreamAsync(slug, ct);
        return new ResolvedStream(stream, title.Length > 0 ? title : "TED");
    }
}
