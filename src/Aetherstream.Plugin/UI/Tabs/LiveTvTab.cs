using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// The guide: your numbered channels across the top, and the whole list underneath as rows.
/// <para>
/// Rows, not tiles. The default list is thirteen thousand channels, and nobody chooses a channel
/// by its artwork — they scan for a name. A guide is a table with a search box, and always has been.
/// </para>
/// </summary>
internal sealed class LiveTvTab(UiContext ui, ChannelDial dial)
{
    /// <summary>Most rows drawn at once. Rows are cheap; this is a "narrow it down" nudge, not a budget.</summary>
    private const int MaxShown = 500;

    private const float RowHeight = 24f;

    private string search = string.Empty;
    private bool pinnedOnly;
    private bool addingPlaylist;
    private string newPlaylistName = string.Empty;
    private string newPlaylistUrl = string.Empty;

    private List<M3uPlaylist.Channel> filtered = [];
    private (string Term, string Group, string Country, bool Pinned, int PinCount)? filterKey;

    private List<M3uPlaylist.Channel> channels = [];
    private List<string> groups = [];
    private List<string> countries = [];
    private string status = string.Empty;
    private bool loading;

    /// <summary>Set by the plugin — fetching and parsing three megabytes is not frame work.</summary>
    internal Action<bool>? LoadPlaylist;

    public void SetChannels(List<M3uPlaylist.Channel> value, string message)
    {
        this.channels = value;
        this.groups = M3uPlaylist.GroupsOf(value);
        this.countries = M3uPlaylist.CountriesOf(value);
        this.status = message;
        this.loading = false;
        this.filterKey = null;

        dial.SetChannels(value);
    }

    public void SetStatus(string message, bool busy = false)
    {
        this.status = message;
        this.loading = busy;
    }

    public void Draw()
    {
        if (this.channels.Count == 0)
        {
            this.DrawEmpty();
            return;
        }

        this.DrawMyChannels();
        this.DrawFilters();

        var shown = this.Filtered();

        ImGui.TextColored(
            Theme.TextFaint,
            shown.Count >= MaxShown
                ? $"{this.channels.Count:N0} channels · showing {MaxShown} — narrow it down"
                : $"{shown.Count:N0} of {this.channels.Count:N0} channels");

        if (this.status.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(Theme.TextFaint, $"· {this.status}");
        }

        this.DrawRows(shown);
    }

    // -- empty -------------------------------------------------------------------------------------

    private void DrawEmpty()
    {
        Theme.Heading("Live TV");

        if (this.loading)
        {
            Ui.Hint(this.status.Length > 0 ? this.status : "Loading…");
            return;
        }

        Ui.Hint(
            "A playlist of live channels. The default is the iptv-org directory, which indexes " +
            "publicly available streams — thousands of them, of varying reliability.");

        if (ImGui.Button("Load channels", new Vector2(150, ImGui.GetFrameHeight() + 4)))
        {
            this.loading = true;
            this.LoadPlaylist?.Invoke(true);
        }

        if (this.status.Length > 0)
            ImGui.TextColored(Theme.Bad, this.status);

        ImGui.Spacing();
        this.DrawPlaylistPicker(wide: true);
    }

    // -- my channels -------------------------------------------------------------------------------

    /// <summary>
    /// The pinned channels as numbered presets. The number is the whole idea: it is what channel
    /// up and down step through, and what "CH 3" on the remote refers to.
    /// </summary>
    private void DrawMyChannels()
    {
        Theme.Heading("My channels");

        var pinned = dial.Pinned().ToList();
        var drawList = ImGui.GetWindowDrawList();
        var tile = new Vector2(92f, 46f);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;

        if (pinned.Count == 0)
        {
            Ui.Hint("Nothing pinned yet. Right-click any channel below to give it a number.");
            return;
        }

        var first = true;
        foreach (var (number, channel) in pinned)
        {
            if (!first && ImGui.GetItemRectMax().X + spacing + tile.X < rightEdge)
                ImGui.SameLine();

            first = false;

            var playing = string.Equals(channel.Url, ui.Config.Source, StringComparison.OrdinalIgnoreCase);
            var offline = dial.IsOffline(channel.Url);

            if (ImGui.InvisibleButton($"##pin{channel.Url}", tile))
                dial.Play(channel);

            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var hovered = ImGui.IsItemHovered();

            drawList.AddRectFilled(min, max, Theme.U32(playing ? Theme.GlassLit : Theme.Glass), 4f);
            drawList.AddRect(min, max, Theme.U32(playing || hovered ? Theme.Accent : Theme.GlassEdge), 4f);

            using (Theme.PushDisplay())
            {
                drawList.AddText(min + new Vector2(6f, 2f), Theme.U32(playing ? Theme.Accent : Theme.TextDim), number.ToString());
            }

            var name = Fit(channel.Name, tile.X - 12f);
            drawList.AddText(
                new Vector2(min.X + 6f, max.Y - ImGui.GetTextLineHeight() - 4f),
                Theme.U32(offline ? Theme.TextFaint : Theme.Text),
                name);

            if (hovered)
                Ui.Tip(offline ? $"{channel.Name}\nwent offline recently — click to try again" : $"{channel.Name}\nright-click to unpin");

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                dial.TogglePin(channel.Url);
        }

        ImGui.Dummy(new Vector2(0f, 2f));
    }

    // -- filters -----------------------------------------------------------------------------------

    private void DrawFilters()
    {
        ImGui.SetNextItemWidth(-380);
        ImGui.InputTextWithHint("##tvsearch", $"search {this.channels.Count:N0} channels", ref this.search, 64);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(110);
        this.DrawPicker("##group", "All groups", this.groups, ui.Config.LiveTvGroup, value =>
        {
            ui.Config.LiveTvGroup = value;
            ui.SaveConfig();
        });

        ImGui.SameLine();
        ImGui.SetNextItemWidth(70);
        this.DrawPicker("##country", "Any", this.countries, ui.Config.LiveTvCountry, value =>
        {
            ui.Config.LiveTvCountry = value;
            ui.SaveConfig();
        });

        ImGui.SameLine();
        ImGui.Checkbox("Pinned", ref this.pinnedOnly);

        ImGui.SameLine();
        this.DrawPlaylistPicker(wide: false);

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.Sync, "Re-download this playlist", "##tvrefresh", !this.loading))
        {
            this.loading = true;
            this.LoadPlaylist?.Invoke(true);
        }

        if (this.addingPlaylist)
            this.DrawAddPlaylist();
    }

    /// <summary>
    /// Which channel list is in use. It lives beside the filters rather than behind an empty
    /// state, because the moment anyone has two lists — the public one and their own server — they
    /// switch between them, and switching cannot require unloading the first.
    /// </summary>
    private void DrawPlaylistPicker(bool wide)
    {
        var lists = ui.Config.LiveTvPlaylists;
        var current = lists.FirstOrDefault(p => p.Url == ui.Config.LiveTvPlaylistUrl);
        var label = current?.Name is { Length: > 0 } name ? name : "playlist";

        ImGui.SetNextItemWidth(wide ? 240 : 96);
        using var combo = ImRaii.Combo("##playlist", label);
        if (!combo)
            return;

        foreach (var list in lists)
        {
            if (!ImGui.Selectable(list.Name.Length > 0 ? list.Name : list.Url, list.Url == ui.Config.LiveTvPlaylistUrl))
                continue;

            ui.Config.LiveTvPlaylistUrl = list.Url;
            ui.SaveConfig();
            this.loading = true;
            this.channels = [];
            this.LoadPlaylist?.Invoke(false);
        }

        ImGui.Separator();

        if (ImGui.Selectable("Add a playlist…"))
            this.addingPlaylist = true;

        if (current is not null && lists.Count > 1 && ImGui.Selectable($"Remove \"{label}\""))
        {
            lists.Remove(current);
            ui.Config.LiveTvPlaylistUrl = lists[0].Url;
            ui.SaveConfig();
            this.loading = true;
            this.channels = [];
            this.LoadPlaylist?.Invoke(false);
        }
    }

    private void DrawAddPlaylist()
    {
        Theme.Panel("addplaylist", () =>
        {
            ImGui.TextColored(Theme.TextDim, "Any extended M3U works — an ErsatzTV or Tunarr server, or another public list.");

            ImGui.SetNextItemWidth(140);
            ImGui.InputTextWithHint("##newname", "name", ref this.newPlaylistName, 40);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(-130);
            ImGui.InputTextWithHint("##newurl", "https://…/playlist.m3u", ref this.newPlaylistUrl, 512);

            ImGui.SameLine();
            var ok = this.newPlaylistUrl.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase);
            using (ImRaii.Disabled(!ok))
            {
                if (ImGui.Button("Add"))
                {
                    var url = this.newPlaylistUrl.Trim();
                    var name = this.newPlaylistName.Trim();

                    ui.Config.LiveTvPlaylists.Add(new Playlist
                    {
                        Name = name.Length > 0 ? name : Ui.Pretty(url),
                        Url = url,
                    });

                    ui.Config.LiveTvPlaylistUrl = url;
                    ui.SaveConfig();

                    this.addingPlaylist = false;
                    this.newPlaylistName = string.Empty;
                    this.newPlaylistUrl = string.Empty;
                    this.loading = true;
                    this.channels = [];
                    this.LoadPlaylist?.Invoke(true);
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                this.addingPlaylist = false;
        });
    }

    private void DrawPicker(string id, string anyLabel, List<string> options, string current, Action<string> set)
    {
        using var combo = ImRaii.Combo(id, current.Length > 0 ? current : anyLabel);
        if (!combo)
            return;

        if (ImGui.Selectable(anyLabel, current.Length == 0))
            set(string.Empty);

        foreach (var option in options)
        {
            if (ImGui.Selectable(option, option == current))
                set(option);
        }
    }

    private List<M3uPlaylist.Channel> Filtered()
    {
        var term = this.search.Trim();
        var group = ui.Config.LiveTvGroup;
        var country = ui.Config.LiveTvCountry;
        var pinned = ui.Config.LiveTvFavourites;

        // Recomputed only when an input changes: four predicates across thirteen thousand
        // channels every frame was a visible hitch the first time the tab opened.
        var key = (term, group, country, this.pinnedOnly, pinned.Count);
        if (key == this.filterKey)
            return this.filtered;

        this.filterKey = key;
        this.filtered = this.channels
            .Where(c => !this.pinnedOnly || dial.IsPinned(c.Url))
            .Where(c => group.Length == 0 || c.Group == group)
            .Where(c => country.Length == 0 || c.Country == country)
            .Where(c => term.Length == 0 || c.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(MaxShown)
            .ToList();

        return this.filtered;
    }

    // -- rows --------------------------------------------------------------------------------------

    private void DrawRows(List<M3uPlaylist.Channel> shown)
    {
        using var child = ImRaii.Child("##tvrows", new Vector2(-1, -1), false);
        if (!child)
            return;

        if (shown.Count == 0)
        {
            Ui.Hint(this.pinnedOnly
                ? "Nothing pinned yet. Right-click a channel to pin it."
                : "No channels match that.");

            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var line = ImGui.GetTextLineHeight();

        // Column positions, from the right edge inwards so the name gets whatever is left.
        var tagRight = width - 8f;
        var countryX = width - 120f;
        var groupX = width - 260f;

        foreach (var channel in shown)
        {
            var url = channel.Url;
            var playing = string.Equals(url, ui.Config.Source, StringComparison.OrdinalIgnoreCase);
            var offline = dial.IsOffline(url);
            var number = dial.NumberOf(url);

            if (ImGui.Selectable($"##row{url}", playing, ImGuiSelectableFlags.None, new Vector2(0f, RowHeight)))
                dial.Play(channel);

            // Right-click pins. A dedicated button on every row is more furniture than a guide
            // has room for, and the tooltip says so.
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                dial.TogglePin(url);

            // Everything below is drawing only, and only for rows on screen.
            if (!ImGui.IsItemVisible())
                continue;

            var min = ImGui.GetItemRectMin();
            var textY = min.Y + ((RowHeight - line) / 2f);
            var nameColour = offline ? Theme.TextFaint : playing ? Theme.Accent : Theme.Text;

            // A logo where one is cached. Small and fetched lazily; the guide is not a poster wall.
            var logoBox = new Vector2(26f, 16f);
            var logoAt = new Vector2(min.X + 6f, min.Y + ((RowHeight - logoBox.Y) / 2f));
            if (channel.LogoUrl.Length > 0 && ui.Art.GetUrl(channel.LogoUrl) is { } logo)
            {
                var scale = Math.Min(logoBox.X / logo.Size.X, logoBox.Y / logo.Size.Y);
                var drawn = logo.Size * scale;
                var offset = (logoBox - drawn) * 0.5f;
                drawList.AddImage(logo.Handle, logoAt + offset, logoAt + offset + drawn);
            }
            else
            {
                drawList.AddRectFilled(logoAt, logoAt + logoBox, Theme.U32(Theme.GlassEdge), 2f);
            }

            drawList.AddText(new Vector2(min.X + 40f, textY), Theme.U32(nameColour), Fit(channel.Name, groupX - 48f));
            drawList.AddText(new Vector2(min.X + groupX, textY), Theme.U32(offline ? Theme.TextFaint : Theme.TextDim), Fit(channel.Group, 130f));
            drawList.AddText(new Vector2(min.X + countryX, textY), Theme.U32(offline ? Theme.TextFaint : Theme.TextDim), channel.Country);

            // The tag on the right says the one thing worth knowing about this row.
            var (tag, tagColour) =
                playing ? ("▶ playing", Theme.Accent)
                : offline ? ("offline", Theme.TextFaint)
                : number > 0 ? ($"★ {number}", Theme.Good)
                : (string.Empty, Theme.TextFaint);

            if (tag.Length > 0)
            {
                var tagSize = ImGui.CalcTextSize(tag);
                drawList.AddText(new Vector2(min.X + tagRight - tagSize.X, textY), Theme.U32(tagColour), tag);
            }

            if (ImGui.IsItemHovered())
            {
                Ui.Tip(
                    offline ? $"{channel.Name}\nStopped sending recently. Click to try it again."
                    : number > 0 ? $"{channel.Name}\nChannel {number} — right-click to unpin"
                    : $"{channel.Name}\nClick to watch, right-click to pin");
            }
        }

        ImGui.Spacing();
        Ui.Hint(
            "Plenty of channels in a public list are offline or region-locked at any moment — if " +
            "one does nothing, try another rather than assuming it is broken.");
    }

    /// <summary>Truncates to a pixel width; the draw list does not clip text on its own.</summary>
    private static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width)
            return text;

        var span = text.AsSpan();
        for (var length = text.Length - 1; length > 1; length--)
        {
            if (ImGui.CalcTextSize($"{span[..length]}…").X <= width)
                return string.Concat(span[..length], "…");
        }

        return "…";
    }
}
