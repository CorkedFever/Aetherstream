using System.Numerics;

using Aetherstream.Plugin.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The set. A screen at the top, the remote under it, a strip of inputs, and whichever input is
/// selected filling the rest.
/// <para>
/// The screen and the remote sit outside the tabs deliberately: pausing what is playing should
/// never mean navigating away from what you were doing, and the picture is the one thing worth
/// seeing from every panel.
/// </para>
/// </summary>
internal sealed class ControlWindow : Window
{
    private const float TitleBarHeight = 34f;

    private readonly UiContext ui;
    private readonly Action saveConfig;
    private readonly Remote remote;
    private readonly (string Label, Action Draw)[] inputs;
    private int input;
    private Vector2 unfoldedSize = new(560f, 720f);
    private Vector2? sizeToRestore;
    private bool loggedDrawFailure;

    public ControlWindow(UiContext context, Action saveConfig)
        : base("Aetherstream###AetherstreamMain")
    {
        this.ui = context;
        this.saveConfig = saveConfig;

        this.Dial = new ChannelDial(context);
        this.Screen = new Screen(context);
        this.remote = new Remote(context, this.Dial, this.Screen);

        var watch = new WatchTab(context);
        this.Library = new LibraryTab(context);
        this.LiveTv = new LiveTvTab(context, this.Dial);
        var screen = new ScreenTab(context);
        var sound = new SoundTab(context);
        this.Share = new ShareTab(context);
        var setup = new SetupTab(context);

        this.inputs =
        [
            ("Watch", watch.Draw),
            ("Library", this.Library.Draw),
            ("Live TV", this.LiveTv.Draw),
            ("Screen", screen.Draw),
            ("Sound", sound.Draw),
            ("Share", this.Share.Draw),
            ("Setup", setup.Draw),
        ];

        // Wide enough for four poster columns and a 16:9 picture worth looking at.
        this.Size = this.unfoldedSize;
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
    }

    internal Screen Screen { get; }

    internal ChannelDial Dial { get; }

    internal LibraryTab Library { get; }

    internal ShareTab Share { get; }

    internal LiveTvTab LiveTv { get; }

    public override void PreDraw()
    {
        var folded = this.ui.Config.WindowMinimised;

        // Folded, the window shrinks to its bar rather than leaving a dark slab under it.
        this.Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse
            | (folded ? ImGuiWindowFlags.AlwaysAutoResize : ImGuiWindowFlags.None);

        this.SizeConstraints = folded
            ? new WindowSizeConstraints { MinimumSize = new Vector2(360f, TitleBarHeight + 24f), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) }
            : new WindowSizeConstraints { MinimumSize = new Vector2(440f, 460f), MaximumSize = new Vector2(1400f, 1600f) };

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
            this.DrawFolded();
            return;
        }

        this.Screen.Draw();
        ImGui.Dummy(new Vector2(0f, 4f));
        this.remote.Draw();
        ImGui.Dummy(new Vector2(0f, 6f));
        this.DrawInputStrip();

        // Each input scrolls on its own, so a long panel never pushes the screen off the top.
        using var body = ImRaii.Child($"##body{this.input}", new Vector2(-1f, -1f), false);
        if (body)
            this.inputs[this.input].Draw();
    }

    /// <summary>
    /// The nameplate, what is on, and the fold and close buttons — drawn by hand because the whole
    /// window is drawn by hand, and Dalamud's title bar would sit on top of it like a sticker.
    /// </summary>
    private void DrawTitleBar()
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;

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

            // What is on, right-aligned against the buttons — the folded bar's whole reason to exist.
            if (this.ui.Session.IsPlaying)
            {
                var title = Ui.Ellipsis(this.Screen.Title(), 34).ToUpperInvariant();
                var titleSize = ImGui.CalcTextSize(title);
                ImGui.SetCursorScreenPos(origin + new Vector2(width - titleSize.X - buttonsWidth - 8f, (TitleBarHeight - titleSize.Y) / 2f));
                ImGui.TextColored(Theme.TextDim, title);
            }

            ImGui.SetCursorScreenPos(origin + new Vector2(width - buttonsWidth, (TitleBarHeight - buttonHeight) / 2f));
            if (ImGui.InvisibleButton("##fold", new Vector2(foldWidth, buttonHeight)))
            {
                // Folding lets the window shrink to the bar, which is the size ImGui would then
                // remember; the size before folding is kept and put back on unfolding.
                if (!this.ui.Config.WindowMinimised)
                    this.unfoldedSize = ImGui.GetWindowSize();
                else
                    this.sizeToRestore = this.unfoldedSize;

                this.ui.Config.WindowMinimised = !this.ui.Config.WindowMinimised;
                this.saveConfig();
            }

            var foldHovered = ImGui.IsItemHovered();
            if (foldHovered)
                ImGui.SetTooltip(this.ui.Config.WindowMinimised ? "Unfold" : "Fold down to the bar; the picture keeps playing");

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

    /// <summary>
    /// Folded: the LED, the state, and a thumbnail of the picture, so the window can live in a
    /// corner while the furnishing does the showing and still say at a glance that all is well.
    /// </summary>
    private void DrawFolded()
    {
        var session = this.ui.Session;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        const float Height = 20f;

        var led = session.Error is not null ? Theme.Bad
            : session.StalledAtMs > 0 ? Theme.Warn
            : session.IsPlaying ? Theme.Good
            : Theme.Edge;

        drawList.AddCircleFilled(origin + new Vector2(8f, Height / 2f), 3f, Theme.U32(led), 12);

        var state = session.Error is not null ? "NO PICTURE"
            : session.StalledAtMs > 0 ? "SIGNAL LOST"
            : session.IsPaused ? "PAUSED"
            : session.IsPlaying ? (session.DurationMs <= 0 ? "LIVE" : Ui.Clock(session.PositionMs))
            : "NO SIGNAL";

        ImGui.SetCursorScreenPos(origin + new Vector2(20f, 0f));
        Theme.Displayed(session.IsPlaying ? Theme.Accent : Theme.TextFaint, state);

        if (session.Uploader is { HasFrame: true } uploader)
        {
            var thumb = new Vector2(Height * 16f / 9f, Height);
            var at = origin + new Vector2(ImGui.GetContentRegionAvail().X - thumb.X, 0f);
            drawList.AddImage(uploader.Handle, at, at + thumb);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, Height));
    }

    /// <summary>
    /// The inputs, as the strip on the front of a set: the display face, the selected one lit with
    /// a line under it. Same seven, same order as the tabs they replace.
    /// </summary>
    private void DrawInputStrip()
    {
        var drawList = ImGui.GetWindowDrawList();

        using (Theme.PushDisplay())
        {
            for (var i = 0; i < this.inputs.Length; i++)
            {
                if (i > 0)
                    ImGui.SameLine(0f, 14f);

                var label = this.inputs[i].Label.ToUpperInvariant();
                var size = ImGui.CalcTextSize(label);
                var active = i == this.input;

                if (ImGui.InvisibleButton($"##input{i}", size + new Vector2(6f, 6f)))
                    this.input = i;

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var hovered = ImGui.IsItemHovered();

                drawList.AddText(
                    min + new Vector2(3f, 3f),
                    Theme.U32(active ? Theme.Accent : hovered ? Theme.Text : Theme.TextDim),
                    label);

                if (active)
                    drawList.AddRectFilled(new Vector2(min.X, max.Y - 1f), new Vector2(max.X, max.Y + 1f), Theme.U32(Theme.Accent));
            }
        }

        // The rule the strip sits on.
        var y = ImGui.GetItemRectMax().Y + 5f;
        var left = ImGui.GetCursorScreenPos().X;
        drawList.AddLine(new Vector2(left, y), new Vector2(left + ImGui.GetContentRegionAvail().X, y), Theme.U32(Theme.Edge), 1f);

        ImGui.Dummy(new Vector2(0f, 8f));
    }
}
