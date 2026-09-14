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

        // Column heads. Two lines per node: where and when on the first, what and the window
        // on the second, so nothing has to be squeezed into one.
        const int ColType = 24, ColLevel = 96, ColPlace = 160, ColRight = 1064;
        font.Draw(span, W, "JOB", ColType, 66, Canvas.Amber, 1, all);
        font.Draw(span, W, "LV", ColLevel, 66, Canvas.Amber, 1, all);
        font.Draw(span, W, "WHERE, AND WHAT IT YIELDS", ColPlace, 66, Canvas.Amber, 1, all);
        font.Draw(span, W, "IN / WINDOW", ColRight, 66, Canvas.Amber, 1, all);

        const int RowTop = 108, RowHeight = 68, RowsBottom = 680;
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
                var bottom = Math.Min(y + RowHeight - 4, RowsBottom);
                var bg = r.Up ? Canvas.Rgb(0x0F, 0x38, 0x2E) : i % 2 == 0 ? Canvas.Tube : Canvas.Glass;
                Canvas.Fill(span, 0, top, W, bottom - top, bg);

                var clip = new BitmapFont.Clip(0, top, W, bottom);
                var typeColour = r.Type switch
                {
                    "Mining" or "Quarrying" => Canvas.Amber,
                    "Logging" or "Harvesting" => Canvas.Good,
                    _ => Canvas.Accent,
                };

                // Line one: the job, the level, the place, and the countdown.
                var line1 = y + 2;
                font.Draw(span, W, Abbrev(r.Type), ColType, line1, typeColour, 1, clip);
                font.Draw(span, W, $"{r.Level}", ColLevel, line1, Canvas.Dim, 1, clip);

                var place = Canvas.Cut(r.Place.ToUpperInvariant(), font.Fit(ColRight - ColPlace - 16));
                font.Draw(span, W, place, ColPlace, line1, Canvas.White, 1, clip);

                var left = r.Up ? $"UP {Countdown(r.SecondsLeft)}" : Countdown(r.SecondsLeft);
                font.Draw(span, W, left, ColRight, line1, r.Up ? Canvas.Good : Canvas.White, 1, clip);

                // Line two: the zone and the yields, dim, and the Eorzean window under the countdown.
                var line2 = y + 32;
                var detail = Canvas.Cut($"{r.Zone}  /  {r.Items}".ToUpperInvariant(), font.Fit(ColRight - ColPlace - 16));
                font.Draw(span, W, detail, ColPlace, line2, Canvas.Dim, 1, clip);

                var window = $"{r.WindowStart / 60:00}:{r.WindowStart % 60:00}-{r.WindowEnd / 60:00}:{r.WindowEnd % 60:00}";
                font.Draw(span, W, window, ColRight, line2, Canvas.Faint, 1, clip);

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
