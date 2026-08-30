using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// Passes any http(s) URL straight through. The permanent escape hatch: when a service resolver
/// breaks, a playlist URL lifted from streamlink or browser devtools still plays.
/// </summary>
public sealed class DirectUrlResolver : IStreamResolver
{
    public static bool Matches(string input) =>
        input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct) =>
        Task.FromResult(new ResolvedStream(input, new Uri(input).Host));
}
