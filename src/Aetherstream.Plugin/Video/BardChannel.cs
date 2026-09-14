namespace Aetherstream.Plugin.Video;

/// <summary>
/// The Tonberry's Lantern: the tales of the Twelve, one a time, told from a chair by the fire
/// by a tonberry with the book on his knee and his lantern in the other hand. Each tale is six pages; each page is read from the chair and then shown as a
/// plate in a shadow play: a backlit paper screen with the page's own scene on it, the country
/// and the mortals in cut paper and the god in colour as the game draws them, doing what the
/// words say. On a
/// schedule from the clock, so everyone hears the same tale at the same time.
/// </summary>
/// <summary>One of the Twelve as a sprite made from their render: frame-format pixels, alpha in the top byte. The Traders have two.</summary>
internal sealed record DeitySprite(int Deity, string Name, int Width, int Height, uint[] Pixels);

internal sealed class BardChannel(BitmapFont font, Func<IReadOnlyList<DeitySprite>> deities) : IFrameChannel
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
        var (name, epithet, _, _) = TwelveTales.Of(tale.Deity);

        if (into < TitlesFor)
        {
            this.DrawParlour(span, seconds, HostSprites.Bard.Wave, all);
            Dim(span, 0.4f);
            const string Show = "THE TONBERRY'S LANTERN";
            var tw = font.Measure(Show, 2) + 64;
            Canvas.Fill(span, (W - tw) / 2, 150, tw, 112, Velvet);
            Canvas.Fill(span, (W - tw) / 2, 150, tw, 6, Gold);
            Canvas.Fill(span, (W - tw) / 2, 256, tw, 6, Gold);
            font.Draw(span, W, Show, (W - font.Measure(Show, 2)) / 2, 166, Cream, 2, all);
            const string With = "TALES OF THE TWELVE, TOLD BY LEVARR BURTONBERRY";
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
            var text = tale.Pages[page].Text;

            if (inPage < ReadFor)
            {
                this.DrawParlour(span, seconds, inPage < 0.8 ? HostSprites.Bard.Turn : (int)inPage % 3 == 2 ? HostSprites.Bard.Gesture : HostSprites.Bard.Read, all);
                this.DrawCaption(span, $"PAGE {page + 1}", text, all);
            }
            else
            {
                this.DrawPlate(target, tale, page, seconds, now, all);
                this.DrawCaption(span, $"PAGE {page + 1}", text, all);
            }
        }
        else
        {
            var t = into - TitlesFor - (Pages * (ReadFor + PictureFor));
            this.DrawParlour(span, seconds, t < 3.0 ? HostSprites.Bard.Gesture : HostSprites.Bard.Wave, all);
            var (nextName, _, _, _) = TwelveTales.Of(next.Deity);
            var line = t < 3.5
                ? $"And that is how the old ones tell it, of {name}. Was it so? Every word, kupo... no, that is the other one. But you don't have to take my word for it."
                : $"Next time: {next.Title}, a tale of {nextName}. Bring a blanket.";
            this.DrawCaption(span, "THE END", line, all);
        }

        Canvas.Fill(span, 0, 676, W, 4, Ink);
        Canvas.Fill(span, 0, 676, (int)(W * (into / EpisodeFor)), 4, Gold);
        Canvas.Fill(span, 0, 680, W, 40, Ink);
        font.Draw(span, W, "THE TONBERRY'S LANTERN", 24, 680, Gold, 1, all);
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

    private void DrawPlate(uint[] target, TwelveTales.Tale tale, int page, double seconds, DateTime now, in BitmapFont.Clip all)
    {
        var span = target.AsSpan();
        // A shadow play with the god in colour: the paper screen lit from behind, the page's own
        // scene drawn on it — the country, the props, the mortals in cut paper and the god as the
        // game draws them, doing what the words say — in a wooden frame.
        var (text, scene) = tale.Pages[page];
        var into = seconds % PictureFor;
        var flicker = 0.92f + (0.08f * (float)Math.Sin(seconds * 11.0) * (float)Math.Abs(Math.Sin(seconds * 3.1)));
        var paper = Canvas.Lerp(Canvas.Rgb(0xF4, 0xD8, 0xA0), Canvas.Rgb(0xFF, 0xEC, 0xC0), flicker);
        var paperEdge = Canvas.Rgb(0xC8, 0xA0, 0x60);
        const int Top = 60, Bottom = 540, GroundY = TaleScenes.Ground;

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

        var ink = Canvas.Rgb(0x14, 0x10, 0x0C);
        var (name, _, _, element) = TwelveTales.Of(tale.Deity);

        // The ground band, always; the scene draws everything on and above it.
        Canvas.Fill(span, 0, GroundY, W, Bottom - GroundY, ink);

        var gods = deities().Where(d => d.Deity == tale.Deity).ToList();
        var others = tale.Other > 0 ? deities().Where(d => d.Deity == tale.Other).ToList() : [];
        const int Scale = 2;

        void DrawGods(List<DeitySprite> list, int x, int y, bool flip, float halo)
        {
            if (list.Count == 0)
                return;

            var span = target.AsSpan();

            var h = list.Max(g => g.Height) * Scale;
            var w = list.Sum(g => g.Width * Scale) + ((list.Count - 1) * 20);
            if (halo > 0f)
                Canvas.Disc(span, x + (w / 2), y + (h / 2), (int)(h * 0.36f * halo) + 10, Canvas.Lerp(paper, Canvas.White, Math.Clamp(0.22f * halo, 0f, 0.5f)));
            foreach (var g in list)
            {
                DrawSprite(span, g, x, y + (h - (g.Height * Scale)), Scale, flip);
                x += (g.Width * Scale) + 20;
            }
        }

        var ctx = new TaleScenes.Context
        {
            Paper = paper,
            Ink = ink,
            Seconds = seconds,
            Into = into,
            Element = element,
            God = (x, y, flip, halo) => DrawGods(gods, x, y, flip, halo),
            Other = (x, y, flip, halo) => DrawGods(others, x, y, flip, halo),
            GodWidth = gods.Count > 0 ? gods.Sum(g => g.Width * Scale) + ((gods.Count - 1) * 20) : 200,
            GodHeight = gods.Count > 0 ? gods.Max(g => g.Height) * Scale : 288,
            OtherWidth = others.Count > 0 ? others.Sum(g => g.Width * Scale) + ((others.Count - 1) * 20) : 200,
            OtherHeight = others.Count > 0 ? others.Max(g => g.Height) * Scale : 288,
        };
        TaleScenes.Draw(span, scene, ctx);

        Canvas.Fill(span, 0, Top - 12, W, 12, WoodDark);
        Canvas.Fill(span, 0, Bottom, W, 12, WoodDark);
        Canvas.Fill(span, 0, Top, 12, Bottom - Top, WoodDark);
        Canvas.Fill(span, W - 12, Top, 12, Bottom - Top, WoodDark);
        var caption = Canvas.Cut($"OF {name.ToUpperInvariant()}", 30);
        font.Draw(span, W, caption, W - 30 - font.Measure(caption), 10, Cream, 1, all);
        var plate = $"PLATE {page + 1}";
        font.Draw(span, W, plate, 30, 10, Cream, 1, all);
    }



    /// <summary>A sprite scaled up whole, its transparent cells left alone.</summary>
    /// <summary>A sprite scaled up whole, its transparent cells left alone, mirrored if asked.</summary>
    private static void DrawSprite(Span<uint> span, DeitySprite sprite, int x, int y, int scale, bool flip)
    {
        for (var sy = 0; sy < sprite.Height; sy++)
        {
            for (var sx = 0; sx < sprite.Width; sx++)
            {
                var p = sprite.Pixels[(sy * sprite.Width) + (flip ? sprite.Width - 1 - sx : sx)];
                if (p >> 24 == 0)
                    continue;

                Canvas.Fill(span, x + (sx * scale), y + (sy * scale), scale, scale, p | 0xFF000000u);
            }
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

        // The teller: a tonberry in the high-backed chair, lit by the fire from the right. Green
        // skin, a brown hooded robe, the two yellow eyes that never blink, the book on the lap, and
        // the lantern held up in the far hand, its glass the brightest thing on the left.
        var rim = Canvas.Lerp(glow, Canvas.Rgb(0xFF, 0xD0, 0x80), 0.5f);
        var chairX = 470;
        Canvas.Fill(span, chairX, 250, 180, 330, ink);
        Canvas.Fill(span, chairX + 176, 250, 6, 330, rim);
        Canvas.Fill(span, chairX - 30, 440, 240, 20, ink);
        Canvas.Fill(span, chairX - 30, 460, 16, 100, ink);
        Canvas.Fill(span, chairX + 194, 460, 16, 100, ink);

        var bob = (int)(Math.Sin(seconds * 1.1) * 3);
        var headX = chairX + 100;
        var headY = 330 + bob;
        var skin = Canvas.Lerp(Canvas.Rgb(0x4A, 0x8A, 0x3E), glow, 0.25f);
        var skinLit = Canvas.Lerp(Canvas.Rgb(0x6A, 0xAA, 0x52), rim, 0.35f);
        var robe = Canvas.Lerp(Canvas.Rgb(0x5A, 0x3C, 0x22), glow, 0.2f);
        var robeLit = Canvas.Lerp(Canvas.Rgb(0x7A, 0x54, 0x30), rim, 0.3f);

        // The robe: a hood over the head, a body to the seat, lit on the fire's side.
        Canvas.Fill(span, headX - 62, headY + 20, 124, 150, robe);
        Canvas.Fill(span, headX + 30, headY + 20, 32, 150, robeLit);
        Canvas.Disc(span, headX, headY - 6, 64, robe);
        Canvas.Disc(span, headX + 22, headY - 14, 42, robeLit);

        // The face inside the hood: green, round, the eyes glowing.
        Canvas.Disc(span, headX, headY + 8, 46, skin);
        Canvas.Disc(span, headX + 16, headY + 4, 30, skinLit);
        Canvas.Disc(span, headX - 16, headY + 4, 9, Canvas.Rgb(0xFF, 0xE0, 0x60));
        Canvas.Disc(span, headX + 16, headY + 4, 9, Canvas.Rgb(0xFF, 0xE8, 0x80));
        var blink = (int)(seconds * 0.5) % 7 == 3 && seconds % 2.0 < 0.15;
        if (blink)
        {
            Canvas.Fill(span, headX - 26, headY + 2, 20, 4, skin);
            Canvas.Fill(span, headX + 6, headY + 2, 20, 4, skin);
        }
        else
        {
            Canvas.Disc(span, headX - 16, headY + 4, 3, Canvas.Rgb(0x2A, 0x1A, 0x10));
            Canvas.Disc(span, headX + 16, headY + 4, 3, Canvas.Rgb(0x2A, 0x1A, 0x10));
        }

        Canvas.Fill(span, headX - 8, headY + 26, 16, 3, Canvas.Rgb(0x2A, 0x3A, 0x1E));

        // The book on the knee, and the page turning; the near hand on it.
        var page = action == HostSprites.Bard.Turn ? (int)((seconds * 3.0) % 1.0 * 30) : 0;
        Canvas.Fill(span, headX - 70, 470, 140, 14, Canvas.Lerp(rim, Canvas.White, 0.3f));
        Canvas.Fill(span, headX - 70 + page, 456, 70 - page, 16, Canvas.Lerp(rim, Canvas.White, 0.5f));
        Canvas.Disc(span, headX + 30, 462, 12, skinLit);

        // The lantern in the far hand: raised when he gestures.
        var raise = action is HostSprites.Bard.Gesture or HostSprites.Bard.Wave ? (int)(Math.Abs(Math.Sin(seconds * 1.6)) * 60) : 0;
        var lx = chairX - 40;
        var ly = 420 - raise;
        Canvas.Disc(span, lx + 30, ly + 30, 12, skin);
        Canvas.Fill(span, lx - 3, ly - 40, 6, 32, ink);
        Canvas.Disc(span, lx, ly + 24, 44, Canvas.Lerp(glow, Canvas.Rgb(0xFF, 0xD8, 0x80), 0.3f));
        Canvas.Fill(span, lx - 18, ly, 36, 48, Canvas.Rgb(0xFF, 0xC8, 0x60));
        Canvas.Fill(span, lx - 22, ly - 4, 44, 6, ink);
        Canvas.Fill(span, lx - 22, ly + 46, 44, 6, ink);
        Canvas.Fill(span, lx - 22, ly, 4, 48, ink);
        Canvas.Fill(span, lx + 18, ly, 4, 48, ink);
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
