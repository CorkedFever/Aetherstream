namespace Aetherstream.Plugin.Video;

/// <summary>
/// The Tonberry's Lantern: the tales of the Twelve, one a time, told from a chair by the fire
/// by a tonberry with the book on his knee and his lantern in the other hand. Each tale is six pages; each page is read from the chair and then shown as a
/// plate in a shadow play: a backlit paper screen, the country in cut paper, the mortals as
/// cutouts, and the god in colour as the game draws them, acting the page's beat: arriving,
/// working their element, striking, meeting another of the Twelve, blessing, departing. On a
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
                this.DrawPlate(span, tale, page, seconds, now, all);
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

    private void DrawPlate(Span<uint> span, TwelveTales.Tale tale, int page, double seconds, DateTime now, in BitmapFont.Clip all)
    {
        // A shadow play with one thing in colour: the god, acting the page's beat on the ground
        // among the cut-paper country and the cutout mortals. The screen is paper lit from behind,
        // warmest in the middle, flickering like a candle, in a wooden frame.
        var (text, beat) = tale.Pages[page];
        var into = seconds % PictureFor;
        var flash = beat == Beat.Strike && into is > 2.0 and < 2.35 ? 0.5f : 0f;
        var flicker = 0.92f + (0.08f * (float)Math.Sin(seconds * 11.0) * (float)Math.Abs(Math.Sin(seconds * 3.1))) + flash;
        var paper = Canvas.Lerp(Canvas.Rgb(0xF4, 0xD8, 0xA0), Canvas.Rgb(0xFF, 0xF4, 0xD8), Math.Clamp(flicker, 0f, 1f));
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

        // The ground shakes on a strike.
        var quake = beat == Beat.Strike && into is > 2.0 and < 2.8 ? (int)(Math.Sin(seconds * 60.0) * 6) : 0;

        var ink = Canvas.Rgb(0x14, 0x10, 0x0C);
        var (name, _, _, element) = TwelveTales.Of(tale.Deity);
        var pan = seconds * 3.0;
        ShadowHills(span, GroundY + quake, ink, (page * 17) + 3, 0.006, 70, pan * 0.3);
        ShadowHills(span, GroundY + quake, ink, (page * 17) + 9, 0.011, 44, pan * 0.6);
        Canvas.Fill(span, 0, GroundY + quake, W, Bottom - GroundY, ink);
        ShadowProps(span, tale.Where, GroundY + quake, ink, (page * 17) + 5, pan);

        // Where the god stands, and how, by the beat. The sprites face left, toward the mortals.
        var gods = deities().Where(d => d.Deity == tale.Deity).ToList();
        var others = tale.Other > 0 ? deities().Where(d => d.Deity == tale.Other).ToList() : [];
        const int Scale = 3;
        var godH = gods.Count > 0 ? gods.Max(g => g.Height) * Scale : 288;
        var godW = gods.Count > 0 ? gods.Sum(g => g.Width * Scale) + ((gods.Count - 1) * 20) : 200;
        var homeX = 780;
        var bob = (int)(Math.Sin(seconds * 2.0) * 4);
        int gx, gy;
        var halo = 1f;
        switch (beat)
        {
            case Beat.Arrive:
            {
                var t = Math.Clamp(into / 2.5, 0.0, 1.0);
                var eased = 1.0 - Math.Pow(1.0 - t, 3.0);
                gx = W + 40 - (int)((W + 40 - homeX) * eased);
                gy = GroundY - godH + (t < 1.0 ? (int)(Math.Abs(Math.Sin(into * 9.0)) * -10) : bob);
                halo = (float)t;
                break;
            }

            case Beat.Strike:
            {
                var lunge = into is > 1.6 and < 2.6 ? (int)(Math.Sin((into - 1.6) * Math.PI) * -140) : 0;
                gx = homeX + lunge;
                gy = GroundY - godH + (into is > 1.6 and < 2.6 ? -30 : bob);
                halo = into is > 2.0 and < 2.6 ? 1.6f : 1f;
                break;
            }

            case Beat.Depart:
            {
                var t = Math.Clamp((into - 3.0) / 4.0, 0.0, 1.0);
                gx = homeX + (int)(t * 200);
                gy = GroundY - godH - (int)(t * t * 700) + bob;
                halo = 1f + (float)t;
                break;
            }

            case Beat.Bless:
                gx = homeX;
                gy = GroundY - godH - 30 + (int)(Math.Sin(seconds * 1.2) * 10);
                halo = 1.3f + (0.3f * (float)Math.Sin(seconds * 3.0));
                break;
            default:
                gx = homeX;
                gy = GroundY - godH + bob;
                break;
        }

        // The halo, then the god. The Traders stand side by side.
        if (gods.Count > 0)
        {
            var haloR = (int)((godH / 2 + 30) * halo);
            Canvas.Disc(span, gx + (godW / 2), gy + (godH / 2), haloR, Canvas.Lerp(paper, Canvas.White, Math.Clamp(0.3f * halo, 0f, 0.7f)));
            var x = gx;
            foreach (var g in gods)
            {
                DrawSprite(span, g, x, gy + (godH - (g.Height * Scale)), Scale, flip: false);
                x += (g.Width * Scale) + 20;
            }
        }

        // The other god, on a meeting page, from the left, facing the first.
        if (beat == Beat.Meet && others.Count > 0)
        {
            var t = Math.Clamp(into / 2.5, 0.0, 1.0);
            var eased = 1.0 - Math.Pow(1.0 - t, 3.0);
            var oh = others.Max(o => o.Height) * Scale;
            var ow = others.Sum(o => o.Width * Scale) + ((others.Count - 1) * 20);
            var ox = -ow - 40 + (int)((300 + ow + 40) * eased);
            var oy = GroundY - oh + (t < 1.0 ? (int)(Math.Abs(Math.Sin(into * 9.0)) * -10) : (int)(Math.Sin((seconds * 2.0) + 1.0) * 4));
            Canvas.Disc(span, ox + (ow / 2), oy + (oh / 2), (oh / 2) + 30, Canvas.Lerp(paper, Canvas.White, 0.3f * (float)t));
            var x = ox;
            foreach (var o in others)
            {
                DrawSprite(span, o, x, oy + (oh - (o.Height * Scale)), Scale, flip: true);
                x += (o.Width * Scale) + 20;
            }
        }

        // The element at work: what the god's power looks like over the country.
        if (beat is Beat.Work or Beat.Strike or Beat.Bless)
            DrawElement(span, element, gx + (godW / 2), gy + (godH / 3), seconds, beat == Beat.Strike, ink, paper);

        // The mortals: cutouts on the left, bowing when blessed, flinching at a strike.
        var mortals = beat == Beat.Meet ? 0 : page switch { 0 => 1, 1 => 2, 2 => 2, 3 => 3, 4 => 3, _ => 4 };
        for (var m = 0; m < mortals; m++)
        {
            var mx = 180 + (m * 110) + (int)(Math.Sin((seconds * 0.7) + m) * 3);
            var scale = 0.55f + (0.1f * ((m + page) % 3));
            var bow = beat == Beat.Bless ? 0.8f : beat == Beat.Strike && into is > 2.0 and < 3.0 ? 0.9f : 1f;
            ShadowMortal(span, mx, GroundY + quake, scale * bow, ink, m % 2 == 0);
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

    /// <summary>The god's element made visible: flakes, drops, leaves, sparks, embers or dust, streaming from them.</summary>
    private static void DrawElement(Span<uint> span, string element, int cx, int cy, double seconds, bool hard, uint ink, uint paper)
    {
        var rng = 2463534242u;
        var count = hard ? 40 : 22;
        for (var i = 0; i < count; i++)
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            var phase = ((seconds * (hard ? 1.6 : 0.5)) + (i * 0.137)) % 1.0;
            var angle = ((rng % 360) * Math.PI / 180.0) + (seconds * 0.2);
            var reach = (hard ? 420 : 260) * phase;
            var x = cx + (int)(Math.Cos(angle) * reach);
            var y = cy + (int)(Math.Sin(angle) * reach * 0.6) + (element is "earth" or "water" ? (int)(phase * 120) : element == "fire" ? -(int)(phase * 120) : 0);
            var size = Math.Max(1, (int)((1.0 - phase) * (hard ? 9 : 6)));
            var colour = element switch
            {
                "ice" => Canvas.Rgb(0x9A, 0xD8, 0xFF),
                "water" => Canvas.Rgb(0x4A, 0x8A, 0xE0),
                "wind" => Canvas.Rgb(0x6A, 0xB0, 0x5A),
                "lightning" => Canvas.Rgb(0xC0, 0x8A, 0xFF),
                "fire" => Canvas.Rgb(0xFF, 0x8A, 0x2E),
                _ => Canvas.Rgb(0x9A, 0x6A, 0x3A),
            };

            switch (element)
            {
                case "lightning":
                    Canvas.Line(span, x, y, x + (int)((rng >> 8) % 24) - 12, y + 18, colour);
                    Canvas.Line(span, x + 1, y, x + 1 + (int)((rng >> 8) % 24) - 12, y + 18, colour);
                    break;
                case "wind":
                    Canvas.Line(span, x, y, x + size * 3, y - size, colour);
                    Canvas.Line(span, x, y + 1, x + size * 3, y - size + 1, colour);
                    break;
                default:
                    Canvas.Disc(span, x, y, size, colour);
                    break;
            }
        }
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
