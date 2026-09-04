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
    int? AudioTrackIndex = null,

    /// <summary>
    /// Whether this may be retried through the local HLS relay if it stalls.
    /// <para>
    /// Set for channels out of a public playlist, where a stall usually means an expiring upstream
    /// token the relay can work around. It stays false for everything else, because a video that
    /// stops is far more likely to have simply ended — restarting a finished film through a relay
    /// would replace a normal ending with a confusing loop.
    /// </para>
    /// </summary>
    bool Relayable = false,

    /// <summary>Whether this already points at the relay, so a stall is not retried a second time.</summary>
    bool Relayed = false,

    /// <summary>
    /// The address this was before the relay took it over, so a relayed stream can still be
    /// matched to the channel it came from. Null when it has not been relayed.
    /// </summary>
    string? Origin = null);
