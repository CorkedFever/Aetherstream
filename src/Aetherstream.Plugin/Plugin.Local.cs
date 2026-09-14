using Aetherstream.Plugin.UI.Tabs;

namespace Aetherstream.Plugin;

/// <summary>
/// The library's Local shelf: walks the chosen folders for video files in the background and
/// hands the tab a list, each with a poster if one sits beside it.
/// </summary>
public sealed partial class Plugin
{
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi", ".mov", ".m4v", ".webm", ".wmv", ".ts", ".mpg", ".mpeg"];
    private static readonly string[] PosterExtensions = [".jpg", ".jpeg", ".png"];
    private const int MaxLocalVideos = 400;

    private void ScanLocal(IReadOnlyList<string> folders)
    {
        _ = Task.Run(() =>
        {
            var list = new List<LocalVideo>();
            var missing = new List<string>();
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder))
                {
                    missing.Add(folder);
                    continue;
                }

                try
                {
                    var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 6 };
                    foreach (var path in Directory.EnumerateFiles(folder, "*", options))
                    {
                        if (!VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                            continue;

                        var info = new FileInfo(path);
                        list.Add(new LocalVideo(
                            path,
                            Path.GetFileNameWithoutExtension(path),
                            Path.GetDirectoryName(path) ?? folder,
                            info.Length,
                            info.LastWriteTime,
                            PosterFor(path)));
                        if (list.Count >= MaxLocalVideos)
                            break;
                    }
                }
                catch (Exception ex)
                {
                    this.log.Warning($"[local] could not read {folder}: {ex.Message}");
                }
            }

            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            var message = list.Count == 0
                ? "No videos in those folders. Add one with the folder button."
                : missing.Count > 0 ? $"{list.Count} videos. Not found: {string.Join(", ", missing)}" : (list.Count >= MaxLocalVideos ? $"The first {MaxLocalVideos} videos. Use the filter." : string.Empty);
            this.window.Library.Local.SetVideos(list, message);
            this.log.Information($"[local] {list.Count} videos in {folders.Count} folder(s)");
        });
    }

    /// <summary>An image with the file's own name beside it, or a poster or folder picture in its folder.</summary>
    private static string PosterFor(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var stem = Path.Combine(dir, Path.GetFileNameWithoutExtension(path));
        foreach (var ext in PosterExtensions)
        {
            if (File.Exists(stem + ext))
                return stem + ext;
        }

        foreach (var name in new[] { "poster", "folder", "cover" })
        {
            foreach (var ext in PosterExtensions)
            {
                var candidate = Path.Combine(dir, name + ext);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return string.Empty;
    }
}
