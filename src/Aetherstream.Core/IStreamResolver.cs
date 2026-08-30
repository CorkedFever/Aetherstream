namespace Aetherstream.Core;

/// <summary>
/// Turns user input — a channel name, a service identifier, a raw URL — into something a decoder
/// can open. Resolution is the breakage-prone edge of the system (services change their auth
/// dances), so it lives behind this seam: Twitch today, Plex/Jellyfin later, and a raw-URL
/// passthrough as the permanent escape hatch.
/// </summary>
public interface IStreamResolver
{
    Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct);
}

/// <summary>
/// A playable stream.
/// <para>
/// <paramref name="AudioUrl"/> is set when the service only offers video and audio separately,
/// as YouTube now does — the player has to pull both and combine them. It is null whenever
/// <paramref name="PlaylistUrl"/> is already muxed, which is the common case (Twitch, direct files).
/// </para>
/// <para>
/// <paramref name="HttpHeaders"/> carries per-request headers some services require (a Plex token,
/// a user agent a CDN checks); null means none.
/// </para>
/// </summary>
public sealed record ResolvedStream(
    string PlaylistUrl,
    string DisplayName,
    IReadOnlyDictionary<string, string>? HttpHeaders = null,
    string? AudioUrl = null,

    /// <summary>
    /// Which audio track to open, counted from zero among the audio tracks only. Null lets libvlc
    /// choose, which is right for almost everything — it is set when the track libvlc would pick is
    /// one it cannot actually decode.
    /// </summary>
    int? AudioTrackIndex = null);
