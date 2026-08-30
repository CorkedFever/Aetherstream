using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// One clickable tile: poster, title, and a line under it.
/// <para>
/// Drawn by hand rather than assembled from widgets because the whole tile has to be one hit target
/// — a title that is only clickable on its own text is a worse target than the picture above it.
/// </para>
/// </summary>
internal static class PosterCard
{
    public const float Width = 118f;

    /// <summary>
    /// An episode's artwork is a still from the episode, which is 16:9. Drawing it into a poster
    /// slot wastes most of the tile on empty space, so episodes get their own shape.
    /// </summary>
    public const float WideWidth = 196f;

    private const float PosterAspect = 1.5f;

    private const float StillAspect = 9f / 16f;

    public static float WidthOf(bool wide) => wide ? WideWidth : Width;

    /// <summary>Total height, so a caller can work out how many fit before drawing any.</summary>
    public static float HeightOf(bool wide) =>
        (WidthOf(wide) * (wide ? StillAspect : PosterAspect)) + (ImGui.GetTextLineHeight() * 2f) + 10f;

    public static bool Draw(
        UiContext ui,
        string id,
        string thumb,
        string title,
        string subtitle,
        bool container,
        bool wide = false)
    {
        var width = WidthOf(wide);
        var size = new Vector2(width, HeightOf(wide));
        var origin = ImGui.GetCursorScreenPos();

        var clicked = ImGui.InvisibleButton(id, size);

        // The button is always submitted, so the layout and the scrollbar stay right; everything
        // below it is skipped when the tile is scrolled out of view. That includes asking for the
        // texture, which is what stops a three-hundred-item library from starting three hundred
        // downloads the moment it opens.
        if (!ImGui.IsItemVisible())
            return clicked;

        var hovered = ImGui.IsItemHovered();

        var draw = ImGui.GetWindowDrawList();
        var posterMax = origin + new Vector2(width, width * (wide ? StillAspect : PosterAspect));

        // Frame. It brightens on hover, which is the only affordance a tile has.
        draw.AddRectFilled(
            origin,
            posterMax,
            ImGui.ColorConvertFloat4ToU32(hovered ? new Vector4(1f, 1f, 1f, 0.10f) : new Vector4(1f, 1f, 1f, 0.04f)),
            4f);

        var texture = ui.Art.Get(ui.Config.PlexServer, ui.Config.PlexToken, thumb);
        if (texture is not null)
        {
            // Fit inside, never crop. Episode stills are 16:9 and posters are 2:3, so anything that
            // assumed one shape would distort the other.
            var available = posterMax - origin;
            var scale = Math.Min(available.X / texture.Size.X, available.Y / texture.Size.Y);
            var drawn = texture.Size * scale;
            var offset = (available - drawn) * 0.5f;

            draw.AddImage(texture.Handle, origin + offset, origin + offset + drawn);
        }
        else
        {
            // No art yet, or none on the server. The title goes in the empty frame so the tile is
            // still identifiable rather than being a blank rectangle.
            var placeholder = Ui.Ellipsis(title, 14);
            var textSize = ImGui.CalcTextSize(placeholder);
            draw.AddText(
                origin + ((posterMax - origin - textSize) * 0.5f),
                ImGui.ColorConvertFloat4ToU32(Ui.Faint),
                placeholder);
        }

        if (hovered)
        {
            draw.AddRect(
                origin,
                posterMax,
                ImGui.ColorConvertFloat4ToU32(Ui.Accent),
                4f,
                ImDrawFlags.None,
                1.5f);
        }

        // A chevron marks the tiles that open into something instead of playing.
        if (container)
        {
            var badge = "›";
            var badgeSize = ImGui.CalcTextSize(badge);
            var corner = new Vector2(posterMax.X - badgeSize.X - 6f, origin.Y + 4f);
            draw.AddRectFilled(
                corner - new Vector2(4f, 2f),
                corner + badgeSize + new Vector2(4f, 2f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f)),
                3f);

            draw.AddText(corner, ImGui.ColorConvertFloat4ToU32(Ui.Accent), badge);
        }

        var textTop = posterMax.Y + 4f;
        draw.AddText(
            new Vector2(origin.X, textTop),
            ImGui.ColorConvertFloat4ToU32(hovered ? Ui.Accent : new Vector4(1f, 1f, 1f, 0.92f)),
            Fit(title, width));

        if (subtitle.Length > 0)
        {
            draw.AddText(
                new Vector2(origin.X, textTop + ImGui.GetTextLineHeight()),
                ImGui.ColorConvertFloat4ToU32(Ui.Faint),
                Fit(subtitle, width));
        }

        if (hovered)
            Ui.Tip(subtitle.Length > 0 ? $"{title}\n{subtitle}" : title);

        return clicked;
    }

    /// <summary>
    /// Truncates to a pixel width. The draw list does not clip text, so a long title would otherwise
    /// run straight across the tile beside it.
    /// </summary>
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
