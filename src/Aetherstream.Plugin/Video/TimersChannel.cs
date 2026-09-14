namespace Aetherstream.Plugin.Video;

/// <summary>Something due at a time: a reset, a boat, a retainer coming home.</summary>
internal sealed record TimerRow(string Name, string Detail, DateTime DueUtc, string Kind);

/// <summary>A roulette and whether today's run is done.</summary>
internal readonly record struct RouletteRow(string Name, bool Done);

/// <summary>Something to do, or done: an allowance, a journal, a mission.</summary>
internal readonly record struct TaskRow(string Name, string Value, bool Done);

internal sealed record TimersSnapshot(
    IReadOnlyList<TimerRow> Rows,
    IReadOnlyList<RouletteRow> Roulettes,
    string RetainerNote,
    IReadOnlyList<TaskRow> Tasks,
    IReadOnlyList<TaskRow> Estate);

/// <summary>
/// The timers board: everything on a clock, soonest first, with a countdown that goes amber in
/// the last hour and red in the last five minutes. Resets and the boat on the left; today's
/// roulettes as a row of ticks on the right, with the retainers under them.
/// </summary>
internal sealed class TimersChannel(BitmapFont font, Func<TimersSnapshot?> data) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var utc = DateTime.UtcNow;

        Canvas.Fill(span, 0, 0, W, H, Canvas.Glass);

        // Header.
        Canvas.Fill(span, 0, 0, W, 56, Canvas.GlassLit);
        Canvas.Fill(span, 0, 56, W, 2, Canvas.Edge);
        font.Draw(span, W, "ALMANAC", 24, 8, Canvas.Amber, 1, all);

        var etMinutes = DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 24 / 70;
        var et = $"ET {etMinutes / 60 % 24:00}:{etMinutes % 60:00}";
        font.Draw(span, W, et, (W - font.Measure(et)) / 2, 8, Canvas.White, 1, all);

        var local = now.ToString("ddd h:mm:ss tt").ToUpperInvariant();
        font.Draw(span, W, local, W - 24 - font.Measure(local), 8, Canvas.White, 1, all);

        var snapshot = data();
        if (snapshot is null)
        {
            const string Wait = "WINDING THE CLOCKS";
            font.Draw(span, W, Wait, (W - font.Measure(Wait, 2)) / 2, 330, Canvas.Faint, 2, all);
            this.DrawFooter(span, all);
            return;
        }

        // -- the left: what is due, soonest first; scrolls when there is more than fits --------------
        const int Left = 24, Split = 800, RowTop = 76, RowHeight = 72, RowsBottom = 668;
        var ordered = snapshot.Rows.OrderBy(r => r.DueUtc).ToList();
        var visible = (RowsBottom - RowTop) / RowHeight;
        var total = ordered.Count * RowHeight;
        var offset = ordered.Count <= visible ? 0 : (int)((seconds * 16.0) % total);
        var region = new BitmapFont.Clip(0, RowTop, Split - 16, RowsBottom);

        for (var pass = 0; pass < (ordered.Count <= visible ? 1 : 2); pass++)
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                var y = RowTop - offset + (pass * total) + (i * RowHeight);
                if (y + RowHeight <= RowTop || y >= RowsBottom)
                    continue;

                var row = ordered[i];
                var remaining = row.DueUtc - utc;
                var colour = remaining < TimeSpan.FromMinutes(5) ? Canvas.Bad
                    : remaining < TimeSpan.FromHours(1) ? Canvas.Amber
                    : Canvas.White;

                var kindColour = row.Kind switch
                {
                    "reset" => Canvas.Accent,
                    "boat" => Canvas.Rgb(0x6B, 0xC7, 0xFF),
                    "retainer" => Canvas.Good,
                    "vessel" => Canvas.Rgb(0x5D, 0xCA, 0xA5),
                    "squadron" => Canvas.Rgb(0xB0, 0x7A, 0xE0),
                    "cactpot" => Canvas.Rgb(0xFF, 0xD6, 0x4F),
                    _ => Canvas.Dim,
                };

                font.Draw(span, W, Canvas.Cut(row.Name.ToUpperInvariant(), 26), Left, y, kindColour, 1, region);

                // A boat that has already left is boarding for its fifteen minutes, not "now".
                var countdown = row.Kind == "boat" && remaining <= TimeSpan.Zero
                    ? $"BOARDING {Countdown(remaining + TimeSpan.FromMinutes(15))}"
                    : Countdown(remaining);
                font.Draw(span, W, countdown, Split - 24 - font.Measure(countdown), y, colour, 1, region);

                var when = row.DueUtc.ToLocalTime();
                var whenText = when.Date == now.Date ? when.ToString("h:mm tt") : when.ToString("ddd h:mm tt");
                var detail = Canvas.Cut($"{whenText.ToUpperInvariant()}   {Canvas.Plain(row.Detail).ToUpperInvariant()}", font.Fit(Split - Left - 16));
                font.Draw(span, W, detail, Left, y + 32, Canvas.Dim, 1, region);

                var rule = y + RowHeight - 6;
                if (rule >= RowTop && rule < RowsBottom)
                    Canvas.Fill(span, Left, rule, Split - Left - 24, 1, Canvas.Edge);
            }
        }

        // -- the right: pages that turn every eight seconds -------------------------------------------
        Canvas.Fill(span, Split - 8, 72, 2, 590, Canvas.Edge);

        var pages = new List<(string Title, IReadOnlyList<TaskRow> Items, string Empty)>
        {
            ("ROULETTES TODAY", snapshot.Roulettes.Select(r => new TaskRow(r.Name.Replace("Duty Roulette: ", string.Empty), string.Empty, r.Done)).ToList(), "NOT LOGGED IN"),
            ("TO DO", snapshot.Tasks, "NOT LOGGED IN"),
        };
        if (snapshot.Estate.Count > 0)
            pages.Add(("ESTATE", snapshot.Estate, string.Empty));

        var page = (int)(seconds / 8.0) % pages.Count;
        var (title, items, empty) = pages[page];
        var ry = 76;
        font.Draw(span, W, title, Split + 16, ry, Canvas.Amber, 1, all);

        // Page dots, right of the title.
        for (var i = 0; i < pages.Count; i++)
            Canvas.Disc(span, W - 32 - ((pages.Count - 1 - i) * 18), ry + 20, i == page ? 5 : 3, i == page ? Canvas.Amber : Canvas.Edge);

        ry += 40;
        if (items.Count == 0)
        {
            font.Draw(span, W, empty, Split + 16, ry, Canvas.Faint, 1, all);
            ry += 40;
        }

        foreach (var (name, value, done) in items)
        {
            if (ry > 448)
                break;

            // A box, ticked when done; the value dim beside or under the name.
            Canvas.Rect(span, Split + 16, ry + 10, 20, 20, done ? Canvas.Good : Canvas.Dim);
            if (done)
                Canvas.Fill(span, Split + 20, ry + 14, 12, 12, Canvas.Good);

            var label = Canvas.Cut(name.ToUpperInvariant(), font.Fit(W - Split - 72));
            font.Draw(span, W, label, Split + 48, ry, done ? Canvas.Faint : Canvas.White, 1, all);
            ry += 36;

            if (value.Length > 0)
            {
                font.Draw(span, W, Canvas.Cut(value.ToUpperInvariant(), font.Fit(W - Split - 72)), Split + 48, ry - 6, Canvas.Dim, 1, all);
                ry += 34;
            }
        }

        ry = 488;
        font.Draw(span, W, "RETAINERS", Split + 16, ry, Canvas.Amber, 1, all);
        ry += 40;
        font.Draw(span, W, Canvas.Cut(snapshot.RetainerNote.ToUpperInvariant(), font.Fit(W - Split - 40)), Split + 16, ry, Canvas.Dim, 1, all);

        this.DrawFooter(span, all);
    }

    private void DrawFooter(Span<uint> span, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 680, W, 40, Canvas.GlassLit);
        Canvas.Fill(span, 0, 680, W, 2, Canvas.Edge);
        font.Draw(span, W, "AETHERSTREAM ALMANAC", 24, 680, Canvas.Accent, 1, all);
        const string Note = "RESETS IN YOUR LOCAL TIME";
        font.Draw(span, W, Note, W - 24 - font.Measure(Note), 680, Canvas.Faint, 1, all);
    }

    private static string Countdown(TimeSpan t)
    {
        if (t <= TimeSpan.Zero)
            return "NOW";
        if (t < TimeSpan.FromMinutes(15) && t.TotalHours < 1)
            return $"{t.Minutes:00}:{t.Seconds:00}";
        if (t.TotalDays >= 1)
            return $"{(int)t.TotalDays}D {t.Hours:00}H {t.Minutes:00}M";
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours}H {t.Minutes:00}M {t.Seconds:00}S";
        return $"{t.Minutes:00}:{t.Seconds:00}";
    }
}
