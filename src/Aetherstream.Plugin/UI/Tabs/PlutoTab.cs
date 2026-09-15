using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Pluto TV on demand: its shelves to pick from, a search, posters, and a series opened into its
/// seasons and episodes. Free, with Pluto's own ad breaks in the stream.
/// </summary>
internal sealed class PlutoTab(UiContext ui)
{
    private List<PlutoLibrary.Category> categories = [];
    private List<PlutoLibrary.Item> items = [];
    private List<PlutoLibrary.SeasonOf> seasons = [];
    private PlutoLibrary.Item? series;
    private string status = string.Empty;
    private string query = string.Empty;
    private string shelf = string.Empty;
    private bool autoBrowsed;

    /// <summary>Set by the plugin: the shelves, in the background.</summary>
    internal Action? Browse;

    /// <summary>Set by the plugin: a search.</summary>
    internal Action<string>? Search;

    /// <summary>Set by the plugin: a series' seasons.</summary>
    internal Action<PlutoLibrary.Item>? OpenSeries;

    /// <summary>Set by the plugin: plays a film or an episode, with the rest of the list after it.</summary>
    internal Action<PlutoLibrary.Item, IReadOnlyList<PlutoLibrary.Item>>? Play;

    public void SetCategories(List<PlutoLibrary.Category> value, string message)
    {
        this.categories = value;
        this.status = message;
        if (this.shelf.Length == 0 && value.Count > 0)
            this.shelf = value[0].Name;
        this.items = this.CurrentShelf();
    }

    public void SetSearch(List<PlutoLibrary.Item> value, string message)
    {
        this.items = value;
        this.status = message;
        this.shelf = string.Empty;
    }

    public void SetSeasons(PlutoLibrary.Item forSeries, List<PlutoLibrary.SeasonOf> value, string message)
    {
        if (this.series is not { } open || open.Id != forSeries.Id)
            return;
        this.seasons = value;
        this.status = message;
    }

    public void SetStatus(string value) => this.status = value;

    public void Draw()
    {
        if (!this.autoBrowsed)
        {
            this.autoBrowsed = true;
            this.status = "Asking Pluto…";
            this.Browse?.Invoke();
        }

        if (this.series is { } open)
        {
            this.DrawSeries(open);
            return;
        }

        Ui.Hint("Pluto TV's free films and series, with Pluto's own ad breaks. What is on depends on your country.");
        this.DrawShelves();

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##plutoquery", "Search films and series", ref this.query, 80, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Search##pluto") || go) && this.query.Trim().Length > 0)
        {
            this.status = $"Searching for \"{this.query.Trim()}\"…";
            this.Search?.Invoke(this.query.Trim());
        }

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid(this.items);
    }

    private List<PlutoLibrary.Item> CurrentShelf() =>
        this.categories.FirstOrDefault(c => c.Name == this.shelf).Items ?? [];

    private void DrawShelves()
    {
        if (this.categories.Count == 0)
            return;

        var labels = this.categories.Select(c => c.Name).ToList();
        var picked = Ui.Chips("plutoshelf", labels, labels.IndexOf(this.shelf));
        if (picked >= 0)
        {
            var c = this.categories[picked];
            this.shelf = c.Name;
            this.items = c.Items;
            this.status = string.Empty;
        }
    }

    private void DrawGrid(List<PlutoLibrary.Item> shown)
    {
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##plutogrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(false) + spacing)));
        var column = 0;

        foreach (var item in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var isSeries = item.Type == "series";
            var subtitle = isSeries ? "series" : string.Join("  ·  ", new[] { item.Genre, item.Seconds > 0 ? $"{(int)(item.Seconds / 60)} min" : string.Empty }.Where(x => x.Length > 0));
            var poster = item.Poster;
            if (PosterCard.Draw(ui, $"##pluto{item.Id}", () => poster.Length > 0 ? ui.Art.GetUrl(poster) : null, item.Name, subtitle, container: isSeries))
            {
                if (isSeries)
                {
                    this.series = item;
                    this.seasons = [];
                    this.status = "Reading the seasons…";
                    this.OpenSeries?.Invoke(item);
                }
                else
                {
                    this.Play?.Invoke(item, []);
                }
            }

            if (item.Summary.Length > 0)
                Ui.Tip(Ui.Ellipsis(item.Summary, 300));
            column++;
        }
    }

    private void DrawSeries(PlutoLibrary.Item open)
    {
        if (ImGui.Button("< Back"))
        {
            this.series = null;
            this.seasons = [];
            this.status = string.Empty;
            return;
        }

        ImGui.SameLine();
        using (Theme.PushDisplay())
            ImGui.TextUnformatted(open.Name.ToUpperInvariant());
        if (open.Summary.Length > 0)
            ImGui.TextColored(Theme.TextDim, Ui.Ellipsis(open.Summary, 160));
        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        using var child = ImRaii.Child("##plutoseasons", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        foreach (var season in this.seasons)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.TextDim, season.Name);
            var column = 0;
            foreach (var ep in season.Episodes)
            {
                if (column > 0 && column % perRow != 0)
                    ImGui.SameLine();
                var still = ep.Wide.Length > 0 ? ep.Wide : ep.Poster;
                var subtitle = string.Join("  ·  ", new[] { $"Episode {ep.Number}", ep.Seconds > 0 ? $"{(int)(ep.Seconds / 60)} min" : string.Empty }.Where(x => x.Length > 0));
                if (PosterCard.Draw(ui, $"##plutoep{ep.Id}", () => still.Length > 0 ? ui.Art.GetUrl(still) : null, ep.Name, subtitle, container: false, wide: true))
                {
                    var rest = this.seasons.SelectMany(sn => sn.Episodes).SkipWhile(x => x.Id != ep.Id).Skip(1).ToList();
                    this.Play?.Invoke(ep, rest);
                }

                if (ep.Summary.Length > 0)
                    Ui.Tip(Ui.Ellipsis(ep.Summary, 300));
                column++;
            }
        }
    }
}
