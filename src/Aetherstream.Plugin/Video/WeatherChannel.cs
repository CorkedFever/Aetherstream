namespace Aetherstream.Plugin.Video;

/// <summary>One weather period: when it starts, in real time, and what it is.</summary>
internal readonly record struct WeatherPeriod(long StartUnix, uint WeatherId, string Name);

/// <summary>Somewhere else, and what it is doing there.</summary>
internal readonly record struct WeatherElsewhere(string Zone, uint WeatherId, string Name);

/// <summary>The forecast as the plugin knows it this second.</summary>
internal sealed record WeatherSnapshot(
    string Zone,
    string Region,
    WeatherPeriod Now,
    IReadOnlyList<WeatherPeriod> Next,
    IReadOnlyList<WeatherElsewhere> Around,
    IReadOnlyList<string> Alerts,
    string LiveAlert);

/// <summary>
/// The weather channel: Local on the 8s, for Eorzea.
/// <para>
/// The zone you are standing in and what the sky is doing, when it changes, what comes next,
/// and a crawl of every other zone underneath. Eorzea's weather is a function of the clock, so
/// all of it is computed rather than fetched — no network, and it is never out of date.
/// </para>
/// </summary>
internal sealed class WeatherChannel(BitmapFont font, Func<WeatherSnapshot?> data) : IFrameChannel
{
    private const int Width = 1280;
    private const int Height = 720;

    // 0xAABBGGRR, libvlc order.
    private const uint Sky = 0xFF3A1E0Cu;       // 0C1E3A, a weather-channel blue
    private const uint SkyDeep = 0xFF26150Au;   // 0A1526
    private const uint Band = 0xFF52301Au;      // 1A3052
    private const uint Edge = 0xFF6B4A1Eu;      // 1E4A6B
    private const uint Accent = 0xFFFFC76Bu;    // 6BC7FF
    private const uint White = 0xFFFBF1E6u;     // E6F1FB
    private const uint Dim = 0xFFC8B09Fu;       // 9FB0C8
    private const uint Faint = 0xFF806E5Fu;     // 5F6E80
    private const uint Amber = 0xFF279FEFu;     // EF9F27

    /// <summary>Real seconds per weather period: eight Eorzean hours.</summary>
    private const int PeriodSeconds = 1400;

    private const double CrawlSpeed = 80.0;

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, Width, Height);

        Fill(span, 0, 0, Width, Height, Sky);

        var snapshot = data();
        var unix = DateTimeOffset.Now.ToUnixTimeSeconds();

        this.DrawHeader(span, now, unix, all, snapshot?.LiveAlert ?? string.Empty, seconds);

        if (snapshot is null)
        {
            const string Wait = "READING THE SKY";
            font.Draw(span, Width, Wait, (Width - font.Measure(Wait, 2)) / 2, 330, Faint, 2, all);
            this.DrawFooter(span, all);
            return;
        }

        this.DrawCurrent(span, snapshot, unix, all);
        this.DrawForecast(span, snapshot, unix, all, seconds);
        this.DrawAround(span, snapshot, seconds);
        this.DrawFooter(span, all);
    }

    private void DrawHeader(Span<uint> span, DateTime now, long unix, in BitmapFont.Clip clip, string liveAlert, double seconds)
    {
        if (liveAlert.Length > 0)
        {
            // A special alert: the band turns red and pulses, and says what is happening.
            var pulse = (int)(seconds * 2) % 2 == 0;
            Fill(span, 0, 0, Width, 56, pulse ? Canvas.Rgb(0xA3, 0x2D, 0x2D) : Canvas.Rgb(0x79, 0x1F, 0x1F));
            Fill(span, 0, 56, Width, 2, Canvas.Bad);
            var text = Canvas.Cut($"SPECIAL ALERT   {liveAlert.ToUpperInvariant()}", font.Fit(Width - 48));
            font.Draw(span, Width, text, 24, 8, White, 1, clip);
            return;
        }

        Fill(span, 0, 0, Width, 56, Band);
        Fill(span, 0, 56, Width, 2, Edge);

        font.Draw(span, Width, "LOCAL FORECAST", 24, 8, Amber, 1, clip);

        // Eorzea time: a day is seventy real minutes.
        var etMinutes = unix * 24 / 70;
        var et = $"ET {etMinutes / 60 % 24:00}:{etMinutes % 60:00}";
        font.Draw(span, Width, et, (Width - font.Measure(et)) / 2, 8, White, 1, clip);

        var local = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, Width, local, Width - 24 - font.Measure(local), 8, White, 1, clip);
    }

    private void DrawCurrent(Span<uint> span, WeatherSnapshot s, long unix, in BitmapFont.Clip clip)
    {
        // The big icon, then the words beside it.
        WeatherIcons.Draw(span, Width, Height, s.Now.WeatherId, 48, 84, 12);

        const int Left = 260;
        font.Draw(span, Width, Plain(s.Zone).ToUpperInvariant(), Left, 84, White, 2, clip);
        font.Draw(span, Width, Plain(s.Region).ToUpperInvariant(), Left, 168, Dim, 1, clip);

        font.Draw(span, Width, Plain(s.Now.Name).ToUpperInvariant(), Left, 212, Accent, 2, clip);

        var ends = s.Now.StartUnix + PeriodSeconds;
        var left = Math.Max(0, ends - unix);
        var since = $"SINCE ET {EorzeaHour(s.Now.StartUnix):00}:00";
        var change = left >= 60 ? $"CHANGES IN {left / 60} MIN" : "CHANGING NOW";
        font.Draw(span, Width, $"{since}   {change}", Left, 296, Dim, 1, clip);
    }

    private void DrawForecast(Span<uint> span, WeatherSnapshot s, long unix, in BitmapFont.Clip clip, double seconds)
    {
        const int Top = 352;
        Fill(span, 0, Top, Width, 2, Edge);
        font.Draw(span, Width, "COMING UP", 24, Top + 10, Amber, 1, clip);

        // Rare weather anywhere in the world, one at a time, five seconds each.
        if (s.Alerts.Count > 0)
        {
            var alert = "RARE  " + Canvas.Plain(s.Alerts[(int)(seconds / 5) % s.Alerts.Count]).ToUpperInvariant();
            alert = Canvas.Cut(alert, font.Fit(Width - 24 - 200));
            font.Draw(span, Width, alert, Width - 24 - font.Measure(alert), Top + 10, Canvas.Rgb(0xFF, 0xD6, 0x4F), 1, clip);
        }

        var count = Math.Min(5, s.Next.Count);
        if (count == 0)
            return;

        const int ColTop = Top + 56;
        var colWidth = Width / count;

        for (var i = 0; i < count; i++)
        {
            var p = s.Next[i];
            var x = i * colWidth;
            var centre = x + (colWidth / 2);

            if (i > 0)
                Fill(span, x, ColTop, 1, 190, Edge);

            WeatherIcons.Draw(span, Width, Height, p.WeatherId, centre - 40, ColTop, 5);

            var name = Plain(p.Name).ToUpperInvariant();
            name = name.Length > font.Fit(colWidth - 16) ? name[..font.Fit(colWidth - 16)] : name;
            font.Draw(span, Width, name, centre - (font.Measure(name) / 2), ColTop + 92, White, 1, clip);

            var at = $"ET {EorzeaHour(p.StartUnix):00}:00";
            font.Draw(span, Width, at, centre - (font.Measure(at) / 2), ColTop + 124, Dim, 1, clip);

            var inMin = Math.Max(0, (p.StartUnix - unix) / 60);
            var real = inMin < 60 ? $"IN {inMin} MIN" : $"IN {inMin / 60}H {inMin % 60:00}M";
            font.Draw(span, Width, real, centre - (font.Measure(real) / 2), ColTop + 150, Faint, 1, clip);
        }
    }

    private void DrawAround(Span<uint> span, WeatherSnapshot s, double seconds)
    {
        const int Top = 604;
        Fill(span, 0, Top, Width, 2, Edge);
        Fill(span, 0, Top + 2, Width, 74, SkyDeep);

        // The label sits in its own box; the crawl passes to the right of it, never under it.
        const int LabelWidth = 264;
        var label = new BitmapFont.Clip(0, Top, LabelWidth, Top + 76);
        font.Draw(span, Width, "AROUND", 24, Top + 8, Amber, 1, label);
        font.Draw(span, Width, "EORZEA", 24, Top + 36, Amber, 1, label);
        Fill(span, LabelWidth - 2, Top + 2, 2, 74, Edge);

        var clip = new BitmapFont.Clip(LabelWidth, Top, Width, Top + 76);

        if (s.Around.Count == 0)
            return;

        // One long line, crawling. Each entry is "ZONE  weather" with room between.
        var parts = new List<(string Zone, string Weather, uint Id)>(s.Around.Count);
        var totalWidth = 0;
        foreach (var a in s.Around)
        {
            var zone = Plain(a.Zone).ToUpperInvariant();
            var weather = Plain(a.Name).ToUpperInvariant();
            parts.Add((zone, weather, a.WeatherId));
            totalWidth += font.Measure(zone) + 8 + 36 + font.Measure(weather) + 64;
        }

        var cycle = totalWidth + Width;
        var x = Width - (int)((seconds * CrawlSpeed) % cycle);
        var y = Top + 40;

        foreach (var (zone, weather, id) in parts)
        {
            if (x + font.Measure(zone) + 8 + 36 + font.Measure(weather) > 0 && x < Width)
            {
                font.Draw(span, Width, zone, x, y - 4, White, 1, clip);
                var ix = x + font.Measure(zone) + 8;
                if (ix >= LabelWidth && ix + 32 <= Width)
                    WeatherIcons.Draw(span, Width, Height, id, ix, y - 6, 2);
                font.Draw(span, Width, weather, ix + 36, y - 4, Dim, 1, clip);
            }

            x += font.Measure(zone) + 8 + 36 + font.Measure(weather) + 64;
        }
    }

    private void DrawFooter(Span<uint> span, in BitmapFont.Clip clip)
    {
        Fill(span, 0, 680, Width, 40, Band);
        Fill(span, 0, 680, Width, 2, Edge);
        font.Draw(span, Width, "AETHERSTREAM WEATHER", 24, 680, Accent, 1, clip);

        const string Line = "LIVE FROM THE LIFESTREAM";
        font.Draw(span, Width, Line, Width - 24 - font.Measure(Line), 680, Faint, 1, clip);
    }

    // -- helpers ---------------------------------------------------------------------------------

    /// <summary>Eorzean hour a real unix time falls in. 175 real seconds per Eorzean hour.</summary>
    private static long EorzeaHour(long unix) => unix / 175 % 24;

    private static void Fill(Span<uint> target, int x, int y, int w, int h, uint colour)
    {
        var x0 = Math.Max(0, x);
        var x1 = Math.Min(Width, x + w);
        if (x1 <= x0)
            return;

        for (var row = Math.Max(0, y); row < Math.Min(Height, y + h); row++)
            target.Slice((row * Width) + x0, x1 - x0).Fill(colour);
    }

    private static string Plain(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(c is '’' or '‘' ? '\'' : c is '—' or '–' ? '-' : c);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Weather glyphs as 16x16 pixel art, drawn at integer scales. Pixel art on purpose: the set
/// is a CRT in a game about a world with moogles in it, and a vector sun would look wrong.
/// </summary>
internal static class WeatherIcons
{
    private const uint Yellow = 0xFF4FD6FFu;   // FFD64F
    private const uint Orange = 0xFF279FEFu;   // EF9F27
    private const uint White = 0xFFFBF1E6u;
    private const uint Grey = 0xFFC8B09Fu;     // 9FB0C8
    private const uint DarkGrey = 0xFF806E5Fu; // 5F6E80
    private const uint Blue = 0xFFFFC76Bu;     // 6BC7FF
    private const uint Purple = 0xFFE07AB0u;   // B07AE0
    private const uint Green = 0xFFA5CA5Du;    // 5DCAA5
    private const uint Sand = 0xFF8FB8D9u;     // D9B88F
    private const uint Red = 0xFF4A4BE2u;      // E24B4A

    private static readonly string[] Sun =
    [
        "................",
        ".......y........",
        "...y...y...y....",
        "....y.....y.....",
        ".....yyyyy......",
        "....yyyyyyy.....",
        "...yyyyyyyyy....",
        "yy.yyyyyyyyy.yy.",
        "...yyyyyyyyy....",
        "....yyyyyyy.....",
        ".....yyyyy......",
        "....y.....y.....",
        "...y...y...y....",
        ".......y........",
        "................",
        "................",
    ];

    private static readonly string[] FairSkies =
    [
        "................",
        "....y...........",
        ".y..y..y........",
        "..yyyyy.........",
        ".yyyyyyy........",
        "yyyyyyyyy.......",
        ".yyyyyyy.wwww...",
        "..yyyyy.wwwwww..",
        ".y..y..wwwwwwww.",
        "....y.wwwwwwwwww",
        "......wwwwwwwwww",
        "......wwwwwwwwww",
        ".......wwwwwwww.",
        "................",
        "................",
        "................",
    ];

    private static readonly string[] Clouds =
    [
        "................",
        "................",
        "......gggg......",
        ".....gggggg.....",
        "..ggggggggggg...",
        ".ggggggggggggg..",
        "gggggggggggggggg",
        "gggggggggggggggg",
        "gggggggggggggggg",
        ".gggggggggggggg.",
        "................",
        "....wwwwwww.....",
        "..wwwwwwwwwww...",
        "..wwwwwwwwwww...",
        "...wwwwwwwww....",
        "................",
    ];

    private static readonly string[] Fog =
    [
        "................",
        "................",
        "..gggggggggggg..",
        "................",
        "gggggggggggggggg",
        "................",
        "....gggggggggggg",
        "................",
        "gggggggggggg....",
        "................",
        "..gggggggggggg..",
        "................",
        "gggggggggggggggg",
        "................",
        "................",
        "................",
    ];

    private static readonly string[] Wind =
    [
        "................",
        "................",
        "......wwwww.....",
        ".....w.....w....",
        "...........w....",
        "wwwwwwwwwwww....",
        "................",
        "........wwwwwww.",
        ".......w......w.",
        "..............w.",
        "wwwwwwwwwwwwwww.",
        "................",
        "....wwwwwww.....",
        "...w......w.....",
        "wwww......w.....",
        "................",
    ];

    private static readonly string[] Rain =
    [
        "................",
        "......gggg......",
        ".....gggggg.....",
        "..ggggggggggg...",
        ".ggggggggggggg..",
        "gggggggggggggggg",
        "gggggggggggggggg",
        ".gggggggggggggg.",
        "................",
        "...b...b...b....",
        "..b...b...b.....",
        "................",
        "...b...b...b....",
        "..b...b...b.....",
        "................",
        "................",
    ];

    private static readonly string[] Thunder =
    [
        "................",
        "......dddd......",
        ".....dddddd.....",
        "..ddddddddddd...",
        ".ddddddddddddd..",
        "dddddddddddddddd",
        "dddddddddddddddd",
        ".dddddddddddddd.",
        "........yy......",
        ".......yy.......",
        "......yyyy......",
        ".......yy.......",
        "......yy........",
        ".....yy.........",
        "....y...........",
        "................",
    ];

    private static readonly string[] Snow =
    [
        "................",
        "......gggg......",
        ".....gggggg.....",
        "..ggggggggggg...",
        ".ggggggggggggg..",
        "gggggggggggggggg",
        ".gggggggggggggg.",
        "................",
        "..w....w....w...",
        ".www..www..www..",
        "..w....w....w...",
        "................",
        "....w.....w.....",
        "...www...www....",
        "....w.....w.....",
        "................",
    ];

    private static readonly string[] Dust =
    [
        "................",
        "..s..s....s.....",
        ".....s..s...s...",
        "..s....s..s.....",
        "ssssssss........",
        "....s...s..s..s.",
        "..s...s...s.....",
        "....ssssssssss..",
        ".s..s...s...s...",
        "...s..s...s..s..",
        "sssssss.........",
        "..s...s..s..s...",
        "....s..s...s....",
        "......ssssssss..",
        "..s.....s.......",
        "................",
    ];

    private static readonly string[] Heat =
    [
        "................",
        ".......o........",
        "...o...o...o....",
        "....o.....o.....",
        ".....ooooo......",
        "....ooooooo.....",
        "oo.ooooooooo.oo.",
        "....ooooooo.....",
        ".....ooooo......",
        "....o.....o.....",
        "...o...o...o....",
        "................",
        ".oo..oo..oo..oo.",
        "o..oo..oo..oo..o",
        "................",
        "................",
    ];

    private static readonly string[] Gloom =
    [
        "................",
        "......pppp......",
        ".....pppppp.....",
        "..ppppppppppp...",
        ".ppppppppppppp..",
        "pppppppppppppppp",
        "pppppppppppppppp",
        "pppppppppppppppp",
        ".pppppppppppppp.",
        "................",
        "..p..p..p..p..p.",
        "................",
        ".p..p..p..p..p..",
        "................",
        "................",
        "................",
    ];

    private static readonly string[] Aurora =
    [
        "................",
        "..gg......gg....",
        ".g..g....g..g...",
        "g....gggg....ggg",
        "................",
        "....bb......bb..",
        "...b..b....b..b.",
        "bbb....bbbb....b",
        "................",
        "..pp......pp....",
        ".p..p....p..p...",
        "p....pppp....ppp",
        "................",
        "................",
        "................",
        "................",
    ];

    private static readonly string[] Unknown =
    [
        "................",
        "................",
        "....wwwwwwww....",
        "...ww......ww...",
        "..ww........ww..",
        "............ww..",
        "...........ww...",
        ".........www....",
        "........ww......",
        "........ww......",
        "................",
        "........ww......",
        "........ww......",
        "................",
        "................",
        "................",
    ];

    /// <summary>Which picture a weather id gets. Ids from the game's Weather sheet.</summary>
    private static string[] Pick(uint id) => id switch
    {
        1 => Sun,                                   // Clear Skies
        2 or 30 or 31 or 32 or 33 or 34 => FairSkies,
        3 or 21 or 22 or 25 or 40 => Clouds,       // Clouds, Storm Clouds, Louring, Shelf Clouds
        4 => Fog,
        5 or 6 or 28 => Wind,                       // Wind, Gales
        7 or 8 or 23 or 24 => Rain,                 // Rain, Showers, Rough Seas
        9 or 10 => Thunder,
        11 or 12 => Dust,                           // Dust Storms, Sandstorms
        13 or 14 or 26 or 29 => Heat,               // Hot Spells, Heat Waves, Eruptions
        15 or 16 => Snow,
        17 or 19 or 20 or 27 => Gloom,              // Gloom, Darkness, Tension
        18 or 35 or 36 or 37 or 38 or 39 => Aurora, // Auroras, Irradiance, Core Radiation
        _ => Unknown,
    };

    private static uint Colour(char c) => c switch
    {
        'y' => Yellow,
        'o' => Orange,
        'w' => White,
        'g' => Grey,
        'd' => DarkGrey,
        'b' => Blue,
        'p' => Purple,
        's' => Sand,
        'r' => Red,
        'n' => Green,
        _ => 0,
    };

    /// <summary>Draws the icon for a weather id with its top-left at (x, y), each art pixel <paramref name="scale"/> wide.</summary>
    public static void Draw(Span<uint> target, int width, int height, uint id, int x, int y, int scale)
    {
        var art = Pick(id);
        for (var ay = 0; ay < 16; ay++)
        {
            var row = art[ay];
            for (var ax = 0; ax < 16; ax++)
            {
                var colour = Colour(row[ax]);
                if (colour == 0)
                    continue;

                var px = x + (ax * scale);
                var py = y + (ay * scale);
                for (var dy = 0; dy < scale; dy++)
                {
                    var yy = py + dy;
                    if (yy < 0 || yy >= height)
                        continue;

                    var line = target.Slice(yy * width, width);
                    for (var dx = 0; dx < scale; dx++)
                    {
                        var xx = px + dx;
                        if (xx >= 0 && xx < width)
                            line[xx] = colour;
                    }
                }
            }
        }
    }
}
