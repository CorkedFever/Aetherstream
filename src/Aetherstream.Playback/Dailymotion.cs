using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// Dailymotion: an open public API for its channels, listings and search, and a player metadata
/// search. No key for any of it. Playback is yt-dlp's: the manifest host turns away any client
/// whose TLS handshake is not a browser's, and yt-dlp is the one thing here that can pass for one.
/// </summary>
public sealed class Dailymotion(HttpClient http)
{
    public const int MaxItems = 40;

    private const string Agent = "Mozilla/5.0 Aetherstream";

    public readonly record struct Channel(string Id, string Name);

    public readonly record struct Item(string Id, string Title, string Thumb, double Seconds, string Owner)
    {
        public string Url => "https://www.dailymotion.com/video/" + this.Id;
    }

    public async Task<List<Channel>> ChannelsAsync(CancellationToken ct)
    {
        var text = await this.GetAsync("https://api.dailymotion.com/channels?fields=id,name&limit=40", ct);
        var list = new List<Channel>();
        using var doc = JsonDocument.Parse(text);
        foreach (var c in doc.RootElement.GetProperty("list").EnumerateArray())
            list.Add(new Channel(Str(c, "id"), Str(c, "name")));
        return list;
    }

    /// <summary>Trending videos, in a channel when one is given, or matching a search; English, and longer than a clip.</summary>
    public async Task<List<Item>> VideosAsync(string channel, string search, CancellationToken ct)
    {
        var url = $"https://api.dailymotion.com/videos?fields=id,title,thumbnail_360_url,duration,owner.screenname&limit={MaxItems}&language=en&longer_than=5";
        url += search.Trim().Length > 0 ? "&sort=relevance&search=" + Uri.EscapeDataString(search.Trim()) : "&sort=trending";
        if (channel.Length > 0)
            url += "&channel=" + Uri.EscapeDataString(channel);

        var text = await this.GetAsync(url, ct);
        var list = new List<Item>();
        using var doc = JsonDocument.Parse(text);
        foreach (var v in doc.RootElement.GetProperty("list").EnumerateArray())
        {
            var seconds = v.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0;
            list.Add(new Item(Str(v, "id"), Str(v, "title"), Str(v, "thumbnail_360_url"), seconds, Str(v, "owner.screenname")));
        }

        return list;
    }

    private async Task<string> GetAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", Agent);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
