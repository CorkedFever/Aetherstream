using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// A little logo for each drawn channel, as a cable network would have on its ident: drawn from
/// primitives so it scales with the window and takes the set's colours. Anything without one
/// gets a plain dot, so a new channel never breaks the tiles.
/// </summary>
internal static class ChannelLogos
{
    private static readonly Vector4 Sun = Theme.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly Vector4 Sky = Theme.Rgb(0x8F, 0xD6, 0xFF);
    private static readonly Vector4 Ember = Theme.Rgb(0xFF, 0x7A, 0x2E);
    private static readonly Vector4 Flame = Theme.Rgb(0xFF, 0xC2, 0x4A);
    private static readonly Vector4 Coin = Theme.Rgb(0xF2, 0xC1, 0x4E);
    private static readonly Vector4 Leaf = Theme.Rgb(0x7F, 0xD9, 0x8B);
    private static readonly Vector4 Rose = Theme.Rgb(0xF0, 0x7C, 0xB5);
    private static readonly Vector4 Violet = Theme.Rgb(0xB8, 0x8C, 0xFF);
    private static readonly Vector4 Coral = Theme.Rgb(0xFF, 0x8C, 0x69);
    private static readonly Vector4 Brick = Theme.Rgb(0xC9, 0x6A, 0x4A);
    private static readonly Vector4 Mortar = Theme.Rgb(0xE8, 0xD5, 0xB5);
    private static readonly Vector4 Pipe = Theme.Rgb(0x5D, 0xCA, 0xA5);
    private static readonly Vector4 Paper = Theme.Rgb(0xE6, 0xF1, 0xFB);

    /// <summary>
    /// Draws the logo for <paramref name="channel"/> in a badge whose top-left is
    /// <paramref name="at"/> and whose side is <paramref name="size"/>.
    /// </summary>
    public static void Draw(ImDrawListPtr d, string channel, Vector2 at, float size, bool lit)
    {
        // The badge: a rounded plate the logo sits on, a touch brighter when the channel is up.
        d.AddRectFilled(at, at + new Vector2(size, size), Theme.U32(lit ? Theme.GlassEdge : Theme.Tube), size * 0.2f);
        d.AddRect(at, at + new Vector2(size, size), Theme.U32(lit ? Theme.AccentDim : Theme.GlassEdge), size * 0.2f);

        // Everything below draws in a unit square: c is the centre, u one unit of size/40.
        var u = size / 40f;
        var c = at + new Vector2(size / 2f, size / 2f);
        Vector2 P(float x, float y) => c + new Vector2(x * u, y * u);

        switch (channel.ToLowerInvariant())
        {
            case "guide":
                // Listing rows: a bright one for the channel you are on, dim ones around it.
                for (var row = 0; row < 4; row++)
                {
                    var y = -13f + (row * 8f);
                    var colour = row == 1 ? Theme.Accent : Theme.TextFaint;
                    d.AddRectFilled(P(-13f, y), P(-7f, y + 5f), Theme.U32(colour), 1f);
                    d.AddRectFilled(P(-4f, y), P(13f, y + 5f), Theme.U32(colour), 1f);
                }

                break;

            case "weather":
                // A sun peeking over a cloud.
                d.AddCircleFilled(P(4f, -5f), 7f * u, Theme.U32(Sun), 16);
                for (var ray = 0; ray < 8; ray++)
                {
                    var a = ray * MathF.PI / 4f;
                    var (dx, dy) = (MathF.Cos(a), MathF.Sin(a));
                    d.AddLine(P(4f + (dx * 9.5f), -5f + (dy * 9.5f)), P(4f + (dx * 12.5f), -5f + (dy * 12.5f)), Theme.U32(Sun), 1.6f * u);
                }

                d.AddCircleFilled(P(-6f, 6f), 6f * u, Theme.U32(Paper), 16);
                d.AddCircleFilled(P(1f, 4f), 7f * u, Theme.U32(Paper), 16);
                d.AddCircleFilled(P(7f, 7f), 5f * u, Theme.U32(Paper), 16);
                d.AddRectFilled(P(-6f, 6f), P(7f, 12f), Theme.U32(Paper), 3f * u);
                break;

            case "clock":
                d.AddCircle(P(0f, 0f), 13f * u, Theme.U32(Sky), 24, 2f * u);
                for (var tick = 0; tick < 4; tick++)
                {
                    var a = tick * MathF.PI / 2f;
                    d.AddLine(P(MathF.Cos(a) * 10f, MathF.Sin(a) * 10f), P(MathF.Cos(a) * 12f, MathF.Sin(a) * 12f), Theme.U32(Sky), 1.5f * u);
                }

                d.AddLine(P(0f, 0f), P(0f, -8f), Theme.U32(Paper), 2.2f * u);
                d.AddLine(P(0f, 0f), P(6f, 3f), Theme.U32(Paper), 2.2f * u);
                d.AddCircleFilled(P(0f, 0f), 1.8f * u, Theme.U32(Sky), 8);
                break;

            case "news":
                // A folded paper: a masthead, a photo, and columns of type.
                d.AddRectFilled(P(-13f, -13f), P(13f, 13f), Theme.U32(Paper), 2f * u);
                d.AddRectFilled(P(-10f, -10f), P(10f, -6f), Theme.U32(Theme.Shell));
                d.AddRectFilled(P(-10f, -3f), P(-1f, 5f), Theme.U32(Theme.Accent));
                for (var line = 0; line < 4; line++)
                    d.AddRectFilled(P(2f, -3f + (line * 3f)), P(10f, -1.5f + (line * 3f)), Theme.U32(Theme.TextFaint));
                d.AddRectFilled(P(-10f, 8f), P(10f, 9.5f), Theme.U32(Theme.TextFaint));
                break;

            case "market":
                // Bars climbing, and a line over them. Number go up.
                var heights = new[] { 5f, 9f, 7f, 13f, 17f };
                for (var bar = 0; bar < heights.Length; bar++)
                {
                    var x = -13f + (bar * 5.5f);
                    d.AddRectFilled(P(x, 13f - heights[bar]), P(x + 4f, 13f), Theme.U32(bar == heights.Length - 1 ? Coin : Theme.AccentDim), 1f);
                }

                for (var bar = 0; bar + 1 < heights.Length; bar++)
                    d.AddLine(P(-11f + (bar * 5.5f), 11f - heights[bar]), P(-5.5f + (bar * 5.5f), 11f - heights[bar + 1]), Theme.U32(Coin), 1.8f * u);
                break;

            case "gathering":
                // A crystal, cut with a lit face, and a sprout by it.
                d.AddQuadFilled(P(3f, -13f), P(12f, -2f), P(3f, 13f), P(-6f, -2f), Theme.U32(Sky));
                d.AddTriangleFilled(P(3f, -13f), P(12f, -2f), P(3f, -2f), Theme.U32(Paper));
                d.AddTriangleFilled(P(3f, -2f), P(3f, 13f), P(-6f, -2f), Theme.U32(Theme.Accent));
                d.AddLine(P(-11f, 13f), P(-11f, 2f), Theme.U32(Leaf), 1.8f * u);
                d.AddCircleFilled(P(-14f, 3f), 2.8f * u, Theme.U32(Leaf), 10);
                d.AddCircleFilled(P(-8f, 6f), 2.8f * u, Theme.U32(Leaf), 10);
                break;

            case "fishing":
                // A rod's line down to a bobber, red over white, and ripples under it.
                d.AddLine(P(-12f, -13f), P(2f, -13f), Theme.U32(Theme.TextDim), 1.6f * u);
                d.AddLine(P(2f, -13f), P(2f, -2f), Theme.U32(Theme.TextDim), 1.2f * u);
                d.AddCircleFilled(P(2f, 3f), 6f * u, Theme.U32(Theme.Bad), 16);
                d.AddCircleFilled(P(2f, 5.5f), 4f * u, Theme.U32(Paper), 14);
                d.AddLine(P(-12f, 11f), P(16f, 11f), Theme.U32(Sky), 1.4f * u);
                d.AddLine(P(-8f, 14f), P(12f, 14f), Theme.U32(Theme.WithAlpha(Sky, 0.5f)), 1.2f * u);
                break;

            case "horoscope":
                // A crescent and a star: the Sharlayan reading.
                d.AddCircleFilled(P(-3f, 0f), 11f * u, Theme.U32(Sun), 20);
                d.AddCircleFilled(P(2f, -3f), 10f * u, Theme.U32(lit ? Theme.GlassEdge : Theme.Tube), 20);
                DrawStar(d, P(8f, 6f), 4.5f * u, Paper);
                DrawStar(d, P(11f, -8f), 2.5f * u, Paper);
                break;

            case "kitchen":
                // A pot on the boil, steam rising.
                d.AddRectFilled(P(-11f, -1f), P(11f, 12f), Theme.U32(Theme.TextDim), 3f * u);
                d.AddRectFilled(P(-13f, -3f), P(13f, 0f), Theme.U32(Paper), 1f);
                d.AddLine(P(-13f, 4f), P(-16f, 4f), Theme.U32(Theme.TextDim), 3f * u);
                d.AddLine(P(13f, 4f), P(16f, 4f), Theme.U32(Theme.TextDim), 3f * u);
                for (var s = 0; s < 3; s++)
                {
                    var x = -6f + (s * 6f);
                    d.AddLine(P(x, -6f), P(x + 1.5f, -9f), Theme.U32(Theme.WithAlpha(Paper, 0.7f)), 1.6f * u);
                    d.AddLine(P(x + 1.5f, -9f), P(x, -12f), Theme.U32(Theme.WithAlpha(Paper, 0.7f)), 1.6f * u);
                }

                break;

            case "wildlife":
                // A bush hat over a pair of eyes in the grass.
                d.AddRectFilled(P(-6f, -10f), P(6f, -2f), Theme.U32(Coin), 2f * u);
                d.AddRectFilled(P(-14f, -3f), P(14f, 0f), Theme.U32(Coin), 1f);
                d.AddCircleFilled(P(-4f, 5f), 2f * u, Theme.U32(Paper), 8);
                d.AddCircleFilled(P(4f, 5f), 2f * u, Theme.U32(Paper), 8);
                for (var g = 0; g < 7; g++)
                    d.AddLine(P(-13f + (g * 4.3f), 13f), P(-12f + (g * 4.3f), 7f), Theme.U32(Leaf), 1.6f * u);
                break;

            case "stories":
                // An open book, a page lifting.
                d.AddRectFilled(P(-13f, -8f), P(0f, 10f), Theme.U32(Paper), 1f);
                d.AddRectFilled(P(0f, -8f), P(13f, 10f), Theme.U32(Paper), 1f);
                d.AddLine(P(0f, -8f), P(0f, 10f), Theme.U32(Theme.TextFaint), 1.2f * u);
                for (var l = 0; l < 3; l++)
                {
                    d.AddLine(P(-10f, -4f + (l * 4f)), P(-3f, -4f + (l * 4f)), Theme.U32(Theme.TextFaint), 1f * u);
                    d.AddLine(P(3f, -4f + (l * 4f)), P(10f, -4f + (l * 4f)), Theme.U32(Theme.TextFaint), 1f * u);
                }

                d.AddTriangleFilled(P(0f, -8f), P(9f, -13f), P(11f, -6f), Theme.U32(Violet));
                break;

            case "shopping":
                // A price tag with a string, and a starburst behind it.
                for (var s = 0; s < 8; s++)
                {
                    var a = s * MathF.PI / 4f;
                    d.AddLine(P(0f, 0f), P(MathF.Cos(a) * 14f, MathF.Sin(a) * 14f), Theme.U32(Coin), 1.4f * u);
                }

                d.AddRectFilled(P(-9f, -5f), P(9f, 8f), Theme.U32(Rose), 1.5f * u);
                d.AddTriangleFilled(P(-9f, -5f), P(0f, -12f), P(9f, -5f), Theme.U32(Rose));
                d.AddCircleFilled(P(0f, -6f), 1.6f * u, Theme.U32(Paper), 8);
                d.AddLine(P(-5f, 2f), P(5f, 2f), Theme.U32(Paper), 1.4f * u);
                break;

            case "radio":
                // A record with its label, and a few bars of a spectrum rising beside it.
                d.AddCircleFilled(P(-5f, 0f), 10f * u, Theme.U32(Theme.Text), 24);
                d.AddCircle(P(-5f, 0f), 7f * u, Theme.U32(Theme.TextFaint), 24, 1f * u);
                d.AddCircleFilled(P(-5f, 0f), 3.2f * u, Theme.U32(Rose), 12);
                d.AddCircleFilled(P(-5f, 0f), 0.9f * u, Theme.U32(Paper), 8);
                for (var b = 0; b < 3; b++)
                    d.AddRectFilled(P(8f + (b * 3.2f), 6f - (b * 4f) - 2f), P(10f + (b * 3.2f), 8f), Theme.U32(Sky), 1f);
                break;

            case "housing":
                // A little house with a sold sign that says nothing yet.
                d.AddTriangleFilled(P(-13f, -2f), P(0f, -13f), P(13f, -2f), Theme.U32(Rose));
                d.AddRectFilled(P(-10f, -2f), P(10f, 11f), Theme.U32(Paper), 1f);
                d.AddRectFilled(P(-3f, 3f), P(3f, 11f), Theme.U32(Theme.Text), 1f);
                d.AddRectFilled(P(5f, 1f), P(9f, 5f), Theme.U32(Sky), 1f);
                d.AddRectFilled(P(-9f, 1f), P(-5f, 5f), Theme.U32(Sky), 1f);
                d.AddLine(P(9f, 13f), P(9f, 4f), Theme.U32(Coin), 1.4f * u);
                d.AddRectFilled(P(5f, 4f), P(14f, 8f), Theme.U32(Coin), 1f);
                break;

            case "painting":
                // A palette with its thumb hole and dabs, and a brush across it.
                d.AddCircleFilled(P(-2f, 1f), 12f * u, Theme.U32(Coin), 24);
                d.AddCircleFilled(P(5f, 6f), 3f * u, Theme.U32(Theme.Shell), 12);
                d.AddCircleFilled(P(-8f, -3f), 2.4f * u, Theme.U32(Rose), 10);
                d.AddCircleFilled(P(-2f, -8f), 2.4f * u, Theme.U32(Sky), 10);
                d.AddCircleFilled(P(5f, -5f), 2.4f * u, Theme.U32(Leaf), 10);
                d.AddCircleFilled(P(-8f, 5f), 2.4f * u, Theme.U32(Paper), 10);
                d.AddLine(P(-12f, 12f), P(12f, -12f), Theme.U32(Theme.Text), 2f * u);
                d.AddLine(P(9f, -9f), P(13f, -13f), Theme.U32(Rose), 3f * u);
                break;

            case "scrambled":
                // A picture torn into bands, each slid a different way, with a sync line through it.
                for (var row = 0; row < 5; row++)
                {
                    var y = -13f + (row * 5.4f);
                    var slide = (row % 2 == 0 ? -1f : 1f) * (2f + row);
                    var colour = row switch { 1 => Rose, 3 => Sky, _ => Theme.TextFaint };
                    d.AddRectFilled(P(-12f + slide, y), P(12f + slide, y + 4f), Theme.U32(colour), 1f);
                }

                d.AddLine(P(-14f, 1f), P(14f, 1f), Theme.U32(Paper), 1.4f * u);
                break;

            case "almanac":
                // A calendar page with its rings, and a marked day.
                d.AddRectFilled(P(-13f, -10f), P(13f, 13f), Theme.U32(Paper), 2f * u);
                d.AddRectFilled(P(-13f, -10f), P(13f, -3f), Theme.U32(Theme.Bad), 2f * u);
                d.AddRectFilled(P(-13f, -6f), P(13f, -3f), Theme.U32(Theme.Bad));
                d.AddRectFilled(P(-8f, -13f), P(-6f, -7f), Theme.U32(Theme.Text), 1f);
                d.AddRectFilled(P(6f, -13f), P(8f, -7f), Theme.U32(Theme.Text), 1f);
                for (var row = 0; row < 3; row++)
                {
                    for (var col = 0; col < 4; col++)
                    {
                        var marked = row == 1 && col == 2;
                        d.AddRectFilled(P(-10f + (col * 5.5f), 0f + (row * 4f)), P(-7f + (col * 5.5f), 2.5f + (row * 4f)), Theme.U32(marked ? Theme.Bad : Theme.TextFaint));
                    }
                }

                break;

            case "venues":
                // A cocktail, with an olive. Tonight's the night.
                d.AddTriangleFilled(P(-12f, -12f), P(12f, -12f), P(0f, 2f), Theme.U32(Rose));
                d.AddTriangleFilled(P(-8f, -9f), P(8f, -9f), P(0f, 0f), Theme.U32(Paper));
                d.AddLine(P(0f, 2f), P(0f, 11f), Theme.U32(Rose), 2f * u);
                d.AddLine(P(-7f, 12f), P(7f, 12f), Theme.U32(Rose), 2f * u);
                d.AddLine(P(-6f, -13f), P(2f, -6f), Theme.U32(Theme.TextDim), 1.2f * u);
                d.AddCircleFilled(P(3f, -5f), 2.2f * u, Theme.U32(Leaf), 10);
                break;

            case "aquarium":
                // A fish, as the aquarium draws them: not a lot of polygons.
                d.AddCircleFilled(P(-1f, 0f), 8f * u, Theme.U32(Coral), 16);
                d.AddTriangleFilled(P(6f, 0f), P(14f, -7f), P(14f, 7f), Theme.U32(Coral));
                d.AddTriangleFilled(P(-3f, -6f), P(2f, -12f), P(4f, -5f), Theme.U32(Coral));
                d.AddCircleFilled(P(-5f, -2f), 2f * u, Theme.U32(Theme.Shell), 8);
                d.AddCircleFilled(P(-11f, -9f), 1.5f * u, Theme.U32(Sky), 8);
                d.AddCircleFilled(P(-13f, -13f), 1f * u, Theme.U32(Sky), 8);
                break;

            case "fireplace":
                // A flame in two tones over logs.
                d.AddLine(P(-11f, 11f), P(11f, 11f), Theme.U32(Brick), 3f * u);
                d.AddTriangleFilled(P(-9f, 9f), P(9f, 9f), P(0f, -13f), Theme.U32(Ember));
                d.AddCircleFilled(P(-5f, 5f), 5f * u, Theme.U32(Ember), 12);
                d.AddCircleFilled(P(5f, 5f), 5f * u, Theme.U32(Ember), 12);
                d.AddTriangleFilled(P(-4f, 9f), P(4f, 9f), P(0f, -3f), Theme.U32(Flame));
                d.AddCircleFilled(P(0f, 6f), 3.5f * u, Theme.U32(Flame), 12);
                break;

            case "starfield":
                DrawStar(d, P(-6f, -4f), 6f * u, Paper);
                DrawStar(d, P(8f, 6f), 4f * u, Sky);
                DrawStar(d, P(7f, -9f), 2.5f * u, Paper);
                d.AddCircleFilled(P(-11f, 9f), 1.2f * u, Theme.U32(Theme.TextDim), 6);
                d.AddCircleFilled(P(0f, 11f), 1f * u, Theme.U32(Theme.TextDim), 6);
                d.AddCircleFilled(P(12f, -2f), 1f * u, Theme.U32(Theme.TextDim), 6);
                break;

            case "plasma":
                // Rings of colour, out from a hot centre.
                d.AddCircleFilled(P(0f, 0f), 13f * u, Theme.U32(Violet), 20);
                d.AddCircleFilled(P(0f, 0f), 10f * u, Theme.U32(Rose), 20);
                d.AddCircleFilled(P(0f, 0f), 7f * u, Theme.U32(Ember), 16);
                d.AddCircleFilled(P(0f, 0f), 4f * u, Theme.U32(Flame), 12);
                break;

            case "mystify":
                // Two polygons and their ghosts.
                d.AddQuad(P(-12f, -6f), P(4f, -12f), P(11f, 4f), P(-8f, 11f), Theme.U32(Theme.WithAlpha(Sky, 0.4f)), 1.2f * u);
                d.AddQuad(P(-10f, -9f), P(7f, -10f), P(12f, 7f), P(-6f, 12f), Theme.U32(Sky), 1.6f * u);
                d.AddQuad(P(-5f, 2f), P(3f, -6f), P(9f, 1f), P(-1f, 8f), Theme.U32(Theme.WithAlpha(Rose, 0.4f)), 1.2f * u);
                d.AddQuad(P(-3f, 5f), P(1f, -3f), P(7f, 3f), P(-2f, 10f), Theme.U32(Rose), 1.6f * u);
                break;

            case "3d maze":
                // A brick wall receding to a vanishing point.
                d.AddRectFilled(P(-13f, -13f), P(13f, 13f), Theme.U32(Brick), 2f * u);
                for (var row = 0; row < 5; row++)
                {
                    var y = -13f + (row * 5.2f);
                    d.AddLine(P(-13f, y), P(13f, y), Theme.U32(Mortar), 1f * u);
                    for (var col = 0; col < 3; col++)
                        d.AddLine(P(-13f + (col * 8.7f) + (row % 2 == 0 ? 0f : 4.3f), y), P(-13f + (col * 8.7f) + (row % 2 == 0 ? 0f : 4.3f), y + 5.2f), Theme.U32(Mortar), 1f * u);
                }

                d.AddRectFilled(P(-5f, -5f), P(5f, 5f), Theme.U32(Theme.Shell));
                d.AddRectFilled(P(-2f, -2f), P(2f, 2f), Theme.U32(Sun));
                break;

            case "pipes":
                // An elbow of pipe with a joint, the way the screensaver bends.
                d.AddLine(P(-13f, -6f), P(3f, -6f), Theme.U32(Pipe), 6f * u);
                d.AddLine(P(3f, -6f), P(3f, 13f), Theme.U32(Pipe), 6f * u);
                d.AddCircleFilled(P(3f, -6f), 4.5f * u, Theme.U32(Pipe), 14);
                d.AddCircleFilled(P(3f, -6f), 2f * u, Theme.U32(Theme.Good), 10);
                d.AddLine(P(-13f, -8f), P(1f, -8f), Theme.U32(Theme.WithAlpha(Paper, 0.35f)), 1.2f * u);
                d.AddLine(P(1f, -2f), P(1f, 12f), Theme.U32(Theme.WithAlpha(Paper, 0.35f)), 1.2f * u);
                break;

            default:
                d.AddCircleFilled(c, 5f * u, Theme.U32(Theme.Accent), 12);
                break;
        }
    }

    private static void DrawStar(ImDrawListPtr d, Vector2 centre, float radius, Vector4 colour)
    {
        // A four-point twinkle: two thin diamonds crossed.
        d.AddQuadFilled(
            centre + new Vector2(0f, -radius), centre + new Vector2(radius * 0.25f, 0f),
            centre + new Vector2(0f, radius), centre + new Vector2(-radius * 0.25f, 0f), Theme.U32(colour));
        d.AddQuadFilled(
            centre + new Vector2(-radius, 0f), centre + new Vector2(0f, -radius * 0.25f),
            centre + new Vector2(radius, 0f), centre + new Vector2(0f, radius * 0.25f), Theme.U32(colour));
    }
}
