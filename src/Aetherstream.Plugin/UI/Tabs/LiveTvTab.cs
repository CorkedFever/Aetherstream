using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Live channels from an M3U playlist.
/// <para>
/// The default list is around thirteen thousand channels, so this is a search box before it is a
/// grid: filters first, and a hard cap on how many tiles are drawn. Every visible tile can ask for
/// a logo, and thirteen thousand of those is not a page — it is a denial of service against
/// yourself.
/// </para>
/// </summary>
internal sealed class LiveTvTab(UiContext ui)
{
    /// <summary>
    /// Most tiles drawn at once. A display limit rather than a scroll limit, because the cost of a
    /// tile is a logo fetch rather than a row of text.
    /// </summary>
    private const int MaxShown = 240;

    private string search = string.Empty;
    private bool pinnedOnly;

    private List<M3uPlaylist.Channel> channels = [];
    private List<string> groups = [];
    private List<string> countries = [];
    private string status = string.Empty;
    private bool loading;

    /// <summary>Set by the plugin — fetching and parsing three megabytes is not frame work.</summary>
    internal Action<bool>? LoadPlaylist;

    internal Action<M3uPlaylist.Channel>? PlayChannel;

    public void SetChannels(List<M3uPlaylist.Channel> value, string message)
    {
        this.channels = value;
        this.groups = M3uPlaylist.GroupsOf(value);
        this.countries = M3uPlaylist.CountriesOf(value);
        this.status = message;
        this.loading = false;
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

        this.DrawFilters();

        var shown = this.Filtered();

        ImGui.TextColored(
            Ui.Faint,
            shown.Count >= MaxShown
                ? $"{this.channels.Count:N0} channels · showing {MaxShown} — narrow it down"
                : $"{shown.Count:N0} of {this.channels.Count:N0} channels");

        if (this.status.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(Ui.Faint, $"· {this.status}");
        }

        this.DrawGrid(shown);
    }

    private void DrawEmpty()
    {
        Ui.Section("Live TV");

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
            ImGui.TextColored(Ui.Bad, this.status);

        ImGui.Spacing();
        ImGui.TextColored(Ui.Faint, "Playlist");

        var url = ui.Config.LiveTvPlaylistUrl;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##playlisturl", "https://…/playlist.m3u", ref url, 512))
        {
            ui.Config.LiveTvPlaylistUrl = url.Trim();
            ui.SaveConfig();
        }

        Ui.Tip("Any extended M3U works — swap in your own list if you have one.");
    }

    private void DrawFilters()
    {
        ImGui.SetNextItemWidth(-330);
        ImGui.InputTextWithHint("##tvsearch", "search channels", ref this.search, 64);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        this.DrawPicker("##group", "All groups", this.groups, ui.Config.LiveTvGroup, value =>
        {
            ui.Config.LiveTvGroup = value;
            ui.SaveConfig();
        });

        ImGui.SameLine();
        ImGui.SetNextItemWidth(80);
        this.DrawPicker("##country", "Any", this.countries, ui.Config.LiveTvCountry, value =>
        {
            ui.Config.LiveTvCountry = value;
            ui.SaveConfig();
        });

        ImGui.SameLine();
        ImGui.Checkbox("Pinned", ref this.pinnedOnly);
        Ui.Tip("Right-click any channel to pin it. A list this size needs a shortlist.");

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.Sync, "Re-download the playlist", "##tvrefresh", !this.loading))
        {
            this.loading = true;
            this.LoadPlaylist?.Invoke(true);
        }
    }

    /// <summary>
    /// A combo with an "everything" entry on top. There are 180-odd groups and as many countries in
    /// the default list, which is far past what the row of chips in the library would take.
    /// </summary>
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

        return this.channels
            .Where(c => !this.pinnedOnly || pinned.Contains(c.Url))
            .Where(c => group.Length == 0 || c.Group == group)
            .Where(c => country.Length == 0 || c.Country == country)
            .Where(c => term.Length == 0 || c.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(MaxShown)
            .ToList();
    }

    private void DrawGrid(List<M3uPlaylist.Channel> shown)
    {
        using var child = ImRaii.Child("##tvgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;

        foreach (var channel in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var url = channel.Url;
            var pinned = ui.Config.LiveTvFavourites.Contains(url);

            var subtitle = channel.Country.Length > 0
                ? $"{channel.Country} · {channel.Group}"
                : channel.Group;

            // Logos are ordinary image URLs, so they go through the art cache by URL and inherit its
            // fetching limit, retirement and eviction. The lambda is only called for tiles actually
            // on screen.
            if (PosterCard.Draw(
                ui,
                $"##tv{url}",
                () => ui.Art.GetUrl(channel.LogoUrl),
                pinned ? $"★ {channel.Name}" : channel.Name,
                subtitle,
                container: false,
                wide: true))
            {
                this.PlayChannel?.Invoke(channel);
            }

            // Right-click pins. A dedicated button on every tile would cost more room than a tile
            // this size has to give.
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                if (pinned)
                    ui.Config.LiveTvFavourites.Remove(url);
                else
                    ui.Config.LiveTvFavourites.Add(url);

                ui.SaveConfig();
            }

            column++;
        }

        if (shown.Count == 0)
        {
            Ui.Hint(this.pinnedOnly
                ? "Nothing pinned yet. Right-click a channel to pin it."
                : "No channels match that.");

            return;
        }

        ImGui.Spacing();
        Ui.Hint(
            "Click to watch, right-click to pin. Plenty of channels in a public list are offline or " +
            "region-locked at any moment — if one does nothing, try another rather than assuming " +
            "it is broken.");
    }
}
