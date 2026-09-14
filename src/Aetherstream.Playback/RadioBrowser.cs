using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// Internet radio through the Radio Browser directory: a community-kept list of some thirty
/// thousand stations with their stream addresses, logos and tags, behind a public API on a few
/// mirrors. No key. A station is a URL the jukebox plays like any track, except it never ends.
/// </summary>
public sealed class RadioBrowser(HttpClient http)
{
    private static readonly string[] Mirrors = ["https://de1.api.radio-browser.info", "https://at1.api.radio-browser.info", "https://nl1.api.radio-browser.info"];

    /// <summary>Genres worth a chip, in the order they are offered.</summary>
    public static readonly string[] Tags = ["ambient", "jazz", "lofi", "classical", "chillout", "electronic", "rock", "pop", "soul", "news", "talk", "anime", "video game", "80s", "synthwave", "world music"];

    public readonly record struct Station(string Id, string Name, string Url, string Logo, string Tags, string Country, string Codec, int Bitrate, long Votes);

    /// <summary>Stations by name and, or, tag: the most voted first, broken ones left out.</summary>
    public async Task<List<Station>> SearchAsync(string name, string tag, int limit, CancellationToken ct)
    {
        var query = $"/json/stations/search?limit={limit}&hidebroken=true&order=votes&reverse=true"
            + (name.Trim().Length > 0 ? "&name=" + Uri.EscapeDataString(name.Trim()) : string.Empty)
            + (tag.Length > 0 ? "&tag=" + Uri.EscapeDataString(tag) : string.Empty);

        Exception? last = null;
        foreach (var mirror in Mirrors)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, mirror + query);
                request.Headers.UserAgent.ParseAdd("Aetherstream/1.0");
                using var response = await http.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                var text = await response.Content.ReadAsStringAsync(ct);
                return Parse(text);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
            }
        }

        throw last ?? new InvalidOperationException("no mirror answered");
    }

    private static List<Station> Parse(string json)
    {
        var list = new List<Station>();
        using var doc = JsonDocument.Parse(json);
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var url = Str(e, "url_resolved");
            if (url.Length == 0)
                url = Str(e, "url");
            var name = Str(e, "name").Trim();
            if (url.Length == 0 || name.Length == 0)
                continue;

            list.Add(new Station(
                Str(e, "stationuuid"),
                name,
                url,
                Str(e, "favicon"),
                Str(e, "tags"),
                Str(e, "country"),
                Str(e, "codec"),
                e.TryGetProperty("bitrate", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetInt32() : 0,
                e.TryGetProperty("votes", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0));
        }

        return list;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
