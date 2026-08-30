using System.Numerics;

using Aetherstream.Plugin.UI.Tabs;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The window: a transport that is always visible, and everything else behind a tab.
/// <para>
/// It was one long scroll before, and the cost of that was not tidiness — the controls you touch
/// constantly sat in the same list as the ones you touch once, so the common case meant scrolling
/// past the rare one every time.
/// </para>
/// </summary>
internal sealed class ControlWindow : Window
{
    private readonly UiContext ui;
    private readonly WatchTab watch;
    private readonly ScreenTab screen;
    private readonly SoundTab sound;
    private readonly SetupTab setup;

    public ControlWindow(UiContext context)
        : base("Aetherstream###AetherstreamMain")
    {
        this.ui = context;
        this.NowPlaying = new NowPlayingBar(context);
        this.watch = new WatchTab(context);
        this.Library = new LibraryTab(context);
        this.screen = new ScreenTab(context);
        this.sound = new SoundTab(context);
        this.Share = new ShareTab(context);
        this.setup = new SetupTab(context);

        // Wide enough for four poster columns, which is what makes the library read as a library.
        this.Size = new Vector2(560, 620);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 380),
            MaximumSize = new Vector2(1400, 1600),
        };
    }

    internal NowPlayingBar NowPlaying { get; }

    internal LibraryTab Library { get; }

    internal ShareTab Share { get; }

    public override void Draw()
    {
        // Art.Update is deliberately NOT called here. It is driven from the plugin's own draw
        // callback instead, which ticks whether or not this window is open — and calling it from
        // both would advance the retirement countdown twice a frame.
        this.NowPlaying.Draw();

        using var tabs = ImRaii.TabBar("##tabs");
        if (!tabs)
            return;

        Tab("Watch", this.watch.Draw);
        Tab("Library", this.Library.Draw);
        Tab("Screen", this.screen.Draw);
        Tab("Sound", this.sound.Draw);
        Tab("Share", this.Share.Draw);
        Tab("Setup", this.setup.Draw);
    }

    private static void Tab(string label, Action draw)
    {
        using var tab = ImRaii.TabItem(label);
        if (!tab)
            return;

        // Each tab scrolls on its own, so a long panel never pushes the transport off the top.
        using var child = ImRaii.Child($"##body{label}", new Vector2(-1, -1), false);
        if (child)
            draw();
    }
}
