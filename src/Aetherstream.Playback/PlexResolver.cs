using System.Net.Http.Headers;
using System.Text.Json;

using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// Plays from a Plex Media Server by searching its library and handing back a direct media URL.
/// <para>
/// Plex is not a site yt-dlp understands — it is your own server, behind your own token — so it
/// gets its own resolver. The token identifies you to your server and is supplied by you; it is
/// never sent anywhere except the server address you configured.
/// </para>
/// <para>
/// The URL returned is a direct-play link to the original file. Plex can also transcode on the fly,
/// but there is no reason to make the server work when libvlc already decodes essentially anything
/// a media library contains.
/// </para>
/// </summary>
public sealed class PlexResolver(HttpClient http, string server, string token, int maxKilobits = 0)
    : IStreamResolver
{
    /// <summary>
    /// Identifies this client to the server. Plex wants one, and a stable value keeps a single
    /// transcode session rather than starting a new one on every play.
    /// </summary>
    private const string ClientId = "aetherstream-ffxiv";

    /// <summary>Anything beginning with "plex:" is a search of your library.</summary>
    public const string Scheme = "plex:";

    /// <summary>"plex:id:1234" plays that exact item, with no searching or guessing involved.</summary>
    public const string IdScheme = "plex:id:";

    /// <summary>
    /// Ceiling used when a file has to be transcoded because nothing in it can be decoded here,
    /// rather than because a bitrate limit was asked for. High enough not to visibly degrade the
    /// picture on a surface this size.
    /// </summary>
    private const int TranscodeFallbackKilobits = 12000;

    /// <summary>The source string that plays a specific library item.</summary>
    public static string SourceFor(string ratingKey) => IdScheme + ratingKey;

    public static bool Matches(string input) =>
        input.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);

    public async Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        if (server.Length == 0 || token.Length == 0)
            throw new InvalidOperationException("Set the Plex server address and token first.");

        // An exact item was chosen from the library; there is nothing to search for.
        if (input.StartsWith(IdScheme, StringComparison.OrdinalIgnoreCase))
            return await this.ResolveByKeyAsync(input[IdScheme.Length..].Trim(), ct);

        var query = input.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            ? input[Scheme.Length..].Trim()
            : input.Trim();

        if (query.Length == 0)
            throw new InvalidOperationException("Say what to play, e.g. \"plex: blade runner\".");

        var baseUrl = server.TrimEnd('/');
        var search = $"{baseUrl}/search?query={Uri.EscapeDataString(query)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, search);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Plex-Token", token);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "Plex rejected the token."
                    : $"Plex returned {(int)response.StatusCode} for that search.");
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        if (!json.RootElement.TryGetProperty("MediaContainer", out var container)
            || !container.TryGetProperty("Metadata", out var results)
            || results.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Nothing in your library matches '{query}'.");
        }

        // Plex returns artists, albums and people alongside playables; take the first entry that
        // actually has a file behind it, then resolve it by key so a search and a click from the
        // library go down exactly the same path — including the file and audio-track checks.
        foreach (var item in results.EnumerateArray())
        {
            if (!TryFindPart(item, out _, out var path))
                continue;

            if (item.TryGetProperty("ratingKey", out var rk) && rk.GetString() is { } ratingKey)
                return await this.ResolveByKeyAsync(ratingKey, ct);

            // No key to resolve by, so the part URL is all there is. The token rides in the query
            // string because libvlc opens the URL itself and cannot be given headers per request.
            var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
            var year = item.TryGetProperty("year", out var y) ? y.GetInt32().ToString() : null;

            return new ResolvedStream(
                $"{baseUrl}{path}?X-Plex-Token={Uri.EscapeDataString(token)}",
                year is null ? title ?? query : $"{title} ({year})");
        }

        throw new InvalidOperationException($"'{query}' matched, but nothing playable.");
    }

    /// <summary>Resolves a specific library item to something playable.</summary>
    private async Task<ResolvedStream> ResolveByKeyAsync(string ratingKey, CancellationToken ct)
    {
        var baseUrl = server.TrimEnd('/');

        // checkFiles=1 makes the server stat the file, so a library entry whose file has gone can be
        // reported as such instead of failing later as an unexplained 404 inside libvlc.
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/library/metadata/{ratingKey}?checkFiles=1");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Plex-Token", token);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Plex returned {(int)response.StatusCode} for that item.");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        if (!json.RootElement.TryGetProperty("MediaContainer", out var container)
            || !container.TryGetProperty("Metadata", out var results)
            || results.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("That item is no longer in the library.");
        }

        var item = results[0];
        var title = DisplayName(item);

        if (maxKilobits > 0)
            return new ResolvedStream(TranscodeUrl(baseUrl, ratingKey, maxKilobits), title);

        if (!TryFindPart(item, out var part, out var path))
            throw new InvalidOperationException($"'{title}' has no playable file.");

        // Plex keeps the database row when a file disappears, so the library still lists it and the
        // part URL still resolves — to a 404. Saying so beats "nothing happened".
        if (IsZero(part, "exists") || IsZero(part, "accessible"))
        {
            throw new InvalidOperationException(
                $"Plex lists '{title}', but the file is missing from the server — nothing to play. " +
                "It was probably moved or deleted without a library scan.");
        }

        var audio = ReadAudioTracks(part);
        var usable = audio.FindIndex(a => !a.Undecodable);

        // Every audio track is one libvlc cannot decode, so the server has to do the work.
        if (audio.Count > 0 && usable < 0)
        {
            return new ResolvedStream(
                TranscodeUrl(baseUrl, ratingKey, TranscodeFallbackKilobits),
                title);
        }

        // The default track is undecodable but another one is fine. Picking it explicitly keeps
        // direct play — no transcode, no load on the server — at the cost of whatever language or
        // channel layout the broken track had.
        int? track = audio.Count > 0 && audio[0].Undecodable && usable > 0 ? usable : null;

        return new ResolvedStream(
            $"{baseUrl}{path}?X-Plex-Token={Uri.EscapeDataString(token)}",
            title,
            AudioTrackIndex: track);
    }

    /// <summary>
    /// What to call this while it plays. An episode's own title is not enough on its own — half the
    /// library is called "Episode 1" — so the show and the episode number are put back in front of
    /// it, which is also what ends up in the history.
    /// </summary>
    private static string DisplayName(JsonElement item)
    {
        var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "Plex" : "Plex";
        var show = item.TryGetProperty("grandparentTitle", out var g) ? g.GetString() : null;

        if (string.IsNullOrEmpty(show))
            return title;

        var season = Int(item, "parentIndex");
        var episode = Int(item, "index");

        var code = season > 0 && episode > 0 ? $"S{season:00}E{episode:00}"
            : episode > 0 ? $"E{episode:00}"
            : string.Empty;

        return code.Length > 0 ? $"{show} · {code} · {title}" : $"{show} · {title}";

        static int Int(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : 0;
    }

    private static bool TryFindPart(JsonElement item, out JsonElement part, out string path)
    {
        if (item.TryGetProperty("Media", out var media))
        {
            foreach (var medium in media.EnumerateArray())
            {
                if (!medium.TryGetProperty("Part", out var parts))
                    continue;

                foreach (var candidate in parts.EnumerateArray())
                {
                    if (candidate.TryGetProperty("key", out var key) && key.GetString() is { } found)
                    {
                        part = candidate;
                        path = found;
                        return true;
                    }
                }
            }
        }

        part = default;
        path = string.Empty;
        return false;
    }

    private static bool IsZero(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && (value.ValueKind == JsonValueKind.False
            || (value.ValueKind == JsonValueKind.Number && value.GetInt32() == 0));

    private readonly record struct AudioTrack(string Codec, int Channels)
    {
        /// <summary>
        /// True for combinations the bundled libvlc gets wrong. Measured, not assumed: every other
        /// codec in a real library — including 8-channel E-AC3, 6-channel DTS-HD, FLAC and PCM —
        /// decodes, and so does stereo Opus. Multichannel Opus is the one that fails, with
        /// "cannot read Opus header", because libavcodec here has no Opus decoder at all and the
        /// standalone plugin cannot parse a multichannel mapping.
        /// </summary>
        public bool Undecodable =>
            Channels > 2 && Codec.Equals("opus", StringComparison.OrdinalIgnoreCase);
    }

    private static List<AudioTrack> ReadAudioTracks(JsonElement part)
    {
        var tracks = new List<AudioTrack>();
        if (!part.TryGetProperty("Stream", out var streams))
            return tracks;

        foreach (var stream in streams.EnumerateArray())
        {
            // streamType 2 is audio; 1 is video and 3 is subtitles.
            if (!stream.TryGetProperty("streamType", out var type) || type.GetInt32() != 2)
                continue;

            tracks.Add(new AudioTrack(
                stream.TryGetProperty("codec", out var codec) ? codec.GetString() ?? string.Empty : string.Empty,
                stream.TryGetProperty("channels", out var channels) ? channels.GetInt32() : 2));
        }

        return tracks;
    }

    /// <summary>
    /// Asks the server to transcode to HLS at a bitrate ceiling.
    /// <para>
    /// Direct play sends the original file, which over a WAN link means pulling a whole remux
    /// across the internet in real time. Transcoding moves that work to the server — which is what
    /// remote streaming is for — and produces ordinary HLS, the same thing Twitch serves.
    /// </para>
    /// </summary>
    private string TranscodeUrl(string baseUrl, string ratingKey, int kilobits)
    {
        var path = Uri.EscapeDataString($"/library/metadata/{ratingKey}");

        return $"{baseUrl}/video/:/transcode/universal/start.m3u8" +
            $"?path={path}" +
            "&mediaIndex=0&partIndex=0" +
            "&protocol=hls" +

            // directStream=1 lets the server remux rather than re-encode wherever it can, and
            // convert only what has to be converted. Forcing directStream=0 made it re-encode the
            // video every time, which on a seedbox is the difference between working and melting.
            "&directPlay=0&directStream=1" +
            "&fastSeek=1" +
            $"&maxVideoBitrate={kilobits}" +
            "&videoQuality=100" +
            "&subtitles=none" +
            "&audioBoost=100" +
            $"&session={ClientId}" +

            // Plex refuses the whole request with HTTP 400 unless the client identifies itself:
            // it builds the transcode profile from these, and treats their absence as malformed
            // rather than as a default. Identifier alone is not enough.
            $"&X-Plex-Client-Identifier={ClientId}" +
            "&X-Plex-Product=Aetherstream" +
            "&X-Plex-Version=1.0" +
            "&X-Plex-Platform=Windows" +
            "&X-Plex-Device=Windows" +
            "&X-Plex-Model=standalone" +
            $"&X-Plex-Token={Uri.EscapeDataString(token)}";
    }
}
