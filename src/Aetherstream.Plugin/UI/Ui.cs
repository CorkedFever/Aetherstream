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
    // The palette lives in Theme; these names stay so every panel written against them keeps working.

    public static readonly Vector4 Accent = Theme.Accent;

    public static readonly Vector4 AccentDim = Theme.AccentDim;

    public static readonly Vector4 Good = Theme.Good;

    public static readonly Vector4 Warn = Theme.Warn;

    public static readonly Vector4 Bad = Theme.Bad;

    public static readonly Vector4 Faint = Theme.TextDim;

    /// <summary>A strip of parts, drawn the way the input strip is: the display face, the chosen one underlined.</summary>
    public static void Strip(string id, string[] labels, ref int selected)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        using (Theme.PushDisplay())
        {
            for (var i = 0; i < labels.Length; i++)
            {
                var size = ImGui.CalcTextSize(labels[i]);

                // On one line while they fit; a strip that outgrows the panel continues below
                // rather than running off its edge.
                if (i > 0 && ImGui.GetItemRectMax().X + 14f + size.X + 6f <= rightEdge)
                    ImGui.SameLine(0f, 14f);
                var active = i == selected;
                if (ImGui.InvisibleButton($"##{id}{i}", size + new Vector2(6f, 6f)))
                    selected = i;
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var hovered = ImGui.IsItemHovered();
                drawList.AddText(min + new Vector2(3f, 3f), Theme.U32(active ? Theme.Accent : hovered ? Theme.Text : Theme.TextDim), labels[i]);
                if (active)
                    drawList.AddRectFilled(new Vector2(min.X, max.Y - 1f), new Vector2(max.X, max.Y + 1f), Theme.U32(Theme.Accent));
            }
        }

        var y = ImGui.GetItemRectMax().Y + 5f;
        var left = ImGui.GetCursorScreenPos().X;
        drawList.AddLine(new Vector2(left, y), new Vector2(left + ImGui.GetContentRegionAvail().X, y), Theme.U32(Theme.Edge), 1f);
        ImGui.Dummy(new Vector2(0f, 8f));
    }

    /// <summary>A heading in the display face with a rule under it.</summary>
    public static void Section(string title) => Theme.Heading(title);

    /// <summary>
    /// A way to pick one of a shelf's listings: a row of chips while there are few enough to read
    /// at a glance, a dropdown once there are more, since rows of chips become a wall. Returns
    /// the index pressed this frame, or -1 when none was; the caller keeps the selection, which
    /// may legitimately be none.
    /// </summary>
    public static int Chips(string id, IReadOnlyList<string> labels, int selected)
    {
        var pressed = -1;
        if (labels.Count > 6)
        {
            ImGui.SetNextItemWidth(Math.Min(320f, ImGui.GetContentRegionAvail().X));
            var preview = selected >= 0 && selected < labels.Count ? labels[selected] : "Choose a shelf…";
            using var combo = ImRaii.Combo($"##{id}pick", preview);
            if (combo)
            {
                for (var i = 0; i < labels.Count; i++)
                {
                    if (ImGui.Selectable($"{labels[i]}##{id}{i}", i == selected))
                        pressed = i;
                    if (i == selected)
                        ImGui.SetItemDefaultFocus();
                }
            }

            return pressed;
        }

        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        for (var i = 0; i < labels.Count; i++)
        {
            var width = ImGui.CalcTextSize(labels[i]).X + (ImGui.GetStyle().FramePadding.X * 2);
            if (i > 0 && ImGui.GetItemRectMax().X + spacing + width < rightEdge)
                ImGui.SameLine();

            var lit = i == selected;
            using var colours = ImRaii.PushColor(ImGuiCol.Button, lit ? Theme.GlassLit : Theme.Glass)
                .Push(ImGuiCol.Border, lit ? Theme.Accent : Theme.GlassEdge)
                .Push(ImGuiCol.Text, lit ? Theme.Accent : Theme.Text);
            using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
            if (ImGui.Button($"{labels[i]}##{id}{i}"))
                pressed = i;
        }

        return pressed;
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

    /// <summary>The text cut with an ellipsis to fit a width in the current font, so a line never wraps or runs off.</summary>
    public static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width)
            return text;
        var keep = text.Length;
        while (keep > 1 && ImGui.CalcTextSize(string.Concat(text.AsSpan(0, keep), "…")).X > width)
            keep -= Math.Max(1, keep / 12);
        return string.Concat(text.AsSpan(0, Math.Max(1, keep)), "…");
    }

    public static string Ellipsis(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, Math.Max(1, max - 1)), "…");
}
