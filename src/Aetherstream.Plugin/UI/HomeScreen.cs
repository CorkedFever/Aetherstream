using System.Numerics;

using Aetherstream.Plugin.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The set's home screen, laid out the way a tablet lays out its own: one line saying what is
/// on, a grid of app icons with their names beneath, and a dock along the bottom for the things
/// that are not sources. Each app is one of the panels that used to be a tab or a shelf; opening
/// one takes the whole screen with a Home button in the corner, and the grid shows only the apps
/// that have not been put away under Setup. Adding a source means adding an icon here and
/// nothing else.
/// </summary>
internal sealed class HomeScreen
{
    internal readonly record struct App(string Key, string Name, FontAwesomeIcon Icon, Vector4 Colour, Func<string> Status, Action Draw, Action<string>? SearchFor = null);

    private const float IconSize = 52f;
    private const float CellWidth = 96f;
    private const float DockHeight = 82f;

    private static readonly Vector4 Purple = new(0.61f, 0.50f, 0.87f, 1f);
    private static readonly Vector4 Plain = Theme.TextDim;

    private readonly UiContext ui;
    private readonly Screen screen;
    private readonly List<App> apps;
    private readonly List<App> dock;
    private string? open;
    private string search = string.Empty;

    public HomeScreen(UiContext ui, Screen screen, ChannelDial dial, WatchTab watch, LibraryTab library, LiveTvTab liveTv, ChannelsTab channels, SoundTab sound, ShareTab share, MirrorTab mirror, SetupTab setupTab)
    {
        this.ui = ui;
        this.screen = screen;
        var config = ui.Config;

        this.apps =
        [
            new("channels", "Aetherstream", FontAwesomeIcon.Tv, Theme.Accent, () => $"{ui.Channels.Count} drawn channels", channels.Draw),
            new("livetv", "Live TV", FontAwesomeIcon.BroadcastTower, Theme.Accent, () => dial.HasChannels ? "guide and dial" : "no lineup yet", liveTv.Draw),
            new("mirror", "Mirror", FontAwesomeIcon.Desktop, Theme.Accent, () => mirror.State?.Invoke().Status ?? "another window, on the set", mirror.Draw),
            new("plex", "Plex", FontAwesomeIcon.Server, Theme.Warn, () => config.PlexServer.Length > 0 && config.PlexToken.Length > 0 ? "connected" : "not signed in", () => library.DrawShelf(0)),
            new("server", "Media server", FontAwesomeIcon.Database, Purple, () => config.MediaServerToken.Length > 0 ? $"{config.MediaServerKind}, as {config.MediaServerUserName}" : "Jellyfin or Emby", () => library.DrawShelf(1), library.MediaServer.SearchFor),
            new("local", "Local videos", FontAwesomeIcon.Folder, Plain, () => config.LocalVideoFolders.Count == 1 ? "1 folder" : $"{config.LocalVideoFolders.Count} folders", () => library.DrawShelf(11)),
            new("youtube", "YouTube", FontAwesomeIcon.Video, Theme.Bad, () => "your feed, lists, search", () => library.DrawShelf(2), library.YouTube.SearchFor),
            new("twitch", "Twitch", FontAwesomeIcon.Gamepad, Purple, () => "followed, top, DJs", () => library.DrawShelf(3), library.Twitch.SearchFor),
            new("dailymotion", "Dailymotion", FontAwesomeIcon.PhotoVideo, Theme.Accent, () => "trending and channels", () => library.DrawShelf(4), library.Dailymotion.SearchFor),
            new("pluto", "Pluto TV", FontAwesomeIcon.Film, Theme.Warn, () => "free films and series", () => library.DrawShelf(5), library.Pluto.SearchFor),
            new("redbull", "Red Bull TV", FontAwesomeIcon.Trophy, Theme.Bad, () => "films and documentaries", () => library.DrawShelf(6), library.RedBull.SearchFor),
            new("pbs", "PBS", FontAwesomeIcon.Newspaper, Theme.Accent, () => "NOVA, Nature, FRONTLINE", () => library.DrawShelf(7)),
            new("nasa", "NASA+", FontAwesomeIcon.Rocket, Theme.Accent, () => "documentaries and series", () => library.DrawShelf(8), library.Nasa.SearchFor),
            new("ted", "TED", FontAwesomeIcon.Microphone, Theme.Bad, () => "talks and playlists", () => library.DrawShelf(9), library.Ted.SearchFor),
            new("archive", "Archive", FontAwesomeIcon.Archive, Plain, () => "public domain films", () => library.DrawShelf(10), library.Archive.SearchFor),
            new("southpark", "South Park", FontAwesomeIcon.Snowflake, Theme.Accent, () => library.SouthPark.Summary, () => library.DrawShelf(12)),
            new("adultswim", "Adult Swim", FontAwesomeIcon.Swimmer, Theme.Text, () => library.AdultSwim.Summary, () => library.DrawShelf(13)),
            new("music", "Music", FontAwesomeIcon.Music, Theme.Good, () => MusicStatus(config), () => sound.DrawPart(0)),
            new("rolls", "Orchestrion", FontAwesomeIcon.CompactDisc, Theme.Good, () => "your rolls", () => sound.DrawPart(1)),
            new("radio", "Radio", FontAwesomeIcon.Headphones, Theme.Good, () => "internet radio", () => sound.DrawPart(2)),
            new("podcasts", "Podcasts", FontAwesomeIcon.Podcast, Theme.Good, () => "shows and episodes", () => sound.DrawPart(3)),
        ];

        this.dock =
        [
            new("search", "Search", FontAwesomeIcon.Search, Theme.Accent, () => "type, then pick where", this.DrawSearch),
            new("link", "Paste a link", FontAwesomeIcon.Link, Theme.Accent, () => "any address, and history", watch.Draw),
            new("party", "Watch party", FontAwesomeIcon.Users, Theme.Accent, () => "share or join", share.Draw),
            new("setup", "Setup", FontAwesomeIcon.Cog, Plain, () => string.Empty, setupTab.Draw),
        ];
    }

    /// <summary>Every app that can be shown or put away, for Setup's tick boxes. The dock stays.</summary>
    public IReadOnlyList<(string Key, string Name)> Apps => this.apps.Select(a => (a.Key, a.Name)).ToList();

    /// <summary>What is open, or null for the grid.</summary>
    public string? Open => this.open;

    public void GoHome() => this.open = null;

    public void OpenApp(string key) => this.open = key;

    /// <summary>A name for the title bar: the open app's, or nothing on the grid.</summary>
    public string Title() => this.open is { } key ? this.Find(key)?.Name ?? string.Empty : string.Empty;

    public void Draw()
    {
        if (this.open is { } key)
        {
            if (this.Find(key) is not { } app)
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

        this.DrawNowOn();

        using (var grid = ImRaii.Child("##grid", new Vector2(-1f, -DockHeight), false))
        {
            if (grid)
                this.DrawGrid(this.apps.Where(a => !this.ui.Config.HiddenApps.Contains(a.Key)).ToList(), "grid");
        }

        this.DrawDock();
    }

    private App? Find(string key) =>
        this.apps.Concat(this.dock).Where(a => a.Key == key).Select(a => (App?)a).FirstOrDefault();

    /// <summary>The way back, the app's name, and its status, on one line above it.</summary>
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

        var status = app.Status();
        if (status.Length > 0)
        {
            ImGui.SameLine(0f, 10f);
            ImGui.TextColored(Theme.TextFaint, status);
        }

        var drawList = ImGui.GetWindowDrawList();
        var y = ImGui.GetItemRectMax().Y + 5f;
        var left = ImGui.GetCursorScreenPos().X;
        drawList.AddLine(new Vector2(left, y), new Vector2(left + ImGui.GetContentRegionAvail().X, y), Theme.U32(Theme.Edge), 1f);
        ImGui.Dummy(new Vector2(0f, 8f));
    }

    /// <summary>One line: what is on and how far in, or what was on last and a way to bring it back.</summary>
    private void DrawNowOn()
    {
        var session = this.ui.Session;
        var onAir = session.IsPlaying || session.Channel is not null;

        ImGui.AlignTextToFramePadding();
        using (Theme.PushDisplay())
            ImGui.TextColored(onAir ? Theme.Accent : Theme.TextFaint, onAir ? "NOW ON" : "NOTHING ON");

        ImGui.SameLine(0f, 12f);

        if (onAir)
        {
            var title = session.Channel is not null && !session.IsPlaying
                ? this.ui.Channels.FirstOrDefault(c => ReferenceEquals(c.Channel, session.Channel)).Name ?? "a drawn channel"
                : this.screen.Title();
            var detail = session.IsPlaying
                ? session.DurationMs > 0 ? $"  ·  {Ui.Clock(session.PositionMs)} of {Ui.Clock(session.DurationMs)}" : "  ·  live"
                : string.Empty;
            if (this.ui.NextUp.Count > 0)
                detail += $"  ·  next: {this.ui.NextUp[0].Label}";
            ImGui.TextColored(Theme.TextDim, Ui.Fit(title + detail, ImGui.GetContentRegionAvail().X));
        }
        else if (this.ui.Config.Recents.Count > 0)
        {
            var last = this.ui.Config.Recents[0];
            var buttonWidth = ImGui.CalcTextSize("Play again").X + 16f;
            ImGui.TextColored(Theme.TextDim, Ui.Fit("Last: " + last.Label, ImGui.GetContentRegionAvail().X - buttonWidth - 12f));
            ImGui.SameLine(0f, 10f);
            if (ImGui.SmallButton("Play again"))
                this.ui.PlayAndRemember(last.Source, last.Label, last.Thumb);
        }
        else
        {
            ImGui.TextColored(Theme.TextDim, "Pick an app, or dial a channel on the remote.");
        }

        ImGui.Dummy(new Vector2(0f, 6f));
    }

    /// <summary>Icons with names under them, the width deciding how many to a row.</summary>
    private void DrawGrid(List<App> shown, string id)
    {
        if (shown.Count == 0)
        {
            Ui.Hint("Every app is put away. Setup, System brings them back.");
            return;
        }

        var available = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max(1, (int)(available / CellWidth));
        var cell = available / columns;

        for (var i = 0; i < shown.Count; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine(0f, 0f);

            this.DrawIcon(shown[i], cell, IconSize, id);
        }
    }

    /// <summary>One app: a rounded square in its colour with the icon in it, the name below, and the status as a tooltip.</summary>
    private void DrawIcon(App app, float cellWidth, float iconSize, string id)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var height = iconSize + 6f + ImGui.GetTextLineHeight() + 10f;

        if (ImGui.InvisibleButton($"##{id}{app.Key}", new Vector2(cellWidth, height)))
        {
            if (this.open == "search" && app.SearchFor is { } into && this.search.Trim().Length > 0)
                into(this.search.Trim());
            this.open = app.Key;
        }

        var hovered = ImGui.IsItemHovered();
        var status = app.Status();
        if (hovered && status.Length > 0)
            ImGui.SetTooltip(status);

        var p0 = origin + new Vector2((cellWidth - iconSize) / 2f, 4f);
        var p1 = p0 + new Vector2(iconSize, iconSize);
        if (AppLogos.Has(app.Key, this.ui.Config.MediaServerKind))
        {
            AppLogos.Draw(drawList, app.Key, p0, iconSize, this.ui.Config.MediaServerKind, hovered);
        }
        else
        {
            var plain = app.Colour == Plain;
            var tint = plain ? Theme.Glass : app.Colour with { W = hovered ? 0.32f : 0.18f };
            drawList.AddRectFilled(p0, p1, Theme.U32(tint), iconSize * 0.27f);
            drawList.AddRect(p0, p1, Theme.U32(plain ? Theme.GlassEdge : app.Colour with { W = hovered ? 0.9f : 0.45f }), iconSize * 0.27f);

            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var icon = app.Icon.ToIconString();
                var size = ImGui.CalcTextSize(icon);
                drawList.AddText(p0 + ((new Vector2(iconSize) - size) / 2f), Theme.U32(plain ? Theme.TextDim : app.Colour), icon);
            }
        }

        var label = Ui.Fit(app.Name, cellWidth - 8f);
        var labelSize = ImGui.CalcTextSize(label);
        drawList.AddText(origin + new Vector2((cellWidth - labelSize.X) / 2f, iconSize + 8f), Theme.U32(hovered ? Theme.Text : Theme.TextDim), label);
    }

    /// <summary>The dock: search, a pasted link, the party, and setup, on a shelf of their own at the foot.</summary>
    private void DrawDock()
    {
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var end = start + new Vector2(width, DockHeight - 4f);
        drawList.AddRectFilled(start, end, Theme.U32(Theme.Glass), 14f);
        drawList.AddRect(start, end, Theme.U32(Theme.GlassEdge), 14f);

        var cell = Math.Min(140f, (width - 24f) / this.dock.Count);
        var inset = (width - (cell * this.dock.Count)) / 2f;
        ImGui.SetCursorScreenPos(start + new Vector2(inset, 6f));
        for (var i = 0; i < this.dock.Count; i++)
        {
            if (i > 0)
                ImGui.SameLine(0f, 0f);
            this.DrawIcon(this.dock[i], cell, 44f, "dock");
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, DockHeight - 4f));
    }

    /// <summary>Search: a box, then the apps that can be searched. Type, then pick where.</summary>
    private void DrawSearch()
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##everysearch", "What are you looking for?", ref this.search, 120);
        Ui.Hint(this.search.Trim().Length > 0
            ? "Now pick where to look. The app opens with the search run."
            : "Type something, then pick an app to search it in. Each app searches its own service; there is no one index of them all.");
        ImGui.Dummy(new Vector2(0f, 6f));

        var searchable = this.apps.Where(a => a.SearchFor is not null && !this.ui.Config.HiddenApps.Contains(a.Key)).ToList();
        this.DrawGrid(searchable, "find");
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
