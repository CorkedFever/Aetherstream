using Aetherstream.Plugin.Weather;

namespace Aetherstream.Plugin.Video;

internal sealed record CruiseSnapshot(IReadOnlyList<ResetTimers.Voyage> Voyages, IReadOnlyList<(string Zone, string Weather)> Coasts);

/// <summary>
/// Sea News, with Wavv. A Sahagin at a desk that is mostly underwater reads the ocean fishing
/// schedule like the shipping forecast: the next boat on both routes with a countdown, where
/// each is bound and at what hour of the day, the departures board for the next six, and the
/// conditions on the coasts. The sea behind him is drawn for the next voyage's time of day. He
/// does not believe in the surface and reports it anyway.
/// </summary>
internal sealed class CruiseChannel(BitmapFont font, Func<CruiseSnapshot?> data) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double SegmentFor = 20.0;

    private static readonly uint Deep = Canvas.Rgb(0x0A, 0x1E, 0x3A);
    private static readonly uint Water = Canvas.Rgb(0x1E, 0x5A, 0x8A);
    private static readonly uint WaterLit = Canvas.Rgb(0x3A, 0x8A, 0xB8);
    private static readonly uint Panel = Canvas.Rgb(0x08, 0x16, 0x2A);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Faint = Canvas.Rgb(0x8A, 0xA8, 0xB8);
    private static readonly uint Gold = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint Mint = Canvas.Rgb(0x8A, 0xE8, 0xC0);
    private static readonly uint Rose = Canvas.Rgb(0xFF, 0x8A, 0xB0);
    private static readonly uint Wood = Canvas.Rgb(0x6A, 0x46, 0x2A);
    private static readonly uint Sail = Canvas.Rgb(0xE8, 0xE0, 0xD0);

    private static readonly string[] Patter =
    [
        "Good evening, or whatever it is up there. This is Sea News. The sea: still here.",
        "The Indigo Route leaves from Limsa Lominsa. The Ruby Route leaves from Kugane. Neither leaves from the sea, which is an oversight.",
        "Boats depart every two hours. They come back. Sahagin have questions about that.",
        "Bring bait. Bring a friend. Bring twenty-four friends; the boat is greedy.",
        "Spectral currents are when the sea gets excited. It happens. Fish everything.",
        "A sunset voyage is the pretty one. A night voyage is the profitable one. Sahagin does not care for either; sahagin lives here.",
        "Land-dwellers call it 'ocean fishing'. We call it 'the neighbours'.",
        "Conditions on the coasts follow. Please do not stand on the coasts. They are ours.",
        "Registration closes fifteen minutes after the bell. The boat will not wait, and neither would I.",
    ];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var slot = (int)((unix - 1_700_000_000) / (long)SegmentFor);
        var into = (unix - 1_700_000_000) % (long)SegmentFor + (seconds % 1.0);
        this.RenderAt(target, data(), slot, into, now, seconds);
    }

    private void RenderAt(uint[] target, CruiseSnapshot? snapshot, int slot, double into, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var next = snapshot?.Voyages.Count > 0 ? snapshot.Voyages[0] : (ResetTimers.Voyage?)null;
        var segment = slot % 3;

        this.DrawSea(span, next?.IndigoTime ?? "Day", seconds);

        Canvas.Fill(span, 0, 0, W, 56, Panel);
        Canvas.Fill(span, 0, 56, W, 2, WaterLit);
        font.Draw(span, W, "SEA NEWS", 24, 8, Mint, 1, all);
        font.Draw(span, W, "WITH WAVV", 24 + font.Measure("SEA NEWS") + 24, 8, Faint, 1, all);
        var clock = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, clock, W - 24 - font.Measure(clock), 8, Cream, 1, all);

        if (snapshot is null || next is null)
        {
            font.Draw(span, W, "THE TIDE TABLES ARE NOT IN YET", 60, 320, Cream, 1, all);
            HostSprites.DrawSahagin(span, HostSprites.Sahagin.Talk, seconds, 60, 330, 7);
            return;
        }

        switch (segment)
        {
            case 0: this.DrawNextBoat(span, snapshot, now, seconds, all); break;
            case 1: this.DrawBoard(span, snapshot, now, all); break;
            default: this.DrawCoasts(span, snapshot, all); break;
        }

        var point = into is > 5 and < 10 || into > 16;
        HostSprites.DrawSahagin(span, point ? HostSprites.Sahagin.Point : HostSprites.Sahagin.Talk, seconds, 60, 330, 7);
        this.DrawDesk(span, all);
        this.DrawLowerThird(span, "WAVV", Patter[Pick((slot * 5) + (int)(into / 10.0), Patter.Length)], all);

        Canvas.Fill(span, 0, 680, W, 40, Panel);
        var names = new[] { "NEXT BOAT", "DEPARTURES", "COASTS" };
        var strip = string.Join("   ", names.Select((n, i) => i == segment ? $"[{n}]" : n)) + $"   /   DEPARTS {Left(next.Value.DepartsUtc - now.ToUniversalTime())}";
        font.Draw(span, W, Canvas.Cut(strip, font.Fit(W - 48)), 24, 680, Faint, 1, all);
    }

    private void DrawNextBoat(Span<uint> span, CruiseSnapshot s, DateTime now, double seconds, in BitmapFont.Clip all)
    {
        var v = s.Voyages[0];
        var left = v.DepartsUtc - now.ToUniversalTime();
        var boarding = left < TimeSpan.FromMinutes(15) && left > TimeSpan.Zero;
        this.Card(span, boarding ? "NOW BOARDING" : "NEXT BOAT", all);
        var big = left <= TimeSpan.Zero ? "GONE" : $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
        font.Draw(span, W, big, 800 - (font.Measure(big, 2) / 2), 150, boarding ? Mint : Gold, 2, all);
        var at = "DEPARTS " + v.DepartsUtc.ToLocalTime().ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, at, 800 - (font.Measure(at) / 2), 240, Cream, 1, all);

        Route(span, 400, 300, "INDIGO ROUTE", "FROM LIMSA LOMINSA", v.IndigoDestination, v.IndigoTime, Rose, all);
        Route(span, 820, 300, "RUBY ROUTE", "FROM KUGANE", v.RubyDestination, v.RubyTime, Gold, all);
        Boat(span, 800, 520, seconds);
    }

    private void Route(Span<uint> span, int x, int y, string name, string from, string dest, string time, uint colour, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, x, y, 400, 190, Canvas.Lerp(Panel, colour, 0.12f));
        Canvas.Fill(span, x, y, 400, 4, colour);
        font.Draw(span, W, name, x + 16, y + 10, colour, 1, all);
        font.Draw(span, W, from, x + 16, y + 46, Faint, 1, all);
        var d = dest.ToUpperInvariant();
        if (d.StartsWith("THE ", StringComparison.Ordinal))
            d = d[4..];
        var y2 = y + 90;
        foreach (var l in Canvas.Wrap(d, font.Fit(370), 2))
        {
            font.Draw(span, W, l, x + 16, y2, Cream, 1, all);
            y2 += 36;
        }

        var tod = time.ToUpperInvariant();
        font.Draw(span, W, "AT " + tod, x + 400 - 16 - font.Measure("AT " + tod), y + 10, Cream, 1, all);
        WeatherGlyphs.Draw(span, time == "Night" ? "moon" : time == "Sunset" ? "fair" : "clear", x + 400 - 56, y + 46, 40);
    }

    private void DrawBoard(Span<uint> span, CruiseSnapshot s, DateTime now, in BitmapFont.Clip all)
    {
        this.Card(span, "DEPARTURES", all);
        var y = 140;
        font.Draw(span, W, "LEAVES", 380, y, Faint, 1, all);
        font.Draw(span, W, "INDIGO ROUTE", 600, y, Rose, 1, all);
        font.Draw(span, W, "RUBY ROUTE", 940, y, Gold, 1, all);
        y += 44;
        foreach (var v in s.Voyages.Take(6))
        {
            var local = v.DepartsUtc.ToLocalTime();
            var when = local.Date == now.Date ? local.ToString("h:mm tt") : local.ToString("ddd h:mm tt");
            font.Draw(span, W, when.ToUpperInvariant(), 380, y, Cream, 1, all);
            font.Draw(span, W, Canvas.Cut(Short(v.IndigoDestination) + " " + v.IndigoTime.ToUpperInvariant(), 20), 600, y, Cream, 1, all);
            font.Draw(span, W, Canvas.Cut(Short(v.RubyDestination) + " " + v.RubyTime.ToUpperInvariant(), 18), 940, y, Cream, 1, all);
            Canvas.Fill(span, 380, y + 38, 840, 1, Canvas.Lerp(Panel, Cream, 0.15f));
            y += 46;
        }

        font.Draw(span, W, "EVERY TWO HOURS. BOARDING CLOSES 15 MIN AFTER.", 380, y + 10, Faint, 1, all);
    }

    private void DrawCoasts(Span<uint> span, CruiseSnapshot s, in BitmapFont.Clip all)
    {
        this.Card(span, "CONDITIONS ON THE COASTS", all);
        if (s.Coasts.Count == 0)
        {
            font.Draw(span, W, "THE COASTS ARE NOT ANSWERING", 380, 200, Faint, 1, all);
            return;
        }

        var y = 140;
        foreach (var (zone, weather) in s.Coasts.Take(7))
        {
            WeatherGlyphs.Draw(span, weather, 380, y - 4, 44);
            font.Draw(span, W, Canvas.Cut(zone.ToUpperInvariant(), 26), 440, y, Cream, 1, all);
            font.Draw(span, W, weather.ToUpperInvariant(), 900, y, Sea(weather) ? Rose : Mint, 1, all);
            y += 52;
        }

        font.Draw(span, W, Canvas.Cut("ROUGH ON THE SURFACE MEANS CALM UNDERNEATH. COME DOWN.", font.Fit(840)), 380, y + 8, Faint, 1, all);
    }

    private static bool Sea(string weather) => weather.Contains("Rain") || weather.Contains("Storm") || weather.Contains("Gale") || weather.Contains("Thunder") || weather.Contains("Blizzard");

    // -- the set ------------------------------------------------------------------------------------------------

    private void DrawSea(Span<uint> span, string timeOfDay, double seconds)
    {
        var (top, horizon) = timeOfDay switch
        {
            "Night" => (Canvas.Rgb(0x06, 0x0C, 0x24), Canvas.Rgb(0x14, 0x22, 0x4A)),
            "Sunset" => (Canvas.Rgb(0x3A, 0x1E, 0x4A), Canvas.Rgb(0xE8, 0x7A, 0x4A)),
            _ => (Canvas.Rgb(0x6A, 0xB8, 0xF0), Canvas.Rgb(0xC8, 0xE4, 0xF8)),
        };
        for (var y = 0; y < 330; y++)
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(top, horizon, y / 330f));
        if (timeOfDay == "Night")
        {
            for (var i = 0; i < 40; i++)
                Canvas.Plot(span, (i * 97 + 13) % W, (i * 53 + 7) % 300, Cream);
            Canvas.Disc(span, W - 200, 110, 30, Cream);
        }
        else if (timeOfDay == "Sunset")
            Canvas.Disc(span, W - 300, 300, 50, Canvas.Rgb(0xFF, 0xB0, 0x5A));
        else
            Canvas.Disc(span, W - 200, 90, 34, Canvas.Rgb(0xFF, 0xE8, 0xA0));

        for (var y = 330; y < H; y++)
        {
            var t = (y - 330) / (float)(H - 330);
            var wave = (float)(Math.Sin((y * 0.15) + (seconds * 1.5)) * 0.06);
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(WaterLit, Deep, Math.Clamp(t + wave, 0, 1)));
        }

        for (var i = 0; i < 12; i++)
        {
            var x = (int)(((seconds * 30) + (i * 190)) % (W + 100)) - 50;
            var y = 340 + ((i * 41) % 200);
            Canvas.Fill(span, x, y, 40 + (i % 3 * 20), 2, Canvas.Lerp(Water, Cream, 0.4f));
        }
    }

    private void DrawDesk(Span<uint> span, in BitmapFont.Clip all)
    {
        // The desk, half under the waterline, with the show's name and a coral.
        Canvas.Fill(span, 30, 540, 300, 50, Canvas.Rgb(0x3A, 0x5A, 0x7A));
        Canvas.Fill(span, 30, 540, 300, 6, Canvas.Lerp(Cream, Water, 0.5f));
        font.Draw(span, W, "SEA NEWS", 60, 548, Cream, 1, all);
        Canvas.Fill(span, 300, 500, 6, 40, Rose);
        Canvas.Fill(span, 290, 510, 6, 30, Rose);
        Canvas.Fill(span, 310, 515, 6, 25, Rose);
    }

    private static void Boat(Span<uint> span, int cx, int y, double seconds)
    {
        var bob = (int)(Math.Sin(seconds * 1.4) * 4);
        var yy = y + bob;
        Canvas.Fill(span, cx - 90, yy, 180, 26, Wood);
        Canvas.Fill(span, cx - 100, yy - 6, 200, 8, Canvas.Lerp(Wood, Canvas.Black, 0.3f));
        Canvas.Fill(span, cx - 4, yy - 90, 8, 90, Wood);
        for (var i = 0; i < 70; i++)
            Canvas.Fill(span, cx + 4, yy - 88 + i, i * 7 / 8, 1, Sail);
        Canvas.Fill(span, cx - 60, yy - 30, 40, 24, Canvas.Lerp(Wood, Cream, 0.3f));
    }

    private void Card(Span<uint> span, string title, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 360, 80, 880, 500, Canvas.Lerp(Panel, Canvas.Black, 0.1f));
        Canvas.Rect(span, 360, 80, 880, 500, WaterLit, 3);
        Canvas.Fill(span, 360, 80, 880, 48, Canvas.Lerp(Panel, WaterLit, 0.3f));
        font.Draw(span, W, title, 800 - (font.Measure(title) / 2), 84, Mint, 1, all);
    }

    private static string Short(string dest)
    {
        var d = dest.StartsWith("The ", StringComparison.Ordinal) ? dest[4..] : dest;
        return d.Replace("Northern Strait of Merlthor", "N. Merlthor").ToUpperInvariant();
    }

    private static string Left(TimeSpan t)
    {
        if (t < TimeSpan.Zero)
            t = TimeSpan.Zero;
        return t.TotalHours >= 1 ? $"IN {(int)t.TotalHours}H {t.Minutes}M" : $"IN {t.Minutes}M {t.Seconds}S";
    }

    private void DrawLowerThird(Span<uint> span, string tab, string line, in BitmapFont.Clip all)
    {
        const int Top = 596;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - font.Measure(tab) - 64), 2);
        var height = 16 + (lines.Count * 40);
        Canvas.Fill(span, 40, Top, W - 80, height, Panel);
        Canvas.Fill(span, 40, Top, W - 80, 4, Mint);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, Top + 4, tabW, height - 4, Water);
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
