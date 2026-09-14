using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Podcasts: your shows along the top, a search box that finds new ones by name or takes a feed
/// address, and the chosen show's episodes. Pick an episode and it plays under the drawn channels
/// in place of the music, in order from there.
/// </summary>
internal sealed class PodcastsTab(UiContext ui)
{
    private List<PodcastFeed.Show> found = [];
    private PodcastFeed.Feed? feed;
    private string feedUrl = string.Empty;
    private string status = string.Empty;
    private string query = string.Empty;
    private bool autoOpened;

    /// <summary>Set by the plugin: the directory search, in the background.</summary>
    internal Action<string>? Search;

    /// <summary>Set by the plugin: fetches a feed's episodes.</summary>
    internal Action<string>? OpenFeed;

    /// <summary>Set by the plugin: plays from an episode onward, in order.</summary>
    internal Action<string, string, string>? Play;

    public void SetFound(List<PodcastFeed.Show> value, string message)
    {
        this.found = value;
        this.status = message;
    }

    public void SetFeed(string url, PodcastFeed.Feed? value, string message)
    {
        this.feedUrl = url;
        this.feed = value;
        this.status = message;
    }

    public void SetStatus(string value) => this.status = value;

    public void Draw()
    {
        if (!this.autoOpened)
        {
            this.autoOpened = true;
            var last = ui.Config.ChannelMusicPodcastFeed;
            if (last.Length > 0)
                this.OpenFeed?.Invoke(last);
        }

        Ui.Hint("Search finds shows through Apple's podcast directory; a feed address pasted in works too. An episode plays under the drawn channels in place of the music, and the show carries on from there.");

        this.DrawShows();

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##podquery", "Search for a show, or paste a feed address", ref this.query, 200, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Search##pod") || go) && this.query.Trim().Length > 0)
        {
            var q = this.query.Trim();
            if (q.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                this.status = "Reading the feed…";
                this.found = [];
                this.OpenFeed?.Invoke(q);
            }
            else
            {
                this.status = "Searching…";
                this.Search?.Invoke(q);
            }
        }

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        if (this.found.Count > 0)
            this.DrawFound();
        else if (this.feed is { } open)
            this.DrawEpisodes(open);
    }

    private void DrawShows()
    {
        var shows = ui.Config.PodcastFeeds;
        if (shows.Count == 0)
            return;

        ImGui.Spacing();
        ImGui.TextColored(Theme.TextDim, "Your shows");
        string? remove = null;
        foreach (var s in shows)
        {
            var open = s.Url == this.feedUrl;
            using (ImRaii.PushColor(ImGuiCol.Button, open ? Theme.GlassLit : Theme.Glass).Push(ImGuiCol.Border, open ? Theme.Accent : Theme.GlassEdge))
            using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f))
            {
                if (ImGui.Button($"{Ui.Ellipsis(s.Title, 28)}##show{s.Url}"))
                {
                    this.found = [];
                    this.status = "Reading the feed…";
                    this.OpenFeed?.Invoke(s.Url);
                }
            }

            Ui.Tip($"{s.Title}\nRight-click to remove.");
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                remove = s.Url;
            ImGui.SameLine();
        }

        ImGui.NewLine();
        if (remove is not null)
        {
            shows.RemoveAll(s => s.Url == remove);
            ui.SaveConfig();
        }
    }

    private void DrawFound()
    {
        using var child = ImRaii.Child("##podfound", new Vector2(-1, -1), false);
        if (!child)
            return;

        foreach (var s in this.found)
        {
            var origin = ImGui.GetCursorScreenPos();
            const float RowHeight = 52f;
            if (ImGui.InvisibleButton($"##show{s.FeedUrl}", new Vector2(ImGui.GetContentRegionAvail().X, RowHeight)))
            {
                if (!ui.Config.PodcastFeeds.Any(p => p.Url == s.FeedUrl))
                {
                    ui.Config.PodcastFeeds.Add(new PodcastRef { Title = s.Title, Url = s.FeedUrl, Image = s.Image });
                    ui.SaveConfig();
                }

                this.found = [];
                this.status = "Reading the feed…";
                this.OpenFeed?.Invoke(s.FeedUrl);
            }

            var draw = ImGui.GetWindowDrawList();
            if (ImGui.IsItemHovered())
                draw.AddRectFilled(origin, origin + new Vector2(ImGui.GetContentRegionAvail().X, RowHeight), Theme.U32(Theme.Glass), 4f);
            var art = s.Image.Length > 0 ? ui.Art.GetUrl(s.Image) : null;
            if (art is not null)
                draw.AddImage(art.Handle, origin + new Vector2(4f, 4f), origin + new Vector2(48f, 48f));
            else
                draw.AddRectFilled(origin + new Vector2(4f, 4f), origin + new Vector2(48f, 48f), Theme.U32(Theme.GlassEdge), 4f);
            draw.AddText(origin + new Vector2(56f, 6f), Theme.U32(Theme.Text), s.Title);
            draw.AddText(origin + new Vector2(56f, 28f), Theme.U32(Theme.TextDim), $"{s.Author}  ·  {s.Episodes} episodes  ·  click to add and open");
        }
    }

    private void DrawEpisodes(PodcastFeed.Feed open)
    {
        ImGui.Spacing();
        var art = open.Image.Length > 0 ? ui.Art.GetUrl(open.Image) : null;
        if (art is not null)
        {
            ImGui.Image(art.Handle, new Vector2(64f, 64f));
            ImGui.SameLine();
        }

        using (ImRaii.Group())
        {
            using (Theme.PushDisplay())
                ImGui.TextUnformatted(open.Title.ToUpperInvariant());
            ImGui.TextColored(Theme.TextDim, Ui.Ellipsis(open.Description, 140));
            ImGui.TextColored(Theme.TextDim, $"{open.Episodes.Count} episodes");
        }

        using var child = ImRaii.Child("##episodes", new Vector2(-1, -1), false);
        if (!child)
            return;

        var playing = ui.Config.ChannelMusicSource == "podcast" ? ui.Config.ChannelMusicPodcastEpisode : string.Empty;
        foreach (var ep in open.Episodes)
        {
            var on = ep.Url == playing;
            var origin = ImGui.GetCursorScreenPos();
            const float RowHeight = 44f;
            if (ImGui.InvisibleButton($"##ep{ep.Url}", new Vector2(ImGui.GetContentRegionAvail().X, RowHeight)))
                this.Play?.Invoke(this.feedUrl, ep.Url, ep.Title);

            var draw = ImGui.GetWindowDrawList();
            if (on || ImGui.IsItemHovered())
                draw.AddRectFilled(origin, origin + new Vector2(ImGui.GetContentRegionAvail().X, RowHeight), Theme.U32(on ? Theme.GlassLit : Theme.Glass), 4f);
            draw.AddText(origin + new Vector2(8f, 4f), Theme.U32(on ? Theme.Accent : Theme.Text), Ui.Ellipsis(ep.Title, 70));
            var when = ep.PublishedUtc > DateTime.MinValue ? ep.PublishedUtc.ToLocalTime().ToString("d MMM yyyy") : string.Empty;
            var length = ep.Seconds > 0 ? $"{(int)(ep.Seconds / 60)} min" : string.Empty;
            draw.AddText(origin + new Vector2(8f, 24f), Theme.U32(Theme.TextDim), string.Join("  ·  ", new[] { when, length }.Where(x => x.Length > 0)));
            if (ep.Description.Length > 0)
                Ui.Tip(ep.Description);
        }
    }
}
