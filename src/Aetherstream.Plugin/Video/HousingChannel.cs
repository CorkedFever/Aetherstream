namespace Aetherstream.Plugin.Video;

/// <summary>
/// An open plot as PaissaDB last saw it. Ward and plot are counted from one; size is 0, 1, 2 for
/// small, medium, large; the purchase system is the game's flags (1 free company, 2 individual,
/// 4 lottery); the lottery phase is 1 while entries are taken, 2 while results stand, 3 when it is
/// between; entries is -1 when unknown.
/// </summary>
internal sealed record OpenPlot(string District, int Ward, int Plot, int Size, long Price, int PurchaseSystem, int LottoPhase, int LottoEntries, DateTime LottoPhaseUntil, DateTime LastSeen);

internal sealed record HousingSnapshot(string World, IReadOnlyList<OpenPlot> Plots, DateTime FetchedAt, string Status);

/// <summary>
/// Loporrit Estates. A Loporrit estate agent, Listingway, shows the open plots on your world one
/// at a time, as PaissaDB last saw them: district, ward and plot, the size, the price, whether it
/// is a lottery and how many have entered, and how long ago somebody actually looked. A house of
/// the district's kind is drawn on its plot with the sky by your hour. He is from the moon, and
/// has read a great deal about houses.
/// </summary>
internal sealed class HousingChannel(BitmapFont font, Func<HousingSnapshot?> data) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double PlotFor = 24.0;

    private static readonly uint Studio = Canvas.Rgb(0x1C, 0x22, 0x3A);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Ink = Canvas.Rgb(0x10, 0x0E, 0x1A);
    private static readonly uint Faint = Canvas.Rgb(0x9A, 0x9E, 0xB8);
    private static readonly uint Gold = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint Mint = Canvas.Rgb(0x8A, 0xE0, 0xB0);
    private static readonly uint Rose = Canvas.Rgb(0xFF, 0x8A, 0xB0);
    private static readonly uint Card = Canvas.Rgb(0x26, 0x2C, 0x4A);
    private static readonly uint Grass = Canvas.Rgb(0x5A, 0x9A, 0x4A);
    private static readonly uint GrassDark = Canvas.Rgb(0x3E, 0x74, 0x34);
    private static readonly uint Sand = Canvas.Rgb(0xE0, 0xC8, 0x90);
    private static readonly uint Snow = Canvas.Rgb(0xEC, 0xF0, 0xF6);

    private static readonly string[] Pitch =
    [
        "Now this one has what we on the moon call 'a ground'. Very popular down here. You stand on it.",
        "I have read that Eorzeans keep their belongings inside the house. This one has an inside. Perfect.",
        "The neighbours are lovely. I have not met them. But statistically, lovely.",
        "Plenty of room for a garden, a workshop, or forty-one carbuncles. I am not here to judge.",
        "It comes with a door. I checked. Doors are important; that is how the house knows you are home.",
        "A little bird told me this plot is going fast. The bird was a chocobo. Big bird.",
        "On the moon we did not have weather. Here your house gets weather for free. Every day!",
        "The view is described as 'breathtaking'. Please continue breathing; the plot is not worth that.",
        "Ideal for a free company, a family, or one person with a great many minions.",
        "I am told the walls are load-bearing, which I understand is the best kind of bearing.",
        "You could put a very large fish on the wall. I am simply reporting what people do.",
        "Location, location, location. I say it three times because it is three plots down from the aetheryte.",
    ];

    private static readonly string[] Empty =
    [
        "Nothing open on {world} right now. That is not a failure; that is a very popular world.",
        "Every plot on {world} is spoken for. Have you considered an apartment? Very moon-like. Compact.",
        "No listings on {world} at the moment. Somebody go look at a ward so I have something to say.",
    ];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var snapshot = data();
        var plots = snapshot?.Plots ?? [];
        var world = snapshot?.World ?? string.Empty;

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var slot = (int)((unix - 1_700_000_000) / (long)PlotFor);
        var into = (unix - 1_700_000_000) % (long)PlotFor + (seconds % 1.0);
        this.RenderAt(target, snapshot, slot, into, now, seconds);
    }

    /// <summary>One frame of one listing slot; the harness calls this directly.</summary>
    private void RenderAt(uint[] target, HousingSnapshot? snapshot, int slot, double into, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var plots = snapshot?.Plots ?? [];
        var world = snapshot?.World ?? string.Empty;

        this.DrawHeader(span, world, into, plots.Count > 0, all);

        if (plots.Count == 0)
        {
            this.DrawScene(span, null, now, seconds);
            HostSprites.DrawLoporrit(span, HostSprites.Loporrit.Talk, seconds, 60, 330, 7);
            var reason = snapshot?.Status is { Length: > 0 } s ? s.ToUpperInvariant()
                : Empty[Pick(slot / 4, Empty.Length)].Replace("{world}", world.Length > 0 ? world : "this world");
            this.DrawLowerThird(span, "LISTINGWAY", reason, all);
            this.DrawFooter(span, plots, snapshot, now, all);
            return;
        }

        var plot = plots[slot % plots.Count];
        var next = plots[(slot + 1) % plots.Count];
        this.DrawScene(span, plot, now, seconds);
        var excited = into is > 6 and < 11 || into > 19;
        HostSprites.DrawLoporrit(span, excited ? HostSprites.Loporrit.Point : HostSprites.Loporrit.Talk, seconds, 60, 330, 7);
        this.DrawCard(span, plot, now, all);

        var line = Pitch[Pick((slot * 7) + (int)(into / 8.0), Pitch.Length)];
        this.DrawLowerThird(span, "LISTINGWAY", line, all);
        this.DrawFooter(span, plots, snapshot, now, all, next);
    }

    // -- the parts ---------------------------------------------------------------------------------------------

    private void DrawHeader(Span<uint> span, string world, double into, bool listing, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 0, W, 56, Ink);
        Canvas.Fill(span, 24, 10, 36, 36, Gold);
        Canvas.Fill(span, 32, 22, 20, 24, Ink);
        Canvas.Fill(span, 36, 30, 12, 16, Gold);
        font.Draw(span, W, "LOPORRIT ESTATES", 72, 8, Gold, 1, all);
        var where = world.Length > 0 ? $"OPEN PLOTS ON {world.ToUpperInvariant()}" : "OPEN PLOTS";
        font.Draw(span, W, where, 72 + font.Measure("LOPORRIT ESTATES") + 32, 8, Faint, 1, all);
        if (listing)
        {
            var countdown = $"NEXT 0:{(int)(PlotFor - into):00}";
            font.Draw(span, W, countdown, W - 120 - font.Measure(countdown), 8, Faint, 1, all);
        }

        Canvas.Fill(span, W - 100, 12, 76, 32, Rose);
        font.Draw(span, W, "LIVE", W - 94, 8, Cream, 1, all);
    }

    /// <summary>The plot: a sky by the hour, the district's ground, and a house of the district's kind at the size on offer.</summary>
    private void DrawScene(Span<uint> span, OpenPlot? plot, DateTime now, double seconds)
    {
        var hour = now.Hour + (now.Minute / 60f);
        var daylight = Math.Clamp(1f - (Math.Abs(hour - 13f) / 7f), 0f, 1f);
        var district = plot?.District ?? "Mist";
        var (skyDay, ground, groundDark) = district switch
        {
            var d when d.Contains("Lavender") => (Canvas.Rgb(0x9A, 0xD0, 0xF0), Grass, GrassDark),
            var d when d.Contains("Goblet") => (Canvas.Rgb(0xF0, 0xD8, 0xA0), Sand, Canvas.Rgb(0xC0, 0x98, 0x54)),
            var d when d.Contains("Shirogane") => (Canvas.Rgb(0xB0, 0xD8, 0xF8), Canvas.Rgb(0x7A, 0xA0, 0x60), Canvas.Rgb(0x5A, 0x7A, 0x44)),
            var d when d.Contains("Empyreum") => (Canvas.Rgb(0xC8, 0xD8, 0xE8), Snow, Canvas.Rgb(0xC0, 0xC8, 0xD8)),
            _ => (Canvas.Rgb(0xA8, 0xDC, 0xFF), Sand, Canvas.Rgb(0xC8, 0xB4, 0x7A)),
        };
        var sky = Canvas.Lerp(Canvas.Rgb(0x10, 0x18, 0x30), skyDay, daylight);
        for (var y = 56; y < 420; y++)
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(sky, Canvas.Lerp(sky, Cream, 0.25f), (y - 56) / 364f));

        // The moon, always. He likes to see home.
        Canvas.Disc(span, W - 160, 120, 26, Canvas.Lerp(Cream, sky, 0.3f));
        Canvas.Disc(span, W - 150, 112, 5, Canvas.Lerp(Cream, sky, 0.5f));
        Canvas.Disc(span, W - 168, 128, 4, Canvas.Lerp(Cream, sky, 0.5f));

        if (district.Contains("Mist"))
            Canvas.Fill(span, 0, 380, W, 40, Canvas.Lerp(Canvas.Rgb(0x3A, 0x7A, 0xA8), sky, 0.3f));

        Canvas.Fill(span, 0, 420, W, 160, ground);
        Canvas.Fill(span, 0, 420, W, 4, groundDark);
        for (var x = 0; x < W; x += 90)
            Canvas.Fill(span, x + ((int)(seconds * 0) % 90), 500 + ((x / 90) % 3 * 14), 30, 3, groundDark);

        if (plot is null)
        {
            // An empty lot with a sign.
            Canvas.Fill(span, 460, 430, 8, 80, Canvas.Rgb(0x5E, 0x42, 0x26));
            Canvas.Fill(span, 390, 396, 150, 40, Cream);
            Canvas.Rect(span, 390, 396, 150, 40, Ink, 2);
            font.Draw(span, W, "ALL SOLD", 401, 394, Ink, 1, new BitmapFont.Clip(0, 0, W, H));
            return;
        }

        this.DrawHouse(span, plot, 450, 430, seconds, now);
    }

    /// <summary>
    /// A house in the district's own style: Lominsan stucco under terracotta in Mist, Gridanian
    /// timber frame under shingles in the Lavender Beds, Ul'dahn sandstone with a parapet in the
    /// Goblet, Hingan plaster over dark wood under a curved tiled roof in Shirogane, and Ishgardian
    /// stone under steep slate with a spire in Empyreum. Small is a cottage, medium a house, and
    /// large a mansion with two floors and a wing each side.
    /// </summary>
    private void DrawHouse(Span<uint> span, OpenPlot plot, int cx, int groundY, double seconds, DateTime now)
    {
        var (w, h, floors) = plot.Size switch { 2 => (320, 240, 2), 1 => (270, 170, 1), _ => (190, 130, 1) };
        var d = plot.District;
        var style = d.Contains("Lavender") ? 1 : d.Contains("Goblet") ? 2 : d.Contains("Shirogane") ? 3 : d.Contains("Empyreum") ? 4 : 0;
        var lit = now.Hour is < 6 or >= 18;
        var glass = lit ? Gold : Canvas.Rgb(0x8A, 0xB8, 0xD8);
        var x = cx - (w / 2);

        // Wings on a mansion: a lower block each side, drawn first so the main block sits in front.
        if (plot.Size == 2)
        {
            var wingW = 90;
            var wingH = h * 3 / 5;
            this.Block(span, style, x - wingW + 10, groundY, wingW, wingH, 1, glass, seconds, wing: true);
            this.Block(span, style, x + w - 10, groundY, wingW, wingH, 1, glass, seconds, wing: true);
        }

        this.Block(span, style, x, groundY, w, h, floors, glass, seconds, wing: false);

        // The door, and a plaque with the plot number beside it.
        var wall = WallOf(style);
        var trim = Canvas.Lerp(wall, Canvas.Black, 0.4f);
        var doorW = Math.Max(28, w / 8);
        var doorH = Math.Min(80, h / 2);
        var dx = x + (w / 2) - (doorW / 2);
        if (style == 2)
            Canvas.Disc(span, x + (w / 2), groundY - doorH, doorW / 2, trim);
        Canvas.Fill(span, dx, groundY - doorH, doorW, doorH, style == 3 ? Canvas.Rgb(0x8A, 0x2A, 0x2A) : trim);
        if (style == 3)
            Canvas.Fill(span, dx + (doorW / 2) - 2, groundY - doorH, 4, doorH, Canvas.Rgb(0x3A, 0x1A, 0x1A));
        else
            Canvas.Disc(span, dx + doorW - 7, groundY - (doorH / 2), 3, Gold);
        if (plot.Size == 2)
        {
            Canvas.Fill(span, dx - 16, groundY - 8, doorW + 32, 8, Canvas.Lerp(wall, Canvas.White, 0.25f));
            Canvas.Fill(span, dx - 26, groundY - 4, doorW + 52, 4, Canvas.Lerp(wall, Canvas.White, 0.1f));
        }

        var all = new BitmapFont.Clip(0, 0, W, H);
        var num = plot.Plot.ToString();
        var px = dx + doorW + 10;
        Canvas.Fill(span, px, groundY - doorH + 6, font.Measure(num) + 12, 30, Cream);
        Canvas.Rect(span, px, groundY - doorH + 6, font.Measure(num) + 12, 30, Ink, 2);
        font.Draw(span, W, num, px + 6, groundY - doorH + 2, Ink, 1, all);

        // The garden: what grows there. A mansion's wings take the room a tree would.
        if (plot.Size == 2)
            return;

        var sway = (int)(Math.Sin(seconds * 0.8) * 3);
        var tx = x + w + (plot.Size == 2 ? 130 : 70);
        switch (style)
        {
            case 0:
                Palm(span, tx, groundY, sway);
                Canvas.Fill(span, x - 90, groundY - 22, 60, 5, Cream);
                for (var i = 0; i < 4; i++)
                    Canvas.Fill(span, x - 88 + (i * 16), groundY - 30, 6, 30, Cream);
                break;
            case 1:
                Canvas.Fill(span, tx - 6, groundY - 70, 12, 70, Canvas.Rgb(0x5E, 0x42, 0x26));
                Canvas.Disc(span, tx + sway, groundY - 96, 42, GrassDark);
                Canvas.Disc(span, tx + sway - 10, groundY - 108, 28, Grass);
                for (var i = 0; i < 6; i++)
                    Canvas.Disc(span, x - 80 + (i * 14), groundY - 10 - ((i % 2) * 4), 8, Canvas.Rgb(0x9A, 0x6A, 0xC8));
                break;
            case 2:
                Palm(span, tx, groundY, sway);
                Canvas.Fill(span, x - 60, groundY - 40, 14, 40, Canvas.Rgb(0x5A, 0x8A, 0x4A));
                Canvas.Fill(span, x - 74, groundY - 30, 12, 6, Canvas.Rgb(0x5A, 0x8A, 0x4A));
                Canvas.Fill(span, x - 74, groundY - 30, 6, 16, Canvas.Rgb(0x5A, 0x8A, 0x4A));
                break;
            case 3:
                Canvas.Fill(span, tx - 5, groundY - 60, 10, 60, Canvas.Rgb(0x4A, 0x32, 0x1E));
                for (var i = 0; i < 3; i++)
                    Canvas.Fill(span, tx - 40 + (i * 8) + sway, groundY - 60 - (i * 26), 80 - (i * 16), 16, i % 2 == 0 ? Canvas.Rgb(0x2E, 0x5C, 0x3C) : Canvas.Rgb(0x3E, 0x74, 0x44));
                Canvas.Fill(span, x - 90, groundY - 6, 70, 6, Canvas.Rgb(0x8A, 0x8A, 0x8A));
                Canvas.Disc(span, x - 70, groundY - 12, 10, Canvas.Rgb(0x9A, 0x9A, 0xA0));
                break;
            default:
                Canvas.Fill(span, tx - 4, groundY - 70, 8, 70, Canvas.Rgb(0x4A, 0x40, 0x3A));
                Canvas.Line(span, tx, groundY - 60, tx - 24 + sway, groundY - 90, Canvas.Rgb(0x4A, 0x40, 0x3A));
                Canvas.Line(span, tx, groundY - 50, tx + 26 + sway, groundY - 84, Canvas.Rgb(0x4A, 0x40, 0x3A));
                Canvas.Line(span, tx, groundY - 40, tx - 18 + sway, groundY - 62, Canvas.Rgb(0x4A, 0x40, 0x3A));
                Canvas.Fill(span, x - 90, groundY - 30, 40, 30, Canvas.Rgb(0x7A, 0x7A, 0x8A));
                Canvas.Fill(span, x - 90, groundY - 34, 40, 6, Snow);
                break;
        }
    }

    private static uint WallOf(int style) => style switch
    {
        1 => Canvas.Rgb(0xEA, 0xDC, 0xBC),
        2 => Canvas.Rgb(0xD8, 0xB0, 0x78),
        3 => Canvas.Rgb(0xF0, 0xEA, 0xDC),
        4 => Canvas.Rgb(0xA8, 0xA8, 0xB4),
        _ => Canvas.Rgb(0xF4, 0xEE, 0xE0),
    };

    /// <summary>One block of a house: walls, the roof of the style, and windows; a wing is a smaller block with no door.</summary>
    private void Block(Span<uint> span, int style, int x, int groundY, int w, int h, int floors, uint glass, double seconds, bool wing)
    {
        var wall = WallOf(style);
        var trim = Canvas.Lerp(wall, Canvas.Black, 0.4f);
        var roofH = style == 4 ? h * 2 / 5 : h / 3;
        var y = groundY - h;
        var wallTop = y + roofH;
        var wallH = h - roofH;

        Canvas.Fill(span, x + 10, groundY - 6, w, 12, Canvas.Lerp(GrassDark, Canvas.Black, 0.3f));
        Canvas.Fill(span, x, wallTop, w, wallH, wall);

        switch (style)
        {
            case 1:
                // Timber frame: dark beams, uprights every so often, a diagonal in each bay.
                Canvas.Rect(span, x, wallTop, w, wallH, Canvas.Rgb(0x5A, 0x3E, 0x24), 4);
                for (var bx = x; bx < x + w; bx += 60)
                {
                    Canvas.Fill(span, bx, wallTop, 4, wallH, Canvas.Rgb(0x5A, 0x3E, 0x24));
                    Canvas.Line(span, bx + 4, wallTop + 4, Math.Min(x + w - 4, bx + 56), wallTop + 30, Canvas.Rgb(0x5A, 0x3E, 0x24));
                }

                Canvas.Fill(span, x, wallTop + 34, w, 4, Canvas.Rgb(0x5A, 0x3E, 0x24));
                HipRoof(span, x, y, w, roofH, 16, Canvas.Rgb(0x6A, 0x7A, 0x3E), Canvas.Rgb(0x4E, 0x5A, 0x2C), 0.5f, 7);
                Canvas.Fill(span, x + w - 44, y + 6, 20, roofH - 4, Canvas.Rgb(0x8A, 0x5A, 0x3A));
                break;
            case 2:
                // Sandstone: courses of blocks, a parapet with merlons, and on a wing a small dome.
                for (var cy = wallTop + 16; cy < groundY; cy += 16)
                    Canvas.Fill(span, x, cy, w, 1, Canvas.Lerp(wall, Canvas.Black, 0.12f));
                Canvas.Rect(span, x, wallTop, w, wallH, trim, 2);
                Canvas.Fill(span, x - 6, wallTop - 10, w + 12, 12, Canvas.Lerp(wall, Canvas.Black, 0.15f));
                for (var mx = x - 6; mx < x + w + 6; mx += 26)
                    Canvas.Fill(span, mx, wallTop - 22, 14, 12, Canvas.Lerp(wall, Canvas.Black, 0.15f));
                if (wing)
                {
                    Canvas.Fill(span, x + (w / 2) - 16, wallTop - 40, 32, 20, Canvas.Lerp(wall, Canvas.Black, 0.2f));
                    Canvas.Disc(span, x + (w / 2), wallTop - 40, 22, Canvas.Rgb(0x6A, 0x8A, 0xA8));
                }

                break;
            case 3:
                // Hingan: dark wood below, white plaster above, a curved tiled roof with wide eaves.
                Canvas.Fill(span, x, groundY - (wallH / 2), w, wallH / 2, Canvas.Rgb(0x4A, 0x32, 0x1E));
                Canvas.Fill(span, x, groundY - (wallH / 2), w, 4, Canvas.Rgb(0x8A, 0x2A, 0x2A));
                Canvas.Rect(span, x, wallTop, w, wallH, Canvas.Rgb(0x3A, 0x26, 0x16), 3);
                CurvedRoof(span, x, y, w, roofH, 28, Canvas.Rgb(0x3E, 0x44, 0x5A), Canvas.Rgb(0x2A, 0x2E, 0x40));
                break;
            case 4:
                // Ishgardian: stone blocks, a steep slate roof with snow along the eaves and a spire.
                for (var cy = wallTop + 14; cy < groundY; cy += 14)
                {
                    Canvas.Fill(span, x, cy, w, 1, Canvas.Lerp(wall, Canvas.Black, 0.18f));
                    for (var bx = x + ((cy / 14) % 2 * 20); bx < x + w; bx += 40)
                        Canvas.Fill(span, bx, cy - 14, 1, 14, Canvas.Lerp(wall, Canvas.Black, 0.12f));
                }

                Canvas.Rect(span, x, wallTop, w, wallH, trim, 3);
                HipRoof(span, x, y, w, roofH, 10, Canvas.Rgb(0x46, 0x50, 0x70), Canvas.Rgb(0x34, 0x3C, 0x58), 0.12f, 6);
                Canvas.Fill(span, x - 10, y + roofH - 6, w + 20, 6, Snow);
                if (!wing)
                {
                    Canvas.Fill(span, x + (w / 2) - 4, y - 34, 8, 40, Canvas.Rgb(0x34, 0x3C, 0x58));
                    Canvas.Fill(span, x + (w / 2) - 2, y - 46, 4, 14, Gold);
                }

                break;
            default:
                // Lominsan: stucco, terracotta tiles, blue shutters.
                Canvas.Rect(span, x, wallTop, w, wallH, trim, 2);
                HipRoof(span, x, y, w, roofH, 14, Canvas.Rgb(0xC8, 0x5A, 0x3A), Canvas.Rgb(0xA0, 0x42, 0x2A), 0.45f, 6);
                Canvas.Fill(span, x + w - 40, y + 8, 18, roofH - 6, Canvas.Lerp(wall, Canvas.Black, 0.2f));
                break;
        }

        // Windows: a row per floor; the ground floor leaves the middle for the door unless this is a wing.
        var perRow = Math.Max(1, w / 90);
        for (var f = 0; f < floors; f++)
        {
            var wy = floors == 2 && f == 0 ? wallTop + 18 : groundY - Math.Min(80, h / 2) + 12;
            if (floors == 1 && !wing)
                wy = wallTop + (wallH / 2) - 30;
            for (var i = 0; i < perRow; i++)
            {
                var wx = perRow == 1 ? x + (w / 2) - 18 : x + 22 + (i * (w - 44 - 36) / (perRow - 1));
                if (!wing && f == floors - 1 && Math.Abs(wx + 18 - (x + (w / 2))) < (w / 8) + 24)
                    continue;
                Window(span, style, wx, wy, glass, trim);
            }
        }
    }

    private static void Window(Span<uint> span, int style, int wx, int wy, uint glass, uint trim)
    {
        switch (style)
        {
            case 2:
                Canvas.Disc(span, wx + 16, wy + 10, 12, trim);
                Canvas.Fill(span, wx + 4, wy + 10, 24, 30, trim);
                Canvas.Disc(span, wx + 16, wy + 12, 9, glass);
                Canvas.Fill(span, wx + 7, wy + 12, 18, 25, glass);
                break;
            case 3:
                Canvas.Fill(span, wx, wy, 36, 36, Canvas.Lerp(glass, Canvas.White, 0.5f));
                Canvas.Rect(span, wx, wy, 36, 36, trim, 3);
                for (var g = 9; g < 36; g += 9)
                {
                    Canvas.Fill(span, wx + g, wy, 2, 36, trim);
                    Canvas.Fill(span, wx, wy + g, 36, 2, trim);
                }

                break;
            case 4:
                Canvas.Disc(span, wx + 14, wy + 10, 12, trim);
                Canvas.Fill(span, wx + 2, wy + 10, 24, 34, trim);
                Canvas.Disc(span, wx + 14, wy + 12, 8, glass);
                Canvas.Fill(span, wx + 6, wy + 12, 16, 28, glass);
                Canvas.Fill(span, wx + 13, wy + 6, 2, 36, trim);
                break;
            default:
                Canvas.Fill(span, wx, wy, 36, 36, glass);
                Canvas.Rect(span, wx, wy, 36, 36, trim, 3);
                Canvas.Fill(span, wx + 16, wy, 4, 36, trim);
                if (style == 0)
                {
                    Canvas.Fill(span, wx - 12, wy, 10, 36, Canvas.Rgb(0x3A, 0x6A, 0xA8));
                    Canvas.Fill(span, wx + 38, wy, 10, 36, Canvas.Rgb(0x3A, 0x6A, 0xA8));
                }

                break;
        }
    }

    /// <summary>A hipped roof: a flat ridge of <paramref name="ridge"/> the width, sloping out past the walls by <paramref name="overhang"/>, with tile lines every <paramref name="course"/> rows.</summary>
    private static void HipRoof(Span<uint> span, int x, int y, int w, int roofH, int overhang, uint colour, uint dark, float ridge, int course)
    {
        var top = (int)(w * ridge);
        var bottom = w + (2 * overhang);
        for (var i = 0; i < roofH; i++)
        {
            var width = top + ((bottom - top) * i / roofH);
            var c = i % course == course - 1 ? dark : colour;
            Canvas.Fill(span, x + (w / 2) - (width / 2), y + i, width, 1, c);
        }

        Canvas.Fill(span, x - overhang, y + roofH - 4, bottom, 4, dark);
        Canvas.Fill(span, x + (w / 2) - (top / 2), y, top, 3, dark);
    }

    /// <summary>A Hingan roof: the eaves sweep out and up, the ridge is capped, tiles in courses.</summary>
    private static void CurvedRoof(Span<uint> span, int x, int y, int w, int roofH, int overhang, uint colour, uint dark)
    {
        var top = w / 3;
        var bottom = w + (2 * overhang);
        for (var i = 0; i < roofH; i++)
        {
            var t = i / (float)roofH;
            var width = top + (int)((bottom - top) * MathF.Pow(t, 1.7f));
            var c = i % 6 == 5 ? dark : colour;
            Canvas.Fill(span, x + (w / 2) - (width / 2), y + i, width, 2, c);
        }

        // The eave line, turned up at the ends.
        Canvas.Fill(span, x - overhang, y + roofH - 5, bottom, 5, dark);
        Canvas.Fill(span, x - overhang, y + roofH - 12, 8, 8, dark);
        Canvas.Fill(span, x + w + overhang - 8, y + roofH - 12, 8, 8, dark);
        Canvas.Fill(span, x + (w / 2) - (top / 2) - 6, y - 2, top + 12, 6, dark);
    }

    private static void Palm(Span<uint> span, int tx, int groundY, int sway)
    {
        for (var i = 0; i < 70; i += 6)
            Canvas.Fill(span, tx - 4 + (i * i / 900), groundY - i, 9, 6, i % 12 == 0 ? Canvas.Rgb(0x8A, 0x6A, 0x3A) : Canvas.Rgb(0x7A, 0x5A, 0x2E));
        var top = (tx + 5, groundY - 72);
        foreach (var (dx, dy) in new[] { (-40, -6), (-28, -22), (0, -30), (28, -22), (42, -4), (-34, 10), (36, 12) })
            Canvas.Line(span, top.Item1, top.Item2, top.Item1 + dx + sway, top.Item2 + dy, Canvas.Rgb(0x3E, 0x8A, 0x44));
        Canvas.Disc(span, top.Item1, top.Item2, 6, Canvas.Rgb(0x6A, 0x4A, 0x2A));
    }

    private void DrawCard(Span<uint> span, OpenPlot plot, DateTime now, in BitmapFont.Clip all)
    {
        const int X = 700;
        const int Y = 76;
        const int CardW = 540;
        Canvas.Fill(span, X, Y, CardW, 270, Card);
        Canvas.Fill(span, X, Y, CardW, 6, Gold);
        var y = Y + 16;
        var district = Canvas.Plain(plot.District).ToUpperInvariant();
        if (district.StartsWith("THE ", StringComparison.Ordinal))
            district = district[4..];
        font.Draw(span, W, Canvas.Cut(district, font.Fit(CardW - 40, 2)), X + 20, y, Cream, 2, all);
        y += 84;
        var where = $"WARD {plot.Ward}, PLOT {plot.Plot}   {SizeName(plot.Size)}";
        font.Draw(span, W, where, X + 20, y, Cream, 1, all);
        y += 40;
        font.Draw(span, W, $"{plot.Price:N0} GIL", X + 20, y, Gold, 1, all);

        // Who may buy, as stickers on the price line.
        var sx = X + CardW - 20;
        foreach (var (flag, label, c) in new[] { (2, "PERSONAL", Rose), (1, "FC", Mint) })
        {
            if ((plot.PurchaseSystem & flag) == 0)
                continue;
            var lw = font.Measure(label) + 16;
            sx -= lw + 8;
            Canvas.Fill(span, sx, y + 4, lw, 30, c);
            font.Draw(span, W, label, sx + 8, y, Ink, 1, all);
        }

        y += 40;
        var (phase, colour) = PhaseOf(plot, now);
        font.Draw(span, W, Canvas.Cut(phase, font.Fit(CardW - 40)), X + 20, y, colour, 1, all);
        y += 40;
        var seen = Ago(now.ToUniversalTime() - plot.LastSeen);
        font.Draw(span, W, $"LAST SEEN {seen}", X + 20, y, Faint, 1, all);
    }

    private static (string Text, uint Colour) PhaseOf(OpenPlot plot, DateTime now)
    {
        if ((plot.PurchaseSystem & 4) == 0)
            return ("FIRST COME, FIRST SERVED", Mint);

        var left = plot.LottoPhaseUntil > DateTime.MinValue ? ", " + Ago(plot.LottoPhaseUntil - now.ToUniversalTime(), future: true) + " LEFT" : string.Empty;
        return plot.LottoPhase switch
        {
            1 => (plot.LottoEntries >= 0 ? $"LOTTERY: {plot.LottoEntries} ENTERED{left}" : $"LOTTERY OPEN{left}", Mint),
            2 => ($"RESULTS UP{left}", Gold),
            3 => (plot.LottoPhaseUntil > DateTime.MinValue ? $"NEXT LOTTERY IN {Ago(plot.LottoPhaseUntil - now.ToUniversalTime(), future: true)}" : "BETWEEN LOTTERIES", Faint),
            _ => ("LOTTERY", Faint),
        };
    }

    private static string SizeName(int size) => size switch { 2 => "LARGE", 1 => "MEDIUM", _ => "SMALL" };

    private static string Ago(TimeSpan span, bool future = false)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        var text = span.TotalMinutes < 1 ? "MOMENTS" : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} MIN" : span.TotalDays < 1 ? $"{(int)span.TotalHours} HR" : $"{(int)span.TotalDays} DAYS";
        return future ? text : text + " AGO";
    }

    private void DrawLowerThird(Span<uint> span, string tab, string line, in BitmapFont.Clip all)
    {
        const int Top = 590;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - font.Measure(tab) - 64), 2);
        var height = 16 + (lines.Count * 40);
        Canvas.Fill(span, 40, Top, W - 80, height, Ink);
        Canvas.Fill(span, 40, Top, W - 80, 4, Gold);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, Top + 4, tabW, height - 4, Canvas.Rgb(0x4A, 0x3A, 0x6A));
        font.Draw(span, W, tab, 56, Top + 8, Cream, 1, all);
        var y = Top + 8;
        foreach (var l in lines)
        {
            font.Draw(span, W, l, 40 + tabW + 16, y, Cream, 1, all);
            y += 40;
        }
    }

    private void DrawFooter(Span<uint> span, IReadOnlyList<OpenPlot> plots, HousingSnapshot? snapshot, DateTime now, in BitmapFont.Clip all, OpenPlot? next = null)
    {
        Canvas.Fill(span, 0, 680, W, 40, Ink);
        Canvas.Fill(span, 0, 680, W, 2, Card);
        string text;
        if (plots.Count == 0)
        {
            text = snapshot is { FetchedAt: var at } && at > DateTime.MinValue ? $"CHECKED {Ago(now.ToUniversalTime() - at)}   /   DATA: PAISSADB, FROM PLAYERS WHO LOOKED" : "DATA: PAISSADB, FROM PLAYERS WHO LOOKED";
        }
        else
        {
            var counts = plots.GroupBy(p => p.District).Select(g => $"{Canvas.Plain(g.Key).ToUpperInvariant()} {g.Count()}");
            text = $"{plots.Count} OPEN: " + string.Join("  ", counts) + (next is null ? string.Empty : $"   /   NEXT: {Canvas.Plain(next.District).ToUpperInvariant()} W{next.Ward} P{next.Plot}");
        }

        font.Draw(span, W, Canvas.Cut(text, font.Fit(W - 48)), 24, 680, Faint, 1, all);
    }

    private static int Pick(int seed, int count)
    {
        var x = (uint)seed * 2654435761u;
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
