using System.IO.Compression;

namespace Aetherstream.Playback;

/// <summary>
/// Fetches yt-dlp, Deno and ffmpeg into the plugin's own folder when someone presses the button
/// for it: yt-dlp and Deno from their projects' own GitHub releases, ffmpeg from gyan.dev, the
/// Windows build ffmpeg.org points to and the one winget installs. This replaces "open
/// PowerShell, run winget, restart the game", which is where most installs got stuck. All are
/// free: yt-dlp is public domain (Unlicense), Deno is MIT, and this ffmpeg build is GPL. Nothing is fetched unasked, and a fetch is written beside
/// the target and moved over it only once complete and checked, so a dropped connection never
/// leaves a broken tool behind. Pressing it again is the update.
/// </summary>
public static class ToolInstaller
{
    public enum Tool
    {
        YtDlp,
        Deno,
        Ffmpeg,
    }

    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string DenoUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
    private const string FfmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    /// <summary>The programs a tool is, the first being the one it is known by.</summary>
    private static string[] ProgramsOf(Tool tool) => tool switch
    {
        Tool.YtDlp => ["yt-dlp.exe"],
        Tool.Deno => ["deno.exe"],
        _ => ["ffmpeg.exe", "ffprobe.exe"],
    };

    /// <summary>The file a tool lands as, inside <paramref name="folder"/>.</summary>
    public static string PathOf(Tool tool, string folder) => Path.Combine(folder, ProgramsOf(tool)[0]);

    /// <summary>Roughly what the download weighs, for the button.</summary>
    public static string SizeOf(Tool tool) => tool switch
    {
        Tool.YtDlp => "about 18 MB",
        Tool.Deno => "about 45 MB",
        _ => "about 110 MB",
    };

    /// <summary>
    /// Downloads a tool into <paramref name="folder"/> and returns where it landed.
    /// <paramref name="progress"/> hears the fraction done, or a negative number while the size
    /// is unknown.
    /// </summary>
    public static async Task<string> InstallAsync(HttpClient http, Tool tool, string folder, Action<float> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var programs = ProgramsOf(tool);
        var download = Path.Combine(folder, $"{tool}.download");
        var staged = programs.Select(p => Path.Combine(folder, p + ".new")).ToArray();
        var url = tool switch { Tool.YtDlp => YtDlpUrl, Tool.Deno => DenoUrl, _ => FfmpegUrl };

        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", "Aetherstream");
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1;
                await using var body = await response.Content.ReadAsStreamAsync(ct);
                await using var file = File.Create(download);
                var buffer = new byte[1 << 16];
                long done = 0;
                int read;
                while ((read = await body.ReadAsync(buffer, ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                    done += read;
                    progress(total > 0 ? (float)done / total : -1f);
                }
            }

            if (tool == Tool.YtDlp)
            {
                File.Move(download, staged[0], overwrite: true);
            }
            else
            {
                // Deno's zip holds deno.exe alone; ffmpeg's holds a versioned folder with the
                // programs under bin. Each is found by name wherever it sits.
                using var zip = ZipFile.OpenRead(download);
                for (var i = 0; i < programs.Length; i++)
                {
                    var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals(programs[i], StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException($"The download held no {programs[i]}.");
                    entry.ExtractToFile(staged[i], overwrite: true);
                }
            }

            foreach (var file in staged)
            {
                if (!LooksLikeProgram(file))
                    throw new InvalidOperationException("The download is not a Windows program; nothing was changed.");
            }

            // Over the old copies. A copy that is running (a stream resolving, a broadcast going)
            // cannot be replaced; saying so beats a half-written file.
            try
            {
                for (var i = 0; i < programs.Length; i++)
                    File.Move(staged[i], Path.Combine(folder, programs[i]), overwrite: true);
            }
            catch (IOException)
            {
                throw new InvalidOperationException("The old copy is in use. Stop what is playing or broadcasting and press it again.");
            }

            return PathOf(tool, folder);
        }
        finally
        {
            TryDelete(download);
            foreach (var file in staged)
                TryDelete(file);
        }
    }

    /// <summary>A Windows program starts "MZ". Anything else is an error page or a truncated file.</summary>
    private static bool LooksLikeProgram(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            return file.Length > 1_000_000 && file.ReadByte() == 'M' && file.ReadByte() == 'Z';
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
