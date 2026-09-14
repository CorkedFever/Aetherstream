namespace Aetherstream.Plugin.Video;

/// <summary>A zone's sky now and for the next three periods, and how common the current one is there.</summary>
internal sealed record ForecastZone(string Name, string[] Periods, int Chance);

internal sealed record ForecastRegion(string Name, IReadOnlyList<ForecastZone> Zones);

internal sealed record ForecastSnapshot(IReadOnlyList<ForecastRegion> Regions, IReadOnlyList<string> Alerts, string Here, long PeriodStart);

/// <summary>
/// The Forecast, with Nimbly. A pixie in a raincoat flutters beside a board of every zone in a
/// region, the sky now and for the next three bells, region by region around the world, with a
/// rare-weather watch along the bottom for the fishers and the gatherers. Eorzea's weather is a
/// function of the clock, so tomorrow is real and the pixie is never wrong, which she mentions.
/// </summary>
internal sealed class ForecastChannel(BitmapFont font, Func<ForecastSnapshot?> data) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double PageFor = 18.0;
    private const int RowsPerPage = 8;
    private const int Period = 1400;

    private static readonly uint Sky = Canvas.Rgb(0x14, 0x22, 0x44);
    private static readonly uint SkyLight = Canvas.Rgb(0x2A, 0x44, 0x7A);
    private static readonly uint Board = Canvas.Rgb(0x0E, 0x18, 0x30);
    private static readonly uint Line = Canvas.Rgb(0x2E, 0x44, 0x6E);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Faint = Canvas.Rgb(0x8A, 0x9A, 0xB8);
    private static readonly uint Gold = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint Rose = Canvas.Rgb(0xFF, 0x8A, 0xB0);
    private static readonly uint Mint = Canvas.Rgb(0x8A, 0xE8, 0xC0);

    private static readonly string[] Patter =
    [
        "The sky does what the sky does, and I happen to know what that is. Pixies do.",
        "If you are waiting on the rain, keep waiting; it is coming, and I will tell you when.",
        "A bell is twenty-three of your minutes and a bit. I do not make the rules. I do read them.",
        "Fishers, gatherers, people who simply like a cloud: this board is for you.",
        "I have never once been wrong about the weather. I have been wrong about other things.",
        "Bring a coat. Or do not. Mortals are so brave about weather.",
        "That one is rare. If you need it, go now. I will still be here, being right.",
        "Il Mheg's forecast is always 'whatever the fae feel like'. I have left it off.",
        "Look at all those little suns. Somebody is having a lovely day. It is probably not you.",
        "The next bell turns in a moment. Watch the board. I love this part.",
    ];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var slot = (int)((unix - 1_700_000_000) / (long)PageFor);
        var into = (unix - 1_700_000_000) % (long)PageFor + (seconds % 1.0);
        this.RenderAt(target, data(), slot, into, now, seconds);
    }

    private void RenderAt(uint[] target, ForecastSnapshot? snapshot, int slot, double into, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);

        // The set: a sky that drifts, a few clouds, and the board.
        for (var y = 0; y < H; y++)
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(SkyLight, Sky, y / (float)H));
        for (var i = 0; i < 5; i++)
        {
            var cx = (int)(((seconds * 12) + (i * 300)) % (W + 200)) - 100;
            var cy = 40 + (i * 60 % 140);
            Canvas.Disc(span, cx, cy, 26, Canvas.Lerp(SkyLight, Cream, 0.15f));
            Canvas.Disc(span, cx + 28, cy + 6, 20, Canvas.Lerp(SkyLight, Cream, 0.15f));
            Canvas.Disc(span, cx - 26, cy + 8, 18, Canvas.Lerp(SkyLight, Cream, 0.15f));
        }

        Canvas.Fill(span, 0, 0, W, 56, Board);
        Canvas.Fill(span, 0, 56, W, 2, Gold);
        font.Draw(span, W, "THE FORECAST", 24, 8, Gold, 1, all);
        font.Draw(span, W, "WITH NIMBLY", 24 + font.Measure("THE FORECAST") + 24, 8, Faint, 1, all);
        var clock = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, clock, W - 24 - font.Measure(clock), 8, Cream, 1, all);

        if (snapshot is null || snapshot.Regions.Count == 0)
        {
            font.Draw(span, W, "THE WEATHER TABLES ARE NOT IN YET", 60, 320, Cream, 1, all);
            HostSprites.DrawPixie(span, HostSprites.Pixie.Hover, seconds, 1000, 300, 6);
            return;
        }

        // Pages: each region, split into eights.
        var pages = new List<(string Title, IReadOnlyList<ForecastZone> Zones)>();
        foreach (var region in snapshot.Regions)
        {
            var parts = (region.Zones.Count + RowsPerPage - 1) / RowsPerPage;
            for (var p = 0; p < parts; p++)
                pages.Add((parts > 1 ? $"{region.Name} ({p + 1}/{parts})" : region.Name, region.Zones.Skip(p * RowsPerPage).Take(RowsPerPage).ToList()));
        }

        var page = pages[slot % pages.Count];
        var next = pages[(slot + 1) % pages.Count];

        // The board: the region, the column headers with the real times, then a row per zone.
        const int BX = 40;
        const int BY = 80;
        const int BW = 900;
        var bh = 60 + (page.Zones.Count * 48) + 12;
        Canvas.Fill(span, BX, BY, BW, bh, Board);
        Canvas.Rect(span, BX, BY, BW, bh, Line, 2);
        font.Draw(span, W, Canvas.Cut(page.Title.ToUpperInvariant(), font.Fit(BW - 40)), BX + 20, BY + 8, Cream, 1, all);

        var colX = new[] { BX + 440, BX + 555, BX + 670, BX + 785 };
        var local = DateTimeOffset.FromUnixTimeSeconds(snapshot.PeriodStart).ToLocalTime();
        for (var c = 0; c < 4; c++)
        {
            var label = c == 0 ? "NOW" : local.AddSeconds(c * Period).ToString("h:mm").ToUpperInvariant();
            font.Draw(span, W, label, colX[c] + 30 - (font.Measure(label) / 2), BY + 8, c == 0 ? Gold : Faint, 1, all);
        }

        Canvas.Fill(span, BX + 10, BY + 52, BW - 20, 2, Line);

        var highlight = (int)(into / (PageFor / Math.Max(1, page.Zones.Count))) % Math.Max(1, page.Zones.Count);
        for (var i = 0; i < page.Zones.Count; i++)
        {
            var zone = page.Zones[i];
            var ry = BY + 60 + (i * 48);
            var here = zone.Name == snapshot.Here;
            if (i == highlight)
                Canvas.Fill(span, BX + 10, ry - 2, BW - 20, 48, Canvas.Lerp(Board, SkyLight, 0.5f));
            var name = Canvas.Cut(zone.Name.ToUpperInvariant(), font.Fit(410));
            font.Draw(span, W, name, BX + 20, ry + 2, here ? Gold : Cream, 1, all);
            if (here)
                Canvas.Disc(span, BX + 12, ry + 22, 4, Gold);
            for (var c = 0; c < 4; c++)
            {
                WeatherGlyphs.Draw(span, zone.Periods[c], colX[c] + 8, ry + 1, 42);
                if (c > 0 && zone.Periods[c] != zone.Periods[c - 1])
                    Canvas.Fill(span, colX[c] - 8, ry + 8, 2, 28, Line);
            }
        }

        // The current row's word, so the glyphs are not a puzzle, and how common it is.
        if (page.Zones.Count > 0)
        {
            var z = page.Zones[highlight];
            var word = $"{z.Name.ToUpperInvariant()}: {z.Periods[0].ToUpperInvariant()} NOW" + (z.Chance > 0 && z.Chance <= 15 ? $" ({z.Chance}% CHANCE, RARE)" : string.Empty)
                + (z.Periods[1] != z.Periods[0] ? $", THEN {z.Periods[1].ToUpperInvariant()}" : ", AND STAYING");
            font.Draw(span, W, Canvas.Cut(word, font.Fit(BW - 40)), BX + 20, BY + bh + 8, Mint, 1, all);
        }

        // The pixie, fluttering by the board, pointing at the row.
        var bob = (int)(Math.Sin(seconds * 3.0) * 8);
        var py = Math.Clamp(BY + 60 + (highlight * 48) - 60 + bob, 70, 520);
        HostSprites.DrawPixie(span, HostSprites.Pixie.Point, seconds, 980, py, 6, flip: true);
        this.DrawLowerThird(span, "NIMBLY", Patter[Pick(slot * 3 + (int)(into / 9.0), Patter.Length)], all);

        // The watch: rare weather ahead anywhere, crawling; else what is next on the board.
        Canvas.Fill(span, 0, 680, W, 40, Board);
        Canvas.Fill(span, 0, 680, W, 2, Rose);
        var watch = snapshot.Alerts.Count > 0
            ? "RARE WEATHER WATCH: " + string.Join("   /   ", snapshot.Alerts.Select(a => a.ToUpperInvariant())) + "   "
            : $"NOTHING RARE ON THE WAY. NEXT: {next.Title.ToUpperInvariant()}   ";
        var width = font.Measure(watch);
        var offset = (int)(seconds * 80.0) % Math.Max(1, width);
        var clip = new BitmapFont.Clip(0, 682, W, 720);
        font.Draw(span, W, watch, -offset, 680, snapshot.Alerts.Count > 0 ? Rose : Faint, 1, clip);
        font.Draw(span, W, watch, -offset + width, 680, snapshot.Alerts.Count > 0 ? Rose : Faint, 1, clip);
    }

    private void DrawLowerThird(Span<uint> span, string tab, string line, in BitmapFont.Clip all)
    {
        const int Top = 590;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - font.Measure(tab) - 64), 2);
        var height = 16 + (lines.Count * 40);
        Canvas.Fill(span, 40, Top, W - 80, height, Board);
        Canvas.Fill(span, 40, Top, W - 80, 4, Gold);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, Top + 4, tabW, height - 4, Canvas.Rgb(0x5A, 0x3A, 0x7A));
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
