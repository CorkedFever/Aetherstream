using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// South Park: one list of the episodes the site still plays, newest season first, under a line
/// per season; the rest of that season is queued after the one pressed. Plays from the site's
/// own playlist, nothing to install.
/// </summary>
internal sealed class SouthParkTab(UiContext ui)
{
    private List<SouthParkStudios.Episode> episodes = [];
    private Dictionary<int, int> perSeason = [];
    private string status = string.Empty;
    private bool autoBrowsed;

    internal Action? Browse;

    internal Action? Refresh;

    /// <summary>For the home tile: how many play, once that is known.</summary>
    public string Summary => this.episodes.Count > 0 ? $"{this.episodes.Count} free episodes" : "the free episodes";

    public void SetEpisodes(List<SouthParkStudios.Episode> value, string message)
    {
        // Replaced, never edited: Draw may be walking the old list on the render thread.
        this.perSeason = value.GroupBy(e => e.Season).ToDictionary(g => g.Key, g => g.Count());
        this.episodes = value;
        this.status = message;
    }

    public void SetStatus(string value) => this.status = value;

    public void Draw()
    {
        if (!this.autoBrowsed)
        {
            this.autoBrowsed = true;
            this.status = "Asking South Park Studios…";
            this.Browse?.Invoke();
        }

        Ui.Hint("What Paramount's South Park Studios still plays free, with its own ad breaks. Most episodes have moved to its paid service; these are the rest, newest season first.");

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);
        if (this.episodes.Count > 0 && this.Refresh is { } refresh)
        {
            if (this.status.Length > 0)
                ImGui.SameLine();
            if (ImGui.SmallButton("Check again"))
            {
                this.status = "Asking South Park Studios…";
                refresh();
            }

            Ui.Tip("The list is kept for twelve hours; this asks the site now.");
        }

        this.DrawGrid();
    }

    private void DrawGrid()
    {
        var shown = this.episodes;
        var counts = this.perSeason;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##spgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;
        var season = -1;

        for (var i = 0; i < shown.Count; i++)
        {
            var ep = shown[i];
            if (ep.Season != season)
            {
                season = ep.Season;
                column = 0;
                if (i > 0)
                    ImGui.Spacing();
                var count = counts.TryGetValue(season, out var c) ? c : 0;
                ImGui.TextColored(Ui.Faint, count == 1 ? $"Season {season}  ·  one episode" : $"Season {season}  ·  {count} episodes");
            }

            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var still = ep.Still;
            if (PosterCard.Draw(ui, $"##sp{ep.Url}", () => still.Length > 0 ? ui.Art.GetUrl(still) : null, ep.Title, ep.Code, container: false, wide: true))
            {
                ui.PlayAndRemember(ep.Url, $"South Park {ep.Code} · {ep.Title}", still);
                for (var j = i + 1; j < shown.Count && shown[j].Season == season; j++)
                    ui.NextUp.Add((shown[j].Url, $"South Park {shown[j].Code} · {shown[j].Title}", shown[j].Still, 0));
            }

            if (ep.Description.Length > 0)
                Ui.Tip(ep.Description);
            column++;
        }
    }
}
