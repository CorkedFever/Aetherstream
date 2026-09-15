using System.Numerics;

using Aetherstream.Plugin.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The set's home screen, as a smart television has one: what is on, and a grid of apps. Each
/// app is one of the panels that used to be a tab or a shelf; opening one takes the whole screen
/// with a Home button in the corner, and the grid shows only the apps that have not been put
/// away under Setup. Adding a source means adding a tile here and nothing else.
/// </summary>
internal sealed class HomeScreen
{
    internal readonly record struct App(string Key, string Name, FontAwesomeIcon Icon, Func<string> Status, Action Draw);

    private const float TileWidth = 150f;
    private const float TileHeight = 62f;

    private readonly UiContext ui;
    private readonly Screen screen;
    private readonly List<App> apps;
    private readonly App setup;
    private string? open;

    public HomeScreen(UiContext ui, Screen screen, ChannelDial dial, WatchTab watch, LibraryTab library, LiveTvTab liveTv, ChannelsTab channels, SoundTab sound, ShareTab share, SetupTab setupTab)
    {
        this.ui = ui;
        this.screen = screen;
        var config = ui.Config;

        this.apps =
        [
            new("channels", "Aetherstream", FontAwesomeIcon.Tv, () => $"{ui.Channels.Count} drawn channels", channels.Draw),
            new("livetv", "Live TV", FontAwesomeIcon.BroadcastTower, () => dial.HasChannels ? "guide and dial" : "no lineup yet", liveTv.Draw),
            new("plex", "Plex", FontAwesomeIcon.Server, () => config.PlexServer.Length > 0 && config.PlexToken.Length > 0 ? "connected" : "not signed in", () => library.DrawShelf(0)),
            new("server", "Media server", FontAwesomeIcon.Database, () => config.MediaServerToken.Length > 0 ? $"{config.MediaServerKind}, as {config.MediaServerUserName}" : "Jellyfin or Emby", () => library.DrawShelf(1)),
            new("local", "Local videos", FontAwesomeIcon.Folder, () => config.LocalVideoFolders.Count == 1 ? "1 folder" : $"{config.LocalVideoFolders.Count} folders", () => library.DrawShelf(11)),
            new("youtube", "YouTube", FontAwesomeIcon.Video, () => "your feed, lists, search", () => library.DrawShelf(2)),
            new("twitch", "Twitch", FontAwesomeIcon.Gamepad, () => "followed, top, DJs", () => library.DrawShelf(3)),
            new("dailymotion", "Dailymotion", FontAwesomeIcon.PhotoVideo, () => "trending and channels", () => library.DrawShelf(4)),
            new("pluto", "Pluto TV", FontAwesomeIcon.Film, () => "free films and series", () => library.DrawShelf(5)),
            new("redbull", "Red Bull TV", FontAwesomeIcon.Trophy, () => "films and documentaries", () => library.DrawShelf(6)),
            new("pbs", "PBS", FontAwesomeIcon.Newspaper, () => "NOVA, Nature, FRONTLINE", () => library.DrawShelf(7)),
            new("nasa", "NASA+", FontAwesomeIcon.Rocket, () => "documentaries and series", () => library.DrawShelf(8)),
            new("ted", "TED", FontAwesomeIcon.Microphone, () => "talks and playlists", () => library.DrawShelf(9)),
            new("archive", "Internet Archive", FontAwesomeIcon.Archive, () => "public domain films", () => library.DrawShelf(10)),
            new("music", "Music", FontAwesomeIcon.Music, () => MusicStatus(config), () => sound.DrawPart(0)),
            new("rolls", "Orchestrion", FontAwesomeIcon.CompactDisc, () => "your rolls", () => sound.DrawPart(1)),
            new("radio", "Radio", FontAwesomeIcon.Headphones, () => "internet radio", () => sound.DrawPart(2)),
            new("podcasts", "Podcasts", FontAwesomeIcon.Podcast, () => "shows and episodes", () => sound.DrawPart(3)),
            new("party", "Watch party", FontAwesomeIcon.Users, () => "share or join", share.Draw),
            new("link", "Paste a link", FontAwesomeIcon.Link, () => "any address, and history", watch.Draw),
        ];

        this.setup = new App("setup", "Setup", FontAwesomeIcon.Cog, () => string.Empty, setupTab.Draw);
    }

    /// <summary>Every app that can be shown or put away, for Setup's tick boxes.</summary>
    public IReadOnlyList<(string Key, string Name)> Apps => this.apps.Select(a => (a.Key, a.Name)).ToList();

    /// <summary>What is open, or null for the grid.</summary>
    public string? Open => this.open;

    public void GoHome() => this.open = null;

    public void OpenApp(string key) => this.open = key;

    /// <summary>A name for the display strip: the open app's, or nothing on the grid.</summary>
    public string Title() => this.open is { } key ? (key == "setup" ? this.setup : this.apps.FirstOrDefault(a => a.Key == key)).Name ?? string.Empty : string.Empty;

    public void Draw()
    {
        if (this.open is { } key)
        {
            var app = key == "setup" ? this.setup : this.apps.FirstOrDefault(a => a.Key == key);
            if (app.Draw is null)
            {
                this.open = null;
                return;
            }

            this.DrawAppHeader(app);
            using var body = ImRaii.Child($"##app{key}", new Vector2(-1f, -1f), false);
            if (body)
                app.Draw();
            return;
        }

        using var home = ImRaii.Child("##home", new Vector2(-1f, -1f), false);
        if (!home)
            return;

        this.DrawNowPlaying();
        this.DrawGrid();
    }

    /// <summary>The way back, and the app's name, on one line above it.</summary>
    private void DrawAppHeader(App app)
    {
        using (Theme.PushDisplay())
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextDim))
            {
                if (ImGui.SmallButton("< HOME"))
                    this.open = null;
            }

            Ui.Tip("Back to the apps");
            ImGui.SameLine(0f, 12f);
            ImGui.TextColored(Theme.Accent, app.Name.ToUpperInvariant());
        }

        var drawList = ImGui.GetWindowDrawList();
        var y = ImGui.GetItemRectMax().Y + 5f;
        var left = ImGui.GetCursorScreenPos().X;
        drawList.AddLine(new Vector2(left, y), new Vector2(left + ImGui.GetContentRegionAvail().X, y), Theme.U32(Theme.Edge), 1f);
        ImGui.Dummy(new Vector2(0f, 8f));
    }

    /// <summary>What is on, what is next, and the way to what was on before, above the grid.</summary>
    private void DrawNowPlaying()
    {
        var session = this.ui.Session;
        var onAir = session.IsPlaying || session.Channel is not null;

        Theme.Panel("##nowplaying", () =>
        {
            using (Theme.PushDisplay())
                ImGui.TextColored(onAir ? Theme.Accent : Theme.TextFaint, onAir ? "NOW ON" : "NOTHING ON");

            if (onAir)
            {
                var title = session.Channel is not null && !session.IsPlaying
                    ? this.ui.Channels.FirstOrDefault(c => ReferenceEquals(c.Channel, session.Channel)).Name ?? "a drawn channel"
                    : this.screen.Title();
                ImGui.TextUnformatted(Ui.Ellipsis(title, 60));

                var detail = session.IsPlaying
                    ? session.DurationMs > 0 ? $"{Ui.Clock(session.PositionMs)} of {Ui.Clock(session.DurationMs)}" : "live"
                    : "drawn by the set";
                if (this.ui.NextUp.Count > 0)
                    detail += $"  ·  next: {Ui.Ellipsis(this.ui.NextUp[0].Label, 40)}";
                ImGui.TextColored(Theme.TextDim, detail);
            }
            else if (this.ui.Config.Recents.Count > 0)
            {
                var last = this.ui.Config.Recents[0];
                ImGui.TextColored(Theme.TextDim, $"Last on: {Ui.Ellipsis(last.Label, 50)}");
                ImGui.SameLine();
                if (ImGui.SmallButton("Play it again"))
                    this.ui.PlayAndRemember(last.Source, last.Label, last.Thumb);
            }
            else
            {
                ImGui.TextColored(Theme.TextDim, "Pick an app, or dial a channel on the remote.");
            }
        }, lit: onAir);

        ImGui.Dummy(new Vector2(0f, 6f));
    }

    private void DrawGrid()
    {
        var hidden = this.ui.Config.HiddenApps;
        var shown = this.apps.Where(a => !hidden.Contains(a.Key)).ToList();
        if (shown.Count == 0)
        {
            Ui.Hint("Every app is put away. Setup, System brings them back.");
            return;
        }

        var spacing = 8f;
        var columns = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + spacing) / (TileWidth + spacing)));
        var width = (ImGui.GetContentRegionAvail().X - ((columns - 1) * spacing)) / columns;
        var tile = new Vector2(width, TileHeight);
        var drawList = ImGui.GetWindowDrawList();

        for (var i = 0; i < shown.Count; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine(0f, spacing);

            var app = shown[i];
            var origin = ImGui.GetCursorScreenPos();

            using var colours = ImRaii.PushColor(ImGuiCol.Button, Theme.Glass)
                .Push(ImGuiCol.ButtonHovered, Theme.GlassLit)
                .Push(ImGuiCol.ButtonActive, Theme.GlassLit)
                .Push(ImGuiCol.Border, Theme.GlassEdge);
            using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
            using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 8f);

            if (ImGui.Button($"##app{app.Key}", tile))
                this.open = app.Key;

            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var icon = app.Icon.ToIconString();
                var size = ImGui.CalcTextSize(icon);
                drawList.AddText(origin + new Vector2(14f, (tile.Y - size.Y) / 2f), Theme.U32(Theme.Accent), icon);
            }

            using (Theme.PushDisplay())
                drawList.AddText(origin + new Vector2(44f, 12f), Theme.U32(Theme.Text), app.Name.ToUpperInvariant());

            var status = app.Status();
            if (status.Length > 0)
                drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), origin + new Vector2(44f, 34f), Theme.U32(Theme.TextDim), Ui.Ellipsis(status, 28), tile.X - 52f);
        }
    }

    private static string MusicStatus(Configuration config) =>
        !config.ChannelMusic ? "off"
        : config.ChannelMusicSource switch
        {
            "folder" => "your folder",
            "plex" => "a Plex playlist",
            "radio" => "a radio station",
            "podcast" => "a podcast",
            "rolls" => "orchestrion rolls",
            _ => "the bundled music",
        };
}
