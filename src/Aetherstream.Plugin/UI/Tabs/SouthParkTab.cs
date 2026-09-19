using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>South Park: a season to pick, its episodes as stills, the rest of the season queued after the one pressed. Plays through yt-dlp.</summary>
internal sealed class SouthParkTab(UiContext ui)
{
    private List<SouthParkStudios.Season> seasons = [];
    private List<SouthParkStudios.Episode> episodes = [];
    private SouthParkStudios.Season? season;
    private string status = string.Empty;
    private bool autoBrowsed;

    internal Action? Browse;

    internal Action<SouthParkStudios.Season>? Open;

    public void SetSeasons(List<SouthParkStudios.Season> value, string message)
    {
        this.seasons = value;
        this.status = message;
        if (this.season is null && value.Count > 0)
        {
            this.autoPicking = true;
            this.Go(value[^1]);
        }
    }

    public void SetEpisodes(SouthParkStudios.Season forSeason, List<SouthParkStudios.Episode> value, string message)
    {
        if (this.season is not { } s || s.Number != forSeason.Number)
            return;

        // The newest seasons are often on the air, or kept for the paid service, so they come
        // up empty or wholly locked; when nobody asked for one, step back to the newest season
        // with something that plays.
        if (this.autoPicking && value.All(e => e.Locked))
        {
            var index = this.seasons.FindIndex(x => x.Number == s.Number);
            if (index > 0)
            {
                this.Go(this.seasons[index - 1]);
                return;
            }
        }

        this.autoPicking = false;
        this.episodes = value;
        this.status = message;
    }

    private bool autoPicking;

    public void SetStatus(string value) => this.status = value;

    public void Draw()
    {
        if (ui.LocateYtDlp() is null)
        {
            Ui.Hint("South Park plays through yt-dlp. Setup, Sources says where to get it.");
            return;
        }

        if (!this.autoBrowsed)
        {
            this.autoBrowsed = true;
            this.status = "Asking South Park Studios…";
            this.Browse?.Invoke();
        }

        Ui.Hint("Every episode, free with the site's own ad breaks, from Paramount's South Park Studios. A few are locked at any one time; those are greyed.");

        if (this.seasons.Count > 0)
        {
            var labels = this.seasons.Select(s => $"Season {s.Number}").ToList();
            var selected = this.season is { } cur ? this.seasons.FindIndex(s => s.Number == cur.Number) : -1;
            var picked = Ui.Chips("sp", labels, selected);
            if (picked >= 0 && picked != selected)
            {
                this.autoPicking = false;
                this.Go(this.seasons[picked]);
            }
        }

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void Go(SouthParkStudios.Season s)
    {
        this.season = s;
        this.episodes = [];
        this.status = $"Listing season {s.Number}…";
        this.Open?.Invoke(s);
    }

    private void DrawGrid()
    {
        var shown = this.episodes;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##spgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;

        for (var i = 0; i < shown.Count; i++)
        {
            var ep = shown[i];
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var still = ep.Still;
            using (ImRaii.Disabled(ep.Locked))
            {
                if (PosterCard.Draw(ui, $"##sp{ep.Url}", () => still.Length > 0 ? ui.Art.GetUrl(still) : null, ep.Title, ep.Locked ? $"{ep.Code}  ·  locked" : ep.Code, container: false, wide: true) && !ep.Locked)
                {
                    ui.PlayAndRemember(ep.Url, $"South Park {ep.Code} · {ep.Title}", still);
                    for (var j = i + 1; j < shown.Count; j++)
                    {
                        if (!shown[j].Locked)
                            ui.NextUp.Add((shown[j].Url, $"South Park {shown[j].Code} · {shown[j].Title}", shown[j].Still, 0));
                    }
                }
            }

            if (ep.Description.Length > 0)
                Ui.Tip(ep.Description);
            column++;
        }
    }
}
