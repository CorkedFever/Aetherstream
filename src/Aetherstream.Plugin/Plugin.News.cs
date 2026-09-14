using System.Text.Json;

using Aetherstream.Plugin.Video;

namespace Aetherstream.Plugin;

/// <summary>
/// Lodestone headlines for the news channel, through lodestonenews.com — a free mirror of the
/// official news as JSON, since the Lodestone itself publishes no feed. Fetched every fifteen
/// minutes while the channel is up, cached to disk so the set has yesterday's news to show
/// before today's arrives.
/// </summary>
public sealed partial class Plugin
{
    private static readonly TimeSpan NewsFreshFor = TimeSpan.FromMinutes(15);
    private const string NewsUrl = "https://lodestonenews.com/news/all?locale=en&limit=12";

    private NewsSnapshot? newsSnapshot;
    private long newsRequestedAtMs = -1;
    private bool newsFetching;

    private string NewsCachePath => Path.Combine(this.pluginInterface.GetPluginConfigDirectory(), "news.json");

    private NewsSnapshot? NewsSnapshot()
    {
        var now = Environment.TickCount64;

        // First ask: the cache, then a fetch. Later asks: a fetch when the last is stale.
        if (this.newsSnapshot is null && this.newsRequestedAtMs < 0)
        {
            this.newsSnapshot = this.ReadNewsCache();
            this.newsRequestedAtMs = now;
            this.FetchNews();
        }
        else if (!this.newsFetching && now - this.newsRequestedAtMs > NewsFreshFor.TotalMilliseconds)
        {
            this.newsRequestedAtMs = now;
            this.FetchNews();
        }

        return this.newsSnapshot;
    }

    private void FetchNews()
    {
        this.newsFetching = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var text = await this.http.GetStringAsync(NewsUrl);
                var items = ParseNews(text);
                if (items.Count > 0)
                {
                    this.newsSnapshot = new NewsSnapshot(items, DateTime.UtcNow, string.Empty);
                    Directory.CreateDirectory(Path.GetDirectoryName(this.NewsCachePath)!);
                    await File.WriteAllTextAsync(this.NewsCachePath, text);
                    this.log.Information($"[news] {items.Count} headlines");
                }
                else if (this.newsSnapshot is null)
                {
                    this.newsSnapshot = new NewsSnapshot([], DateTime.MinValue, "the Lodestone feed came back empty");
                }
            }
            catch (Exception ex)
            {
                this.log.Warning($"[news] fetch failed: {ex.Message}");
                this.newsSnapshot ??= new NewsSnapshot([], DateTime.MinValue, "could not reach the news feed");
            }
            finally
            {
                this.newsFetching = false;
            }
        });
    }

    private NewsSnapshot? ReadNewsCache()
    {
        try
        {
            var path = this.NewsCachePath;
            if (!File.Exists(path))
                return null;

            var items = ParseNews(File.ReadAllText(path));
            return items.Count > 0 ? new NewsSnapshot(items, File.GetLastWriteTimeUtc(path), string.Empty) : null;
        }
        catch (Exception ex)
        {
            this.log.Debug($"[news] cache unreadable: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// The feed's shape: either a list, or an object of lists keyed by kind ("topics", "notices",
    /// "maintenance", …). Both are read; entries are sorted newest first.
    /// </summary>
    private static List<NewsItem> ParseNews(string json)
    {
        var items = new List<NewsItem>();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            ReadNewsArray(root, "news", items);
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                    ReadNewsArray(property.Value, property.Name, items);
            }
        }

        // Order of interest, not of time: maintenance that is still ahead, then the real news,
        // then the housekeeping, then world status notices — which arrive by the dozen and all
        // say the same thing — capped so they cannot crowd everything else off.
        var now = DateTime.UtcNow;
        static int Rank(NewsItem i) => i.Kind switch
        {
            "maintenance" => i.End is { } end && end > DateTime.UtcNow ? 0 : 5,
            "topic" => 1,
            "developer" => 2,
            "update" => 3,
            "notice" => 3,
            "status" => 4,
            _ => 3,
        };

        var status = items.Where(i => i.Kind == "status").OrderByDescending(i => i.Time).Take(3).ToList();
        return items
            .Where(i => i.Kind != "status")
            .Concat(status)
            .OrderBy(Rank)
            .ThenBy(i => Rank(i) == 0 ? (i.Start ?? now) - now : now - i.Time)
            .Take(16)
            .ToList();
    }

    private static DateTime? Stamp(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            && DateTime.TryParse(value.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    private static void ReadNewsArray(JsonElement array, string kind, List<NewsItem> into)
    {
        foreach (var entry in array.EnumerateArray())
        {
            var title = entry.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            if (title.Length == 0)
                continue;

            var description = entry.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            var time = entry.TryGetProperty("time", out var tm) && tm.ValueKind == JsonValueKind.String
                && DateTime.TryParse(tm.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : DateTime.MinValue;

            // Descriptions are long; the channel wraps eight lines and no more.
            if (description.Length > 480)
                description = description[..480];

            var start = Stamp(entry, "start");
            var end = Stamp(entry, "end");

            into.Add(new NewsItem(title, description, time, kind.TrimEnd('s'), start, end));
        }
    }
}
