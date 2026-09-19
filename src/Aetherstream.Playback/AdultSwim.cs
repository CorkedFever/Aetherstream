using System.Globalization;
using System.Net;
using System.Text.Json;

using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// Adult Swim's marathon streams: the site's own free loops, a show each (Rick and Morty, The
/// Venture Bros., Channel 5, Infomercials…), no login, as plain AES-128 HLS at up to 480p. The
/// list is the site's streams API narrowed to what it offers the web, what each is showing
/// comes from the schedule API by marathon id, and a stream's playlist from the video API.
/// yt-dlp's extractor for the site is broken, so this is the plugin's own.
/// </summary>
public sealed class AdultSwim(HttpClient http)
{
    private const string Site = "https://www.adultswim.com";

    /// <summary>The user agent the site is asked with; libvlc is given the same one.</summary>
    public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36 Aetherstream";

    /// <summary>One marathon: <paramref name="VideoId"/> is what the video API is asked by, <paramref name="MarathonId"/> what the schedule is.</summary>
    public sealed record Stream(string Id, string Title, string Description, string Image, string VideoId, string MarathonId)
    {
        /// <summary>The page on the site, which the resolver turns back into the playlist.</summary>
        public string Url => $"{Site}/streams/{this.Id}";
    }

    /// <summary>One thing a marathon shows, from the schedule.</summary>
    public readonly record struct Showing(string Name, string Series, DateTime StartUtc, TimeSpan Length)
    {
        public DateTime EndUtc => this.StartUtc + this.Length;
    }

    /// <summary>The streams the site offers the web, in the site's order.</summary>
    public async Task<List<Stream>> StreamsAsync(CancellationToken ct)
    {
        var text = await this.GetAsync($"{Site}/api/streams/v1/streams", ct);
        var list = new List<Stream>();
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("streams", out var streams))
            return list;

        foreach (var s in streams.EnumerateArray())
        {
            var id = Str(s, "id");
            var title = Str(s, "title");
            var videoId = Str(s, "stream");
            if (id.Length == 0 || title.Length == 0 || videoId.Length == 0)
                continue;
            if (s.TryGetProperty("platforms", out var platforms) && platforms.ValueKind == JsonValueKind.Array
                && !platforms.EnumerateArray().Any(p => p.ValueKind == JsonValueKind.String && p.GetString() == "web"))
                continue;

            // The wide thumbnail suits a tile; the poster is the fallback, whatever its shape.
            var image = Str(s, "meta_thumbnail");
            if (image.Length == 0)
                image = Str(s, "poster");
            if (image.Length == 0)
                image = Str(s, "list_poster");
            list.Add(new Stream(id, WebUtility.HtmlDecode(title), WebUtility.HtmlDecode(Str(s, "description")), image, videoId, Str(s, "vod_to_live_id")));
        }

        return list;
    }

    /// <summary>What a marathon is showing, from now on: the schedule names each piece with its start and length.</summary>
    public async Task<List<Showing>> ScheduleAsync(string marathonId, CancellationToken ct)
    {
        var text = await this.GetAsync($"{Site}/api/schedule/marathons/{Uri.EscapeDataString(marathonId)}", ct);
        var list = new List<Showing>();
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var e in data.EnumerateArray())
        {
            // Milliseconds since the epoch, with a fraction on most of them, so never an integer.
            if (!e.TryGetProperty("time", out var time) || time.ValueKind != JsonValueKind.Number)
                continue;
            var start = DateTimeOffset.FromUnixTimeMilliseconds((long)time.GetDouble()).UtcDateTime;
            var seconds = e.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0;
            var name = WebUtility.HtmlDecode(Str(e, "name"));
            if (name.Length == 0)
                continue;
            list.Add(new Showing(name, WebUtility.HtmlDecode(Str(e, "seriesName")), start, TimeSpan.FromSeconds(Math.Max(0, seconds))));
        }

        list.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));
        return list;
    }

    /// <summary>
    /// A stream's playlist, from the video API's assets: the HLS one, as the site's player takes
    /// it. Null when the API has nothing for the stream: a seasonal marathon between its runs
    /// still sits in the list, and the simulcasts want a television provider's login.
    /// </summary>
    public async Task<string?> PlaylistAsync(string videoId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Site}/api/shows/v1/videos/{Uri.EscapeDataString(videoId)}?fields=stream");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("video", out var video)
            && video.TryGetProperty("stream", out var stream) && stream.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                var url = Str(a, "url");
                if (url.Length > 0 && (Str(a, "mime_type").Contains("mpegurl", StringComparison.OrdinalIgnoreCase) || url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)))
                    return url;
            }
        }

        return null;
    }

    private async Task<string> GetAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("Referer", Site + "/streams");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}

/// <summary>A marathon's page on Adult Swim, played from the playlist the site's video API names for it.</summary>
public sealed class AdultSwimResolver(HttpClient http) : IStreamResolver
{
    public static bool Matches(string input) => IdOf(input) is not null;

    public async Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        var id = IdOf(input) ?? throw new InvalidOperationException("Not an Adult Swim stream address.");
        var swim = new AdultSwim(http);
        var streams = await swim.StreamsAsync(ct);
        var stream = streams.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Adult Swim has no stream called '{id}'.");
        var playlist = await swim.PlaylistAsync(stream.VideoId, ct)
            ?? throw new InvalidOperationException($"Adult Swim has nothing on '{stream.Title}' right now.");
        // A television master, about -28 dBFS RMS against YouTube's -18: lifted all the player can.
        return new ResolvedStream(playlist, "Adult Swim · " + stream.Title, new Dictionary<string, string> { ["User-Agent"] = AdultSwim.UserAgent }, GainDb: 8);
    }

    /// <summary>The stream's id from its page address, in either form the site uses.</summary>
    private static string? IdOf(string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return null;
        if (!uri.Host.Equals("www.adultswim.com", StringComparison.OrdinalIgnoreCase) && !uri.Host.Equals("adultswim.com", StringComparison.OrdinalIgnoreCase))
            return null;
        var path = uri.AbsolutePath.Trim('/');
        foreach (var prefix in new[] { "streams/", "videos/live-streams/" })
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.Length > prefix.Length)
            {
                var id = path[prefix.Length..];
                return id.Contains('/') ? null : id.ToLower(CultureInfo.InvariantCulture);
            }
        }

        return null;
    }
}
