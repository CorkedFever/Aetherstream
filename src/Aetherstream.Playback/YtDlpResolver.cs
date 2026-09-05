using System.Diagnostics;
using System.Text.Json;
using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// Resolves any page URL to a playable stream by delegating to yt-dlp.
/// <para>
/// This is the general answer to "play from any site": yt-dlp already understands a thousand of
/// them and absorbs the breakage when they change, which is work no one here wants to repeat per
/// service. Site-specific resolvers stay only where they buy something — <see cref="TwitchResolver"/>
/// exists so Twitch still works with nothing installed.
/// </para>
/// </summary>
public sealed class YtDlpResolver(string executable) : IStreamResolver
{
    /// <summary>
    /// Finds yt-dlp: a path the user gave, then the given folders, then beside the application,
    /// then PATH. Null when it is nowhere.
    /// <para>
    /// The folders passed in are the plugin's config directory and its install directory. The
    /// config directory matters more: Dalamud installs each plugin version into its own numbered
    /// folder, so a file dropped beside the DLL vanishes on the next update, whereas the config
    /// folder is the same for the life of the install. Inside a Dalamud plugin the "application"
    /// directory is the game's, which nobody would guess, so it is only a last resort.
    /// </para>
    /// </summary>
    public static string? Locate(string? explicitPath = null, params string[] directories)
    {
        // A path someone typed wins outright — a file or the folder it is in. This is the answer for
        // "I downloaded it to my Desktop": nobody should have to learn where a plugin lives.
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var typed = explicitPath.Trim().Trim('"');
            if (File.Exists(typed))
                return typed;

            var inFolder = Path.Combine(typed, "yt-dlp.exe");
            if (Directory.Exists(typed) && File.Exists(inFolder))
                return inFolder;
        }

        foreach (var directory in directories)
        {
            var candidate = Path.Combine(directory, "yt-dlp.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        var local = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
        if (File.Exists(local))
            return local;

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), "yt-dlp.exe");
                if (File.Exists(candidate))
                    return candidate;
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry is not worth failing over.
            }
        }

        return null;
    }

    public async Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--no-warnings");
        start.ArgumentList.Add("--no-playlist");
        start.ArgumentList.Add("-f");

        // Prefer a single muxed stream, which is one URL and nothing to synchronise (Twitch, files).
        // Fall back to a video+audio pair, which is all YouTube offers now. Plain "best" is wrong
        // here: it means "best muxed" and simply fails on sites that no longer publish one.
        start.ArgumentList.Add("b/bv*+ba");

        // Within whatever the selector allows, prefer what the decoder can actually use. The
        // framebuffer is 1280x720, so anything above that is decode work thrown away — and left to
        // its own ranking yt-dlp reaches for 2160p AV1 the moment its preferred formats are
        // missing, which they are on any machine without a JavaScript runtime for YouTube's
        // challenges. Software-decoding 4K AV1 inside the game is not a stream that plays; it is a
        // slideshow that looks like a broken plugin. H.264 first because every libvlc build decodes
        // it in hardware or cheaply in software; AAC over Opus for the same reason.
        start.ArgumentList.Add("-S");
        start.ArgumentList.Add("res:720,vcodec:h264,acodec:aac");
        start.ArgumentList.Add("--dump-single-json");
        start.ArgumentList.Add(input);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start {executable}.");

        // Both pipes must be drained concurrently. Reading one to completion while the other fills
        // its buffer deadlocks the child, which presents as a hang with no output at all.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var reason = stderr.Split('\n').FirstOrDefault(l => l.Contains("ERROR"))?.Trim();
            throw new InvalidOperationException(reason ?? $"yt-dlp failed ({process.ExitCode}).");
        }

        using var json = JsonDocument.Parse(stdout);
        var root = json.RootElement;

        // A muxed pick lands in "url"; a video+audio pick lands in "requested_formats" instead.
        string? url = root.TryGetProperty("url", out var direct) ? direct.GetString() : null;
        string? audioUrl = null;

        if (url is null && root.TryGetProperty("requested_formats", out var parts))
        {
            foreach (var part in parts.EnumerateArray())
            {
                var partUrl = part.TryGetProperty("url", out var pu) ? pu.GetString() : null;
                if (partUrl is null)
                    continue;

                var hasVideo = part.TryGetProperty("vcodec", out var vc)
                    && vc.GetString() is { } v && v != "none";

                if (hasVideo)
                    url ??= partUrl;
                else
                    audioUrl ??= partUrl;
            }
        }

        if (url is null)
            throw new InvalidOperationException("yt-dlp found no playable video stream there.");

        var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
        var uploader = root.TryGetProperty("uploader", out var u) ? u.GetString() : null;

        // Many sites reject requests without the headers yt-dlp negotiated (referer, user agent).
        Dictionary<string, string>? headers = null;
        if (root.TryGetProperty("http_headers", out var h) && h.ValueKind == JsonValueKind.Object)
        {
            headers = new Dictionary<string, string>();
            foreach (var header in h.EnumerateObject())
            {
                if (header.Value.GetString() is { } value)
                    headers[header.Name] = value;
            }
        }

        return new ResolvedStream(url, title ?? uploader ?? input, headers, audioUrl);
    }
}
