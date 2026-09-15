using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>Red Bull TV: its shelves to pick from, a search, and wide tiles with the cover art. Free, no account.</summary>
internal sealed class RedBullTab(UiContext ui)
{
    private List<RedBullTv.Shelf> shelves = [];
    private List<RedBullTv.Item> items = [];
    private string status = string.Empty;
    private string query = string.Empty;

    private bool pendingSearch;

    /// <summary>A search handed in from the home screen's Search: run as soon as the app is drawn.</summary>
    public void SearchFor(string text)
    {
        this.query = text;
        this.pendingSearch = true;
    }
    private string shelf = string.Empty;
    private bool autoBrowsed;

    internal Action? Browse;

    internal Action<RedBullTv.Shelf>? OpenShelf;

    internal Action<string>? Search;

    internal Action<RedBullTv.Item>? Play;

    public void SetShelves(List<RedBullTv.Shelf> value, string message)
    {
        this.shelves = value;
        this.status = message;
        if (this.shelf.Length == 0 && value.Count > 0)
        {
            var first = value.FirstOrDefault(s => s.Label.Contains("Films", StringComparison.OrdinalIgnoreCase));
            var pick = first.Id is { Length: > 0 } ? first : value[0];
            this.shelf = pick.Id;
            this.OpenShelf?.Invoke(pick);
        }
    }

    public void SetItems(string forShelf, List<RedBullTv.Item> value, string message)
    {
        if (forShelf != this.shelf)
            return;
        this.items = value;
        this.status = message;
    }

    public void SetStatus(string value) => this.status = value;

    public void Draw()
    {
        if (!this.autoBrowsed)
        {
            this.autoBrowsed = true;
            this.status = "Asking Red Bull TV…";
            this.Browse?.Invoke();
        }

        Ui.Hint("Red Bull TV: films, documentaries, shows and replays. Free, worldwide, no account.");

        var picked = Ui.Chips("rb", this.shelves.Select(s => s.Label).ToList(), this.shelves.FindIndex(s => s.Id == this.shelf));
        if (picked >= 0)
        {
            var s = this.shelves[picked];
            this.shelf = s.Id;
            this.items = [];
            this.status = $"Looking in {s.Label}…";
            this.OpenShelf?.Invoke(s);
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##rbquery", "Search films, shows and events", ref this.query, 80, ImGuiInputTextFlags.EnterReturnsTrue);
        if (this.pendingSearch)
        {
            this.pendingSearch = false;
            go = true;
        }
        ImGui.SameLine();
        if ((ImGui.Button("Search##rb") || go) && this.query.Trim().Length > 0)
        {
            this.shelf = "search:" + this.query.Trim();
            this.items = [];
            this.status = $"Searching for \"{this.query.Trim()}\"…";
            this.Search?.Invoke(this.query.Trim());
        }

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void DrawGrid()
    {
        var shown = this.items;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##rbgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;

        foreach (var item in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var subtitle = item.IsShow ? "show" : string.Join("  ·  ", new[] { item.Subheading, item.Seconds > 0 ? $"{(int)(item.Seconds / 60)} min" : string.Empty }.Where(x => x.Length > 0));
            var image = item.Image;
            if (PosterCard.Draw(ui, $"##rb{item.Id}", () => ui.Art.GetUrl(image), item.Title, subtitle, container: item.IsShow, wide: true))
                this.Play?.Invoke(item);

            if (item.Description.Length > 0)
                Ui.Tip(Ui.Ellipsis(item.Description, 300));
            column++;
        }
    }
}
