using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>PBS: a shelf of its best shows, each opened into the episodes its page lists, with stills. Plays through yt-dlp.</summary>
internal sealed class PbsTab(UiContext ui)
{
    private List<PbsShows.Episode> episodes = [];
    private string status = string.Empty;
    private string slug = string.Empty;
    private bool autoBrowsed;

    internal Action<string>? Open;

    public void SetEpisodes(string forSlug, List<PbsShows.Episode> value, string message)
    {
        if (forSlug != this.slug)
            return;
        this.episodes = value;
        this.status = message;
    }

    public void Draw()
    {
        if (ui.LocateYtDlp() is null)
        {
            Ui.Hint("PBS plays through yt-dlp. Setup, Sources says where to get it.");
            return;
        }

        if (!this.autoBrowsed)
        {
            this.autoBrowsed = true;
            this.Go(PbsShows.Shows[0].Slug);
        }

        Ui.Hint("PBS's shows, read from their episode pages. Free; some titles are only for viewers in the United States.");

        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var first = true;
        foreach (var show in PbsShows.Shows)
        {
            var width = ImGui.CalcTextSize(show.Name).X + (ImGui.GetStyle().FramePadding.X * 2);
            if (!first && ImGui.GetItemRectMax().X + spacing + width < rightEdge)
                ImGui.SameLine();
            first = false;

            var selected = show.Slug == this.slug;
            using var colours = ImRaii.PushColor(ImGuiCol.Button, selected ? Theme.GlassLit : Theme.Glass)
                .Push(ImGuiCol.Border, selected ? Theme.Accent : Theme.GlassEdge)
                .Push(ImGuiCol.Text, selected ? Theme.Accent : Theme.Text);
            using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
            if (ImGui.Button($"{show.Name}##pbs{show.Slug}"))
                this.Go(show.Slug);
        }

        ImGui.Spacing();
        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void Go(string newSlug)
    {
        this.slug = newSlug;
        this.episodes = [];
        this.status = "Reading the episodes…";
        this.Open?.Invoke(newSlug);
    }

    private void DrawGrid()
    {
        var shown = this.episodes;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##pbsgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;

        foreach (var ep in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var subtitle = string.Join("  ·  ", new[] { ep.Kind, ep.Length }.Where(x => x.Length > 0));
            var still = ep.Still;
            if (PosterCard.Draw(ui, $"##pbs{ep.Url}", () => still.Length > 0 ? ui.Art.GetUrl(still + "?resize=400x225") : null, ep.Title, subtitle, container: false, wide: true))
                ui.PlayAndRemember(ep.Url, ep.Title, still.Length > 0 ? still + "?resize=400x225" : string.Empty);

            if (ep.Description.Length > 0)
                Ui.Tip(ep.Description);
            column++;
        }
    }
}
