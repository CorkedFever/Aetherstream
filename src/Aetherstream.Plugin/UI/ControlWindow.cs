using System.Numerics;

using Aetherstream.Plugin.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The set: a remote in the left hand and the television beside it. The remote is always
/// there; the screen and the home screen of apps appear to its right when the window is
/// unfolded, and folding puts them away and leaves the remote alone in a corner of the game.
/// <para>
/// The remote sits outside the apps deliberately: pausing what is playing, or dialling a channel,
/// should never mean navigating away from whatever you were looking at.
/// </para>
/// </summary>
internal sealed class ControlWindow : Window
{
    private const float TitleBarHeight = 34f;
    private const float ColumnGap = 12f;

    private readonly UiContext ui;
    private readonly Action saveConfig;
    private readonly RemoteWidget remote;
    private Vector2 unfoldedSize = new(820f, 720f);

    /// <summary>The folded window's content width. Fixed, or auto-resize would keep whatever width the window had.</summary>
    private const float FoldedWidth = RemoteWidget.BodyWidth;
    private Vector2? sizeToRestore;
    private bool loggedDrawFailure;

    public ControlWindow(UiContext context, Action saveConfig)
        : base("Aetherstream###AetherstreamMain")
    {
        this.ui = context;
        this.saveConfig = saveConfig;

        this.Dial = new ChannelDial(context);
        this.Screen = new Screen(context);
        this.remote = new RemoteWidget(context, this.Dial, this.Screen) { Home = this.GoHome, OpenSetup = () => this.OpenApp("setup") };

        var watch = new WatchTab(context);
        this.Library = new LibraryTab(context);
        this.LiveTv = new LiveTvTab(context, this.Dial);
        this.Dial.Lineup = () => this.LiveTv.Lineup(120);
        var screen = new ScreenTab(context);
        this.Sound = new SoundTab(context);
        this.Channels = new ChannelsTab(context);
        this.Share = new ShareTab(context);
        this.Mirror = new MirrorTab(context);
        this.Setup = new SetupTab(context) { LineupChoices = () => this.LiveTv.Choices, DrawScreen = screen.Draw, DrawSound = this.Sound.DrawOutput };
        this.Home = new HomeScreen(context, this.Screen, this.Dial, watch, this.Library, this.LiveTv, this.Channels, this.Sound, this.Share, this.Mirror, this.Setup);
        this.Setup.Apps = () => this.Home.Apps;

        // Wide enough for the remote, four poster columns and a 16:9 picture worth looking at.
        this.Size = this.unfoldedSize;
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    internal Screen Screen { get; }

    internal ChannelDial Dial { get; }

    internal HomeScreen Home { get; }

    internal LibraryTab Library { get; }

    internal ShareTab Share { get; }

    internal MirrorTab Mirror { get; }

    internal LiveTvTab LiveTv { get; }

    internal SoundTab Sound { get; }

    internal ChannelsTab Channels { get; }

    internal SetupTab Setup { get; }

    /// <summary>Shows an app on the home screen, unfolding the window if it was folded.</summary>
    internal void OpenApp(string key)
    {
        this.Home.OpenApp(key);
        if (this.ui.Config.WindowMinimised)
            this.ToggleFold();
        this.IsOpen = true;
    }

    private void GoHome()
    {
        this.Home.GoHome();
        if (this.ui.Config.WindowMinimised)
            this.ToggleFold();
    }

    public override void PreDraw()
    {
        var folded = this.ui.Config.WindowMinimised;

        // Folded, the window shrinks to the remote rather than leaving a dark slab beside it.
        this.Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
            | (folded ? ImGuiWindowFlags.AlwaysAutoResize : ImGuiWindowFlags.None);

        this.SizeConstraints = folded
            ? new WindowSizeConstraints { MinimumSize = new Vector2(FoldedWidth + 16f, TitleBarHeight + 24f), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) }
            : new WindowSizeConstraints { MinimumSize = new Vector2(FoldedWidth + ColumnGap + 460f, 560f), MaximumSize = new Vector2(1600f, 1600f) };

        if (this.sizeToRestore is { } restore)
        {
            this.Size = restore;
            this.SizeCondition = ImGuiCond.Always;
            this.sizeToRestore = null;
        }
        else
        {
            this.SizeCondition = ImGuiCond.FirstUseEver;
        }

        Theme.PushShell();
    }

    public override void PostDraw() => Theme.PopShell();

    /// <summary>
    /// The style pushed in PreDraw is popped in PostDraw, so an exception escaping here would leave
    /// the stack unbalanced and restyle every other plugin's window for the rest of the frame.
    /// Nothing in a draw call is worth that.
    /// </summary>
    public override void Draw()
    {
        try
        {
            this.DrawContents();
        }
        catch (Exception ex)
        {
            if (!this.loggedDrawFailure)
            {
                this.loggedDrawFailure = true;
                this.ui.Log.Error(ex, "Aetherstream window draw failed; suppressing further reports.");
            }
        }
    }

    private void DrawContents()
    {
        // Art.Update is deliberately NOT called here. It is driven from the plugin's own draw
        // callback instead, which ticks whether or not this window is open — and calling it from
        // both would advance the retirement countdown twice a frame.
        this.Dial.Track();

        Theme.WindowFrame();
        this.DrawTitleBar();

        if (this.ui.Config.WindowMinimised)
        {
            this.remote.Draw();
            return;
        }

        // The remote in its own column, so the television beside it scrolls without moving it.
        using (var column = ImRaii.Child("##remote", new Vector2(FoldedWidth, -1f), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (column)
                this.remote.Draw(stretch: true);
        }

        ImGui.SameLine(0f, ColumnGap);

        using var set = ImRaii.Child("##set", new Vector2(-1f, -1f), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!set)
            return;

        this.Screen.Draw();
        ImGui.Dummy(new Vector2(0f, 6f));
        this.Home.Draw();
    }

    /// <summary>
    /// The nameplate, what is open, and the fold and close buttons — drawn by hand because the
    /// whole window is drawn by hand, and Dalamud's title bar would sit on top of it like a sticker.
    /// </summary>
    private void DrawTitleBar()
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = this.ui.Config.WindowMinimised ? FoldedWidth : ImGui.GetContentRegionAvail().X;

        drawList.AddRectFilled(origin, origin + new Vector2(width, TitleBarHeight), Theme.U32(Theme.TitleBarFill), 6f);

        using (Theme.PushDisplay())
        {
            const string Caption = "AETHERSTREAM";

            var captionSize = ImGui.CalcTextSize(Caption);
            var closeWidth = ImGui.CalcTextSize("×").X + 16f;
            var foldWidth = ImGui.CalcTextSize("_").X + 16f;
            var buttonsWidth = closeWidth + foldWidth;
            var buttonHeight = Math.Max(captionSize.Y, 16f);

            // The drag area stops short of the buttons: ImGui gives a click to whichever item
            // claimed the spot first, so a bar that spanned the whole width would make the close
            // button impossible to press.
            ImGui.InvisibleButton("##titlebar", new Vector2(Math.Max(1f, width - buttonsWidth), TitleBarHeight));
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);

            ImGui.SetCursorScreenPos(origin + new Vector2(12f, (TitleBarHeight - captionSize.Y) / 2f));
            ImGui.TextColored(Theme.Text, Caption);

            // What is open, right-aligned against the buttons: the app, or what is on from the grid.
            if (!this.ui.Config.WindowMinimised)
            {
                var title = this.Home.Title();
                if (title.Length == 0 && this.ui.Session.IsPlaying)
                    title = this.Screen.Title();
                if (title.Length > 0)
                {
                    title = Ui.Ellipsis(title, 34).ToUpperInvariant();
                    var titleSize = ImGui.CalcTextSize(title);
                    ImGui.SetCursorScreenPos(origin + new Vector2(width - titleSize.X - buttonsWidth - 8f, (TitleBarHeight - titleSize.Y) / 2f));
                    ImGui.TextColored(Theme.TextDim, title);
                }
            }

            ImGui.SetCursorScreenPos(origin + new Vector2(width - buttonsWidth, (TitleBarHeight - buttonHeight) / 2f));
            if (ImGui.InvisibleButton("##fold", new Vector2(foldWidth, buttonHeight)))
                this.ToggleFold();

            var foldHovered = ImGui.IsItemHovered();
            if (foldHovered)
                ImGui.SetTooltip(this.ui.Config.WindowMinimised ? "Unfold the television" : "Fold down to the remote; the picture keeps playing");

            ImGui.SetCursorScreenPos(origin + new Vector2(width - buttonsWidth + 6f, (TitleBarHeight - captionSize.Y) / 2f - 4f));
            ImGui.TextColored(foldHovered ? Theme.Text : Theme.TextFaint, this.ui.Config.WindowMinimised ? "^" : "_");

            ImGui.SetCursorScreenPos(origin + new Vector2(width - closeWidth, (TitleBarHeight - buttonHeight) / 2f));
            if (ImGui.InvisibleButton("##close", new Vector2(closeWidth, buttonHeight)))
                this.IsOpen = false;

            var closeHovered = ImGui.IsItemHovered();
            ImGui.SetCursorScreenPos(origin + new Vector2(width - closeWidth + 6f, (TitleBarHeight - captionSize.Y) / 2f));
            ImGui.TextColored(closeHovered ? Theme.Bad : Theme.TextFaint, "×");
        }

        ImGui.SetCursorScreenPos(origin + new Vector2(0f, TitleBarHeight + 8f));
    }

    private void ToggleFold()
    {
        // Folding lets the window shrink to the remote, which is the size ImGui would then
        // remember; the size before folding is kept and put back on unfolding.
        if (!this.ui.Config.WindowMinimised)
            this.unfoldedSize = ImGui.GetWindowSize();
        else
            this.sizeToRestore = this.unfoldedSize;

        this.ui.Config.WindowMinimised = !this.ui.Config.WindowMinimised;
        this.saveConfig();
    }
}
