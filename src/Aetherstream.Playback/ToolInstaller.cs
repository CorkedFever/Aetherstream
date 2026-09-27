using System.IO.Compression;

namespace Aetherstream.Playback;

/// <summary>
/// Fetches yt-dlp and Deno into the plugin's own folder, from their projects' own GitHub
/// releases, when someone presses the button for it. This replaces "open PowerShell, run winget,
/// restart the game", which is where most installs got stuck. Both are free: yt-dlp is public
/// domain (Unlicense) and Deno is MIT. Nothing is fetched unasked, and a fetch is written beside
/// the target and moved over it only once complete and checked, so a dropped connection never
/// leaves a broken tool behind. Pressing it again is the update.
/// </summary>
public static class ToolInstaller
{
    public enum Tool
    {
        YtDlp,
        Deno,
    }

    private const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string DenoUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

    /// <summary>The file a tool lands as, inside <paramref name="folder"/>.</summary>
    public static string PathOf(Tool tool, string folder) =>
        Path.Combine(folder, tool == Tool.YtDlp ? "yt-dlp.exe" : "deno.exe");

    /// <summary>Roughly what the download weighs, for the button.</summary>
    public static string SizeOf(Tool tool) => tool == Tool.YtDlp ? "about 18 MB" : "about 45 MB";

    /// <summary>
    /// Downloads a tool into <paramref name="folder"/> and returns where it landed.
    /// <paramref name="progress"/> hears the fraction done, or a negative number while the size
    /// is unknown.
    /// </summary>
    public static async Task<string> InstallAsync(HttpClient http, Tool tool, string folder, Action<float> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var target = PathOf(tool, folder);
        var part = target + ".part";
        var url = tool == Tool.YtDlp ? YtDlpUrl : DenoUrl;

        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", "Aetherstream");
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1;
                await using var body = await response.Content.ReadAsStreamAsync(ct);
                await using var file = File.Create(part);
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

            if (tool == Tool.Deno)
            {
                // The release is a zip holding deno.exe alone; it is unpacked beside the target
                // and checked like yt-dlp is.
                var unpacked = target + ".new";
                using (var zip = ZipFile.OpenRead(part))
                {
                    var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals("deno.exe", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException("The Deno download held no deno.exe.");
                    entry.ExtractToFile(unpacked, overwrite: true);
                }

                File.Delete(part);
                part = unpacked;
            }

            if (!LooksLikeProgram(part))
                throw new InvalidOperationException("The download is not a Windows program; nothing was changed.");

            // Over the old copy in one step. A copy that is running (a stream resolving right
            // now) cannot be replaced; saying so beats a half-written file.
            try
            {
                File.Move(part, target, overwrite: true);
            }
            catch (IOException)
            {
                throw new InvalidOperationException("The old copy is in use. Stop what is playing and press it again.");
            }

            return target;
        }
        finally
        {
            TryDelete(target + ".part");
            TryDelete(target + ".new");
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
