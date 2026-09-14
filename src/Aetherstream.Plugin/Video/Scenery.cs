namespace Aetherstream.Plugin.Video;

/// <summary>The kind of country a scene is set in, from the zone's region.</summary>
internal enum Biome
{
    Forest,
    Coast,
    Desert,
    Snow,
    Highland,
    Marsh,
    Steppe,
    Night,
}

/// <summary>
/// Backdrops for the shows that go outside: a sky by the hour, hills, a ground band, and the
/// props that say where you are. Drawn from a seed, so the same place looks the same twice.
/// </summary>
internal static class Scenery
{
    /// <summary>Which biome a region reads as, from the game's region name.</summary>
    public static Biome BiomeOf(string region, string zone)
    {
        var r = region.ToLowerInvariant();
        var z = zone.ToLowerInvariant();
        if (z.Contains("sea of clouds") || z.Contains("churning mists") || z.Contains("azys lla"))
            return Biome.Highland;
        if (r.Contains("noscea") || z.Contains("ruby sea") || z.Contains("kholusia") || z.Contains("tempest"))
            return Biome.Coast;
        if (r.Contains("thanalan") || z.Contains("amh araeng") || z.Contains("sagolii") || z.Contains("thavnair") || z.Contains("shaaloani"))
            return Biome.Desert;
        if (r.Contains("coerthas") || z.Contains("garlemald") || z.Contains("mare lamentorum") || z.Contains("snow"))
            return Biome.Snow;
        if (r.Contains("shroud") || z.Contains("rak'tika") || z.Contains("kozama'uka") || z.Contains("yak t'el") || z.Contains("labyrinthos"))
            return Biome.Forest;
        if (r.Contains("othard") || z.Contains("azim steppe") || z.Contains("gyr abania") || z.Contains("peaks") || z.Contains("fringes") || z.Contains("lochs") || z.Contains("urqopacha"))
            return Biome.Steppe;
        if (z.Contains("dravanian forelands") || z.Contains("hinterlands") || z.Contains("lakeland") || z.Contains("il mheg") || z.Contains("elpis"))
            return Biome.Marsh;
        if (z.Contains("void") || z.Contains("heritage found") || z.Contains("living memory") || z.Contains("ultima thule"))
            return Biome.Night;
        return Biome.Forest;
    }

    private static readonly Dictionary<Biome, (uint SkyDay, uint SkyNight, uint Far, uint Near, uint Ground, uint GroundDark)> Palettes = new()
    {
        [Biome.Forest] = (Canvas.Rgb(0x9A, 0xD0, 0xF0), Canvas.Rgb(0x10, 0x18, 0x30), Canvas.Rgb(0x4A, 0x7A, 0x5A), Canvas.Rgb(0x2E, 0x5C, 0x3C), Canvas.Rgb(0x3C, 0x6E, 0x34), Canvas.Rgb(0x2A, 0x4E, 0x26)),
        [Biome.Coast] = (Canvas.Rgb(0xA8, 0xDC, 0xFF), Canvas.Rgb(0x10, 0x1C, 0x3A), Canvas.Rgb(0x3A, 0x7A, 0xA8), Canvas.Rgb(0x2A, 0x60, 0x90), Canvas.Rgb(0xE8, 0xD8, 0xA0), Canvas.Rgb(0xC8, 0xB4, 0x7A)),
        [Biome.Desert] = (Canvas.Rgb(0xF0, 0xD8, 0xA0), Canvas.Rgb(0x1A, 0x14, 0x2A), Canvas.Rgb(0xC0, 0x8A, 0x5A), Canvas.Rgb(0xA0, 0x6A, 0x40), Canvas.Rgb(0xE0, 0xB8, 0x70), Canvas.Rgb(0xC0, 0x98, 0x54)),
        [Biome.Snow] = (Canvas.Rgb(0xC8, 0xD8, 0xE8), Canvas.Rgb(0x0C, 0x14, 0x28), Canvas.Rgb(0x8A, 0x9A, 0xB0), Canvas.Rgb(0x6A, 0x7A, 0x90), Canvas.Rgb(0xF0, 0xF4, 0xF8), Canvas.Rgb(0xD0, 0xD8, 0xE4)),
        [Biome.Highland] = (Canvas.Rgb(0xB0, 0xD0, 0xF8), Canvas.Rgb(0x10, 0x18, 0x38), Canvas.Rgb(0x8A, 0xA0, 0xC0), Canvas.Rgb(0x60, 0x78, 0xA0), Canvas.Rgb(0x8A, 0xB0, 0x70), Canvas.Rgb(0x6A, 0x90, 0x58)),
        [Biome.Marsh] = (Canvas.Rgb(0xB8, 0xC8, 0xB0), Canvas.Rgb(0x10, 0x18, 0x24), Canvas.Rgb(0x5A, 0x74, 0x5A), Canvas.Rgb(0x3E, 0x56, 0x40), Canvas.Rgb(0x50, 0x68, 0x40), Canvas.Rgb(0x3A, 0x4E, 0x30)),
        [Biome.Steppe] = (Canvas.Rgb(0xC8, 0xDC, 0xF0), Canvas.Rgb(0x14, 0x18, 0x30), Canvas.Rgb(0x9A, 0x8A, 0x6A), Canvas.Rgb(0x7A, 0x6A, 0x50), Canvas.Rgb(0xB0, 0xA0, 0x60), Canvas.Rgb(0x90, 0x80, 0x48)),
        [Biome.Night] = (Canvas.Rgb(0x2A, 0x20, 0x48), Canvas.Rgb(0x08, 0x06, 0x14), Canvas.Rgb(0x3A, 0x2E, 0x58), Canvas.Rgb(0x28, 0x1E, 0x40), Canvas.Rgb(0x30, 0x28, 0x44), Canvas.Rgb(0x20, 0x1A, 0x30)),
    };

    /// <summary>
    /// Paints a place: sky, two ranges of hills, ground from <paramref name="horizon"/> down to
    /// <paramref name="bottom"/>, and a scatter of props. <paramref name="daylight"/> is 0 at
    /// night and 1 at noon; <paramref name="scroll"/> slides the hills for a slow pan.
    /// </summary>
    public static void Paint(Span<uint> span, int width, int top, int horizon, int bottom, Biome biome, float daylight, int seed, double scroll)
    {
        var p = Palettes[biome];
        var sky = Canvas.Lerp(p.SkyNight, p.SkyDay, daylight);
        var skyLow = Canvas.Lerp(sky, Canvas.Rgb(0xFF, 0xE0, 0xB0), daylight * 0.35f);

        // The sky: a gradient, and stars when it is dark.
        for (var y = top; y < horizon; y++)
        {
            var t = (float)(y - top) / Math.Max(1, horizon - top);
            Canvas.Fill(span, 0, y, width, 1, Canvas.Lerp(sky, skyLow, t));
        }

        if (daylight < 0.35f)
        {
            var rng = (uint)seed * 2654435761u | 1u;
            for (var i = 0; i < 60; i++)
            {
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                var sx = (int)(rng % (uint)width);
                var sy = top + (int)((rng >> 8) % (uint)Math.Max(1, (horizon - top) * 3 / 4));
                Canvas.Fill(span, sx, sy, 2, 2, Canvas.Lerp(sky, Canvas.White, 1f - (daylight * 2f)));
            }
        }

        // The sun or the moon.
        var orbX = width - 200;
        var orbY = top + 60 + (int)((1f - daylight) * 40);
        if (daylight > 0.3f)
            Canvas.Disc(span, orbX, orbY, 26, Canvas.Lerp(Canvas.Rgb(0xFF, 0xB0, 0x60), Canvas.Rgb(0xFF, 0xF0, 0xC0), daylight));
        else
            Canvas.Disc(span, orbX, orbY, 18, Canvas.Rgb(0xE8, 0xEC, 0xF0));

        // Two ranges of hills, the far one paler, both sliding with the pan.
        Hills(span, width, horizon, p.Far, seed * 3, 0.006, 70, scroll * 0.3);
        Hills(span, width, horizon, p.Near, seed * 7, 0.011, 44, scroll * 0.6);

        // The ground, darker toward the bottom.
        for (var y = horizon; y < bottom; y++)
        {
            var t = (float)(y - horizon) / Math.Max(1, bottom - horizon);
            Canvas.Fill(span, 0, y, width, 1, Canvas.Lerp(p.Ground, p.GroundDark, t));
        }

        // Props along the ground line, by biome.
        var prng = (uint)(seed * 11) | 1u;
        for (var i = 0; i < 9; i++)
        {
            prng ^= prng << 13; prng ^= prng >> 17; prng ^= prng << 5;
            // Signed on purpose: an unsigned remainder minus a constant stays unsigned and wraps
            // to four billion, which the int cast then saturates — and a prop at the far edge of
            // the integers sends the line drawer off for a walk it never comes back from.
            var px = (int)(prng % (uint)(width + 200)) - 100 - (int)((scroll * 0.9) % (width + 200));
            if (px < -100)
                px += width + 200;
            var scale = 1 + (int)((prng >> 8) % 3);
            var py = horizon - 6 + (int)((prng >> 12) % 24);
            Prop(span, biome, px, py, scale, p.GroundDark);
        }

        // A little darkness at the very bottom so figures stand on something.
        Canvas.Fill(span, 0, bottom - 6, width, 6, p.GroundDark);
    }

    private static void Hills(Span<uint> span, int width, int horizon, uint colour, int seed, double freq, int height, double shift)
    {
        for (var x = 0; x < width; x++)
        {
            var wx = x + shift;
            var h = (Math.Sin((wx * freq) + seed) * 0.5) + (Math.Sin((wx * freq * 2.3) + (seed * 1.7)) * 0.3) + (Math.Sin((wx * freq * 5.1) + (seed * 0.4)) * 0.2);
            var top = horizon - (int)(((h + 1.0) / 2.0) * height) - 4;
            Canvas.Fill(span, x, top, 1, horizon - top, colour);
        }
    }

    private static void Prop(Span<uint> span, Biome biome, int x, int y, int scale, uint dark)
    {
        switch (biome)
        {
            case Biome.Forest:
            case Biome.Marsh:
                // A tree: trunk and a round crown.
                Canvas.Fill(span, x - (2 * scale), y - (18 * scale), 4 * scale, 18 * scale, Canvas.Rgb(0x5A, 0x3C, 0x22));
                Canvas.Disc(span, x, y - (22 * scale), 10 * scale, biome == Biome.Marsh ? Canvas.Rgb(0x4E, 0x6E, 0x48) : Canvas.Rgb(0x2E, 0x6A, 0x36));
                Canvas.Disc(span, x - (4 * scale), y - (18 * scale), 7 * scale, biome == Biome.Marsh ? Canvas.Rgb(0x5A, 0x7A, 0x50) : Canvas.Rgb(0x3A, 0x7A, 0x40));
                break;
            case Biome.Desert:
                // A cactus with arms.
                Canvas.Fill(span, x - (2 * scale), y - (16 * scale), 4 * scale, 16 * scale, Canvas.Rgb(0x4A, 0x8A, 0x4A));
                Canvas.Fill(span, x - (7 * scale), y - (12 * scale), 5 * scale, 2 * scale, Canvas.Rgb(0x4A, 0x8A, 0x4A));
                Canvas.Fill(span, x - (7 * scale), y - (16 * scale), 2 * scale, 5 * scale, Canvas.Rgb(0x4A, 0x8A, 0x4A));
                Canvas.Fill(span, x + (2 * scale), y - (9 * scale), 5 * scale, 2 * scale, Canvas.Rgb(0x4A, 0x8A, 0x4A));
                Canvas.Fill(span, x + (5 * scale), y - (13 * scale), 2 * scale, 5 * scale, Canvas.Rgb(0x4A, 0x8A, 0x4A));
                break;
            case Biome.Snow:
                // A fir under snow.
                for (var t = 0; t < 3; t++)
                {
                    var w = (10 - (t * 2)) * scale;
                    var ty = y - (6 * scale) - (t * 6 * scale);
                    for (var r = 0; r < 6 * scale; r++)
                        Canvas.Fill(span, x - (w * r / (6 * scale)), ty - r, (2 * w * r / (6 * scale)) + 1, 1, Canvas.Rgb(0x2A, 0x4A, 0x3A));
                    Canvas.Fill(span, x - (w / 2), ty - (6 * scale), w, 2, Canvas.White);
                }

                break;
            case Biome.Coast:
                // A palm: a leaning trunk and fronds.
                for (var i = 0; i < 16 * scale; i++)
                    Canvas.Fill(span, x + (i / 4), y - i, 3 * scale / 2, 1, Canvas.Rgb(0x8A, 0x6A, 0x3A));
                for (var f = -2; f <= 2; f++)
                    Canvas.Line(span, x + (4 * scale), y - (16 * scale), x + (4 * scale) + (f * 7 * scale), y - (16 * scale) + (Math.Abs(f) * 3 * scale) - (2 * scale), Canvas.Rgb(0x3A, 0x8A, 0x4A));
                break;
            case Biome.Highland:
            case Biome.Steppe:
                // A rock, and a tuft.
                Canvas.Disc(span, x, y - (3 * scale), 6 * scale, dark);
                Canvas.Disc(span, x - (2 * scale), y - (5 * scale), 4 * scale, Canvas.Lerp(dark, Canvas.White, 0.2f));
                Canvas.Line(span, x + (9 * scale), y, x + (11 * scale), y - (6 * scale), Canvas.Rgb(0xA0, 0xA0, 0x60));
                Canvas.Line(span, x + (11 * scale), y, x + (12 * scale), y - (7 * scale), Canvas.Rgb(0xA0, 0xA0, 0x60));
                break;
            default:
                // A crystal spar.
                Canvas.Line(span, x, y, x + (3 * scale), y - (14 * scale), Canvas.Rgb(0x8A, 0x70, 0xC0));
                Canvas.Line(span, x + (6 * scale), y, x + (3 * scale), y - (14 * scale), Canvas.Rgb(0xB0, 0x98, 0xE0));
                Canvas.Line(span, x, y, x + (6 * scale), y, Canvas.Rgb(0x8A, 0x70, 0xC0));
                break;
        }
    }
}
