namespace Aetherstream.Plugin.Video;

/// <summary>
/// Small pictures of the weather, by its name, for the forecast and the sea news: a sun, a
/// cloud, rain, a bolt, flakes, and the odd ones the later expansions brought. Drawn in a box
/// of <c>size</c> pixels; anything unknown gets a star so it is still a picture.
/// </summary>
internal static class WeatherGlyphs
{
    private static readonly uint Sun = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint SunHot = Canvas.Rgb(0xFF, 0x8A, 0x4A);
    private static readonly uint Cloud = Canvas.Rgb(0xE8, 0xEC, 0xF4);
    private static readonly uint CloudDark = Canvas.Rgb(0x8A, 0x92, 0xA8);
    private static readonly uint Drop = Canvas.Rgb(0x6A, 0xB8, 0xFF);
    private static readonly uint Bolt = Canvas.Rgb(0xFF, 0xF0, 0x60);
    private static readonly uint Flake = Canvas.Rgb(0xF4, 0xF8, 0xFF);
    private static readonly uint Dust = Canvas.Rgb(0xD8, 0xB0, 0x70);
    private static readonly uint Violet = Canvas.Rgb(0xB0, 0x7A, 0xE8);
    private static readonly uint Mint = Canvas.Rgb(0x8A, 0xE8, 0xC0);

    public static void Draw(Span<uint> span, string weather, int x, int y, int size)
    {
        var w = weather.ToLowerInvariant();
        var cx = x + (size / 2);
        var cy = y + (size / 2);
        var r = size / 2;

        if (w.Contains("clear"))
        {
            SunAt(span, cx, cy, r * 6 / 10, Sun);
        }
        else if (w.Contains("fair"))
        {
            SunAt(span, cx - (r / 4), cy - (r / 4), r / 2, Sun);
            CloudAt(span, cx + (r / 5), cy + (r / 3), r / 2, Cloud);
        }
        else if (w.Contains("heat") || w.Contains("hot"))
        {
            SunAt(span, cx, cy, r * 6 / 10, SunHot);
        }
        else if (w.Contains("thunder") || w.Contains("levin") || w.Contains("electric"))
        {
            CloudAt(span, cx, cy - (r / 4), r * 7 / 10, CloudDark);
            Canvas.Line(span, cx + (r / 6), cy, cx - (r / 6), cy + (r / 3), Bolt);
            Canvas.Line(span, cx - (r / 6), cy + (r / 3), cx + (r / 4), cy + (r / 3), Bolt);
            Canvas.Line(span, cx + (r / 4), cy + (r / 3), cx - (r / 8), cy + (r * 3 / 4), Bolt);
        }
        else if (w.Contains("shower") || w.Contains("rain"))
        {
            CloudAt(span, cx, cy - (r / 4), r * 7 / 10, w.Contains("shower") ? CloudDark : Cloud);
            for (var i = -1; i <= 1; i++)
                Canvas.Line(span, cx + (i * r / 3), cy + (r / 4), cx + (i * r / 3) - (r / 8), cy + (r * 3 / 4), Drop);
        }
        else if (w.Contains("blizzard") || w.Contains("snow"))
        {
            if (w.Contains("blizzard"))
                CloudAt(span, cx, cy - (r / 3), r * 7 / 10, CloudDark);
            for (var i = -1; i <= 1; i++)
                FlakeAt(span, cx + (i * r / 2), cy + (r / 3) + (i * i * r / 6), r / 5, Flake);
        }
        else if (w.Contains("fog") || w.Contains("mist"))
        {
            for (var i = 0; i < 4; i++)
                Canvas.Fill(span, cx - r + (i % 2 * r / 4), cy - (r / 2) + (i * r / 3), r * 3 / 2, Math.Max(2, r / 8), Cloud);
        }
        else if (w.Contains("gale") || w.Contains("wind") || w.Contains("cyclone"))
        {
            for (var i = 0; i < 3; i++)
            {
                var yy = cy - (r / 2) + (i * r / 2);
                Canvas.Fill(span, cx - r + (i * r / 5), yy, r + (r / 2) - (i * r / 4), Math.Max(2, r / 8), Cloud);
                Canvas.Disc(span, cx + (r / 2) + (r / 4) - (i * r / 4), yy - (r / 8), r / 6, Cloud);
            }
        }
        else if (w.Contains("dust") || w.Contains("sand"))
        {
            for (var i = 0; i < 3; i++)
                Canvas.Fill(span, cx - r + (i * r / 3), cy - (r / 2) + (i * r / 2), r * 4 / 3, Math.Max(2, r / 8), Dust);
            Canvas.Disc(span, cx + (r / 3), cy - (r / 2), r / 5, Dust);
        }
        else if (w.Contains("gloom") || w.Contains("louring") || w.Contains("dark") || w.Contains("oppress") || w.Contains("storm cloud"))
        {
            CloudAt(span, cx, cy, r * 8 / 10, CloudDark);
        }
        else if (w.Contains("aurora") || w.Contains("umbral") || w.Contains("moon") || w.Contains("star"))
        {
            for (var i = 0; i < 3; i++)
                Canvas.Line(span, cx - r + (i * r / 4), cy + (r / 2) - (i * r / 3), cx + (r / 2) + (i * r / 4), cy - (r / 2) - (i * r / 6), i == 1 ? Mint : Violet);
        }
        else if (w.Contains("cloud"))
        {
            CloudAt(span, cx, cy, r * 8 / 10, Cloud);
        }
        else
        {
            // Something the later expansions brought: a star, in violet.
            Canvas.Line(span, cx, cy - r + 2, cx, cy + r - 2, Violet);
            Canvas.Line(span, cx - r + 2, cy, cx + r - 2, cy, Violet);
            Canvas.Line(span, cx - (r / 2), cy - (r / 2), cx + (r / 2), cy + (r / 2), Violet);
            Canvas.Line(span, cx - (r / 2), cy + (r / 2), cx + (r / 2), cy - (r / 2), Violet);
        }
    }

    private static void SunAt(Span<uint> span, int cx, int cy, int r, uint colour)
    {
        Canvas.Disc(span, cx, cy, r, colour);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4;
            Canvas.Line(span, cx + (int)(Math.Cos(a) * (r + 3)), cy + (int)(Math.Sin(a) * (r + 3)), cx + (int)(Math.Cos(a) * (r + 3 + (r / 2))), cy + (int)(Math.Sin(a) * (r + 3 + (r / 2))), colour);
        }
    }

    private static void CloudAt(Span<uint> span, int cx, int cy, int r, uint colour)
    {
        Canvas.Disc(span, cx - (r / 2), cy, r / 2, colour);
        Canvas.Disc(span, cx, cy - (r / 3), r * 6 / 10, colour);
        Canvas.Disc(span, cx + (r / 2), cy, r / 2, colour);
        Canvas.Fill(span, cx - (r / 2), cy, r, r / 2, colour);
    }

    private static void FlakeAt(Span<uint> span, int cx, int cy, int r, uint colour)
    {
        Canvas.Line(span, cx - r, cy, cx + r, cy, colour);
        Canvas.Line(span, cx, cy - r, cx, cy + r, colour);
        Canvas.Line(span, cx - (r * 2 / 3), cy - (r * 2 / 3), cx + (r * 2 / 3), cy + (r * 2 / 3), colour);
        Canvas.Line(span, cx - (r * 2 / 3), cy + (r * 2 / 3), cx + (r * 2 / 3), cy - (r * 2 / 3), colour);
    }
}
