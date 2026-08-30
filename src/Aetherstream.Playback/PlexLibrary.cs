using System.Net.Http.Headers;
using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// Browses a Plex server: its libraries, and what is in them.
/// <para>
/// Searching by name is fine when you know what you want and can spell it. Picking from a list is
/// how anyone actually chooses something to watch, and it removes the guesswork of "the first
/// playable match" from the resolver.
/// </para>
/// </summary>
public sealed class PlexLibrary(HttpClient http, string server, string token)
{
    /// <summary>A library on the server — Movies, TV, and so on.</summary>
    public readonly record struct Section(string Key, string Title, string Type);

    /// <summary>
    /// Something in the library: a film, a show, a season or an episode. <paramref name="Thumb"/> is
    /// a path on the server rather than a URL — it is fed to the photo transcoder, which resizes it.
    /// <para>
    /// Episode and season numbers are carried as numbers rather than folded into the title, because
    /// "9. The Lake Effect" is indistinguishable from every other ninth episode once seasons are
    /// flattened. The caller formats them.
    /// </para>
    /// </summary>
    public readonly record struct Item(
        string RatingKey,
        string Title,
        string Year,
        string Type,
        string Thumb = "",
        long DurationMs = 0,
        int Index = 0,
        int ParentIndex = 0,
        int ChildCount = 0,
        int LeafCount = 0,
        string ShowTitle = "")
    {
        public bool IsShow => this.Type == "show";

        public bool IsSeason => this.Type == "season";

        public bool IsEpisode => this.Type == "episode";

        /// <summary>Opening it lists what is inside; it is not itself playable.</summary>
        public bool IsContainer => this.IsShow || this.IsSeason;

        /// <summary>"S03E09" when both numbers are known, so seasons stay distinguishable.</summary>
        public string EpisodeCode =>
            this.IsEpisode && this.ParentIndex > 0 && this.Index > 0
                ? $"S{this.ParentIndex:00}E{this.Index:00}"
                : this.IsEpisode && this.Index > 0 ? $"E{this.Index:00}"
                : string.Empty;
    }

    public async Task<List<Section>> ListSectionsAsync(CancellationToken ct)
    {
        var sections = new List<Section>();
        using var json = await this.GetAsync("/library/sections", ct);
        if (json is null)
            return sections;

        if (!json.RootElement.TryGetProperty("MediaContainer", out var container)
            || !container.TryGetProperty("Directory", out var directories))
        {
            return sections;
        }

        foreach (var directory in directories.EnumerateArray())
        {
            var key = directory.TryGetProperty("key", out var k) ? k.GetString() : null;
            if (key is null)
                continue;

            sections.Add(new Section(
                key,
                directory.TryGetProperty("title", out var t) ? t.GetString() ?? key : key,
                directory.TryGetProperty("type", out var ty) ? ty.GetString() ?? string.Empty : string.Empty));
        }

        return sections;
    }

    /// <summary>
    /// Lists a library's contents, newest first. Capped, because a large library is thousands of
    /// entries and nobody scrolls that far — the filter is there for anything past the cap.
    /// </summary>
    public async Task<List<Item>> ListItemsAsync(string sectionKey, string filter, int limit, CancellationToken ct)
    {
        var items = new List<Item>();

        var path = filter.Length > 0
            ? $"/library/sections/{sectionKey}/all?title={Uri.EscapeDataString(filter)}"
            : $"/library/sections/{sectionKey}/all?sort=addedAt%3Adesc";

        using var json = await this.GetAsync(path, ct);
        if (json is null)
            return items;

        if (!json.RootElement.TryGetProperty("MediaContainer", out var container)
            || !container.TryGetProperty("Metadata", out var metadata))
        {
            return items;
        }

        foreach (var entry in metadata.EnumerateArray())
        {
            if (items.Count >= limit)
                break;

            var ratingKey = entry.TryGetProperty("ratingKey", out var rk) ? rk.GetString() : null;
            if (ratingKey is null)
                continue;

            items.Add(ReadItem(entry, ratingKey));
        }

        return items;
    }

    /// <summary>
    /// One entry, whatever kind it is. Plex returns films, shows, seasons and episodes through the
    /// same shape, so they are read the same way and told apart by <c>type</c>.
    /// </summary>
    private static Item ReadItem(JsonElement entry, string ratingKey) => new(
        ratingKey,
        entry.TryGetProperty("title", out var t) ? t.GetString() ?? "(untitled)" : "(untitled)",
        entry.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number
            ? y.GetInt32().ToString()
            : string.Empty,
        entry.TryGetProperty("type", out var ty) ? ty.GetString() ?? string.Empty : string.Empty,
        Thumbnail(entry),
        Number(entry, "duration"),
        (int)Number(entry, "index"),
        (int)Number(entry, "parentIndex"),
        (int)Number(entry, "childCount"),
        (int)Number(entry, "leafCount"),
        entry.TryGetProperty("grandparentTitle", out var g) ? g.GetString() ?? string.Empty : string.Empty);

    private static long Number(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : 0;

    /// <summary>
    /// The poster to show. An episode's own thumbnail is a still from the episode, which is what you
    /// want when browsing a season; falling back to the show's poster keeps a card from being blank
    /// when an episode has no still of its own.
    /// </summary>
    private static string Thumbnail(JsonElement entry)
    {
        foreach (var field in (string[])["thumb", "parentThumb", "grandparentThumb", "art"])
        {
            if (entry.TryGetProperty(field, out var value)
                && value.GetString() is { Length: > 0 } path)
            {
                return path;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// What is directly inside a show or a season — its seasons, or its episodes. One level at a
    /// time, because a show with two hundred episodes is not a list anyone reads.
    /// <para>
    /// Plex prepends a synthetic "All episodes" row to a show's children. It carries no ratingKey,
    /// so it is dropped here and offered by the UI instead, which can say what it costs.
    /// </para>
    /// </summary>
    public Task<List<Item>> ListChildrenAsync(string ratingKey, CancellationToken ct) =>
        this.ListFromAsync($"/library/metadata/{ratingKey}/children", MaxItems, ct);

    /// <summary>Every episode of a show, flattened past its seasons.</summary>
    public Task<List<Item>> ListEpisodesAsync(string ratingKey, CancellationToken ct) =>
        this.ListFromAsync($"/library/metadata/{ratingKey}/allLeaves", MaxItems, ct);

    /// <summary>
    /// Cap on any one listing. A long-running cartoon is hundreds of episodes, and every one of them
    /// would otherwise become a tile with a thumbnail to fetch.
    /// </summary>
    public const int MaxItems = 300;

    private async Task<List<Item>> ListFromAsync(string path, int limit, CancellationToken ct)
    {
        var items = new List<Item>();
        using var json = await this.GetAsync(path, ct);
        if (json is null)
            return items;

        if (!json.RootElement.TryGetProperty("MediaContainer", out var container)
            || !container.TryGetProperty("Metadata", out var metadata))
        {
            return items;
        }

        foreach (var entry in metadata.EnumerateArray())
        {
            if (items.Count >= limit)
                break;

            var ratingKey = entry.TryGetProperty("ratingKey", out var rk) ? rk.GetString() : null;
            if (ratingKey is null)
                continue;

            items.Add(ReadItem(entry, ratingKey));
        }

        return items;
    }

    private async Task<JsonDocument?> GetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, server.TrimEnd('/') + path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Plex-Token", token);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
}
