namespace Aetherstream.Plugin.Video;

/// <summary>A vista from the Sightseeing Log: its picture, its words, and its hours.</summary>
internal sealed record Vista(int Index, string Name, string Place, string Impression, string Description, int MinTime, int MaxTime, string Emote, uint Icon);

/// <summary>
/// The Kupo of Painting. A moogle in a beret, Pom Ross, paints a vista from the Sightseeing Log
/// in front of you: the sky first in broad strokes, then the shapes, then the detail, one
/// sweep at a time, with the quiet talk of someone who likes paint. The picture is the log's own
/// painting of the vista, so when he is done it is the real one, and the card says whether you
/// have stood there yet.
/// </summary>
internal sealed class PaintingChannel(BitmapFont font, Func<IReadOnlyList<Vista>> vistas, Func<uint, IconPixels?> icon, Func<int, bool> found) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const int PicW = 800;
    private const int PicH = 480;
    private const int PicX = 430;
    private const int PicY = 64;
    private const double EpisodeFor = 180.0;
    private const double TitlesEnd = 7.0;
    private const double PaintEnd = 150.0;
    private const double DoneEnd = 166.0;

    private static readonly uint Studio = Canvas.Rgb(0x2A, 0x24, 0x20);
    private static readonly uint StudioLit = Canvas.Rgb(0x3E, 0x36, 0x2E);
    private static readonly uint Floor = Canvas.Rgb(0x4A, 0x3A, 0x2C);
    private static readonly uint Wood = Canvas.Rgb(0x8A, 0x62, 0x3A);
    private static readonly uint WoodDark = Canvas.Rgb(0x5E, 0x42, 0x26);
    private static readonly uint Linen = Canvas.Rgb(0xF2, 0xEA, 0xD8);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Ink = Canvas.Rgb(0x14, 0x10, 0x0C);
    private static readonly uint Faint = Canvas.Rgb(0xA8, 0x9C, 0x88);
    private static readonly uint Gold = Canvas.Rgb(0xE8, 0xC0, 0x60);
    private static readonly uint Mint = Canvas.Rgb(0x8A, 0xD8, 0xA8);

    private static readonly string[] Talk =
    [
        "We'll start with the sky today, kupo. The sky is where the light lives, so it goes on first.",
        "Just let the brush wander. It knows where the hills are better than we do, kupo.",
        "There are no mistakes on this canvas. Only places we haven't painted yet.",
        "A little of this colour here. Nobody will ever know it's there, but you'll feel it, kupo.",
        "Take your time. The paint is drying and so are we. That's fine. That's the whole idea.",
        "See how the far things go soft and blue? That's the air between you and them, kupo.",
        "Now we put in the shapes. Big ones. We can be brave, it's only paint.",
        "I like to think every picture already lives in the canvas, kupo. We just clear the fog off it.",
        "Press a little harder there. Let the brush decide what a rock looks like.",
        "This bit is my favourite. It doesn't matter which bit. Whichever one I'm on, kupo.",
        "If it looks wrong, wait. It's not finished being right yet.",
        "A dab of light on the edge, and now it's morning. That's all morning is, kupo. A dab.",
        "We don't paint what's there. We paint what it felt like to stand there.",
        "My pom says more shadow. My pom is usually right, kupo.",
        "Nearly there. Don't rush the end; the end is where the picture decides to like you.",
        "Let's put a happy little stroke right about here, kupo. There. It lives there now.",
    ];

    private static readonly string[] Done =
    [
        "And that's our vista, kupo. Thank you for painting along with me.",
        "There it is. You could almost walk into it. Please don't; it's still wet, kupo.",
        "Not bad for a moogle with one brush. Same time tomorrow, kupo.",
    ];

    private readonly Dictionary<uint, (uint[] Coarse, uint[] Mid)> blocks = [];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var list = vistas();

        if (list.Count == 0)
        {
            this.DrawStudio(span, seconds);
            font.Draw(span, W, "THE LOG IS EMPTY, KUPO. NOTHING TO PAINT YET.", 60, 620, Cream, 1, all);
            HostSprites.DrawPainter(span, HostSprites.Painter.Stand, seconds, 80, 300, 7);
            return;
        }

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var episode = (int)((unix - 1_700_000_000) / (long)EpisodeFor);
        var into = (unix - 1_700_000_000) % (long)EpisodeFor + (seconds % 1.0);
        this.RenderAt(target, list, episode, into, now, seconds);
    }

    /// <summary>One frame of one episode, at <paramref name="into"/> seconds; the harness calls this directly.</summary>
    private void RenderAt(uint[] target, IReadOnlyList<Vista> list, int episode, double into, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var vista = list[Pick(episode, list.Count)];
        var pic = icon(vista.Icon);
        this.DrawStudio(span, seconds);

        if (into < TitlesEnd)
        {
            this.DrawTitles(span, vista, into, seconds, all);
            return;
        }

        this.DrawEasel(span);
        if (pic is null || pic.Width != PicW || pic.Height != PicH)
        {
            Canvas.Fill(span, PicX, PicY, PicW, PicH, Linen);
            font.Draw(span, W, "THIS ONE HAS NO PICTURE, KUPO", PicX + 40, PicY + 220, Faint, 1, all);
        }
        else if (into < PaintEnd)
        {
            var p = (into - TitlesEnd) / (PaintEnd - TitlesEnd);
            var (bx, by, colour) = this.DrawPainting(span, pic, p, seconds);
            DrawBrush(span, PicX + bx, PicY + by, colour, seconds);
        }
        else
        {
            this.DrawPainting(span, pic, 1.0, seconds);
        }

        // The painter, and what he says.
        var painting = into < PaintEnd && pic is not null;
        HostSprites.DrawPainter(span, painting ? HostSprites.Painter.Stroke : HostSprites.Painter.Stand, seconds, 120, 290, 7);
        this.DrawPalette(span, 60, 470, seconds);

        string line;
        string tab;
        if (into < PaintEnd)
        {
            var beat = (int)((into - TitlesEnd) / 9.0);
            line = beat % 5 == 4 ? Quoted(vista) : Talk[Pick((episode * 13) + beat, Talk.Length)];
            tab = beat % 5 == 4 ? "THE LOG" : "POM ROSS";
        }
        else if (into < DoneEnd)
        {
            line = Done[Pick(episode * 17, Done.Length)];
            if (vista.Emote.Length > 0)
                line += $" When you find it, the log wants a {vista.Emote} from you, kupo.";
            tab = "POM ROSS";
        }
        else
        {
            line = Canvas.Plain(vista.Description);
            tab = "THE LOG";
        }

        var page = into >= DoneEnd ? (int)((into - DoneEnd) / 7.0) : 0;
        this.DrawLowerThird(span, tab, line, all, page);
        this.DrawCard(span, vista, into >= PaintEnd, all);
    }

    // -- the picture ------------------------------------------------------------------------------------------

    /// <summary>
    /// Paints the vista to <paramref name="progress"/>: the first stretch lays it in as broad
    /// blocks in bands across the sky and down, the second sharpens it into smaller blocks, and
    /// the last brings up the real pixels in sweeps down the canvas. Returns where the brush is
    /// and the colour on it.
    /// </summary>
    private (int X, int Y, uint Colour) DrawPainting(Span<uint> span, IconPixels pic, double progress, double seconds)
    {
        var (coarse, mid) = this.Blocks(pic);
        var px = pic.Pixels;

        // Three stages, each a share of the whole: blocks, then finer blocks, then detail.
        const double S1 = 0.22;
        const double S2 = 0.52;
        var t1 = Math.Clamp(progress / S1, 0, 1);
        var t2 = Math.Clamp((progress - S1) / (S2 - S1), 0, 1);
        var t3 = Math.Clamp((progress - S2) / (1 - S2), 0, 1);

        // Stage one and two sweep in horizontal bands, alternating direction; stage three in
        // columns, top to bottom. Each stroke's leading edge wobbles so it reads as a brush.
        const int Bands1 = 12;
        const int Bands2 = 24;
        const int Cols3 = 10;
        var head1 = t1 * Bands1;
        var head2 = t2 * Bands2;
        var head3 = t3 * Cols3;
        var brushX = 0;
        var brushY = 0;
        uint brushColour = Linen;

        var colW = PicW / Cols3;
        var col3 = (int)Math.Floor(head3);
        var frac3 = head3 - Math.Floor(head3);
        var edge3Y = frac3 * PicH;
        for (var y = 0; y < PicH; y++)
        {
            var row = span.Slice(((PicY + y) * W) + PicX, PicW);
            var from = y * PicW;
            var b1 = y / (PicH / Bands1);
            var b2 = y / (PicH / Bands2);
            var wob = Math.Sin(y * 0.25) * 14;

            row.Fill(Linen);
            var (c0, c1) = Range(Edge(head1, b1, wob), b1);
            if (c1 > c0)
                coarse.AsSpan(from + c0, c1 - c0).CopyTo(row[c0..]);
            var (m0, m1) = Range(Edge(head2, b2, wob), b2);
            if (m1 > m0)
                mid.AsSpan(from + m0, m1 - m0).CopyTo(row[m0..]);

            // Detail: whole columns to the left of the head, and the head column down to its edge.
            var full = Math.Clamp(col3 * colW, 0, PicW);
            if (full > 0)
                px.AsSpan(from, full).CopyTo(row);
            if (col3 < Cols3)
            {
                var x1 = Math.Min(PicW, full + colW);
                for (var x = full; x < x1; x++)
                {
                    if (y < edge3Y + WobX[x])
                        row[x] = px[from + x];
                }
            }
        }

        for (var i = 0; i < PicH; i++)
        {
            // The picture's own alpha: opaque, so the copies above read as paint.
            var row = span.Slice(((PicY + i) * W) + PicX, PicW);
            for (var x = 0; x < PicW; x++)
                row[x] |= 0xFF000000u;
        }

        if (t3 > 0 && t3 < 1)
        {
            var col = Math.Min(Cols3 - 1, (int)Math.Floor(head3));
            brushX = (col * (PicW / Cols3)) + (PicW / Cols3 / 2);
            brushY = (int)((head3 - col) * PicH);
            brushColour = px[Math.Clamp((brushY * PicW) + brushX, 0, px.Length - 1)] | 0xFF000000u;
        }
        else if (t2 > 0 && t2 < 1)
        {
            var band = Math.Min(Bands2 - 1, (int)Math.Floor(head2));
            brushY = (band * (PicH / Bands2)) + (PicH / Bands2 / 2);
            brushX = BandX(head2, band);
            brushColour = mid[Math.Clamp((brushY * PicW) + brushX, 0, mid.Length - 1)];
        }
        else if (t1 < 1)
        {
            var band = Math.Min(Bands1 - 1, (int)Math.Floor(head1));
            brushY = (band * (PicH / Bands1)) + (PicH / Bands1 / 2);
            brushX = BandX(head1, band);
            brushColour = coarse[Math.Clamp((brushY * PicW) + brushX, 0, coarse.Length - 1)];
        }
        else
        {
            brushX = PicW / 2;
            brushY = PicH / 2;
        }

        return (brushX, brushY, brushColour);
    }

    /// <summary>Where a band's stroke has got to, in picture x, for a sweep head measured in bands; negative means not started, past the width means done.</summary>
    private static double Edge(double head, int band, double wobble)
    {
        if (head >= band + 1)
            return PicW + 100;
        if (head <= band)
            return -100;
        var frac = head - band;
        var x = frac * PicW;
        return x + wobble;
    }

    /// <summary>The stretch of a row a band's stroke has covered, as [from, to) in picture x.</summary>
    private static (int From, int To) Range(double edge, int band)
    {
        if (edge <= 0)
            return (0, 0);
        if (edge >= PicW)
            return (0, PicW);
        var e = (int)edge;
        return band % 2 == 0 ? (0, e) : (PicW - e, PicW);
    }

    // The detail stroke's ragged lower edge, one wobble per column of the picture.
    private static readonly double[] WobX = Enumerable.Range(0, PicW).Select(x => Math.Sin(x * 0.3) * 10).ToArray();

    private static int BandX(double head, int band)
    {
        var frac = Math.Clamp(head - band, 0, 1);
        var x = (int)(frac * PicW);
        return band % 2 == 0 ? x : PicW - x;
    }

    /// <summary>The picture averaged into 40 and 10 pixel blocks, once per picture.</summary>
    private (uint[] Coarse, uint[] Mid) Blocks(IconPixels pic)
    {
        if (this.blocks.TryGetValue(pic.Pixels.Length == 0 ? 0u : (uint)pic.Pixels[0] ^ (uint)pic.Pixels.Length, out var have) && have.Coarse.Length == pic.Pixels.Length)
            return have;

        var key = pic.Pixels.Length == 0 ? 0u : (uint)pic.Pixels[0] ^ (uint)pic.Pixels.Length;
        var coarse = Average(pic, 40);
        var mid = Average(pic, 10);
        if (this.blocks.Count > 4)
            this.blocks.Clear();
        this.blocks[key] = (coarse, mid);
        return (coarse, mid);
    }

    private static uint[] Average(IconPixels pic, int cell)
    {
        var outp = new uint[pic.Pixels.Length];
        for (var by = 0; by < pic.Height; by += cell)
        {
            for (var bx = 0; bx < pic.Width; bx += cell)
            {
                long r = 0, g = 0, b = 0, n = 0;
                for (var y = by; y < Math.Min(by + cell, pic.Height); y++)
                {
                    for (var x = bx; x < Math.Min(bx + cell, pic.Width); x++)
                    {
                        var p = pic.Pixels[(y * pic.Width) + x];
                        r += p & 0xFF;
                        g += (p >> 8) & 0xFF;
                        b += (p >> 16) & 0xFF;
                        n++;
                    }
                }

                // A touch lighter and flatter than the average, like a first wash.
                var avg = Canvas.Rgb((int)(r / n), (int)(g / n), (int)(b / n));
                var wash = Canvas.Lerp(avg, Linen, cell >= 40 ? 0.25f : 0.08f);
                for (var y = by; y < Math.Min(by + cell, pic.Height); y++)
                    for (var x = bx; x < Math.Min(bx + cell, pic.Width); x++)
                        outp[(y * pic.Width) + x] = wash;
            }
        }

        return outp;
    }

    private static void DrawBrush(Span<uint> span, int x, int y, uint colour, double seconds)
    {
        // A wet dab under the tip, a brush at a slant above it, and the ferrule catching light.
        Canvas.Disc(span, x, y, 9, colour);
        Canvas.Line(span, x + 3, y - 4, x + 26, y - 40, Canvas.Rgb(0xC0, 0xC4, 0xC8));
        Canvas.Line(span, x + 4, y - 5, x + 27, y - 41, Canvas.Rgb(0xE0, 0xE4, 0xE8));
        Canvas.Line(span, x + 26, y - 40, x + 60, y - 96, WoodDark);
        Canvas.Line(span, x + 27, y - 41, x + 61, y - 97, Wood);
        Canvas.Line(span, x + 28, y - 40, x + 62, y - 96, Wood);
    }

    // -- the set ----------------------------------------------------------------------------------------------

    private void DrawStudio(Span<uint> span, double seconds)
    {
        for (var y = 0; y < 560; y++)
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(StudioLit, Studio, y / 560f));
        Canvas.Fill(span, 0, 560, W, H - 560, Floor);
        for (var x = 0; x < W; x += 80)
            Canvas.Fill(span, x, 560, 2, H - 560, Canvas.Lerp(Floor, Canvas.Black, 0.3f));

        // A window's light on the wall, slow.
        var glow = 0.14f + (float)(Math.Sin(seconds * 0.2) * 0.04);
        Canvas.Fill(span, 50, 30, 220, 200, Canvas.Lerp(StudioLit, Cream, glow));
        Canvas.Rect(span, 50, 30, 220, 200, WoodDark, 6);
        Canvas.Fill(span, 157, 30, 6, 200, WoodDark);
        Canvas.Fill(span, 50, 127, 220, 6, WoodDark);
    }

    private void DrawEasel(Span<uint> span)
    {
        // Legs behind, the tray, the canvas on it with a shadow.
        Canvas.Line(span, PicX + 100, PicY + PicH + 40, PicX + 40, H - 40, WoodDark);
        Canvas.Line(span, PicX + 101, PicY + PicH + 40, PicX + 41, H - 40, Wood);
        Canvas.Line(span, PicX + PicW - 100, PicY + PicH + 40, PicX + PicW - 40, H - 40, WoodDark);
        Canvas.Line(span, PicX + PicW - 101, PicY + PicH + 40, PicX + PicW - 41, H - 40, Wood);
        Canvas.Line(span, PicX + (PicW / 2), PicY + PicH + 40, PicX + (PicW / 2), H - 40, WoodDark);
        Canvas.Fill(span, PicX + 8, PicY + 8, PicW, PicH, Canvas.Lerp(Studio, Canvas.Black, 0.4f));
        Canvas.Fill(span, PicX - 6, PicY - 6, PicW + 12, PicH + 12, Wood);
        Canvas.Fill(span, PicX - 40, PicY + PicH + 6, PicW + 80, 14, Wood);
        Canvas.Fill(span, PicX - 40, PicY + PicH + 20, PicW + 80, 4, WoodDark);
    }

    private void DrawPalette(Span<uint> span, int x, int y, double seconds)
    {
        // A palette on a stool with today's dabs, and jars behind it.
        Canvas.Fill(span, x + 30, y + 60, 60, 60, WoodDark);
        Canvas.Disc(span, x + 60, y + 40, 58, Canvas.Rgb(0xC8, 0xA8, 0x70));
        Canvas.Disc(span, x + 60, y + 40, 54, Canvas.Rgb(0xD8, 0xB8, 0x80));
        Canvas.Disc(span, x + 92, y + 22, 10, Canvas.Rgb(0xD8, 0xB8, 0x80));
        var dabs = new[] { Canvas.Rgb(0xE0, 0x40, 0x40), Canvas.Rgb(0xE8, 0xC0, 0x40), Canvas.Rgb(0x40, 0x80, 0xE0), Canvas.Rgb(0x40, 0xA0, 0x60), Canvas.Rgb(0xF4, 0xF0, 0xE8), Canvas.Rgb(0x20, 0x18, 0x14) };
        for (var i = 0; i < dabs.Length; i++)
        {
            var a = (i / (double)dabs.Length * Math.PI * 2) - 1.2;
            Canvas.Disc(span, x + 60 + (int)(Math.Cos(a) * 34), y + 40 + (int)(Math.Sin(a) * 34), 8, dabs[i]);
        }

        Canvas.Fill(span, x + 130, y + 70, 22, 50, Canvas.Rgb(0x8A, 0xB0, 0xC8));
        Canvas.Fill(span, x + 158, y + 80, 22, 40, Canvas.Rgb(0xC8, 0xB0, 0x8A));
    }

    private void DrawTitles(Span<uint> span, Vista vista, double into, double seconds, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 0, W, H, Studio);
        Canvas.Fill(span, 140, 120, W - 280, 480, Linen);
        Canvas.Rect(span, 140, 120, W - 280, 480, Wood, 10);
        const string Title = "THE KUPO OF PAINTING";
        font.Draw(span, W, Title, (W - font.Measure(Title, 2)) / 2, 170, Ink, 2, all);
        const string With = "WITH POM ROSS";
        font.Draw(span, W, With, (W - font.Measure(With)) / 2, 260, Faint, 1, all);
        HostSprites.DrawPainter(span, HostSprites.Painter.Wave, seconds, (W / 2) - 112, 300, 7);
        if (into > 3.0)
        {
            var today = "TODAY: " + Canvas.Plain(vista.Name).ToUpperInvariant();
            font.Draw(span, W, Canvas.Cut(today, font.Fit(W - 320)), (W - font.Measure(Canvas.Cut(today, font.Fit(W - 320)))) / 2, 620, Cream, 1, all);
            var place = Canvas.Plain(vista.Place).ToUpperInvariant();
            font.Draw(span, W, place, (W - font.Measure(place)) / 2, 660, Faint, 1, all);
        }
    }

    private void DrawLowerThird(Span<uint> span, string tab, string line, in BitmapFont.Clip all, int page = 0)
    {
        const int Top = 576;
        var whole = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - font.Measure(tab) - 64), 30);
        var pages = Math.Max(1, (whole.Count + 2) / 3);
        var lines = whole.Skip((page % pages) * 3).Take(3).ToList();
        var height = 16 + (lines.Count * 40);
        Canvas.Fill(span, 40, Top, W - 80, height, Ink);
        Canvas.Fill(span, 40, Top, W - 80, 4, Gold);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, Top + 4, tabW, height - 4, Wood);
        font.Draw(span, W, tab, 56, Top + 8, Cream, 1, all);
        var y = Top + 8;
        foreach (var l in lines)
        {
            font.Draw(span, W, l, 40 + tabW + 16, y, Cream, 1, all);
            y += 40;
        }
    }

    private void DrawCard(Span<uint> span, Vista vista, bool finished, in BitmapFont.Clip all)
    {
        // The line above the easel: the name on the left, the place and hours on the right; once
        // it is done, a stamp on the corner of the picture says whether you have been.
        var where = Canvas.Plain(vista.Place).ToUpperInvariant() + "   " + Hours(vista);
        var whereW = font.Measure(where);
        var name = Canvas.Cut(Canvas.Plain(vista.Name).ToUpperInvariant(), font.Fit(PicW - whereW - 40));
        font.Draw(span, W, name, PicX, 18, Cream, 1, all);
        font.Draw(span, W, where, PicX + PicW - whereW, 18, Faint, 1, all);
        if (!finished)
            return;

        var been = found(vista.Index);
        var stamp = been ? "IN YOUR LOG" : "NOT YET FOUND";
        var sx = PicX + PicW - font.Measure(stamp) - 28;
        Canvas.Fill(span, sx - 10, PicY + 14, font.Measure(stamp) + 20, 38, been ? Mint : Gold);
        Canvas.Rect(span, sx - 10, PicY + 14, font.Measure(stamp) + 20, 38, Ink, 2);
        font.Draw(span, W, stamp, sx, PicY + 13, Ink, 1, all);
    }

    private static string Hours(Vista v)
    {
        static string Clock(int t) => $"{t / 100}:{t % 100:00}";
        return v.MinTime == 0 && v.MaxTime == 0 ? "ANY HOUR" : $"{Clock(v.MinTime)} TO {Clock(v.MaxTime)}";
    }

    private static string Quoted(Vista v)
    {
        var text = Canvas.Plain(v.Impression);
        return text.Length > 0 ? text : $"The log says: {v.Name}, {v.Place}. Any hour will do, kupo.";
    }

    private static int Pick(int seed, int count)
    {
        var x = (uint)seed * 2654435761u;
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
