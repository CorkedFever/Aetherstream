namespace Aetherstream.Plugin.Video;

/// <summary>
/// The Eorzean Shopping Network. The market channel's numbers in a wig: one item from the watch
/// list at a time on a turntable, the price enormous, the average struck through beside it, how
/// many sell a day, a countdown to the next item, a host in a headset who wants you to call now,
/// and a phone number that goes nowhere. The prices are real; the urgency is not.
/// </summary>
internal sealed class ShoppingChannel(BitmapFont font, Func<MarketSnapshot?> data, Func<uint, IconPixels?> icon) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double ItemFor = 30.0;

    private static readonly uint Studio = Canvas.Rgb(0x1A, 0x22, 0x44);
    private static readonly uint StudioLit = Canvas.Rgb(0x2E, 0x3E, 0x7A);
    private static readonly uint Desk = Canvas.Rgb(0x6A, 0x4A, 0x8A);
    private static readonly uint DeskLit = Canvas.Rgb(0x8A, 0x66, 0xB0);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Gold = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint Hot = Canvas.Rgb(0xFF, 0x4A, 0x6A);
    private static readonly uint Ink = Canvas.Rgb(0x10, 0x0C, 0x18);
    private static readonly uint Mint = Canvas.Rgb(0x6A, 0xE0, 0xB0);

    private static readonly string[] Pitch =
    [
        "Look at this. LOOK at it. You have wanted one of these for weeks and you know it.",
        "Now I am not supposed to say this, but the gil you save here could buy a second one.",
        "Crafters, this is your moment. Gatherers, this is also your moment. Everyone else, still your moment.",
        "I have one at home. I have three at home. Do not tell my retainer.",
        "The market sets this price, not us. We just get very excited about it.",
        "At this price it is practically a gift. To yourself. From yourself. You deserve it.",
        "Is it going up? Is it going down? Nobody knows. That is why you buy now.",
        "Our phones are lit up like Starlight. Metaphorically. We do not have phones.",
        "Every time one of these sells, a Lalafell somewhere does a little dance.",
        "Do not go to the market board tomorrow and see this and think of me. Buy it today.",
        "I will not tell you what to do with your gil. I will strongly imply it.",
        "Perfect for the house. Perfect for the FC. Perfect, frankly, for the shelf.",
    ];

    private static readonly string[] Testimonials =
    [
        "\"I bought two and I regret nothing.\" - a viewer on {world}",
        "\"My retainer said no. I said yes.\" - a viewer on {world}",
        "\"Shipped straight to my inventory.\" - a viewer on {world}",
        "\"I do not know what it is for and I love it.\" - a viewer on {world}",
    ];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var snapshot = data();
        var rows = snapshot?.Rows ?? [];
        var world = snapshot?.World ?? "your world";

        this.DrawStudio(span, seconds);

        if (rows.Count == 0)
        {
            var text = snapshot is null ? "WARMING UP THE STUDIO" : "NOTHING ON THE WATCH LIST. ADD ITEMS IN SETUP, THEN CALL NOW.";
            foreach (var l in Canvas.Wrap(text, font.Fit(W - 200), 3))
            {
                font.Draw(span, W, l, (W - font.Measure(l)) / 2, 300, Cream, 1, all);
            }

            this.DrawHost(span, seconds, excited: false);
            this.DrawFooter(span, seconds, rows, all);
            return;
        }

        // One item at a time, on the clock, so everyone sees the same pitch.
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var slot = (int)((unix - 1_700_000_000) / (long)ItemFor);
        var into = (unix - 1_700_000_000) % (long)ItemFor;
        var row = rows[Pick(slot, rows.Count)];
        var next = rows[Pick(slot + 1, rows.Count)];

        // The turntable: the item on a pedestal, turning (bobbing, with a sweep of light).
        var cx = 370;
        var cy = 300;
        Canvas.Disc(span, cx, cy + 130, 130, Canvas.Lerp(Desk, Canvas.Black, 0.4f));
        Canvas.Disc(span, cx, cy + 122, 120, DeskLit);
        var sweep = (int)(Math.Sin(seconds * 1.4) * 60);
        Canvas.Disc(span, cx + sweep, cy + 118, 30, Canvas.Lerp(DeskLit, Canvas.White, 0.35f));
        var bob = (int)(Math.Sin(seconds * 2.0) * 6);
        this.DrawIcon(span, row.Id, cx - 110, cy - 120 + bob, 220);

        // The card: the name, the price huge, the average struck through, the rate, the trend.
        const int CardX = 540;
        Canvas.Fill(span, CardX, 90, 700, 400, Canvas.Lerp(Studio, Canvas.Black, 0.35f));
        Canvas.Fill(span, CardX, 90, 700, 6, Gold);
        var name = Canvas.Plain(row.Name).ToUpperInvariant();
        var y = 106;
        if (name.Length <= font.Fit(660, 2))
        {
            font.Draw(span, W, name, CardX + 20, y, Cream, 2, all);
            y += 84;
        }
        else
        {
            foreach (var l in Canvas.Wrap(name, font.Fit(660), 2))
            {
                font.Draw(span, W, l, CardX + 20, y, Cream, 1, all);
                y += 40;
            }

            y += 6;
        }

        var price = $"{row.Min:N0} GIL";
        var flash = into < 1.0 || (int)(seconds * 3) % 9 == 0;
        font.Draw(span, W, price, CardX + 20, y, flash ? Gold : Cream, 2, all);
        y += 88;
        font.Draw(span, W, "LOWEST ON THE BOARD RIGHT NOW", CardX + 20, y, Mint, 1, all);
        y += 44;

        if (row.Average > 0)
        {
            var avg = $"AVERAGE {row.Average:N0}";
            font.Draw(span, W, avg, CardX + 20, y, Canvas.Rgb(0xA0, 0x98, 0xB8), 1, all);
            if (row.Min < row.Average)
                Canvas.Fill(span, CardX + 20, y + 20, font.Measure(avg), 4, Hot);
            var saving = row.Average - row.Min;
            if (saving > 0)
                font.Draw(span, W, $"YOU SAVE {saving:N0}", CardX + 40 + font.Measure(avg), y, Gold, 1, all);
        }

        y += 44;

        var rate = row.PerDay >= 1 ? $"{row.PerDay:0} SELL EVERY DAY ON {world.ToUpperInvariant()}" : $"A RARE ONE ON {world.ToUpperInvariant()}";
        font.Draw(span, W, Canvas.Cut(rate, font.Fit(660)), CardX + 20, y, Cream, 1, all);
        y += 40;
        var trend = row.Trend > 0 ? "TREND: CLIMBING. BUY BEFORE IT CLIMBS AGAIN." : row.Trend < 0 ? "TREND: FALLING. BUY BEFORE IT STOPS." : "TREND: STEADY. BUY BEFORE IT ISN'T.";
        font.Draw(span, W, Canvas.Cut(trend, font.Fit(660)), CardX + 20, y, row.Trend > 0 ? Mint : row.Trend < 0 ? Hot : Cream, 1, all);

        // Stickers: what the host is excited about, and the countdown.
        var left = 3 + Pick(slot * 7, 9);
        Sticker(span, CardX + 520, 60, $"ONLY {left} LEFT", Hot, seconds, all);
        if (Pick(slot * 11, 3) == 0)
            Sticker(span, CardX + 20, 60, "HOST'S PICK", Gold, seconds, all);
        var countdown = ItemFor - into;
        var next1 = $"NEXT ITEM 0:{(int)countdown:00}";
        font.Draw(span, W, next1, W - 120 - font.Measure(next1), 18, Canvas.Rgb(0xA0, 0x98, 0xB8), 1, all);

        // The host, and the pitch in a lower-third; a testimonial in the last stretch.
        var excited = into is > 8 and < 14 || into > 24;
        this.DrawHost(span, seconds, excited);
        var line = into < 22.0
            ? Pitch[Pick((slot * 3) + (int)(into / 7.5), Pitch.Length)]
            : Testimonials[Pick(slot * 5, Testimonials.Length)].Replace("{world}", world);
        this.DrawLowerThird(span, into < 22.0 ? "BARGAINBIX" : "A VIEWER", line, all);

        this.DrawFooter(span, seconds, rows, all, next);
    }

    private void DrawStudio(Span<uint> span, double seconds)
    {
        // A backdrop that glows in a slow sweep, a big lit logo, and the desk.
        for (var y = 0; y < 560; y++)
        {
            var t = (float)Math.Abs(Math.Sin((y * 0.01) + (seconds * 0.5)));
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(Studio, StudioLit, t * 0.5f));
        }

        for (var i = 0; i < 12; i++)
        {
            var x = (int)(((seconds * 40) + (i * 130)) % (W + 100)) - 50;
            Canvas.Disc(span, x, 40 + ((i * 37) % 30), 3, Canvas.Lerp(StudioLit, Canvas.White, 0.5f));
        }

        Canvas.Fill(span, 0, 560, W, 120, Desk);
        Canvas.Fill(span, 0, 560, W, 8, DeskLit);
        Canvas.Fill(span, 0, 640, W, 2, Canvas.Lerp(Desk, Canvas.Black, 0.3f));

        var all = new BitmapFont.Clip(0, 0, W, H);
        Canvas.Fill(span, 24, 16, 280, 48, Hot);
        font.Draw(span, W, "ESN", 40, 20, Cream, 1, all);
        font.Draw(span, W, "EORZEAN SHOPPING", 110, 20, Cream, 1, all);
        Canvas.Fill(span, W - 100, 22, 76, 32, Hot);
        font.Draw(span, W, "LIVE", W - 100 + 6, 18, Cream, 1, all);
    }

    private static void Sticker(Span<uint> span, int x, int y, string text, uint colour, double seconds, in BitmapFont.Clip all)
    {
        var w = text.Length * 16 + 24;
        var wob = (int)(Math.Sin(seconds * 6.0) * 2);
        Canvas.Fill(span, x, y + wob, w, 36, colour);
        Canvas.Fill(span, x + 4, y + wob + 4, w - 8, 28, Canvas.Lerp(colour, Canvas.Black, 0.25f));
        // The text is drawn by the caller's font through a static: fine to reach for the shared face.
        StickerText(span, x + 12, y + wob - 2, text, all);
    }

    // The font is an instance member; a sticker only needs it to draw text, so the channel
    // lends it through this hook set in the constructor's first render.
    private static Action<Span<uint>, int, int, string, BitmapFont.Clip>? stickerText;

    private static void StickerText(Span<uint> span, int x, int y, string text, in BitmapFont.Clip all) => stickerText?.Invoke(span, x, y, text, all);

    private void DrawHost(Span<uint> span, double seconds, bool excited)
    {
        stickerText ??= (s, x, y, text, all) => font.Draw(s, W, text, x, y, Cream, 1, all);
        HostSprites.DrawGoblin(span, excited ? HostSprites.Goblin.Excited : HostSprites.Goblin.Talk, seconds, 40, 560 - 230, 8);
    }

    private void DrawLowerThird(Span<uint> span, string tab, string line, in BitmapFont.Clip all)
    {
        const int Top = 476;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - 40 - font.Measure(tab) - 48), 2);
        var height = 16 + (lines.Count * 40);
        Canvas.Fill(span, 40, Top, W - 80, height, Ink);
        Canvas.Fill(span, 40, Top, W - 80, 4, Hot);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, Top + 4, tabW, height - 4, Hot);
        font.Draw(span, W, tab, 56, Top + 8, Cream, 1, all);
        var y = Top + 8;
        foreach (var l in lines)
        {
            font.Draw(span, W, l, 40 + tabW + 16, y, Cream, 1, all);
            y += 40;
        }
    }

    private void DrawFooter(Span<uint> span, double seconds, IReadOnlyList<MarketRow> rows, in BitmapFont.Clip all, MarketRow? next = null)
    {
        // A ticker of the rest of the list along the desk edge, then the phone number.
        Canvas.Fill(span, 0, 642, W, 34, Canvas.Lerp(Desk, Canvas.Black, 0.5f));
        if (rows.Count > 0)
        {
            var text = string.Join("   ", rows.Select(r => $"{Canvas.Plain(r.Name).ToUpperInvariant()} {r.Min:N0}")) + "   ";
            var width = font.Measure(text);
            var offset = (int)(seconds * 90.0) % width;
            var clip = new BitmapFont.Clip(0, 642, W, 676);
            font.Draw(span, W, text, -offset, 640, Cream, 1, clip);
            font.Draw(span, W, text, -offset + width, 640, Cream, 1, clip);
        }

        Canvas.Fill(span, 0, 676, W, 44, Ink);
        var phone = next is null ? "CALL NOW: 1-800-555-0199. OR ANY MARKET BOARD." : $"CALL NOW: 1-800-555-0199   /   UP NEXT: {Canvas.Plain(next.Name).ToUpperInvariant()}";
        font.Draw(span, W, Canvas.Cut(phone, font.Fit(W - 48)), 24, 680, Gold, 1, all);
    }

    private void DrawIcon(Span<uint> span, uint itemId, int x, int y, int size)
    {
        // Item ids are not icon ids; the icon lookup takes an item id through the plugin's table.
        if (icon(itemId) is not { } px)
        {
            Canvas.Rect(span, x, y, size, size, Cream);
            return;
        }

        for (var ty = 0; ty < size; ty++)
        {
            var yy = y + ty;
            if (yy < 0 || yy >= H)
                continue;

            var sy = ty * px.Height / size;
            for (var tx = 0; tx < size; tx++)
            {
                var xx = x + tx;
                if (xx < 0 || xx >= W)
                    continue;

                var p = px.Pixels[(sy * px.Width) + (tx * px.Width / size)];
                var a = (int)(p >> 24);
                if (a == 0)
                    continue;

                var i = (yy * W) + xx;
                span[i] = a >= 250 ? p | 0xFF000000u : Canvas.Lerp(span[i], p | 0xFF000000u, a / 255f);
            }
        }
    }

    private static int Pick(int seed, int count)
    {
        var x = (uint)seed * 2654435761u;
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
