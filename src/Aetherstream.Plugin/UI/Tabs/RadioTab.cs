using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Internet radio: favourites along the top, genre chips, a search box, and a list of stations
/// with their logos. Pick one and it plays under the drawn channels in place of the music.
/// </summary>
internal sealed class RadioTab(UiContext ui)
{
    private List<RadioBrowser.Station> stations = [];
    private string status = string.Empty;
    private string query = string.Empty;
    private string tag = "ambient";
    private bool autoSearched;

    /// <summary>Set by the plugin: a directory search, in the background.</summary>
    internal Action<string, string>? Search;

    /// <summary>Set by the plugin: makes this station the music source and starts it.</summary>
    internal Action<string, string, string>? Tune;

    public void SetStations(List<RadioBrowser.Station> value, string message)
    {
        this.stations = value;
        this.status = message;
    }

    public void SetStatus(string value) => this.status = value;

    public void Draw()
    {
        if (!this.autoSearched)
        {
            this.autoSearched = true;
            this.status = "Asking the directory…";
            this.Search?.Invoke(string.Empty, this.tag);
        }

        Ui.Hint("Stations from the Radio Browser directory, a community list of some thirty thousand. A station plays under the drawn channels in place of the music, and on the Radio channel with its logo.");

        var tuned = ui.Config.ChannelMusicSource == "radio" ? ui.Config.ChannelMusicRadioName : string.Empty;
        if (tuned.Length > 0)
        {
            Ui.Dot(Theme.Good, "on");
            ImGui.SameLine();
            ImGui.TextUnformatted($"Tuned to {tuned}");
            if (ui.Session.MusicLiveTitle is { Length: > 0 } live)
            {
                ImGui.SameLine();
                ImGui.TextColored(Theme.TextDim, $"· {live}");
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Back to the music"))
            {
                ui.Config.ChannelMusicSource = "bundled";
                ui.SaveConfig();
                this.Tune?.Invoke(string.Empty, string.Empty, string.Empty);
            }
        }

        this.DrawFavourites();
        this.DrawTags();

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##radioquery", "Search by station name", ref this.query, 80, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if (ImGui.Button("Search##radio") || go)
        {
            this.status = "Searching…";
            this.Search?.Invoke(this.query, this.query.Length > 0 ? string.Empty : this.tag);
        }

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawList();
    }

    private void DrawFavourites()
    {
        var favourites = ui.Config.RadioFavourites;
        if (favourites.Count == 0)
            return;

        ImGui.Spacing();
        ImGui.TextColored(Theme.TextDim, "Favourites");
        string? remove = null;
        foreach (var f in favourites)
        {
            var on = ui.Config.ChannelMusicSource == "radio" && ui.Config.ChannelMusicRadioUrl == f.Url;
            using (ImRaii.PushColor(ImGuiCol.Button, on ? Theme.GlassLit : Theme.Glass).Push(ImGuiCol.Border, on ? Theme.Accent : Theme.GlassEdge))
            using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f))
            {
                if (ImGui.Button($"{f.Name}##fav{f.Url}"))
                    this.Tune?.Invoke(f.Name, f.Url, f.Logo);
            }

            Ui.Tip("Right-click to remove from favourites.");
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                remove = f.Url;
            ImGui.SameLine();
        }

        ImGui.NewLine();
        if (remove is not null)
        {
            favourites.RemoveAll(f => f.Url == remove);
            ui.SaveConfig();
        }
    }

    private void DrawTags()
    {
        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var first = true;
        foreach (var t in RadioBrowser.Tags)
        {
            var width = ImGui.CalcTextSize(t).X + (ImGui.GetStyle().FramePadding.X * 2);
            if (!first && ImGui.GetItemRectMax().X + spacing + width < rightEdge)
                ImGui.SameLine();
            first = false;

            var selected = t == this.tag && this.query.Length == 0;
            using var colours = ImRaii.PushColor(ImGuiCol.Button, selected ? Theme.GlassLit : Theme.Glass)
                .Push(ImGuiCol.Border, selected ? Theme.Accent : Theme.GlassEdge)
                .Push(ImGuiCol.Text, selected ? Theme.Accent : Theme.Text);
            using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
            if (ImGui.Button($"{t}##tag{t}"))
            {
                this.tag = t;
                this.query = string.Empty;
                this.status = $"Looking for {t}…";
                this.Search?.Invoke(string.Empty, t);
            }
        }
    }

    private void DrawList()
    {
        var shown = this.stations;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##radiolist", new Vector2(-1, -1), false);
        if (!child)
            return;

        foreach (var s in shown)
        {
            var on = ui.Config.ChannelMusicSource == "radio" && ui.Config.ChannelMusicRadioUrl == s.Url;
            var origin = ImGui.GetCursorScreenPos();
            var rowHeight = 44f;
            if (ImGui.InvisibleButton($"##st{s.Id}", new Vector2(ImGui.GetContentRegionAvail().X - 40f, rowHeight)))
                this.Tune?.Invoke(s.Name, s.Url, s.Logo);

            var hovered = ImGui.IsItemHovered();
            var draw = ImGui.GetWindowDrawList();
            if (on || hovered)
                draw.AddRectFilled(origin, origin + new Vector2(ImGui.GetContentRegionAvail().X + 40f, rowHeight), Theme.U32(on ? Theme.GlassLit : Theme.Glass), 4f);

            var logo = s.Logo.Length > 0 ? ui.Art.GetUrl(s.Logo) : null;
            if (logo is not null)
                draw.AddImage(logo.Handle, origin + new Vector2(4f, 4f), origin + new Vector2(40f, 40f));
            else
                draw.AddRectFilled(origin + new Vector2(4f, 4f), origin + new Vector2(40f, 40f), Theme.U32(Theme.GlassEdge), 4f);

            draw.AddText(origin + new Vector2(48f, 4f), Theme.U32(on ? Theme.Accent : Theme.Text), s.Name);
            var detail = string.Join("  ·  ", new[] { s.Country, s.Codec.Length > 0 ? $"{s.Codec} {s.Bitrate}k" : string.Empty, s.Tags.Length > 0 ? Ui.Ellipsis(s.Tags, 40) : string.Empty }.Where(x => x.Length > 0));
            draw.AddText(origin + new Vector2(48f, 24f), Theme.U32(Theme.TextDim), detail);

            ImGui.SameLine();
            var isFav = ui.Config.RadioFavourites.Any(f => f.Url == s.Url);
            if (Ui.IconButton(isFav ? FontAwesomeIcon.Star : FontAwesomeIcon.StarHalfAlt, isFav ? "Remove from favourites" : "Add to favourites", $"##fav{s.Id}"))
            {
                if (isFav)
                    ui.Config.RadioFavourites.RemoveAll(f => f.Url == s.Url);
                else
                    ui.Config.RadioFavourites.Add(new RadioStationRef { Name = s.Name, Url = s.Url, Logo = s.Logo });
                ui.SaveConfig();
            }
        }
    }
}
