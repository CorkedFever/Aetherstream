using Aetherstream.Plugin.Weather;

namespace Aetherstream.Plugin.Video;

/// <summary>One line of the fishing board.</summary>
internal sealed record FishingRow(
    string Name,
    string Spot,
    string Zone,
    string Conditions,
    string Bait,
    bool Up,
    int SecondsLeft,
    int WindowStartMinute,
    int WindowEndMinute,
    bool Folklore,
    bool Caught,
    bool Here);

internal sealed record FishingSnapshot(
    IReadOnlyList<FishingRow> Rows,
    int UpCount,
    int Total,
    int Caught,
    IReadOnlyList<ResetTimers.Voyage> Voyages);

/// <summary>
/// The fishing log as a departures board: the fish you still need that are biting now, then the
/// ones whose window opens soonest, each with its spot, its conditions and its bait. The next
/// boats along the top. Rod fishing with a window only; a fish that is always there needs no board.
/// </summary>
internal sealed class FishingChannel(BitmapFont font, Func<FishingSnapshot?> data) : IFrameChannel
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

        Canvas.Fill(span, 0, 0, W, 56, Canvas.GlassLit);
        Canvas.Fill(span, 0, 56, W, 2, Canvas.Edge);
        font.Draw(span, W, "FISHING LOG", 24, 8, Canvas.Amber, 1, all);

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var etMinutes = unix * 24 / 70;
        var et = $"ET {etMinutes / 60 % 24:00}:{etMinutes % 60:00}";
        font.Draw(span, W, et, (W - font.Measure(et)) / 2, 8, Canvas.White, 1, all);

        var local = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, local, W - 24 - font.Measure(local), 8, Canvas.White, 1, all);

        var snapshot = data();

        // The boats: the next one with both routes, then the three after in a line.
        Canvas.Fill(span, 0, 58, W, 84, Canvas.Tube);
        Canvas.Fill(span, 0, 142, W, 2, Canvas.Edge);
        if (snapshot is { Voyages.Count: > 0 } s)
        {
            var next = s.Voyages[0];
            var until = next.DepartsUtc - DateTime.UtcNow;
            var boarding = until <= TimeSpan.Zero;
            var head = boarding ? "BOAT BOARDING NOW" : $"NEXT BOAT IN {Countdown((int)until.TotalSeconds)}";
            font.Draw(span, W, head, 24, 62, boarding ? Canvas.Good : Canvas.Accent, 1, all);

            // Short names throughout: the face is eighty columns wide and the full names are not.
            var routes = $"INDIGO {Short(next.IndigoDestination)} ({next.IndigoTime})   RUBY {Short(next.RubyDestination)} ({next.RubyTime})".ToUpperInvariant();
            var routesLeft = 24 + font.Measure(head) + 48;
            routes = Canvas.Cut(routes, font.Fit(W - routesLeft - 24));
            font.Draw(span, W, routes, routesLeft, 62, Canvas.White, 1, all);

            var later = string.Join("  ", s.Voyages.Skip(1).Select(v =>
                $"{v.DepartsUtc.ToLocalTime():HH:mm} {Short(v.IndigoDestination)}/{Short(v.RubyDestination)}".ToUpperInvariant()));
            font.Draw(span, W, Canvas.Cut("THEN " + later, font.Fit(W - 48)), 24, 100, Canvas.Faint, 1, all);
        }

        var rows = snapshot?.Rows ?? [];
        if (rows.Count == 0)
        {
            var none = snapshot is null ? "READING THE FISH DATA" : "NOTHING WITH A WINDOW IN THE NEXT DAY AND A HALF";
            font.Draw(span, W, Canvas.Cut(none, font.Fit(W - 48, 2)), (W - font.Measure(Canvas.Cut(none, font.Fit(W - 48, 2)), 2)) / 2, 380, Canvas.Faint, 2, all);
            this.DrawFooter(span, snapshot, all);
            return;
        }

        const int ColName = 24, ColRight = 1064;
        font.Draw(span, W, "FISH, WHERE, AND HOW", ColName, 152, Canvas.Amber, 1, all);
        font.Draw(span, W, "IN / WINDOW", ColRight, 152, Canvas.Amber, 1, all);

        const int RowTop = 194, RowHeight = 68, RowsBottom = 680;
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

                // Line one: the fish, the spot and its zone, and the countdown.
                var line1 = y + 2;
                var nameColour = r.Caught ? Canvas.Faint : r.Folklore ? Canvas.Rgb(0xFF, 0xD6, 0x4F) : Canvas.White;
                var name = Canvas.Plain(r.Name).ToUpperInvariant();
                font.Draw(span, W, name, ColName, line1, nameColour, 1, clip);

                var where = Canvas.Cut($"{r.Spot}, {r.Zone}".ToUpperInvariant(), font.Fit(ColRight - ColName - 16 - font.Measure(name) - 32));
                font.Draw(span, W, where, ColName + font.Measure(name) + 32, line1, Canvas.Dim, 1, clip);

                var left = r.Up ? $"UP {Countdown(r.SecondsLeft)}" : Countdown(r.SecondsLeft);
                font.Draw(span, W, left, ColRight, line1, r.Up ? Canvas.Good : Canvas.White, 1, clip);

                // Line two: the conditions and the bait, and the Eorzean window under the countdown.
                var line2 = y + 32;
                var detail = r.Conditions;
                if (r.Bait.Length > 0)
                    detail = (detail.Length > 0 ? detail + "  /  " : string.Empty) + r.Bait;
                detail = Canvas.Cut(Canvas.Plain(detail).ToUpperInvariant(), font.Fit(ColRight - ColName - 16));
                font.Draw(span, W, detail, ColName, line2, Canvas.Faint, 1, clip);

                var window = $"{r.WindowStartMinute / 60:00}:{r.WindowStartMinute % 60:00}-{r.WindowEndMinute / 60:00}:{r.WindowEndMinute % 60:00}";
                font.Draw(span, W, window, ColRight, line2, Canvas.Faint, 1, clip);

                // The spot you are standing in gets a mark in the margin.
                if (r.Here)
                    Canvas.Fill(span, 0, top, 4, bottom - top, Canvas.Accent);
            }
        }

        this.DrawFooter(span, snapshot, all);
    }

    private void DrawFooter(Span<uint> span, FishingSnapshot? snapshot, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 680, W, 40, Canvas.GlassLit);
        Canvas.Fill(span, 0, 680, W, 2, Canvas.Edge);
        font.Draw(span, W, "AETHERSTREAM FISHING", 24, 680, Canvas.Accent, 1, all);

        var right = snapshot is { } s
            ? $"{s.Caught}/{s.Total} IN LOG / GOLD = FOLKLORE / DATA: CARBUNCLE PLUSHY"
            : "READING THE LOG";
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Canvas.Faint, 1, all);
    }

    private static string Countdown(int seconds)
    {
        seconds = Math.Max(0, seconds);
        var h = seconds / 3600;
        var m = seconds % 3600 / 60;
        var s = seconds % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    private static string Short(string destination) => destination switch
    {
        "The Bloodbrine Sea" => "Bloodbrine",
        "The Rothlyt Sound" => "Rothlyt",
        "The Northern Strait of Merlthor" => "N. Merlthor",
        "The Rhotano Sea" => "Rhotano",
        "The One River" => "One River",
        "The Ruby Sea" => "Ruby Sea",
        _ => destination,
    };
}
