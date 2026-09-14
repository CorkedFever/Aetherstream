using System.Diagnostics;
using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// Lists of videos through yt-dlp, flat: a search, a playlist or channel page, or, with a signed-in
/// browser's cookies, the feeds YouTube keeps for you: recommended, subscriptions, watch later,
/// history. Nothing is resolved here; each entry is a page URL the resolver plays when picked.
/// </summary>
public sealed class YtDlpBrowser(string executable, string? cookiesBrowser = null, string? cookiesFile = null)
{
    public const int MaxItems = 40;

    public readonly record struct Video(string Id, string Title, string Url, long Seconds, string Channel, long Views, string Thumb);

    /// <summary>The feeds yt-dlp knows by name. All need a signed-in browser's cookies.</summary>
    public static readonly (string Title, string Target)[] Feeds =
    [
        ("Recommended", ":ytrec"),
        ("Subscriptions", ":ytsubs"),
        ("Watch later", ":ytwatchlater"),
        ("History", ":ythistory"),
    ];

    public static string SearchTarget(string query) => $"ytsearch{MaxItems}:{query.Trim()}";

    /// <summary>Whether the target is one of the feeds that needs cookies.</summary>
    public static bool NeedsCookies(string target) => target.StartsWith(":", StringComparison.Ordinal);

    public async Task<List<Video>> ListAsync(string target, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--no-warnings");
        start.ArgumentList.Add("--flat-playlist");
        start.ArgumentList.Add("--playlist-end");
        start.ArgumentList.Add(MaxItems.ToString());
        start.ArgumentList.Add("--dump-single-json");
        if (!string.IsNullOrWhiteSpace(cookiesFile) && File.Exists(cookiesFile))
        {
            start.ArgumentList.Add("--cookies");
            start.ArgumentList.Add(cookiesFile);
        }
        else if (!string.IsNullOrWhiteSpace(cookiesBrowser))
        {
            start.ArgumentList.Add("--cookies-from-browser");
            start.ArgumentList.Add(cookiesBrowser.Trim().ToLowerInvariant());
        }

        start.ArgumentList.Add("--");
        start.ArgumentList.Add(target);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("yt-dlp did not start");
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var json = await output;
        if (process.ExitCode != 0 || json.Trim().Length == 0)
        {
            var reason = (await errors).Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? $"yt-dlp exited with {process.ExitCode}";
            throw new InvalidOperationException(reason.Replace("ERROR: ", string.Empty));
        }

        var list = new List<Video>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("entries", out var entries))
        {
            foreach (var e in entries.EnumerateArray())
                Add(list, e);
        }
        else
        {
            Add(list, root);
        }

        return list;
    }

    private static void Add(List<Video> list, JsonElement e)
    {
        var id = Str(e, "id");
        var title = Str(e, "title");
        if (id.Length == 0 || title.Length == 0 || title == "[Private video]" || title == "[Deleted video]")
            return;

        var url = Str(e, "url");
        if (url.Length == 0 || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = Str(e, "webpage_url");
        if (url.Length == 0)
            url = $"https://www.youtube.com/watch?v={id}";

        // A playlist or channel inside a list is not a video; skip anything without a duration or a watch page.
        var thumb = string.Empty;
        if (e.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array)
        {
            var best = 0;
            foreach (var t in thumbs.EnumerateArray())
            {
                var w = t.TryGetProperty("width", out var wv) && wv.ValueKind == JsonValueKind.Number ? wv.GetInt32() : 0;
                var u = Str(t, "url");
                if (u.Length > 0 && (thumb.Length == 0 || (w >= 320 && (best < 320 || w < best))))
                {
                    thumb = u;
                    best = w;
                }
            }
        }

        if (thumb.Length == 0 && url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
            thumb = $"https://i.ytimg.com/vi/{id}/mqdefault.jpg";

        list.Add(new Video(
            id,
            title,
            url,
            e.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? (long)d.GetDouble() : 0,
            Str(e, "channel").Length > 0 ? Str(e, "channel") : Str(e, "uploader"),
            e.TryGetProperty("view_count", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0,
            thumb));
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
