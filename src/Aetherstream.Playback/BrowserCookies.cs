using System.Diagnostics;

namespace Aetherstream.Playback;

/// <summary>
/// One cookie out of a browser's signed-in session, for the sites the plugin talks to itself.
/// yt-dlp already knows how to open every browser's cookie store, so it does the opening: it is
/// asked to write its cookie jar to a file and told to do nothing else, and the jar is read for
/// the one value and deleted. An exported cookies file is read directly. Nothing is kept on disk.
/// </summary>
public static class BrowserCookies
{
    /// <summary>A cookie's value for a domain, or null when the session has none.</summary>
    public static async Task<string?> ValueAsync(string? ytDlp, string? cookiesBrowser, string? cookiesFile, string domain, string name, string touchUrl, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(cookiesFile) && File.Exists(cookiesFile))
            return Find(await File.ReadAllTextAsync(cookiesFile, ct), domain, name);

        if (string.IsNullOrWhiteSpace(cookiesBrowser) || ytDlp is null)
            return null;

        var jar = Path.Combine(Path.GetTempPath(), $"aetherstream-jar-{Guid.NewGuid():N}.txt");
        try
        {
            var start = new ProcessStartInfo(ytDlp)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("--no-warnings");
            start.ArgumentList.Add("--cookies-from-browser");
            start.ArgumentList.Add(YtDlpResolver.CookiesArgument(cookiesBrowser));
            start.ArgumentList.Add("--cookies");
            start.ArgumentList.Add(jar);
            start.ArgumentList.Add("--simulate");
            start.ArgumentList.Add("--");
            start.ArgumentList.Add(touchUrl);

            using var process = Process.Start(start) ?? throw new InvalidOperationException("yt-dlp did not start");
            var drainOut = process.StandardOutput.ReadToEndAsync(ct);
            var drainErr = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            await drainOut;
            await drainErr;

            // The jar is written on the way out whether or not the page resolved.
            return File.Exists(jar) ? Find(await File.ReadAllTextAsync(jar, ct), domain, name) : null;
        }
        finally
        {
            try
            {
                if (File.Exists(jar))
                    File.Delete(jar);
            }
            catch (IOException)
            {
                // A jar that would not delete holds nothing but what the browser already holds.
            }
        }
    }

    /// <summary>Reads a Netscape-format cookie file for one cookie.</summary>
    private static string? Find(string netscape, string domain, string name)
    {
        foreach (var raw in netscape.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
                continue;
            var parts = line.Split('\t');
            if (parts.Length < 7)
                continue;
            var host = parts[0].TrimStart('.');
            if ((host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)) && parts[5] == name)
                return parts[6].Trim();
        }

        return null;
    }
}
