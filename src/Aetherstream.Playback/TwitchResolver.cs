using System.Net.Http.Json;
using System.Text.Json;
using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// Resolves a Twitch channel name to its HLS playlist URL: a GQL PlaybackAccessToken request
/// (the approach streamlink uses), then the usher playlist endpoint with the returned token and
/// signature. This is the most breakage-prone code in the project — Twitch changes this dance
/// periodically — which is why it is fully isolated here and DirectUrlResolver exists.
/// </summary>
public sealed class TwitchResolver(HttpClient http) : IStreamResolver
{
    // The public client id Twitch's own web player sends; not a secret.
    private const string ClientId = "kimne78kx3ncx6brgo4mv6wki5h1ko";

    private const string TokenQuery =
        """
        query PlaybackAccessToken($login: String!, $playerType: String!) {
          streamPlaybackAccessToken(
            channelName: $login,
            params: {platform: "web", playerBackend: "mediaplayer", playerType: $playerType}
          ) { value signature }
        }
        """;

    /// <summary>True for a bare channel name or any twitch.tv link — people paste both.</summary>
    public static bool Matches(string input) =>
        !DirectUrlResolver.Matches(input) || ChannelFromUrl(input) is not null;

    private static string? ChannelFromUrl(string input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return null;
        if (!uri.Host.EndsWith("twitch.tv", StringComparison.OrdinalIgnoreCase))
            return null;

        var segment = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
        return string.IsNullOrEmpty(segment) ? null : segment;
    }

    public async Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        var channel = (ChannelFromUrl(input) ?? input).Trim().TrimStart('#').ToLowerInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gql.twitch.tv/gql");
        request.Headers.Add("Client-ID", ClientId);
        request.Content = JsonContent.Create(new
        {
            operationName = "PlaybackAccessToken",
            query = TokenQuery,
            variables = new { login = channel, playerType = "embed" },
        });

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var token = json.RootElement.GetProperty("data").GetProperty("streamPlaybackAccessToken");
        if (token.ValueKind == JsonValueKind.Null)
            throw new InvalidOperationException($"'{channel}' is not live or does not exist.");

        var value = token.GetProperty("value").GetString()
            ?? throw new InvalidOperationException("Twitch returned an empty access token.");
        var signature = token.GetProperty("signature").GetString()
            ?? throw new InvalidOperationException("Twitch returned an empty token signature.");

        var playlist =
            $"https://usher.ttvnw.net/api/channel/hls/{channel}.m3u8" +
            $"?sig={Uri.EscapeDataString(signature)}" +
            $"&token={Uri.EscapeDataString(value)}" +
            "&allow_source=true&fast_bread=true&player_backend=mediaplayer" +
            $"&playlist_include_framerate=true&p={Random.Shared.Next(1_000_000, 9_999_999)}";

        // A token comes back for offline channels too — usher is what actually knows. Probing here
        // turns "an empty window forever" into a clear message, and costs one request.
        using var probe = await http.GetAsync(playlist, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!probe.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                probe.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"'{channel}' is offline."
                    : $"Twitch refused the playlist for '{channel}' ({(int)probe.StatusCode}).");
        }

        return new ResolvedStream(playlist, channel);
    }
}
