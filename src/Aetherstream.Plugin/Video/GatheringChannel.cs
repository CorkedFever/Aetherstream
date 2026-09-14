namespace Aetherstream.Plugin.Video;

/// <summary>One line of the gathering board.</summary>
internal sealed record GatheringRow(
    string Kind,
    string Type,
    int Level,
    string Place,
    string Zone,
    string Items,
    bool Up,
    int SecondsLeft,
    int WindowStart,
    int WindowEnd);

internal sealed record GatheringSnapshot(IReadOnlyList<GatheringRow> Rows, int UpCount, int Total);

/// <summary>
/// The gathering log as a departures board: what is up now, and what comes next, with a clock
/// counting down to each in real time. Timed nodes only — the ones you wait for.
/// </summary>
internal sealed class GatheringChannel(BitmapFont font, Func<GatheringSnapshot?> data) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double RowSpeed = 18.0;

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);

        Canvas.Fill(span, 0, 0, W, H, Canvas.Glass);

        // Header: Eorzea time is the clock that matters here.
        Canvas.Fill(span, 0, 0, W, 56, Canvas.GlassLit);
        Canvas.Fill(span, 0, 56, W, 2, Canvas.Edge);
        font.Draw(span, W, "GATHERING LOG", 24, 8, Canvas.Amber, 1, all);

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var etMinutes = unix * 24 / 70;
        var et = $"ET {etMinutes / 60 % 24:00}:{etMinutes % 60:00}";
        font.Draw(span, W, et, (W - font.Measure(et)) / 2, 8, Canvas.White, 1, all);

        var local = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, local, W - 24 - font.Measure(local), 8, Canvas.White, 1, all);

        var snapshot = data();
        var rows = snapshot?.Rows ?? [];

        if (rows.Count == 0)
        {
            const string None = "NO TIMED NODES LISTED";
            font.Draw(span, W, None, (W - font.Measure(None, 2)) / 2, 330, Canvas.Faint, 2, all);
            this.DrawFooter(span, snapshot, all);
            return;
        }

        // Column heads.
        const int ColType = 24, ColLevel = 104, ColPlace = 168, ColItems = 540, ColWindow = 916, ColLeft = 1112;
        font.Draw(span, W, "JOB", ColType, 68, Canvas.Amber, 1, all);
        font.Draw(span, W, "LV", ColLevel, 68, Canvas.Amber, 1, all);
        font.Draw(span, W, "WHERE", ColPlace, 68, Canvas.Amber, 1, all);
        font.Draw(span, W, "YIELDS", ColItems, 68, Canvas.Amber, 1, all);
        font.Draw(span, W, "WINDOW", ColWindow, 68, Canvas.Amber, 1, all);
        font.Draw(span, W, "IN", ColLeft, 68, Canvas.Amber, 1, all);

        const int RowTop = 108, RowHeight = 40, RowsBottom = 680;
        var region = new BitmapFont.Clip(0, RowTop, W, RowsBottom);
        var visible = (RowsBottom - RowTop) / RowHeight;
        var total = rows.Count * RowHeight;
        var offset = rows.Count <= visible ? 0 : (int)((seconds * RowSpeed) % total);

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
                var bg = r.Up ? Canvas.Rgb(0x0F, 0x38, 0x2E) : i % 2 == 0 ? Canvas.Tube : Canvas.Glass;
                Canvas.Fill(span, 0, top, W, bottom - top, bg);

                var clip = new BitmapFont.Clip(0, top, W, bottom);
                var typeColour = r.Type switch
                {
                    "Mining" or "Quarrying" => Canvas.Amber,
                    "Logging" or "Harvesting" => Canvas.Good,
                    _ => Canvas.Accent,
                };

                font.Draw(span, W, Abbrev(r.Type), ColType, y, typeColour, 1, clip);
                font.Draw(span, W, $"{r.Level}", ColLevel, y, Canvas.Dim, 1, clip);

                var where = Canvas.Cut($"{r.Place}, {r.Zone}".ToUpperInvariant(), font.Fit(ColItems - ColPlace - 8));
                font.Draw(span, W, where, ColPlace, y, Canvas.White, 1, clip);

                var items = Canvas.Cut(r.Items.ToUpperInvariant(), font.Fit(ColWindow - ColItems - 8));
                font.Draw(span, W, items, ColItems, y, Canvas.Dim, 1, clip);

                var window = $"{r.WindowStart / 60:00}:{r.WindowStart % 60:00}-{r.WindowEnd / 60:00}:{r.WindowEnd % 60:00}";
                font.Draw(span, W, window, ColWindow, y, Canvas.Faint, 1, clip);

                var left = r.Up ? $"UP {Countdown(r.SecondsLeft)}" : Countdown(r.SecondsLeft);
                font.Draw(span, W, left, ColLeft, y, r.Up ? Canvas.Good : Canvas.White, 1, clip);

                // Ephemeral and legendary nodes get a mark in the margin, so they are told apart at a glance.
                if (r.Kind == "Ephemeral")
                    Canvas.Fill(span, 0, top, 4, bottom - top, Canvas.Accent);
                else if (r.Kind == "Legendary")
                    Canvas.Fill(span, 0, top, 4, bottom - top, Canvas.Amber);
            }
        }

        this.DrawFooter(span, snapshot, all);
    }

    private void DrawFooter(Span<uint> span, GatheringSnapshot? snapshot, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 680, W, 40, Canvas.GlassLit);
        Canvas.Fill(span, 0, 680, W, 2, Canvas.Edge);
        font.Draw(span, W, "AETHERSTREAM GATHERING", 24, 680, Canvas.Accent, 1, all);

        var right = snapshot is { } s
            ? $"{s.UpCount} UP / {s.Total} TIMED / BLUE EPHEMERAL, GOLD LEGENDARY"
            : "READING THE LOG";
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Canvas.Faint, 1, all);
    }

    private static string Abbrev(string type) => type switch
    {
        "Mining" => "MIN",
        "Quarrying" => "QRY",
        "Logging" => "BTN",
        "Harvesting" => "HRV",
        "Spearfishing" => "FSH",
        _ => Canvas.Cut(type.ToUpperInvariant(), 3),
    };

    private static string Countdown(int seconds) =>
        seconds >= 3600 ? $"{seconds / 3600}H {seconds / 60 % 60:00}M" : $"{seconds / 60:00}:{seconds % 60:00}";
}
