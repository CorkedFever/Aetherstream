using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The shared look. Every colour and every spacing decision lives here rather than being spelled out
/// at each call site, so the window stays coherent as panels are added to it.
/// </summary>
internal static class Ui
{
    /// <summary>Aether blue. Used for anything the eye should land on first.</summary>
    public static readonly Vector4 Accent = new(0.42f, 0.78f, 1f, 1f);

    public static readonly Vector4 AccentDim = new(0.42f, 0.78f, 1f, 0.35f);

    public static readonly Vector4 Good = new(0.45f, 0.85f, 0.55f, 1f);

    public static readonly Vector4 Warn = new(1f, 0.80f, 0.35f, 1f);

    public static readonly Vector4 Bad = new(1f, 0.45f, 0.45f, 1f);

    public static readonly Vector4 Faint = new(1f, 1f, 1f, 0.38f);

    /// <summary>A heading with a rule under it. Cheaper on vertical space than a collapsing header.</summary>
    public static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(Accent, title);
        ImGui.Separator();
        ImGui.Spacing();
    }

    /// <summary>Grey explanatory text, wrapped to the panel.</summary>
    public static void Hint(string text)
    {
        using var colour = ImRaii.PushColor(ImGuiCol.Text, Faint);
        ImGui.TextWrapped(text);
    }

    /// <summary>
    /// Attaches a tooltip to whatever was drawn immediately before it.
    /// <para>
    /// <c>AllowWhenDisabled</c> is the point: without it a greyed-out control reports itself as never
    /// hovered, so exactly the tooltips that explain *why* something is unavailable are the ones that
    /// never appear.
    /// </para>
    /// </summary>
    public static void Tip(string text)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            return;

        using var tooltip = ImRaii.Tooltip();
        using var wrap = ImRaii.TextWrapPos(ImGui.GetFontSize() * 26f);
        ImGui.TextUnformatted(text);
    }

    /// <summary>An icon button with a tooltip, since an icon alone never explains itself.</summary>
    public static bool IconButton(FontAwesomeIcon icon, string tooltip, string id, bool enabled = true)
    {
        using var disabled = ImRaii.Disabled(!enabled);
        var pressed = ImGuiComponents.IconButton(id, icon);
        Tip(tooltip);
        return pressed && enabled;
    }

    /// <summary>
    /// A filled circle in the current line, used to say what the player is doing at a glance. Drawn
    /// rather than written because a coloured word costs a whole line and reads as an error.
    /// </summary>
    public static void Dot(Vector4 colour, string tooltip)
    {
        var radius = ImGui.GetFontSize() * 0.28f;
        var size = new Vector2(radius * 2.6f, ImGui.GetTextLineHeight());
        var origin = ImGui.GetCursorScreenPos();

        ImGui.GetWindowDrawList().AddCircleFilled(
            origin + new Vector2(size.X * 0.5f, size.Y * 0.5f),
            radius,
            ImGui.ColorConvertFloat4ToU32(colour),
            16);

        ImGui.Dummy(size);
        Tip(tooltip);
    }

    /// <summary>
    /// Right-aligns the next item on the current line. Used for clocks and counts, which read far
    /// better pinned to the edge than trailing whatever sits to their left.
    /// </summary>
    public static void RightAlign(float width)
    {
        var x = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - width;
        if (x > ImGui.GetCursorPosX())
            ImGui.SetCursorPosX(x);
    }

    public static void RightAlignedText(string text, Vector4 colour)
    {
        RightAlign(ImGui.CalcTextSize(text).X);
        ImGui.TextColored(colour, text);
    }

    /// <summary>Hours are only shown once there are any, so a 20-minute video reads as 4:31.</summary>
    public static string Clock(long ms)
    {
        if (ms < 0)
            return "--:--";

        var span = TimeSpan.FromMilliseconds(ms);
        return span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }

    /// <summary>
    /// Shortens a source string to something that fits on one line and still identifies what it is.
    /// Falls back to the raw text: a truncated URL is more useful than "(unknown)".
    /// </summary>
    public static string Pretty(string source)
    {
        if (source.Length == 0)
            return "nothing";

        if (source.StartsWith("plex:", StringComparison.OrdinalIgnoreCase))
            return "Plex";

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? uri.Host[4..]
                : uri.Host;

            return host.Length > 0 ? host : source;
        }

        return source;
    }

    public static string Ellipsis(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, Math.Max(1, max - 1)), "…");
}
