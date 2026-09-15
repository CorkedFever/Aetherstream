using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>TED: the newest talks, TED's playlists as chips, a search, and wide tiles. Free, worldwide, no account.</summary>
internal sealed class TedTab(UiContext ui)
{
    private List<TedTalks.Playlist> playlists = [];
    private List<TedTalks.Item> items = [];
    private string status = string.Empty;
    private string query = string.Empty;

    private bool pendingSearch;

    /// <summary>A search handed in from the home screen's Search: run as soon as the app is drawn.</summary>
    public void SearchFor(string text)
    {
        this.query = text;
        this.pendingSearch = true;
    }
    private string listing = string.Empty;
    private bool autoBrowsed;

    internal Action? Browse;

    /// <summary>Opens a listing: an empty playlist id for the newest talks, or a playlist, or a search.</summary>
    internal Action<TedTalks.Playlist?, string>? Open;

    public void SetPlaylists(List<TedTalks.Playlist> value) => this.playlists = value;

    public void SetItems(string forListing, List<TedTalks.Item> value, string message)
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
            this.Go(null, string.Empty);
        }

        Ui.Hint("TED talks: the newest, TED's own playlists, and a search. Free, worldwide, no account.");

        var labels = new List<string> { "Newest" };
        labels.AddRange(this.playlists.Select(p => p.Title));
        var selected = this.listing.StartsWith("search:", StringComparison.Ordinal) ? -1
            : this.listing.Length == 0 || this.listing == "newest" ? 0
            : this.playlists.FindIndex(p => "playlist:" + p.Id == this.listing) + 1;
        var picked = Ui.Chips("ted", labels, selected);
        if (picked >= 0 && picked != selected)
        {
            this.query = string.Empty;
            this.Go(picked == 0 ? null : this.playlists[picked - 1], string.Empty);
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##tedquery", "Search talks", ref this.query, 80, ImGuiInputTextFlags.EnterReturnsTrue);
        if (this.pendingSearch)
        {
            this.pendingSearch = false;
            go = true;
        }
        ImGui.SameLine();
        if ((ImGui.Button("Search##ted") || go) && this.query.Trim().Length > 0)
            this.Go(null, this.query.Trim());

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void Go(TedTalks.Playlist? playlist, string search)
    {
        this.listing = search.Length > 0 ? "search:" + search : playlist is { } p ? "playlist:" + p.Id : "newest";
        this.items = [];
        this.status = search.Length > 0 ? $"Searching TED for \"{search}\"…" : "Asking TED…";
        this.Open?.Invoke(playlist, search);
    }

    private void DrawGrid()
    {
        var shown = this.items;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##tedgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;

        foreach (var item in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var subtitle = string.Join("  ·  ", new[] { item.Speaker, item.Seconds > 0 ? $"{(int)Math.Round(item.Seconds / 60)} min" : string.Empty }.Where(x => x.Length > 0));
            var image = item.Image;
            if (PosterCard.Draw(ui, $"##ted{item.Slug}", () => image.Length > 0 ? ui.Art.GetUrl(image) : null, item.Title, subtitle, container: false, wide: true))
                ui.PlayAndRemember(item.Url, item.Title, image);
            column++;
        }
    }
}
