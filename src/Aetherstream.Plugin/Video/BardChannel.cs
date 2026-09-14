namespace Aetherstream.Plugin.Video;

/// <summary>
/// The story hour: the tales of the Twelve, one a time, told from a chair by the fire. The
/// teller is a silhouette against the firelight with the book on his knee and a lantern in the
/// other hand. Each tale is six pages; each page is read from the chair and then shown as a
/// plate in a shadow play: a backlit paper screen with the god's symbol cut into the sky over
/// the country the tale is set in, and the mortals of the tale as small cutouts below. On a
/// schedule from the clock, so everyone hears the same tale at the same time.
/// </summary>
internal sealed class BardChannel(BitmapFont font) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;

    private const double TitlesFor = 6.0;
    private const double ReadFor = 7.0;
    private const double PictureFor = 8.0;
    private const int Pages = 6;
    private const double OutroFor = 7.0;
    private const double EpisodeFor = TitlesFor + (Pages * (ReadFor + PictureFor)) + OutroFor;

    private static readonly uint Velvet = Canvas.Rgb(0x3A, 0x22, 0x44);
    private static readonly uint Wall = Canvas.Rgb(0x2C, 0x1E, 0x1A);
    private static readonly uint Wood = Canvas.Rgb(0x5E, 0x3C, 0x1A);
    private static readonly uint WoodDark = Canvas.Rgb(0x3A, 0x24, 0x10);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Gold = Canvas.Rgb(0xE0, 0xB8, 0x4A);
    private static readonly uint Ink = Canvas.Rgb(0x1E, 0x16, 0x12);

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var episode = (int)((unix - 1_700_000_000) / (long)EpisodeFor);
        var into = (unix - 1_700_000_000) % (long)EpisodeFor;

        // The tales come round in a shuffled order that repeats: every god gets a turn before
        // any gets a second, and no one waits through the same tale twice in a row.
        var tale = TaleFor(episode);
        var next = TaleFor(episode + 1);
        var (name, epithet, _) = TwelveTales.Of(tale.Deity);

        if (into < TitlesFor)
        {
            this.DrawParlour(span, seconds, HostSprites.Bard.Wave, all);
            Dim(span, 0.4f);
            const string Show = "THE BARD'S HOUR";
            var tw = font.Measure(Show, 2) + 64;
            Canvas.Fill(span, (W - tw) / 2, 150, tw, 112, Velvet);
            Canvas.Fill(span, (W - tw) / 2, 150, tw, 6, Gold);
            Canvas.Fill(span, (W - tw) / 2, 256, tw, 6, Gold);
            font.Draw(span, W, Show, (W - font.Measure(Show, 2)) / 2, 166, Cream, 2, all);
            const string With = "TALES OF THE TWELVE, WITH LEVARR BURTONBERRY";
            font.Draw(span, W, With, (W - font.Measure(With)) / 2, 300, Gold, 1, all);
            if (into > 2.5)
            {
                var title = Canvas.Cut($"TONIGHT: {tale.Title}", font.Fit(W - 100));
                font.Draw(span, W, title, (W - font.Measure(title)) / 2, 380, Cream, 1, all);
                var whom = Canvas.Cut($"OF {name.ToUpperInvariant()}, {epithet.ToUpperInvariant()}", font.Fit(W - 100));
                font.Draw(span, W, whom, (W - font.Measure(whom)) / 2, 424, Canvas.Rgb(0xC8, 0xB0, 0x90), 1, all);
            }
        }
        else if (into < TitlesFor + (Pages * (ReadFor + PictureFor)))
        {
            var t = into - TitlesFor;
            var page = (int)(t / (ReadFor + PictureFor));
            var inPage = t % (ReadFor + PictureFor);
            var text = tale.Pages[page];

            if (inPage < ReadFor)
            {
                this.DrawParlour(span, seconds, inPage < 0.8 ? HostSprites.Bard.Turn : (int)inPage % 3 == 2 ? HostSprites.Bard.Gesture : HostSprites.Bard.Read, all);
                this.DrawCaption(span, $"PAGE {page + 1}", text, all);
            }
            else
            {
                this.DrawPlate(span, tale, page, seconds, now, all);
                this.DrawCaption(span, $"PAGE {page + 1}", text, all);
            }
        }
        else
        {
            var t = into - TitlesFor - (Pages * (ReadFor + PictureFor));
            this.DrawParlour(span, seconds, t < 3.0 ? HostSprites.Bard.Gesture : HostSprites.Bard.Wave, all);
            var (nextName, _, _) = TwelveTales.Of(next.Deity);
            var line = t < 3.5
                ? $"And that is how the old ones tell it, of {name}. Was it so? Every word. But you don't have to take my word for it."
                : $"Next time: {next.Title}, a tale of {nextName}. Bring a blanket.";
            this.DrawCaption(span, "THE END", line, all);
        }

        Canvas.Fill(span, 0, 676, W, 4, Ink);
        Canvas.Fill(span, 0, 676, (int)(W * (into / EpisodeFor)), 4, Gold);
        Canvas.Fill(span, 0, 680, W, 40, Ink);
        font.Draw(span, W, "AETHERSTREAM STORIES", 24, 680, Gold, 1, all);
        var right = Canvas.Cut($"{tale.Title}  /  {name.ToUpperInvariant()}", 52);
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Cream, 1, all);
    }

    /// <summary>The tale for an episode: a shuffle of all of them per round, seeded by the round.</summary>
    private static TwelveTales.Tale TaleFor(int episode)
    {
        var all = TwelveTales.All;
        var round = episode / all.Length;
        var slot = episode % all.Length;
        var order = Enumerable.Range(0, all.Length).OrderBy(i => Pick(round, i + 1, 1_000_000)).ToArray();
        return all[order[slot]];
    }

    // -- the plates -----------------------------------------------------------------------------------------

    private void DrawPlate(Span<uint> span, TwelveTales.Tale tale, int page, double seconds, DateTime now, in BitmapFont.Clip all)
    {
        // A shadow play: a paper screen lit from behind, warmest in the middle and flickering
        // like a candle, in a wooden frame; the god's symbol cut into the sky; the country and
        // the mortals below it as black cutouts.
        var flicker = 0.92f + (0.08f * (float)Math.Sin(seconds * 11.0) * (float)Math.Abs(Math.Sin(seconds * 3.1)));
        var paper = Canvas.Lerp(Canvas.Rgb(0xF4, 0xD8, 0xA0), Canvas.Rgb(0xFF, 0xEC, 0xC0), flicker);
        var paperEdge = Canvas.Rgb(0xC8, 0xA0, 0x60);
        const int Top = 60, Bottom = 540, GroundY = 470;

        Canvas.Fill(span, 0, 0, W, H, Wood);
        for (var y = Top; y < Bottom; y++)
        {
            for (var x = 0; x < W; x += 8)
            {
                var dx = (x - 640) / 640f;
                var dy = (y - 300) / 300f;
                var d = MathF.Sqrt((dx * dx) + (dy * dy));
                Canvas.Fill(span, x, y, 8, 1, Canvas.Lerp(paper, paperEdge, Math.Clamp(d * 0.9f, 0f, 1f)));
            }
        }

        if (tale.Where == Biome.Night)
            Dim(span, 0.2f);

        // The god's symbol, large in the sky, with a brighter paper halo behind it.
        var ink = Canvas.Rgb(0x14, 0x10, 0x0C);
        var (name, _, _) = TwelveTales.Of(tale.Deity);
        var symbolX = 900;
        var symbolY = 230;
        var breathe = (int)(Math.Sin(seconds * 0.8) * 4);
        Canvas.Disc(span, symbolX, symbolY + breathe, 150, Canvas.Lerp(paper, Canvas.White, 0.35f));
        TwelveTales.DrawSymbol(span, W, tale.Deity, symbolX, symbolY + breathe, 200, ink, seconds);

        // The country, in cut paper.
        var pan = seconds * 3.0;
        ShadowHills(span, GroundY, ink, (page * 17) + 3, 0.006, 70, pan * 0.3);
        ShadowHills(span, GroundY, ink, (page * 17) + 9, 0.011, 44, pan * 0.6);
        Canvas.Fill(span, 0, GroundY, W, Bottom - GroundY, ink);
        ShadowProps(span, tale.Where, GroundY, ink, (page * 17) + 5, pan);

        // The mortals: one on the first pages, more as the tale gathers people, all looking up.
        var mortals = page switch { 0 => 1, 1 => 1, 2 => 2, 3 => 3, 4 => 3, _ => 4 };
        for (var m = 0; m < mortals; m++)
        {
            var mx = 260 + (m * 110) + (int)(Math.Sin((seconds * 0.7) + m) * 3);
            var scale = 0.55f + (0.1f * ((m + page) % 3));
            ShadowMortal(span, mx, GroundY, scale, ink, m % 2 == 0);
        }

        Canvas.Fill(span, 0, Top - 12, W, 12, WoodDark);
        Canvas.Fill(span, 0, Bottom, W, 12, WoodDark);
        Canvas.Fill(span, 0, Top, 12, Bottom - Top, WoodDark);
        Canvas.Fill(span, W - 12, Top, 12, Bottom - Top, WoodDark);
        var caption = Canvas.Cut($"OF {name.ToUpperInvariant()}", 30);
        font.Draw(span, W, caption, W - 30 - font.Measure(caption), 10, Cream, 1, all);
        var plate = $"PLATE {page + 1}";
        font.Draw(span, W, plate, 30, 10, Cream, 1, all);
    }

    /// <summary>A mortal as a cutout: a hooded head, a cloak to the ground, a staff or a raised hand.</summary>
    private static void ShadowMortal(Span<uint> span, int x, int ground, float scale, uint ink, bool staff)
    {
        var h = (int)(200 * scale);
        var top = ground - h;
        var cx = x + (int)(40 * scale);
        var headR = (int)(22 * scale);
        var headY = top + headR + (int)(10 * scale);
        Canvas.Disc(span, cx, headY, headR, ink);

        var shoulderY = headY + headR - (int)(4 * scale);
        for (var y = shoulderY; y < ground; y++)
        {
            var t = (float)(y - shoulderY) / Math.Max(1, ground - shoulderY);
            var half = (int)((26 + (t * 22)) * scale);
            Canvas.Fill(span, cx - half, y, half * 2, 1, ink);
        }

        if (staff)
        {
            var sx = cx - (int)(40 * scale);
            Canvas.Fill(span, sx, top - (int)(10 * scale), (int)(5 * scale), ground - top + (int)(10 * scale), ink);
            Canvas.Disc(span, sx + (int)(2 * scale), top - (int)(10 * scale), (int)(6 * scale), ink);
        }
        else
        {
            Canvas.Line(span, cx + (int)(26 * scale), shoulderY + (int)(30 * scale), cx + (int)(56 * scale), shoulderY - (int)(20 * scale), ink);
            Canvas.Line(span, cx + (int)(27 * scale), shoulderY + (int)(31 * scale), cx + (int)(57 * scale), shoulderY - (int)(19 * scale), ink);
            Canvas.Disc(span, cx + (int)(56 * scale), shoulderY - (int)(20 * scale), (int)(5 * scale), ink);
        }
    }

    private void DrawParlour(Span<uint> span, double seconds, HostSprites.Bard action, in BitmapFont.Clip all)
    {
        // The parlour by firelight: everything is a shape against the glow. The hearth on the
        // right throws the light; the teller sits in silhouette in the high-backed chair with the
        // book on the knee and the lantern up, and the bookcase is a dark wall of spines.
        var flicker = 0.85f + (0.15f * (float)Math.Sin(seconds * 9.0)) * (float)Math.Abs(Math.Sin(seconds * 2.3));
        var glow = Canvas.Lerp(Canvas.Rgb(0x6A, 0x3A, 0x1A), Canvas.Rgb(0xB8, 0x6A, 0x2A), flicker);
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x += 8)
            {
                var dx = (x - 1040) / 900f;
                var dy = (y - 430) / 520f;
                var d = MathF.Sqrt((dx * dx) + (dy * dy));
                var c = Canvas.Lerp(glow, Wall, Math.Clamp(d, 0f, 1f));
                Canvas.Fill(span, x, y, 8, 1, c);
            }
        }

        // The hearth: the opening, and the fire that lights the room.
        Canvas.Fill(span, 880, 240, 320, 320, Canvas.Lerp(Wall, Canvas.Black, 0.5f));
        Canvas.Fill(span, 910, 270, 260, 270, Canvas.Lerp(glow, Canvas.Rgb(0xFF, 0xC0, 0x60), 0.4f));
        for (var f = 0; f < 6; f++)
        {
            var fx = 950 + (f * 36) + (int)(Math.Sin((seconds * 5.0) + f) * 6);
            var fh = 70 + (int)(Math.Sin((seconds * 7.0) + (f * 1.3)) * 24);
            Canvas.Disc(span, fx, 520 - (fh / 2), 26, Canvas.Rgb(0xFF, 0x8A, 0x30));
            Canvas.Disc(span, fx, 520 - (fh / 2) - 10, 16, Canvas.Rgb(0xFF, 0xC8, 0x60));
            Canvas.Disc(span, fx, 520 - fh, 8, Canvas.Rgb(0xFF, 0xF0, 0xA0));
        }

        Canvas.Fill(span, 900, 520, 280, 24, Canvas.Lerp(Wall, Canvas.Black, 0.6f));

        // The bookcase in silhouette, spines as a ragged skyline.
        var ink = Canvas.Lerp(Wall, Canvas.Black, 0.75f);
        Canvas.Fill(span, 60, 80, 340, 480, ink);
        var rng = 7919u;
        for (var shelf = 0; shelf < 5; shelf++)
        {
            var sy = 160 + (shelf * 90);
            var bx = 70;
            while (bx < 390)
            {
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                var bw = 12 + (int)(rng % 14);
                var bh = 40 + (int)((rng >> 8) % 28);
                Canvas.Fill(span, bx, sy - bh, bw, bh, Canvas.Lerp(ink, glow, 0.08f + (0.06f * ((rng >> 16) % 3))));
                bx += bw + 2;
            }

            Canvas.Fill(span, 60, sy, 340, 6, Canvas.Lerp(ink, Canvas.Black, 0.5f));
        }

        // The floor and the rug, and the chair's shadow across them.
        Canvas.Fill(span, 0, 560, W, 120, Canvas.Lerp(Wall, Canvas.Black, 0.55f));
        Canvas.Fill(span, 380, 580, 520, 90, Canvas.Lerp(Canvas.Rgb(0x6A, 0x22, 0x22), Canvas.Black, 0.45f));

        // The teller: a silhouette, rim-lit on the fire's side.
        var rim = Canvas.Lerp(glow, Canvas.Rgb(0xFF, 0xD0, 0x80), 0.5f);
        var chairX = 470;
        Canvas.Fill(span, chairX, 250, 180, 330, ink);
        Canvas.Fill(span, chairX + 176, 250, 6, 330, rim);
        Canvas.Fill(span, chairX - 30, 440, 240, 20, ink);
        Canvas.Fill(span, chairX - 30, 460, 16, 100, ink);
        Canvas.Fill(span, chairX + 194, 460, 16, 100, ink);

        var bob = (int)(Math.Sin(seconds * 1.1) * 3);
        var headX = chairX + 100;
        var headY = 300 + bob;
        Canvas.Disc(span, headX, headY, 44, ink);
        Canvas.Disc(span, headX + 36, headY - 8, 8, rim);
        Canvas.Fill(span, headX - 60, headY + 30, 120, 130, ink);

        // The book on the knee, a pale shape the fire catches, and the page turning.
        var page = action == HostSprites.Bard.Turn ? (int)((seconds * 3.0) % 1.0 * 30) : 0;
        Canvas.Fill(span, headX - 70, 470, 140, 12, Canvas.Lerp(rim, Canvas.White, 0.3f));
        Canvas.Fill(span, headX - 70 + page, 456, 70 - page, 14, Canvas.Lerp(rim, Canvas.White, 0.5f));

        // The lantern in the far hand: raised when he gestures, its glass the brightest thing on the left.
        var raise = action is HostSprites.Bard.Gesture or HostSprites.Bard.Wave ? (int)(Math.Abs(Math.Sin(seconds * 1.6)) * 60) : 0;
        var lx = chairX - 40;
        var ly = 420 - raise;
        Canvas.Fill(span, lx - 30, ly - 10, 60, 8, ink);
        Canvas.Fill(span, lx - 3, ly - 40, 6, 32, ink);
        Canvas.Disc(span, lx, ly + 24, 40, Canvas.Lerp(glow, Canvas.Rgb(0xFF, 0xD8, 0x80), 0.25f));
        Canvas.Fill(span, lx - 18, ly, 36, 48, Canvas.Rgb(0xFF, 0xC8, 0x60));
        Canvas.Fill(span, lx - 22, ly - 4, 44, 6, ink);
        Canvas.Fill(span, lx - 22, ly + 46, 44, 6, ink);
        Canvas.Fill(span, lx - 22, ly, 4, 48, ink);
        Canvas.Fill(span, lx + 18, ly, 4, 48, ink);
    }

    private static void ShadowHills(Span<uint> span, int ground, uint ink, int seed, double freq, int height, double shift)
    {
        for (var x = 0; x < W; x++)
        {
            var wx = x + shift;
            var h = (Math.Sin((wx * freq) + seed) * 0.5) + (Math.Sin((wx * freq * 2.3) + (seed * 1.7)) * 0.3) + (Math.Sin((wx * freq * 5.1) + (seed * 0.4)) * 0.2);
            var top = ground - (int)(((h + 1.0) / 2.0) * height) - 4;
            Canvas.Fill(span, x, top, 1, ground - top, ink);
        }
    }

    private static void ShadowProps(Span<uint> span, Biome biome, int ground, uint ink, int seed, double scroll)
    {
        var rng = (uint)(seed * 11) | 1u;
        for (var i = 0; i < 7; i++)
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            var px = (int)((rng % (uint)(W + 200)) - 100 - (scroll * 0.9) % (W + 200));
            if (px < -100)
                px += W + 200;
            var scale = 2 + (int)((rng >> 8) % 2);
            switch (biome)
            {
                case Biome.Desert:
                    Canvas.Fill(span, px - (2 * scale), ground - (16 * scale), 4 * scale, 16 * scale, ink);
                    Canvas.Fill(span, px - (7 * scale), ground - (12 * scale), 5 * scale, 2 * scale, ink);
                    Canvas.Fill(span, px - (7 * scale), ground - (16 * scale), 2 * scale, 5 * scale, ink);
                    Canvas.Fill(span, px + (2 * scale), ground - (9 * scale), 5 * scale, 2 * scale, ink);
                    Canvas.Fill(span, px + (5 * scale), ground - (13 * scale), 2 * scale, 5 * scale, ink);
                    break;
                case Biome.Snow:
                    for (var t = 0; t < 3; t++)
                    {
                        var w = (10 - (t * 2)) * scale;
                        var ty = ground - (6 * scale) - (t * 6 * scale);
                        for (var r = 0; r < 6 * scale; r++)
                            Canvas.Fill(span, px - (w * r / (6 * scale)), ty - r, (2 * w * r / (6 * scale)) + 1, 1, ink);
                    }

                    break;
                case Biome.Coast:
                    for (var k = 0; k < 16 * scale; k++)
                        Canvas.Fill(span, px + (k / 4), ground - k, 3 * scale / 2, 1, ink);
                    for (var f = -2; f <= 2; f++)
                        Canvas.Line(span, px + (4 * scale), ground - (16 * scale), px + (4 * scale) + (f * 7 * scale), ground - (16 * scale) + (Math.Abs(f) * 3 * scale) - (2 * scale), ink);
                    break;
                case Biome.Highland:
                case Biome.Steppe:
                    Canvas.Disc(span, px, ground - (3 * scale), 6 * scale, ink);
                    break;
                default:
                    Canvas.Fill(span, px - (2 * scale), ground - (18 * scale), 4 * scale, 18 * scale, ink);
                    Canvas.Disc(span, px, ground - (22 * scale), 10 * scale, ink);
                    Canvas.Disc(span, px - (4 * scale), ground - (18 * scale), 7 * scale, ink);
                    break;
            }
        }
    }

    private void DrawCaption(Span<uint> span, string tab, string line, in BitmapFont.Clip all)
    {
        const int Top = 556;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - 40 - font.Measure(tab) - 48), 3);
        var height = 16 + (lines.Count * 40);
        var top = Math.Min(Top, 676 - height);
        Canvas.Fill(span, 40, top, W - 80, height, Velvet);
        Canvas.Fill(span, 40, top, W - 80, 4, Gold);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, top + 4, tabW, height - 4, Gold);
        font.Draw(span, W, tab, 56, top + 8, Ink, 1, all);
        var y = top + 8;
        foreach (var l in lines)
        {
            font.Draw(span, W, l, 40 + tabW + 16, y, Cream, 1, all);
            y += 40;
        }
    }

    private static void Dim(Span<uint> span, float amount)
    {
        for (var i = 0; i < span.Length; i++)
            span[i] = Canvas.Lerp(span[i], 0xFF000000u, amount);
    }

    private static int Pick(int episode, int salt, int count)
    {
        var x = (uint)((episode * 2654435761u) ^ (uint)(salt * 40503u));
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
