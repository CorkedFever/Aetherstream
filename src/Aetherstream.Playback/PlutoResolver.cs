using System.Web;

using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// Pluto's stitcher addresses, which the Library's Pluto shelf and the queue hand over as plain
/// URLs. Pluto's current stitcher wants the session token as a bearer header on every playlist
/// as well as on the address, and answers the address alone with a slate; libvlc cannot send the
/// header, so the stream is marked for the relay, which can.
/// </summary>
public sealed class PlutoResolver : IStreamResolver
{
    public static bool Matches(string input) =>
        Uri.TryCreate(input, UriKind.Absolute, out var uri)
        && uri.Host.EndsWith(".pluto.tv", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.Contains("/stitch/", StringComparison.Ordinal);

    public Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        var uri = new Uri(input);
        var token = HttpUtility.ParseQueryString(uri.Query).Get("jwt") ?? string.Empty;
        var headers = token.Length > 0
            ? new Dictionary<string, string> { ["Authorization"] = "Bearer " + token }
            : null;

        return Task.FromResult(new ResolvedStream(
            input,
            "Pluto TV",
            headers,
            Relayable: true,
            RelayRequired: headers is not null));
    }
}
