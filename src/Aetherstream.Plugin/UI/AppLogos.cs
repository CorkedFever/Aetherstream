using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The services' marks, drawn with primitives at icon size. The icon font has no brand glyphs and
/// nothing is shipped as an image, so each is the simplest shape that still reads as the logo
/// everyone knows: YouTube's red tile and play triangle, Twitch's purple bubble, Plex's chevron,
/// NASA's blue disc and red swoosh, TED's three letters. An app without a mark here gets its icon.
/// </summary>
internal static class AppLogos
{
    private static uint C(float r, float g, float b, float a = 1f) => ImGui.ColorConvertFloat4ToU32(new Vector4(r, g, b, a));

    private static readonly uint White = C(1f, 1f, 1f);
    private static readonly uint Black = C(0.04f, 0.04f, 0.06f);

    /// <summary>Whether a mark exists for the app.</summary>
    public static bool Has(string key, string serverKind) =>
        key is "youtube" or "twitch" or "plex" or "pluto" or "redbull" or "pbs" or "nasa" or "ted" or "dailymotion" or "archive" or "server";

    /// <summary>Draws the mark in a square at p0, background included. Rounding matches the plain icons'.</summary>
    public static void Draw(ImDrawListPtr d, string key, Vector2 p0, float size, string serverKind, bool hovered)
    {
        var p1 = p0 + new Vector2(size);
        var r = size * 0.27f;
        var c = (p0 + p1) / 2f;
        var lift = hovered ? 0.12f : 0f;

        switch (key)
        {
            case "youtube":
                d.AddRectFilled(p0, p1, C(1f - lift * 0.3f, 0.05f + lift, 0.05f + lift), r);
                Triangle(d, c, size * 0.20f, White);
                break;

            case "twitch":
                d.AddRectFilled(p0, p1, C(0.57f + lift, 0.27f + lift, 1f), r);
                var b0 = p0 + new Vector2(size * 0.22f, size * 0.20f);
                var b1 = p0 + new Vector2(size * 0.78f, size * 0.66f);
                d.AddRectFilled(b0, b1, White, size * 0.06f);
                d.AddTriangleFilled(new Vector2(b0.X + size * 0.06f, b1.Y - 1f), new Vector2(b0.X + size * 0.22f, b1.Y - 1f), new Vector2(b0.X + size * 0.06f, b1.Y + size * 0.12f), White);
                var eye = C(0.57f, 0.27f, 1f);
                d.AddRectFilled(p0 + new Vector2(size * 0.42f, size * 0.30f), p0 + new Vector2(size * 0.48f, size * 0.50f), eye);
                d.AddRectFilled(p0 + new Vector2(size * 0.57f, size * 0.30f), p0 + new Vector2(size * 0.63f, size * 0.50f), eye);
                break;

            case "plex":
                d.AddRectFilled(p0, p1, C(0.10f + lift, 0.10f + lift, 0.12f + lift), r);
                var amber = C(0.90f, 0.63f, 0.05f);
                var thick = size * 0.13f;
                d.AddLine(p0 + new Vector2(size * 0.32f, size * 0.22f), p0 + new Vector2(size * 0.62f, size * 0.50f), amber, thick);
                d.AddLine(p0 + new Vector2(size * 0.62f, size * 0.50f), p0 + new Vector2(size * 0.32f, size * 0.78f), amber, thick);
                break;

            case "pluto":
                d.AddRectFilled(p0, p1, C(0.08f + lift, 0.08f + lift, 0.10f + lift), r);
                d.AddCircleFilled(c, size * 0.32f, C(1f, 0.88f, 0f), 24);
                Word(d, c + new Vector2(0f, 1f), size * 0.30f, "pluto", Black);
                break;

            case "redbull":
                d.AddRectFilled(p0, p1, C(0.10f + lift, 0.18f + lift, 0.40f + lift), r);
                d.AddCircleFilled(c, size * 0.30f, C(1f, 0.80f, 0.05f), 24);
                var red = C(0.85f, 0.08f, 0.10f);
                d.AddBezierCubic(c + new Vector2(-size * 0.30f, size * 0.06f), c + new Vector2(-size * 0.18f, -size * 0.22f), c + new Vector2(-size * 0.02f, -size * 0.14f), c + new Vector2(-size * 0.01f, size * 0.10f), red, size * 0.07f);
                d.AddBezierCubic(c + new Vector2(size * 0.30f, size * 0.06f), c + new Vector2(size * 0.18f, -size * 0.22f), c + new Vector2(size * 0.02f, -size * 0.14f), c + new Vector2(size * 0.01f, size * 0.10f), red, size * 0.07f);
                break;

            case "pbs":
                d.AddRectFilled(p0, p1, C(0.10f + lift, 0.16f + lift, 0.70f + lift), r);
                d.AddCircleFilled(c + new Vector2(-size * 0.06f, 0f), size * 0.26f, White, 24);
                d.AddCircleFilled(c + new Vector2(-size * 0.12f, -size * 0.04f), size * 0.05f, C(0.10f, 0.16f, 0.70f), 12);
                d.AddTriangleFilled(c + new Vector2(size * 0.12f, -size * 0.10f), c + new Vector2(size * 0.32f, size * 0.02f), c + new Vector2(size * 0.12f, size * 0.14f), White);
                break;

            case "nasa":
                d.AddRectFilled(p0, p1, C(0.04f + lift, 0.20f + lift, 0.55f + lift), r);
                d.AddCircleFilled(c, size * 0.36f, C(0.04f, 0.24f, 0.57f), 28);
                Word(d, c, size * 0.34f, "NASA", White);
                d.AddBezierCubic(c + new Vector2(-size * 0.30f, size * 0.16f), c + new Vector2(-size * 0.10f, -size * 0.30f), c + new Vector2(size * 0.10f, size * 0.30f), c + new Vector2(size * 0.32f, -size * 0.14f), C(0.99f, 0.24f, 0.16f), size * 0.045f);
                break;

            case "ted":
                d.AddRectFilled(p0, p1, C(0.90f + lift * 0.5f, 0.17f + lift, 0.12f + lift), r);
                Word(d, c, size * 0.44f, "TED", White);
                break;

            case "dailymotion":
                d.AddRectFilled(p0, p1, C(0f + lift, 0.40f + lift, 0.86f + lift), r);
                Word(d, c + new Vector2(0f, -size * 0.02f), size * 0.62f, "d", White);
                break;

            case "archive":
                d.AddRectFilled(p0, p1, C(0.92f + lift * 0.3f, 0.92f + lift * 0.3f, 0.90f + lift * 0.3f), r);
                var ink = C(0.10f, 0.10f, 0.10f);
                d.AddTriangleFilled(p0 + new Vector2(size * 0.18f, size * 0.36f), p0 + new Vector2(size * 0.82f, size * 0.36f), p0 + new Vector2(size * 0.50f, size * 0.16f), ink);
                d.AddRectFilled(p0 + new Vector2(size * 0.18f, size * 0.38f), p0 + new Vector2(size * 0.82f, size * 0.44f), ink);
                for (var i = 0; i < 4; i++)
                {
                    var x = size * (0.24f + (i * 0.16f));
                    d.AddRectFilled(p0 + new Vector2(x, size * 0.47f), p0 + new Vector2(x + size * 0.09f, size * 0.72f), ink);
                }

                d.AddRectFilled(p0 + new Vector2(size * 0.18f, size * 0.75f), p0 + new Vector2(size * 0.82f, size * 0.82f), ink);
                break;

            case "server" when serverKind.Equals("Emby", StringComparison.OrdinalIgnoreCase):
                d.AddRectFilled(p0, p1, C(0.32f + lift, 0.71f + lift, 0.29f + lift), r);
                Triangle(d, c, size * 0.20f, White);
                break;

            case "server":
                // Jellyfin's jellyfish: a rounded cap and three trailing legs, in its purple and blue.
                d.AddRectFilled(p0, p1, C(0.06f + lift, 0.06f + lift, 0.10f + lift), r);
                var cap = C(0.62f, 0.32f, 0.86f);
                var leg = C(0.20f, 0.62f, 0.90f);
                d.AddCircleFilled(c + new Vector2(0f, -size * 0.06f), size * 0.24f, cap, 24);
                d.AddRectFilled(c + new Vector2(-size * 0.24f, -size * 0.06f), c + new Vector2(size * 0.24f, size * 0.08f), cap);
                for (var i = -1; i <= 1; i++)
                    d.AddBezierCubic(c + new Vector2(i * size * 0.14f, size * 0.06f), c + new Vector2(i * size * 0.14f - size * 0.06f, size * 0.18f), c + new Vector2(i * size * 0.14f + size * 0.06f, size * 0.24f), c + new Vector2(i * size * 0.14f, size * 0.34f), leg, size * 0.06f);
                break;
        }
    }

    private static void Triangle(ImDrawListPtr d, Vector2 c, float half, uint colour) =>
        d.AddTriangleFilled(c + new Vector2(-half * 0.8f, -half), c + new Vector2(-half * 0.8f, half), c + new Vector2(half * 1.1f, 0f), colour);

    /// <summary>A word centred in the mark, in the display face at the size given.</summary>
    private static void Word(ImDrawListPtr d, Vector2 centre, float fontSize, string text, uint colour)
    {
        using (Theme.PushDisplay())
        {
            var font = ImGui.GetFont();
            var scale = fontSize / ImGui.GetFontSize();
            var size = ImGui.CalcTextSize(text) * scale;
            d.AddText(font, fontSize, centre - (size / 2f), colour, text);
        }
    }
}
