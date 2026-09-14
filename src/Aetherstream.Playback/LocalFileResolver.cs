using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// A file on this machine. libvlc wants an MRL, not a path, so a path becomes a file URI; the
/// display name is the file's own, without its extension.
/// </summary>
public sealed class LocalFileResolver : IStreamResolver
{
    public static bool Matches(string input) =>
        input.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
        || (input.Length > 2 && (input[1] == ':' || input.StartsWith(@"\\", StringComparison.Ordinal)) && File.Exists(input));

    public Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        var path = input.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ? new Uri(input).LocalPath : input;
        var mrl = new Uri(path).AbsoluteUri;
        return Task.FromResult(new ResolvedStream(mrl, Path.GetFileNameWithoutExtension(path)));
    }
}
