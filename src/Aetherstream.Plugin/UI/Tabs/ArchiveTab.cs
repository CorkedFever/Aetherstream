using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// The Internet Archive's films, browsed the way the Plex library is: shelves as chips, a search
/// box, a grid of posters, and a click to play. Public domain and freely licensed, no account.
/// </summary>
internal sealed class ArchiveTab(UiContext ui)
{
    private List<ArchiveLibrary.Film> films = [];
    private string status = string.Empty;
    private string query = string.Empty;
    private string shelf = ArchiveLibrary.Shelves[0].Collection;
    private string shelfTitle = ArchiveLibrary.Shelves[0].Title;
    private string opening = string.Empty;
    private bool autoBrowsed;

    /// <summary>Set by the plugin: a search is a round trip and cannot run in Draw.</summary>
    internal Action<string, string>? Search;

    /// <summary>Set by the plugin: picks the film's best file and plays it.</summary>
    internal Action<ArchiveLibrary.Film>? Open;

    public void SetFilms(List<ArchiveLibrary.Film> value, string message)
    {
        this.films = value;
        this.status = message;
    }

    public void SetStatus(string value) => this.status = value;

    public void SetOpening(string identifier) => this.opening = identifier;

    public void Draw()
    {
        if (!this.autoBrowsed)
        {
            this.autoBrowsed = true;
            this.status = "Asking the archive…";
            this.Search?.Invoke(this.shelf, string.Empty);
        }

        Ui.Hint("Films from the Internet Archive: public domain and freely licensed, played straight from archive.org. Quality varies with the transfer; the popular ones are usually the good ones.");

        this.DrawShelves();
        this.DrawSearch();

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void DrawShelves()
    {
        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var first = true;

        foreach (var (title, collection) in ArchiveLibrary.Shelves)
        {
            var width = ImGui.CalcTextSize(title).X + (ImGui.GetStyle().FramePadding.X * 2);
            if (!first && ImGui.GetItemRectMax().X + spacing + width < rightEdge)
                ImGui.SameLine();
            first = false;

            var selected = collection == this.shelf;
            using var colours = ImRaii.PushColor(ImGuiCol.Button, selected ? Theme.GlassLit : Theme.Glass)
                .Push(ImGuiCol.Border, selected ? Theme.Accent : Theme.GlassEdge)
                .Push(ImGuiCol.Text, selected ? Theme.Accent : Theme.Text);
            using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
            if (ImGui.Button($"{title}##shelf{collection}"))
            {
                this.shelf = collection;
                this.shelfTitle = title;
                this.status = $"Looking in {title}…";
                this.Search?.Invoke(this.shelf, this.query);
            }
        }
    }

    private void DrawSearch()
    {
        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##archivequery", $"Search {this.shelfTitle}, or leave empty for the popular ones", ref this.query, 120, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if (ImGui.Button("Search##archive") || go)
        {
            this.status = this.query.Length > 0 ? $"Searching for \"{this.query}\"…" : $"Looking in {this.shelfTitle}…";
            this.Search?.Invoke(this.shelf, this.query);
        }
    }

    private void DrawGrid()
    {
        var shown = this.films;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##archivegrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(false) + spacing)));
        var column = 0;

        foreach (var film in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var subtitle = film.Identifier == this.opening ? "opening…" : film.Year.Length > 0 ? film.Year : "archive.org";
            var poster = film.Poster;
            if (PosterCard.Draw(ui, $"##archive{film.Identifier}", () => ui.Art.GetUrl(poster), film.Title, subtitle, container: false))
            {
                this.opening = film.Identifier;
                this.Open?.Invoke(film);
            }

            if (ImGui.IsItemHovered() && film.Description.Length > 0)
                Ui.Tip(Plain(film.Description));

            column++;
        }

        if (shown.Count >= ArchiveLibrary.MaxItems)
        {
            ImGui.Spacing();
            Ui.Hint($"The {ArchiveLibrary.MaxItems} most downloaded are shown. Search to find a particular one.");
        }
    }

    /// <summary>A description's first sentence or so, with its HTML taken out, for a tooltip.</summary>
    private static string Plain(string html)
    {
        var sb = new System.Text.StringBuilder();
        var inTag = false;
        foreach (var ch in html)
        {
            if (ch == '<')
                inTag = true;
            else if (ch == '>')
                inTag = false;
            else if (!inTag)
                sb.Append(ch);
        }

        var text = sb.ToString().Replace("&amp;", "&").Replace("&quot;", "\"").Replace("&#39;", "'").Trim();
        return text.Length > 300 ? text[..300] + "…" : text;
    }
}
