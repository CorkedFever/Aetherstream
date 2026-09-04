using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The set: a near-black console shell, a matte bezel, dark glass panels with an aether-cyan edge.
/// <para>
/// Built to sit beside Memoria on the same screen and read as its sibling — the same shell, the same
/// rule that the display face is for short labels only — without borrowing its Final Fantasy blue.
/// Aetherstream's colour is the cyan of an aetheryte, and everything that glows in this window glows
/// that colour so the eye learns one thing to look for.
/// </para>
/// <para>
/// Drawn with the draw list where it matters (the frame, the bezel, the panels) because ImGui's own
/// borders are a single hairline that vanishes against a bright backdrop, and pushed as style
/// colours everywhere else so the stock widgets inside the panels match without every call site
/// knowing about it.
/// </para>
/// </summary>
internal static class Theme
{
    // -- palette ---------------------------------------------------------------------------------

    /// <summary>The console shell the whole window is made of.</summary>
    public static readonly Vector4 Shell = Rgb(0x0B, 0x0B, 0x10);

    public static readonly Vector4 TitleBarFill = Rgb(0x15, 0x15, 0x1D);

    /// <summary>Seams and dividers in the shell.</summary>
    public static readonly Vector4 Edge = Rgb(0x2A, 0x2A, 0x34);

    /// <summary>The light catch on the shell's inner edge.</summary>
    public static readonly Vector4 FrameInner = Rgb(0x4A, 0x4A, 0x60);

    /// <summary>The bezel the picture sits in. True black: a bezel that is not black looks cheap.</summary>
    public static readonly Vector4 Bezel = Rgb(0x00, 0x00, 0x00);

    /// <summary>The dark of a switched-on tube with nothing on it.</summary>
    public static readonly Vector4 Tube = Rgb(0x0A, 0x15, 0x26);

    /// <summary>Panel fill — dark glass.</summary>
    public static readonly Vector4 Glass = Rgb(0x0A, 0x0F, 0x1A);

    /// <summary>Panel fill for the active or selected thing.</summary>
    public static readonly Vector4 GlassLit = Rgb(0x0F, 0x24, 0x38);

    /// <summary>The hairline round a panel, and the frame of every input.</summary>
    public static readonly Vector4 GlassEdge = Rgb(0x1C, 0x2A, 0x44);

    /// <summary>Aether cyan. Anything the eye should land on first.</summary>
    public static readonly Vector4 Accent = Rgb(0x6B, 0xC7, 0xFF);

    public static readonly Vector4 AccentDim = Accent with { W = 0.35f };

    public static readonly Vector4 Text = Rgb(0xE6, 0xF1, 0xFB);

    public static readonly Vector4 TextDim = Rgb(0x9F, 0xB0, 0xC8);

    public static readonly Vector4 TextFaint = Rgb(0x5F, 0x6E, 0x80);

    public static readonly Vector4 TextDisabled = Rgb(0x3C, 0x4A, 0x5C);

    public static readonly Vector4 Good = Rgb(0x5D, 0xCA, 0xA5);

    public static readonly Vector4 Warn = Rgb(0xEF, 0x9F, 0x27);

    public static readonly Vector4 Bad = Rgb(0xE2, 0x4B, 0x4A);

    public const float ShellRounding = 8f;

    private const float PanelRounding = 5f;

    // -- shell -----------------------------------------------------------------------------------

    private static int pushedColours;
    private static int pushedVars;

    /// <summary>
    /// Restyles the host window and every stock widget drawn inside it. Pushed in PreDraw and
    /// popped in PostDraw, so a panel never has to think about it.
    /// </summary>
    public static void PushShell()
    {
        Colour(ImGuiCol.WindowBg, Shell);
        Colour(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0f));
        Colour(ImGuiCol.PopupBg, Rgb(0x10, 0x14, 0x20));
        Colour(ImGuiCol.Border, GlassEdge);
        Colour(ImGuiCol.Text, Text);
        Colour(ImGuiCol.TextDisabled, TextDisabled);

        // Inputs and combos are recessed glass; buttons are raised shell.
        Colour(ImGuiCol.FrameBg, Glass);
        Colour(ImGuiCol.FrameBgHovered, GlassLit);
        Colour(ImGuiCol.FrameBgActive, GlassLit);
        Colour(ImGuiCol.Button, TitleBarFill);
        Colour(ImGuiCol.ButtonHovered, GlassLit);
        Colour(ImGuiCol.ButtonActive, Rgb(0x16, 0x34, 0x50));
        Colour(ImGuiCol.CheckMark, Accent);
        Colour(ImGuiCol.SliderGrab, Accent);
        Colour(ImGuiCol.SliderGrabActive, Accent);
        Colour(ImGuiCol.Header, GlassLit);
        Colour(ImGuiCol.HeaderHovered, GlassLit);
        Colour(ImGuiCol.HeaderActive, Rgb(0x16, 0x34, 0x50));
        Colour(ImGuiCol.Separator, Edge);
        Colour(ImGuiCol.ScrollbarBg, Shell);
        Colour(ImGuiCol.ScrollbarGrab, Edge);
        Colour(ImGuiCol.ScrollbarGrabHovered, FrameInner);
        Colour(ImGuiCol.ScrollbarGrabActive, FrameInner);
        Colour(ImGuiCol.ResizeGrip, new Vector4(0f, 0f, 0f, 0f));
        Colour(ImGuiCol.ResizeGripHovered, AccentDim);
        Colour(ImGuiCol.ResizeGripActive, Accent);

        Var(ImGuiStyleVar.WindowRounding, ShellRounding);

        // Our own frame replaces it; both at once is a muddy double edge.
        Var(ImGuiStyleVar.WindowBorderSize, 0f);
        Var(ImGuiStyleVar.FrameBorderSize, 1f);
        Var(ImGuiStyleVar.FrameRounding, 3f);
        Var(ImGuiStyleVar.ChildRounding, PanelRounding);
        Var(ImGuiStyleVar.PopupRounding, 4f);
        Var(ImGuiStyleVar.GrabRounding, 2f);
        Var(ImGuiStyleVar.WindowPadding, new Vector2(12f, 12f));
    }

    public static void PopShell()
    {
        if (pushedColours > 0)
            ImGui.PopStyleColor(pushedColours);

        if (pushedVars > 0)
            ImGui.PopStyleVar(pushedVars);

        pushedColours = 0;
        pushedVars = 0;
    }

    /// <summary>
    /// The window's own edge: a black outer line and a lighter catch inside it. Clipping is lifted
    /// because a window's draw list is clipped to its content region and the frame would otherwise
    /// be cut off at the padding.
    /// </summary>
    public static void WindowFrame()
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();

        drawList.PushClipRectFullScreen();
        drawList.AddRect(min, max, ImGui.GetColorU32(Bezel), ShellRounding, ImDrawFlags.None, 1f);
        drawList.AddRect(
            min + new Vector2(1f, 1f),
            max - new Vector2(1f, 1f),
            ImGui.GetColorU32(FrameInner),
            ShellRounding - 1f,
            ImDrawFlags.None,
            1f);
        drawList.PopClipRect();
    }

    // -- panels ----------------------------------------------------------------------------------

    /// <summary>
    /// A glass panel sized to whatever <paramref name="content"/> draws.
    /// <para>
    /// The frame has to be behind the content but its height is not known until the content has
    /// been laid out, so the draw list is split into two channels: the content goes down first, the
    /// frame is added to the channel underneath, and the two are merged.
    /// </para>
    /// </summary>
    public static void Panel(string id, Action content, float padding = 10f, bool lit = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(start + new Vector2(padding, padding));
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width - (padding * 2));
        ImGui.PushID(id);

        content();

        ImGui.PopID();
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();

        var height = ImGui.GetItemRectSize().Y + (padding * 2);
        var end = start + new Vector2(width, height);

        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(start, end, ImGui.GetColorU32(lit ? GlassLit : Glass), PanelRounding);
        drawList.AddRect(start, end, ImGui.GetColorU32(lit ? Accent : GlassEdge), PanelRounding, ImDrawFlags.None, 1f);
        drawList.ChannelsMerge();

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(width, height));
    }

    // -- type ------------------------------------------------------------------------------------

    /// <summary>The display face, once the plugin has loaded it. Null falls back to the default.</summary>
    public static DisplayFont? Display { get; set; }

    public static IDisposable PushDisplay() => Display?.Push() ?? Nothing.Instance;

    public static IDisposable PushDisplayLarge() => Display?.PushLarge() ?? Nothing.Instance;

    /// <summary>
    /// A section heading: the display face in cyan, upper-cased, with a hairline under it. Upper
    /// case because VT323 was drawn for a terminal that had nothing else, and its capitals are where
    /// the character lives.
    /// </summary>
    public static void Heading(string text)
    {
        ImGui.Spacing();

        using (PushDisplay())
            ImGui.TextColored(Accent, text.ToUpperInvariant());

        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var y = ImGui.GetItemRectMax().Y + 2f;
        var right = min.X + ImGui.GetContentRegionAvail().X;

        drawList.AddLine(new Vector2(min.X, y), new Vector2(right, y), ImGui.GetColorU32(GlassEdge), 1f);

        ImGui.Dummy(new Vector2(0f, 6f));
    }

    /// <summary>A short value in the display face — a number, a code, a state. Never a path.</summary>
    public static void Displayed(Vector4 colour, string text)
    {
        using (PushDisplay())
            ImGui.TextColored(colour, text);
    }

    /// <summary>Opaque colour at a different alpha, for things meant to sit quietly on the glass.</summary>
    public static Vector4 WithAlpha(Vector4 colour, float alpha) => colour with { W = alpha };

    public static uint U32(Vector4 colour) => ImGui.ColorConvertFloat4ToU32(colour);

    private static void Colour(ImGuiCol target, Vector4 value)
    {
        ImGui.PushStyleColor(target, value);
        pushedColours++;
    }

    private static void Var(ImGuiStyleVar target, float value)
    {
        ImGui.PushStyleVar(target, value);
        pushedVars++;
    }

    private static void Var(ImGuiStyleVar target, Vector2 value)
    {
        ImGui.PushStyleVar(target, value);
        pushedVars++;
    }

    private static Vector4 Rgb(byte r, byte g, byte b) => new(r / 255f, g / 255f, b / 255f, 1f);

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();

        public void Dispose()
        {
        }
    }
}
