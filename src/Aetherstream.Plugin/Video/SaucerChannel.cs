namespace Aetherstream.Plugin.Video;

/// <summary>A Triple Triad card: its numbers, stars, type, and how the game says it is got.</summary>
internal sealed record TriadCard(uint Id, string Name, string Description, int Top, int Bottom, int Left, int Right, int Stars, string Type, string How, int Sale);

internal sealed record SaucerSnapshot(long Mgp, DateTime? Cactpot, DateTime NextGate, bool FashionOpen, DateTime FashionAt, int CardsTotal, int CardsOwned, IReadOnlyList<TriadCard> Missing);

/// <summary>
/// Saucer Tonight, with Ko Bi. A kobold in a hood under a wall of marquee lights runs the Gold
/// Saucer's numbers like a game show: your MGP, the Jumbo Cactpot draw, the next GATE, the
/// Fashion Report window, and the card of the hour, a Triple Triad card you do not have yet,
/// with its numbers on the card and where the game says it comes from.
/// </summary>
internal sealed class SaucerChannel(BitmapFont font, Func<SaucerSnapshot?> data, Func<uint, IconPixels?> icon) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double SegmentFor = 20.0;
    private const int Segments = 5;
    private const uint CardIconBase = 87000;

    private static readonly uint Velvet = Canvas.Rgb(0x3A, 0x0E, 0x22);
    private static readonly uint VelvetLit = Canvas.Rgb(0x6A, 0x1E, 0x3E);
    private static readonly uint Panel = Canvas.Rgb(0x1E, 0x0A, 0x14);
    private static readonly uint Gold = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Faint = Canvas.Rgb(0xB0, 0x8A, 0x9A);
    private static readonly uint Mint = Canvas.Rgb(0x8A, 0xE8, 0xC0);
    private static readonly uint Rose = Canvas.Rgb(0xFF, 0x6A, 0x9A);
    private static readonly uint Cyan = Canvas.Rgb(0x6A, 0xD8, 0xFF);
    private static readonly uint Ink = Canvas.Rgb(0x10, 0x08, 0x0C);

    private static readonly string[] Patter =
    [
        "Welcome back to Saucer Tonight! Kobold counts your points, kobold counts them twice!",
        "Big numbers, shiny numbers, numbers you could have if you stopped standing about!",
        "Somebody wins the cactpot every week. Statistically it is never you. But WHAT IF!",
        "A GATE opens every twenty minutes. Kobold does not know which. That is the fun part, kobold is told.",
        "Fashion Report! Wear the thing! Get the points! Kobold wears a sack and scores eighty!",
        "This card, this card here, you do not have. Kobold checked. Kobold is thorough.",
        "MGP cannot buy happiness. It can buy a mount that is basically happiness with legs!",
        "Chocobo racing is also on. Kobold has a chocobo. It has opinions. It is not fast.",
        "Do not spend it all in one place! Spend it at the Saucer, which is technically one place!",
    ];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var slot = (int)((unix - 1_700_000_000) / (long)SegmentFor);
        var into = ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0) - 1_700_000_000) % SegmentFor;
        this.RenderAt(target, data(), slot, into, now, seconds);
    }

    private void RenderAt(uint[] target, SaucerSnapshot? snapshot, int slot, double into, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);

        // The set: velvet, a spotlight sweep, a marquee round the edge, the title.
        for (var y = 0; y < H; y++)
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(VelvetLit, Velvet, y / (float)H));
        var sx = (int)((Math.Sin(seconds * 0.7) + 1) * 0.5 * W);
        for (var y = 60; y < 560; y += 2)
            Canvas.Fill(span, sx - (y - 60) / 3 - 40, y, 80 + ((y - 60) * 2 / 3), 2, Canvas.Lerp(VelvetLit, Cream, 0.05f));
        Marquee(span, seconds);

        Canvas.Fill(span, 0, 0, W, 56, Panel);
        font.Draw(span, W, "SAUCER TONIGHT", 24, 8, Gold, 1, all);
        font.Draw(span, W, "WITH KO BI", 24 + font.Measure("SAUCER TONIGHT") + 24, 8, Faint, 1, all);
        var clock = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, clock, W - 24 - font.Measure(clock), 8, Cream, 1, all);

        if (snapshot is null)
        {
            font.Draw(span, W, "THE SAUCER OPENS WHEN YOU LOG IN", 60, 320, Cream, 1, all);
            HostSprites.DrawKobold(span, HostSprites.Kobold.Talk, seconds, 60, 330, 7);
            return;
        }

        var segment = slot % Segments;
        switch (segment)
        {
            case 0: this.DrawWinnings(span, snapshot, into, seconds, all); break;
            case 1: this.DrawCactpot(span, snapshot, now, all); break;
            case 2: this.DrawGate(span, snapshot, now, seconds, all); break;
            case 3: this.DrawFashion(span, snapshot, now, all); break;
            default: this.DrawCard(span, snapshot, slot / Segments, seconds, all); break;
        }

        var excited = into is > 4 and < 9 || into > 16;
        HostSprites.DrawKobold(span, excited ? HostSprites.Kobold.Cheer : HostSprites.Kobold.Talk, seconds, 60, 330, 7);
        this.DrawLowerThird(span, "KO BI", Patter[Pick((slot * 5) + (int)(into / 10.0), Patter.Length)], all);

        Canvas.Fill(span, 0, 680, W, 40, Panel);
        var names = new[] { "WINNINGS", "CACTPOT", "GATE", "FASHION", "CARD" };
        var strip = string.Join("   ", names.Select((n, i) => i == segment ? $"[{n}]" : n));
        font.Draw(span, W, Canvas.Cut(strip, font.Fit(W - 48)), 24, 680, Faint, 1, all);
    }

    private void DrawWinnings(Span<uint> span, SaucerSnapshot s, double into, double seconds, in BitmapFont.Clip all)
    {
        Card(span, "YOUR WINNINGS", all);
        var shown = (long)(s.Mgp * Math.Clamp(into / 3.0, 0, 1));
        var text = $"{shown:N0} MGP";
        font.Draw(span, W, text, 800 - (font.Measure(text, 2) / 2), 220, Gold, 2, all);
        for (var i = 0; i < 12; i++)
        {
            var a = (seconds * 1.5) + (i * Math.PI / 6);
            Canvas.Disc(span, 700 + (int)(Math.Cos(a) * 300), 250 + (int)(Math.Sin(a) * 90), 4, Canvas.Lerp(Gold, Cream, (float)((Math.Sin(seconds * 5 + i) + 1) / 2)));
        }

        var line = s.Mgp switch
        {
            0 => "NOTHING YET. THE SAUCER HAS A FREE ONE FOR YOU AT THE DOOR.",
            < 10_000 => "A START. THE MINI CACTPOT IS THREE TICKETS A DAY.",
            < 200_000 => "RESPECTABLE. THERE ARE MOUNTS IN THAT RANGE.",
            < 1_000_000 => "KOBOLD IS IMPRESSED. THE FENRIR IS ONE MILLION.",
            _ => "KOBOLD IS FRIGHTENED. YOU COULD BUY THE SAUCER.",
        };
        font.Draw(span, W, Canvas.Cut(line, font.Fit(840)), 800 - (font.Measure(Canvas.Cut(line, font.Fit(840))) / 2), 330, Cream, 1, all);
        var cards = $"TRIPLE TRIAD: {s.CardsOwned} OF {s.CardsTotal} CARDS";
        font.Draw(span, W, cards, 800 - (font.Measure(cards) / 2), 380, Faint, 1, all);
    }

    private void DrawCactpot(Span<uint> span, SaucerSnapshot s, DateTime now, in BitmapFont.Clip all)
    {
        Card(span, "JUMBO CACTPOT", all);
        if (s.Cactpot is not { } draw)
        {
            font.Draw(span, W, "NO DRAW ON THE BOOKS FOR YOUR REGION", 380, 240, Cream, 1, all);
            return;
        }

        var left = draw - now.ToUniversalTime();
        var big = left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours:00}H" : $"{left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}";
        font.Draw(span, W, big, 800 - (font.Measure(big, 2) / 2), 200, Gold, 2, all);
        var when = "DRAWN " + draw.ToLocalTime().ToString("dddd h:mm tt").ToUpperInvariant();
        font.Draw(span, W, when, 800 - (font.Measure(when) / 2), 300, Cream, 1, all);
        const string Tip = "THREE TICKETS A WEEK. PICK A NUMBER, PICK ANOTHER, PICK THE SAME ONE.";
        var ty = 350;
        foreach (var l in Canvas.Wrap(Tip, font.Fit(840), 2))
        {
            font.Draw(span, W, l, 800 - (font.Measure(l) / 2), ty, Faint, 1, all);
            ty += 40;
        }

        // Three cactuars, because it is the cactpot.
        for (var i = 0; i < 3; i++)
            Cactuar(span, 660 + (i * 140), 550, i);
    }

    private void DrawGate(Span<uint> span, SaucerSnapshot s, DateTime now, double seconds, in BitmapFont.Clip all)
    {
        Card(span, "NEXT GATE", all);
        var left = s.NextGate - now.ToUniversalTime();
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;
        var big = $"{(int)left.TotalMinutes:00}:{left.Seconds:00}";
        font.Draw(span, W, big, 800 - (font.Measure(big, 2) / 2), 200, left.TotalMinutes < 2 ? Rose : Gold, 2, all);
        var line = left.TotalMinutes < 2 ? "GET TO THE SAUCER. RUN. KOBOLD MEANS IT." : "ONE OPENS EVERY TWENTY MINUTES: ON THE HOUR, TWENTY PAST, TWENTY TO.";
        font.Draw(span, W, Canvas.Cut(line, font.Fit(840)), 800 - (font.Measure(Canvas.Cut(line, font.Fit(840))) / 2), 300, Cream, 1, all);
        const string Which = "WHICH ONE? CLIFFHANGER, LEAP OF FAITH, AIR FORCE ONE, ANY WAY THE WIND BLOWS, VINYL... NOBODY KNOWS UNTIL THE BELL.";
        var y = 350;
        foreach (var l in Canvas.Wrap(Which, font.Fit(840), 3))
        {
            font.Draw(span, W, l, 800 - (font.Measure(l) / 2), y, Faint, 1, all);
            y += 40;
        }

        // A gate that swings.
        var open = (int)((Math.Sin(seconds * 1.2) + 1) * 18);
        Canvas.Fill(span, 730, 480, 12, 90, Gold);
        Canvas.Fill(span, 858, 480, 12, 90, Gold);
        Canvas.Fill(span, 730, 472, 140, 8, Gold);
        Canvas.Fill(span, 742, 486, 56 - open, 84, Cyan);
        Canvas.Fill(span, 802 + open, 486, 56 - open, 84, Cyan);
    }

    private void DrawFashion(Span<uint> span, SaucerSnapshot s, DateTime now, in BitmapFont.Clip all)
    {
        Card(span, "FASHION REPORT", all);
        var left = s.FashionAt - now.ToUniversalTime();
        if (left < TimeSpan.Zero)
            left = TimeSpan.Zero;
        var big = s.FashionOpen ? "JUDGING OPEN" : "OPENS SOON";
        font.Draw(span, W, big, 800 - (font.Measure(big, 2) / 2), 200, s.FashionOpen ? Mint : Gold, 2, all);
        var when = (s.FashionOpen ? "CLOSES IN " : "OPENS IN ") + (left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours}H" : $"{left.Hours}H {left.Minutes}M");
        font.Draw(span, W, when, 800 - (font.Measure(when) / 2), 300, Cream, 1, all);
        var tip = s.FashionOpen ? "EIGHTY POINTS IS THE PRIZE. MASKED ROSE IS BY THE CARDS. KOBOLD SAYS: WEAR THE HAT." : "FRIDAY. MASKED ROSE IS BY THE CARDS. KOBOLD IS ALREADY DRESSED.";
        var y = 350;
        foreach (var l in Canvas.Wrap(tip, font.Fit(840), 2))
        {
            font.Draw(span, W, l, 800 - (font.Measure(l) / 2), y, Faint, 1, all);
            y += 40;
        }

        // A dress form, because it is a fashion show.
        Canvas.Fill(span, 790, 440, 20, 30, Faint);
        Canvas.Fill(span, 770, 470, 60, 50, Rose);
        Canvas.Fill(span, 760, 520, 80, 40, Rose);
        Canvas.Fill(span, 796, 560, 8, 14, Faint);
        Canvas.Fill(span, 780, 572, 40, 6, Faint);
    }

    private void DrawCard(Span<uint> span, SaucerSnapshot s, int hour, double seconds, in BitmapFont.Clip all)
    {
        Card(span, "CARD OF THE HOUR", all);
        if (s.Missing.Count == 0)
        {
            font.Draw(span, W, "YOU HAVE EVERY CARD. KOBOLD HAS NOTHING TO SELL YOU.", 380, 240, Mint, 1, all);
            return;
        }

        var card = s.Missing[Pick(hour, s.Missing.Count)];

        // The card itself: the art at 208 by 256 with its numbers on, or a drawn back.
        const int CX = 400;
        const int CY = 150;
        Canvas.Fill(span, CX + 6, CY + 6, 208, 256, Canvas.Lerp(Panel, Canvas.Black, 0.5f));
        if (icon(CardIconBase + card.Id) is { } art && art.Width > 0)
            Blit(span, art, CX, CY, 208, 256);
        else
        {
            Canvas.Fill(span, CX, CY, 208, 256, Canvas.Rgb(0x2A, 0x3A, 0x6A));
            Canvas.Rect(span, CX, CY, 208, 256, Gold, 4);
            font.Draw(span, W, "?", CX + 96, CY + 100, Gold, 2, all);
        }

        // The numbers, the way the card shows them: top, left and right, bottom.
        var nx = CX + 40;
        var ny = CY + 200;
        Canvas.Fill(span, nx - 14, ny - 14, 60, 60, Ink);
        font.Draw(span, W, Num(card.Top), nx + 8, ny - 18, Cream, 1, all);
        font.Draw(span, W, Num(card.Left), nx - 12, ny + 2, Cream, 1, all);
        font.Draw(span, W, Num(card.Right), nx + 28, ny + 2, Cream, 1, all);
        font.Draw(span, W, Num(card.Bottom), nx + 8, ny + 22, Cream, 1, all);
        for (var i = 0; i < card.Stars; i++)
            Star(span, CX + 20 + (i * 22), CY + 20, 8, Gold);

        // Beside it: the name, the type, how it is got, and its words.
        const int TX = 640;
        font.Draw(span, W, Canvas.Cut(card.Name.ToUpperInvariant(), font.Fit(560)), TX, CY, Gold, 1, all);
        var meta = (card.Type.Length > 0 ? card.Type.ToUpperInvariant() + "   " : string.Empty) + $"{card.Stars} STAR" + (card.Stars == 1 ? string.Empty : "S");
        font.Draw(span, W, meta, TX, CY + 40, Faint, 1, all);
        var how = card.How.Length > 0 ? "FROM: " + Canvas.Plain(card.How).ToUpperInvariant() : "FROM: SOMEWHERE. KOBOLD DID NOT WRITE IT DOWN.";
        var y = CY + 90;
        foreach (var l in Canvas.Wrap(how, font.Fit(560), 2))
        {
            font.Draw(span, W, l, TX, y, Mint, 1, all);
            y += 40;
        }

        y += 10;
        foreach (var l in Canvas.Wrap(Canvas.Plain(card.Description).ToUpperInvariant(), font.Fit(560), 5))
        {
            font.Draw(span, W, l, TX, y, Cream, 1, all);
            y += 40;
        }

        font.Draw(span, W, $"{s.CardsOwned} OF {s.CardsTotal}", CX, CY + 270, Faint, 1, all);
        font.Draw(span, W, $"{s.Missing.Count} TO GO", CX, CY + 306, Faint, 1, all);
    }

    // -- bits ---------------------------------------------------------------------------------------------------

    private void Card(Span<uint> span, string title, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 360, 80, 880, 500, Panel);
        Canvas.Rect(span, 360, 80, 880, 500, Gold, 3);
        Canvas.Fill(span, 360, 80, 880, 48, Canvas.Lerp(Panel, Gold, 0.2f));
        font.Draw(span, W, title, 800 - (font.Measure(title) / 2), 84, Gold, 1, all);
    }

    private static void Marquee(Span<uint> span, double seconds)
    {
        var phase = (int)(seconds * 6) % 3;
        var n = 0;
        for (var x = 12; x < W; x += 28)
        {
            Canvas.Disc(span, x, 66, 5, n++ % 3 == phase ? Gold : Canvas.Lerp(Gold, Velvet, 0.7f));
            Canvas.Disc(span, x, 672, 5, n % 3 == phase ? Gold : Canvas.Lerp(Gold, Velvet, 0.7f));
        }
    }

    private static void Cactuar(Span<uint> span, int x, int y, int i)
    {
        var g = Canvas.Rgb(0x5A, 0xB0, 0x4A);
        Canvas.Fill(span, x - 14, y - 60, 28, 90, g);
        Canvas.Fill(span, x - 40, y - 40 + (i * 6), 26, 12, g);
        Canvas.Fill(span, x - 40, y - 40 + (i * 6), 12, 30, g);
        Canvas.Fill(span, x + 14, y - 20 - (i * 6), 26, 12, g);
        Canvas.Fill(span, x + 28, y - 44 - (i * 6), 12, 30, g);
        Canvas.Fill(span, x - 8, y - 44, 4, 8, Ink);
        Canvas.Fill(span, x + 4, y - 44, 4, 8, Ink);
        Canvas.Fill(span, x - 4, y - 28, 8, 4, Ink);
    }

    private static void Star(Span<uint> span, int cx, int cy, int r, uint colour)
    {
        for (var i = 0; i < 5; i++)
        {
            var a = (-Math.PI / 2) + (i * 2 * Math.PI / 5);
            var b = a + (4 * Math.PI / 5);
            Canvas.Line(span, cx + (int)(Math.Cos(a) * r), cy + (int)(Math.Sin(a) * r), cx + (int)(Math.Cos(b) * r), cy + (int)(Math.Sin(b) * r), colour);
        }

        Canvas.Disc(span, cx, cy, r / 3, colour);
    }

    private static string Num(int n) => n >= 10 ? "A" : n.ToString();

    private static void Blit(Span<uint> span, IconPixels art, int x, int y, int w, int h)
    {
        for (var ty = 0; ty < h; ty++)
        {
            var yy = y + ty;
            if (yy < 0 || yy >= H)
                continue;
            var sy = ty * art.Height / h;
            for (var tx = 0; tx < w; tx++)
            {
                var xx = x + tx;
                if (xx < 0 || xx >= W)
                    continue;
                var p = art.Pixels[(sy * art.Width) + (tx * art.Width / w)];
                var a = (int)(p >> 24);
                if (a == 0)
                    continue;
                var i = (yy * W) + xx;
                span[i] = a >= 250 ? p | 0xFF000000u : Canvas.Lerp(span[i], p | 0xFF000000u, a / 255f);
            }
        }
    }

    private void DrawLowerThird(Span<uint> span, string tab, string line, in BitmapFont.Clip all)
    {
        const int Top = 596;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - font.Measure(tab) - 64), 2);
        var height = 16 + (lines.Count * 40);
        Canvas.Fill(span, 40, Top, W - 80, height, Ink);
        Canvas.Fill(span, 40, Top, W - 80, 4, Gold);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, Top + 4, tabW, height - 4, VelvetLit);
        font.Draw(span, W, tab, 56, Top + 8, Cream, 1, all);
        var y = Top + 8;
        foreach (var l in lines)
        {
            font.Draw(span, W, l, 40 + tabW + 16, y, Cream, 1, all);
            y += 40;
        }
    }

    private static int Pick(int seed, int count)
    {
        var x = (uint)seed * 2654435761u;
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
