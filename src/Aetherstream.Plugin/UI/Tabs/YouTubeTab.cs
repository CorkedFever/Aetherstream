using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// YouTube, browsed: a search, a playlist or channel page, or your own feeds when a browser's
/// cookies are set in Setup. Wide tiles with thumbnails; pick one and it plays, and the rest of
/// the list follows it, so a playlist plays through.
/// </summary>
internal sealed class YouTubeTab(UiContext ui)
{
    private List<YtDlpBrowser.Video> videos = [];
    private string status = string.Empty;
    private string query = string.Empty;
    private string target = string.Empty;
    private string title = string.Empty;

    /// <summary>Set by the plugin: a listing through yt-dlp, in the background.</summary>
    internal Action<string>? Browse;

    public void SetVideos(string forTarget, List<YtDlpBrowser.Video> value, string message)
    {
        if (forTarget != this.target)
            return;
        this.videos = value;
        this.status = message;
    }

    public void Draw()
    {
        if (ui.LocateYtDlp() is null)
        {
            Ui.Hint("YouTube needs yt-dlp. Setup, Sources says where to get it.");
            return;
        }

        Ui.Hint("Search, or paste a playlist or channel link. Your own feeds need a signed-in browser's cookies, set under Setup, Sources.");

        var hasCookies = ui.Config.YtDlpCookiesBrowser.Length > 0 || ui.Config.YtDlpCookiesFile.Length > 0;
        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var first = true;
        foreach (var (name, feed) in YtDlpBrowser.Feeds)
        {
            var width = ImGui.CalcTextSize(name).X + (ImGui.GetStyle().FramePadding.X * 2);
            if (!first && ImGui.GetItemRectMax().X + spacing + width < rightEdge)
                ImGui.SameLine();
            first = false;

            var selected = feed == this.target;
            using var colours = ImRaii.PushColor(ImGuiCol.Button, selected ? Theme.GlassLit : Theme.Glass)
                .Push(ImGuiCol.Border, selected ? Theme.Accent : Theme.GlassEdge)
                .Push(ImGuiCol.Text, selected ? Theme.Accent : Theme.Text);
            using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
            using var disabled = ImRaii.Disabled(!hasCookies);
            if (ImGui.Button($"{name}##feed{feed}"))
                this.Go(feed, name);
            if (!hasCookies)
                Ui.Tip("Needs a signed-in browser's cookies: Setup, Sources.");
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##ytquery", "Search YouTube, or paste a playlist or channel link", ref this.query, 300, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Search##yt") || go) && this.query.Trim().Length > 0)
        {
            var q = this.query.Trim();
            if (q.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                this.Go(q, "the link");
            else
                this.Go(YtDlpBrowser.SearchTarget(q), $"\"{q}\"");
        }

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void Go(string newTarget, string name)
    {
        this.target = newTarget;
        this.title = name;
        this.videos = [];
        this.status = $"Asking yt-dlp for {name}…";
        this.Browse?.Invoke(newTarget);
    }

    private void DrawGrid()
    {
        var shown = this.videos;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##ytgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;

        foreach (var v in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var subtitle = string.Join("  ·  ", new[] { v.Channel, v.Seconds > 0 ? Ui.Clock(v.Seconds * 1000) : string.Empty }.Where(x => x.Length > 0));
            var thumb = v.Thumb;
            if (PosterCard.Draw(ui, $"##yt{v.Id}", () => thumb.Length > 0 ? ui.Art.GetUrl(thumb) : null, v.Title, subtitle, container: false, wide: true))
            {
                ui.PlayAndRemember(v.Url, v.Title, v.Thumb);

                // The rest of the list follows, so a playlist or a search plays through.
                var at = shown.IndexOf(v);
                foreach (var later in shown.Skip(at + 1))
                    ui.NextUp.Add((later.Url, later.Title, later.Thumb, later.Seconds * 1000));
            }

            Ui.Tip(v.Views > 0 ? $"{v.Title}\n{v.Views:N0} views" : v.Title);
            column++;
        }
    }
}
