using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>NASA+: topics as chips, a search, and wide tiles with the stills. Free, worldwide, no account.</summary>
internal sealed class NasaTab(UiContext ui)
{
    private List<NasaPlus.Topic> topics = [];
    private List<NasaPlus.Item> items = [];
    private string status = string.Empty;
    private string query = string.Empty;
    private int topic;
    private string listing = string.Empty;
    private bool autoBrowsed;

    internal Action? Browse;

    internal Action<int, string>? Open;

    public void SetTopics(List<NasaPlus.Topic> value) => this.topics = value;

    public void SetItems(string forListing, List<NasaPlus.Item> value, string message)
    {
        if (forListing != this.listing)
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
            this.Browse?.Invoke();
            this.Go(0, string.Empty);
        }

        Ui.Hint("NASA+, the agency's own service: documentaries, series, launches and Earth from orbit. Free, worldwide, no account.");

        var labels = new List<string> { "Latest" };
        labels.AddRange(this.topics.Select(t => t.Name));
        var selected = this.query.Length > 0 && this.listing.StartsWith("search:", StringComparison.Ordinal) ? -1 : this.topics.FindIndex(t => t.Id == this.topic) + 1;
        var picked = Ui.Chips("nasa", labels, selected);
        if (picked >= 0 && picked != selected)
        {
            this.query = string.Empty;
            this.Go(picked == 0 ? 0 : this.topics[picked - 1].Id, string.Empty);
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##nasaquery", "Search NASA+", ref this.query, 80, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Search##nasa") || go) && this.query.Trim().Length > 0)
            this.Go(0, this.query.Trim());

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void Go(int newTopic, string search)
    {
        this.topic = newTopic;
        this.listing = search.Length > 0 ? "search:" + search : "topic:" + newTopic;
        this.items = [];
        this.status = search.Length > 0 ? $"Searching NASA+ for \"{search}\"…" : "Asking NASA+…";
        this.Open?.Invoke(newTopic, search);
    }

    private void DrawGrid()
    {
        var shown = this.items;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##nasagrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;

        foreach (var item in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var subtitle = string.Join("  ·  ", new[] { item.Series, item.Seconds > 0 ? $"{(int)Math.Round(item.Seconds / 60)} min" : string.Empty }.Where(x => x.Length > 0));
            var image = item.Image;
            if (PosterCard.Draw(ui, $"##nasa{item.Id}", () => image.Length > 0 ? ui.Art.GetUrl(image) : null, item.Title, subtitle, container: false, wide: true))
                ui.PlayAndRemember(item.StreamUrl, item.Title, image);

            if (item.Summary.Length > 0)
                Ui.Tip(Ui.Ellipsis(item.Summary, 300));
            column++;
        }
    }
}
