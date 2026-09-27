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
public sealed class YtDlpResolver(string executable, string? cookiesBrowser = null, string? cookiesFile = null, int pictureHeight = 720, bool preferH264 = false, string? jsRuntime = null) : IStreamResolver
{
    /// <summary>Browsers yt-dlp can read a signed-in YouTube session from, as it names them, plus the Firefox forks it does not know by name.</summary>
    public static readonly string[] Browsers = ["floorp", "firefox", "librewolf", "waterfox", "zen", "brave", "chrome", "edge", "vivaldi", "opera"];

    /// <summary>
    /// What to hand yt-dlp for a browser. The Firefox forks keep their profiles under their own
    /// name in AppData, which yt-dlp does not look in on its own, so they become "firefox:" plus
    /// the profile folder; the rest are passed by name.
    /// </summary>
    public static string CookiesArgument(string browser)
    {
        var name = browser.Trim().ToLowerInvariant();
        var fork = name switch { "floorp" => "Floorp", "librewolf" => "librewolf", "waterfox" => "Waterfox", "zen" => "zen", _ => null };
        if (fork is null)
            return name;

        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fork, "Profiles");
            if (Directory.Exists(root))
            {
                var profiles = Directory.GetDirectories(root);
                var chosen = profiles.FirstOrDefault(p => p.EndsWith(".default-release", StringComparison.OrdinalIgnoreCase))
                    ?? profiles.Where(p => File.Exists(Path.Combine(p, "cookies.sqlite"))).OrderByDescending(p => File.GetLastWriteTimeUtc(Path.Combine(p, "cookies.sqlite"))).FirstOrDefault();
                if (chosen is not null)
                    return "firefox:" + chosen;
            }
        }
        catch (Exception)
        {
            // Fall through to plain firefox, which at least names the right family.
        }

        return "firefox";
    }

    /// <summary>
    /// The JavaScript runtime yt-dlp would use for YouTube's challenges, or null. Deno is the one
    /// yt-dlp enables by default; node and bun are recognised but have to be opted into on the
    /// yt-dlp side, so they are reported for information rather than relied on.
    /// </summary>
    public static string? LocateJsRuntime(IReadOnlyList<string>? directories = null)
    {
        foreach (var name in new[] { "deno.exe", "node.exe", "bun.exe" })
        {
            if (Find(name, directories) is { } found)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Every folder a tool might be in, in order: the ones given (the plugin's own), the game's
    /// PATH, then the PATH as Windows has it now and winget's links folder. The last two are why
    /// nothing has to be restarted any more: the game's PATH is the one it started with, so a
    /// yt-dlp or Deno installed by winget while it runs was invisible until a relaunch, which is
    /// the step people most often missed. The registry is read at most every few seconds, since
    /// the Setup tab asks every frame.
    /// </summary>
    private static IEnumerable<string> SearchFolders(IReadOnlyList<string>? directories)
    {
        foreach (var directory in directories ?? [])
            yield return directory;
        yield return AppContext.BaseDirectory;

        foreach (var directory in SplitPath(Environment.GetEnvironmentVariable("PATH")))
            yield return directory;

        foreach (var directory in CurrentPath())
            yield return directory;
    }

    private static readonly object PathGate = new();
    private static (DateTime At, string[] Folders) pathCache = (DateTime.MinValue, []);

    private static string[] CurrentPath()
    {
        lock (PathGate)
        {
            if (DateTime.UtcNow - pathCache.At < TimeSpan.FromSeconds(5))
                return pathCache.Folders;

            var folders = new List<string>();
            try
            {
                folders.AddRange(SplitPath(Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)));
                folders.AddRange(SplitPath(Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)));
            }
            catch (Exception)
            {
                // A registry that cannot be read leaves the game's own PATH, which is what there was before.
            }

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (local.Length > 0)
                folders.Add(Path.Combine(local, "Microsoft", "WinGet", "Links"));

            pathCache = (DateTime.UtcNow, folders.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            return pathCache.Folders;
        }
    }

    private static IEnumerable<string> SplitPath(string? path) =>
        (path ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => Environment.ExpandEnvironmentVariables(d.Trim().Trim('"')))
            .Where(d => d.Length > 0);

    /// <summary>The first copy of <paramref name="exe"/> in <see cref="SearchFolders"/>, or null.</summary>
    public static string? Find(string exe, IReadOnlyList<string>? directories = null)
    {
        foreach (var directory in SearchFolders(directories))
        {
            try
            {
                var candidate = Path.Combine(directory, exe);
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

    /// <summary>
    /// Asks a yt-dlp for its version. Off the render thread — it is a process spawn — and never
    /// throws, because a readout that crashes the tab is worse than a readout that says "unknown".
    /// </summary>
    public static async Task<string> VersionAsync(string path, CancellationToken ct)
    {
        try
        {
            var start = new ProcessStartInfo(path)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("--version");

            using var process = Process.Start(start);
            if (process is null)
                return "unknown";

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            _ = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return output.Trim() is { Length: > 0 } version ? version : "unknown";
        }
        catch (Exception)
        {
            return "unknown";
        }
    }
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
    public static string? Locate(string? explicitPath = null, IReadOnlyList<string>? directories = null)
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

        return Find("yt-dlp.exe", directories);
    }

    public async Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        var (exitCode, stdout, stderr) = await this.RunAsync(input, jsRuntime, ct);

        // --js-runtimes arrived in yt-dlp 2025.11; an older copy stops at the unknown option.
        // Such a copy fails YouTube anyway, but everything else it can still play, so it is
        // asked again without.
        if (exitCode != 0 && jsRuntime is not null && stderr.Contains("--js-runtimes", StringComparison.Ordinal))
            (exitCode, stdout, stderr) = await this.RunAsync(input, null, ct);

        return Parse(input, exitCode, stdout, stderr);
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(string input, string? runtime, CancellationToken ct)
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

        // The best video with the best audio, else the best single stream: yt-dlp's own default.
        // It used to be the other way round, a muxed stream first as one URL with nothing to
        // synchronise, until YouTube started publishing a muxed 360p again (format 18): "b"
        // matched it first, and every YouTube video played at 360p whatever the picture size.
        // Sites that only publish muxed streams (Twitch, Dailymotion) still land on one, since
        // their best video is the muxed stream itself.
        start.ArgumentList.Add("bv*+ba/b");

        // Within whatever the selector allows, prefer what the decoder can actually use. The
        // framebuffer is the configured height, so anything above it is decode work thrown away — and left to
        // its own ranking yt-dlp reaches for 2160p AV1 the moment its preferred formats are
        // missing, which they are on any machine without a JavaScript runtime for YouTube's
        // challenges. Software-decoding 4K AV1 inside the game is not a stream that plays; it is a
        // slideshow that looks like a broken plugin. H.264 first because every libvlc build decodes
        // it in hardware or cheaply in software; AAC over Opus for the same reason.
        start.ArgumentList.Add("-S");
        // Within the picture's size, the best codec the site offers unless asked to stay on
        // H.264: YouTube's AV1 at 1080p is a third of the bits of its H.264 and looks better, and
        // the bundled libvlc decodes it, on the GPU where the driver allows and through dav1d
        // otherwise. H.264 remains the choice for a machine whose CPU would rather not.
        var codecs = preferH264 ? "h264" : "av01:vp9.2:vp9:h264";
        start.ArgumentList.Add($"res:{Math.Clamp(pictureHeight, 360, 2160)},vcodec:{codecs},acodec:aac");

        // YouTube's "confirm you're not a bot" wall wants a signed-in session, and this is the
        // remedy yt-dlp itself names in that error: read the cookies of a browser the person is
        // already signed into. Opt-in, and everything stays on this machine — the cookies go from
        // the browser's store to yt-dlp to YouTube, nowhere else.
        // An exported cookies file beats reading a browser directly: Chromium-family browsers
        // (Brave, Chrome, Edge) encrypt their cookie stores in a way yt-dlp cannot open while they
        // run — "failed to decrypt with DPAPI", yt-dlp issue #10927 — whereas a Netscape-format
        // export from an extension is just a text file and works for everyone.
        if (!string.IsNullOrWhiteSpace(cookiesFile) && File.Exists(cookiesFile))
        {
            start.ArgumentList.Add("--cookies");
            start.ArgumentList.Add(cookiesFile);
        }
        else if (!string.IsNullOrWhiteSpace(cookiesBrowser))
        {
            start.ArgumentList.Add("--cookies-from-browser");
            start.ArgumentList.Add(CookiesArgument(cookiesBrowser));
        }

        // The runtime by its path, so yt-dlp has it whatever PATH it inherited from the game,
        // and node or bun work too, which yt-dlp only uses when told to.
        if (runtime is not null)
        {
            start.ArgumentList.Add("--js-runtimes");
            start.ArgumentList.Add($"{Path.GetFileNameWithoutExtension(runtime).ToLowerInvariant()}:{runtime}");
        }

        start.ArgumentList.Add("--dump-single-json");

        // Everything after this is the input, whatever it looks like. Without it a source that
        // happens to begin with '-' is parsed as an option — which is option injection into a
        // process this plugin runs with the user's rights.
        start.ArgumentList.Add("--");
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
        return (process.ExitCode, stdout, stderr);
    }

    private static ResolvedStream Parse(string input, int exitCode, string stdout, string stderr)
    {
        if (exitCode != 0)
        {
            var reason = stderr.Split('\n').FirstOrDefault(l => l.Contains("ERROR"))?.Trim();
            throw new InvalidOperationException(reason ?? $"yt-dlp failed ({exitCode}).");
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

        // The picked format's own height, so a 480p video is not drawn at 1080 for nothing.
        var height = root.TryGetProperty("height", out var hv) && hv.ValueKind == JsonValueKind.Number ? hv.GetInt32() : 0;
        return new ResolvedStream(url, title ?? uploader ?? input, headers, audioUrl, SourceHeight: height);
    }
}
