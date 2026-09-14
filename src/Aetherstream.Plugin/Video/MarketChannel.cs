namespace Aetherstream.Plugin.Video;

/// <summary>One watched item and its numbers. <paramref name="Trend"/> is -1, 0 or 1 against the last fetch.</summary>
internal sealed record MarketRow(uint Id, string Name, long Min, long Average, double PerDay, int Trend, IReadOnlyList<long> History);

internal sealed record MarketSnapshot(string World, IReadOnlyList<MarketRow> Rows, DateTime FetchedAt, string Status);

/// <summary>
/// Market watch: a stock channel for the market board. The watch list rolls down the screen,
/// one item is featured with a chart of its recent sales, and a ticker of prices crawls along
/// the bottom. Numbers come from Universalis through the plugin's cache.
/// </summary>
internal sealed class MarketChannel(BitmapFont font, Func<MarketSnapshot?> data) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double FeatureSeconds = 8.0;
    private const double TickerSpeed = 100.0;

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);

        Canvas.Fill(span, 0, 0, W, H, Canvas.Glass);

        var snapshot = data();
        var world = snapshot?.World is { Length: > 0 } w ? w.ToUpperInvariant() : "NO WORLD";

        // Header.
        Canvas.Fill(span, 0, 0, W, 56, Canvas.GlassLit);
        Canvas.Fill(span, 0, 56, W, 2, Canvas.Edge);
        font.Draw(span, W, "MARKET WATCH", 24, 8, Canvas.Amber, 1, all);
        font.Draw(span, W, world, (W - font.Measure(world)) / 2, 8, Canvas.White, 1, all);
        var local = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, local, W - 24 - font.Measure(local), 8, Canvas.White, 1, all);

        var rows = snapshot?.Rows ?? [];
        if (rows.Count == 0)
        {
            var line = snapshot?.Status is { Length: > 0 } s ? s.ToUpperInvariant() : "ADD ITEMS IN THE CHANNELS TAB";
            font.Draw(span, W, line, (W - font.Measure(line, 2)) / 2, 330, Canvas.Faint, 2, all);
            this.DrawFooter(span, snapshot, all);
            return;
        }

        // -- the table ------------------------------------------------------------------------------
        const int TableRight = 760;
        const int ColName = 24, ColMin = 380, ColAvg = 520, ColVel = 640;
        const int RowTop = 108, RowHeight = 40, RowsBottom = 640;

        font.Draw(span, W, "ITEM", ColName, 68, Canvas.Amber, 1, all);
        font.Draw(span, W, "LOW", ColMin, 68, Canvas.Amber, 1, all);
        font.Draw(span, W, "AVG", ColAvg, 68, Canvas.Amber, 1, all);
        font.Draw(span, W, "/DAY", ColVel, 68, Canvas.Amber, 1, all);

        var region = new BitmapFont.Clip(0, RowTop, TableRight, RowsBottom);
        var visible = (RowsBottom - RowTop) / RowHeight;
        var total = rows.Count * RowHeight;
        var offset = rows.Count <= visible ? 0 : (int)((seconds * 20.0) % total);
        var featured = (int)(seconds / FeatureSeconds) % rows.Count;

        for (var pass = 0; pass < (rows.Count <= visible ? 1 : 2); pass++)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                var y = RowTop - offset + (pass * total) + (i * RowHeight);
                if (y + RowHeight <= RowTop || y >= RowsBottom)
                    continue;

                var r = rows[i];
                var top = Math.Max(y, RowTop);
                var bottom = Math.Min(y + RowHeight, RowsBottom);
                Canvas.Fill(span, 0, top, TableRight, bottom - top, i == featured ? Canvas.AccentDeep : i % 2 == 0 ? Canvas.Tube : Canvas.Glass);

                var clip = new BitmapFont.Clip(0, top, TableRight, bottom);
                font.Draw(span, W, Canvas.Cut(r.Name.ToUpperInvariant(), font.Fit(ColMin - ColName - 8)), ColName, y, Canvas.White, 1, clip);
                font.Draw(span, W, Money(r.Min), ColMin, y, Canvas.White, 1, clip);
                font.Draw(span, W, Money(r.Average), ColAvg, y, Canvas.Dim, 1, clip);
                font.Draw(span, W, PerDay(r.PerDay), ColVel, y, Canvas.Dim, 1, clip);
                Arrow(span, TableRight - 24, y + 14, r.Trend, region);
            }
        }

        // -- the featured item --------------------------------------------------------------------
        const int ChartLeft = TableRight + 24;
        var f = rows[featured];
        Canvas.Fill(span, TableRight, 58, 2, RowsBottom - 58, Canvas.Edge);

        font.Draw(span, W, Canvas.Cut(f.Name.ToUpperInvariant(), font.Fit(W - ChartLeft - 24)), ChartLeft, 68, Canvas.Amber, 1, all);
        font.Draw(span, W, Money(f.Min), ChartLeft, 108, Canvas.White, 2, all);
        font.Draw(span, W, "GIL, LOWEST NOW", ChartLeft, 190, Canvas.Faint, 1, all);

        this.DrawChart(span, f, ChartLeft, 236, W - ChartLeft - 24, 360, all);

        // -- ticker -------------------------------------------------------------------------------
        Canvas.Fill(span, 0, 640, W, 40, Canvas.GlassLit);
        var ticker = string.Join("     ", rows.Select(r => $"{Canvas.Plain(r.Name).ToUpperInvariant()} {Money(r.Min)}{(r.Trend > 0 ? " UP" : r.Trend < 0 ? " DOWN" : string.Empty)}"));
        var width = font.Measure(ticker);
        var tx = W - (int)((seconds * TickerSpeed) % (width + W));
        font.Draw(span, W, ticker, tx, 640, Canvas.Amber, 1, new BitmapFont.Clip(0, 640, W, 680));

        this.DrawFooter(span, snapshot, all);
    }

    /// <summary>The last sales, oldest to newest, as a line over a filled base. No axes: it is a mood.</summary>
    private void DrawChart(Span<uint> span, MarketRow row, int x, int y, int w, int h, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, x, y, w, h, Canvas.Tube);
        Canvas.Rect(span, x, y, w, h, Canvas.Edge);

        var history = row.History;
        if (history.Count < 2)
        {
            const string None = "NO RECENT SALES";
            font.Draw(span, W, None, x + ((w - font.Measure(None)) / 2), y + (h / 2) - 20, Canvas.Faint, 1, all);
            return;
        }

        var min = history.Min();
        var max = history.Max();
        if (max == min)
            max = min + 1;

        const int Pad = 16;
        var step = (w - (2 * Pad)) / (float)(history.Count - 1);
        int px = -1, py = -1;

        for (var i = 0; i < history.Count; i++)
        {
            var cx = x + Pad + (int)(i * step);
            var cy = y + h - Pad - (int)((h - (2 * Pad)) * (history[i] - min) / (float)(max - min));

            // A faint column under each point, so the line reads as a chart on a screen.
            Canvas.Fill(span, cx, cy, 1, y + h - Pad - cy, Canvas.Edge);

            if (px >= 0)
                Canvas.Line(span, px, py, cx, cy, Canvas.Accent);

            Canvas.Disc(span, cx, cy, 2, Canvas.White);
            (px, py) = (cx, cy);
        }

        font.Draw(span, W, Money(max), x + 8, y + 4, Canvas.Faint, 1, all);
        font.Draw(span, W, Money(min), x + 8, y + h - 44, Canvas.Faint, 1, all);
        font.Draw(span, W, $"LAST {history.Count} SALES", x + w - 8 - font.Measure($"LAST {history.Count} SALES"), y + h - 44, Canvas.Faint, 1, all);
    }

    private static void Arrow(Span<uint> span, int x, int y, int trend, in BitmapFont.Clip clip)
    {
        if (trend == 0)
            return;

        var colour = trend > 0 ? Canvas.Good : Canvas.Bad;
        for (var i = 0; i < 8; i++)
        {
            var row = trend > 0 ? y + i : y + 7 - i;
            if (row < clip.Top || row >= clip.Bottom)
                continue;

            Canvas.Fill(span, x + 7 - i, row, (2 * i) + 1, 1, colour);
        }
    }

    private void DrawFooter(Span<uint> span, MarketSnapshot? snapshot, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 680, W, 40, Canvas.Glass);
        Canvas.Fill(span, 0, 680, W, 2, Canvas.Edge);
        font.Draw(span, W, "AETHERSTREAM MARKET", 24, 680, Canvas.Accent, 1, all);

        var right = snapshot is { FetchedAt: var at } && at > DateTime.MinValue
            ? $"UNIVERSALIS, UPDATED {at.ToLocalTime():h:mm tt}".ToUpperInvariant()
            : "VIA UNIVERSALIS";
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Canvas.Faint, 1, all);
    }

    /// <summary>Sales a day, kept to four characters so it never runs into the arrow.</summary>
    private static string PerDay(double v) => v >= 10000 ? $"{v / 1000:0}K" : v >= 1000 ? $"{v / 1000:0.0}K" : v >= 10 ? $"{v:0}" : $"{v:0.0}";

    private static string Money(long gil) => gil.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}
