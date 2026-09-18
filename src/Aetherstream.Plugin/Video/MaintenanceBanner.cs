using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetherstream.Plugin.Video;

/// <summary>
/// A maintenance notice, from the game's own chat push or the Lodestone feed. Times are UTC and
/// may be missing when the wording could not be read; the banner then shows the words as they came.
/// </summary>
internal sealed record MaintenanceNotice(string Worlds, bool Emergency, DateTime? Start, DateTime? End, string Raw, DateTime Received);

/// <summary>
/// Something painted over whatever is on: a picture, a drawn channel, or the test card.
/// </summary>
internal interface IFrameOverlay
{
    /// <summary>Whether there is anything to paint right now. Asked every frame; keep it cheap.</summary>
    bool Active { get; }

    void Paint(uint[] target, int width, int height, DateTime now, double seconds);
}

/// <summary>
/// The band along the bottom of the picture that says maintenance is coming. Red and pulsing for
/// an emergency, amber for the scheduled kind. The words crawl when they will not fit.
/// </summary>
internal sealed class MaintenanceBanner(BitmapFont font)
{
    private const int BandHeight = 56;
    private const uint White = Canvas.White;

    public void Paint(Span<uint> span, int Width, int Height, MaintenanceNotice notice, DateTime now, double seconds)
    {
        var top = Height - BandHeight;
        var pulse = (int)(seconds * 2) % 2 == 0;
        var band = notice.Emergency
            ? (pulse ? Canvas.Rgb(0xA3, 0x2D, 0x2D) : Canvas.Rgb(0x79, 0x1F, 0x1F))
            : Canvas.Rgb(0x7A, 0x4E, 0x12);
        var edge = notice.Emergency ? Canvas.Bad : Canvas.Amber;

        Canvas.Fill(span, 0, top, Width, BandHeight, band);
        Canvas.Fill(span, 0, top, Width, 3, edge);

        // The tag on the left, on a darker block, then the message beside it.
        var tag = notice.Emergency ? "EMERGENCY" : "MAINTENANCE";
        var tagWidth = font.Measure(tag) + 32;
        Canvas.Fill(span, 0, top + 3, tagWidth, BandHeight - 3, notice.Emergency ? Canvas.Rgb(0x4A, 0x10, 0x10) : Canvas.Rgb(0x45, 0x2B, 0x08));
        font.Draw(span, Width, tag, 16, top + 8, White, 1, new BitmapFont.Clip(0, top, tagWidth, Height));

        var text = Message(notice, now);
        var left = tagWidth + 20;
        var room = Width - left - 16;
        var clip = new BitmapFont.Clip(left, top, Width - 16, Height);
        var measured = font.Measure(text);
        if (measured <= room)
        {
            font.Draw(span, Width, text, left, top + 8, White, 1, clip);
            return;
        }

        // A crawl: the text scrolls in from the right and off the left, then starts again.
        var crawl = text + "        ";
        var crawlWidth = font.Measure(crawl);
        var offset = (int)(seconds * 90.0) % (crawlWidth + room);
        var x = left + room - offset;
        font.Draw(span, Width, crawl, x, top + 8, White, 1, clip);
        font.Draw(span, Width, crawl, x + crawlWidth, top + 8, White, 1, clip);
    }

    /// <summary>The line itself: which worlds, the window in local time, and how far off it is.</summary>
    public static string Message(MaintenanceNotice notice, DateTime now)
    {
        var worlds = notice.Worlds.Length > 0 ? notice.Worlds.ToUpperInvariant() : string.Empty;
        if (notice.Start is not { } start)
            return Canvas.Plain(notice.Raw).ToUpperInvariant();

        var utc = now.Kind == DateTimeKind.Utc ? now : now.ToUniversalTime();
        var s0 = start.ToLocalTime();
        var window = notice.End is { } end
            ? $"{s0:h:mm tt} - {end.ToLocalTime():h:mm tt}"
            : $"FROM {s0:h:mm tt}";

        var untilStart = start - utc;
        var state = untilStart > TimeSpan.Zero
            ? $"STARTS IN {Span(untilStart)}"
            : notice.End is { } e && e > utc
                ? $"UNDER WAY, ENDS IN {Span(e - utc)}"
                : "UNDER WAY";

        // The tag beside the crawl already says EMERGENCY or MAINTENANCE, so the line itself is
        // the worlds, the window and the countdown: short enough to sit still.
        var where = worlds.Length > 0 ? $"{worlds}  " : string.Empty;
        return $"{where}{window.ToUpperInvariant()}  {state}";
    }

    private static string Span(TimeSpan t) =>
        t.TotalDays >= 1 ? $"{(int)t.TotalDays}D {t.Hours}H"
        : t.TotalHours >= 1 ? $"{(int)t.TotalHours}H {t.Minutes:00}M"
        : $"{Math.Max(1, (int)Math.Ceiling(t.TotalMinutes))}M";

    // "From Sep. 14 at 12:00 a.m. to 1:00 a.m. (PDT), emergency maintenance will take place on
    // Gilgamesh World." Also "...to Sep. 15 at 3:00 a.m. (PDT), maintenance will take place on
    // all Worlds." The date on the end is optional; the zone is the game's own abbreviation.
    private static readonly Regex Push = new(
        @"From\s+(?<start>.+?)\s+to\s+(?<end>.+?)\s+\((?<zone>[A-Z]{2,5})\),\s+(?<emergency>emergency\s+)?maintenance\s+will\s+take\s+place\s+on\s+(?<worlds>.+?)\.",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, int> ZoneOffsets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UTC"] = 0, ["GMT"] = 0, ["BST"] = 1, ["IST"] = 1, ["CET"] = 1, ["CEST"] = 2, ["EET"] = 2, ["EEST"] = 3,
        ["EST"] = -5, ["EDT"] = -4, ["CST"] = -6, ["CDT"] = -5, ["MST"] = -7, ["MDT"] = -6, ["PST"] = -8, ["PDT"] = -7,
        ["AKST"] = -9, ["AKDT"] = -8, ["HST"] = -10, ["JST"] = 9, ["KST"] = 9, ["AEST"] = 10, ["AEDT"] = 11, ["NZST"] = 12, ["NZDT"] = 13,
    };

    /// <summary>
    /// Reads the game's maintenance push. Null when the line is not one; a notice with no times
    /// when it is one but the wording defeated the parser.
    /// </summary>
    public static MaintenanceNotice? FromChat(string line, DateTime receivedUtc)
    {
        var plain = Canvas.Plain(line).Trim();
        if (!plain.Contains("maintenance", StringComparison.OrdinalIgnoreCase))
            return null;

        // The push comes as three lines; only the first has the window in it. The others are
        // "please log out" and "see the News section", which are not notices of their own.
        if (!plain.Contains("will take place", StringComparison.OrdinalIgnoreCase))
            return null;

        var m = Push.Match(plain);
        if (!m.Success)
            return new MaintenanceNotice(string.Empty, plain.Contains("emergency", StringComparison.OrdinalIgnoreCase), null, null, plain, receivedUtc);

        var emergency = m.Groups["emergency"].Success;
        var worlds = TidyWorlds(m.Groups["worlds"].Value);
        var offset = ZoneOffsets.GetValueOrDefault(m.Groups["zone"].Value, int.MinValue);

        var start = ParseStamp(m.Groups["start"].Value, null, receivedUtc, offset);
        var end = ParseStamp(m.Groups["end"].Value, start, receivedUtc, offset);
        if (start is { } s && end is { } e && e < s)
            end = e.AddDays(1);

        return new MaintenanceNotice(worlds, emergency, start, end, plain, receivedUtc);
    }

    /// <summary>"Gilgamesh World" → "Gilgamesh"; "all Worlds" → "All Worlds".</summary>
    private static string TidyWorlds(string worlds)
    {
        var w = worlds.Trim();
        if (w.Equals("all worlds", StringComparison.OrdinalIgnoreCase))
            return "All Worlds";

        if (w.EndsWith(" World", StringComparison.OrdinalIgnoreCase))
            w = w[..^6];

        return w;
    }

    /// <summary>
    /// "Sep. 14 at 12:00 a.m." or just "1:00 a.m." (the date then comes from the other end).
    /// Null when unreadable, or when the zone was one the table does not know.
    /// </summary>
    private static DateTime? ParseStamp(string text, DateTime? dateFrom, DateTime receivedUtc, int zoneOffsetHours)
    {
        if (zoneOffsetHours == int.MinValue)
            return null;

        var t = text.Replace("a.m.", "AM", StringComparison.OrdinalIgnoreCase)
            .Replace("p.m.", "PM", StringComparison.OrdinalIgnoreCase)
            .Replace(" at ", " ", StringComparison.OrdinalIgnoreCase)
            .Replace(".", string.Empty)
            .Trim();

        var zone = TimeSpan.FromHours(zoneOffsetHours);
        var year = receivedUtc.Add(zone).Year;
        var culture = CultureInfo.InvariantCulture;

        // With a date: the year is this one (a push is never far ahead).
        foreach (var format in new[] { "MMM d h:mm tt", "MMMM d h:mm tt", "MMM d H:mm", "MMMM d H:mm" })
        {
            if (DateTime.TryParseExact($"{t} {year}", format + " yyyy", culture, DateTimeStyles.None, out var withDate))
                return new DateTimeOffset(withDate, zone).UtcDateTime;
        }

        // Without one: the same local day as the start, or as the push arrived.
        foreach (var format in new[] { "h:mm tt", "H:mm" })
        {
            if (DateTime.TryParseExact(t, format, culture, DateTimeStyles.None, out var timeOnly))
            {
                var day = dateFrom is { } from ? new DateTimeOffset(from, TimeSpan.Zero).ToOffset(zone).Date : receivedUtc.Add(zone).Date;
                return new DateTimeOffset(day.Add(timeOnly.TimeOfDay), zone).UtcDateTime;
            }
        }

        return null;
    }

    /// <summary>
    /// A Lodestone maintenance headline as a notice: "All Worlds Emergency Maintenance (Sep. 14)".
    /// </summary>
    public static MaintenanceNotice FromNews(NewsItem item)
    {
        var title = item.Title;
        var emergency = title.Contains("emergency", StringComparison.OrdinalIgnoreCase);
        // "[Aether] Gilgamesh World Emergency Maintenance (Sep. 14)" -> "Gilgamesh".
        var worlds = Regex.Replace(title, @"\s*[\(\[].*?[\)\]]\s*", " ", RegexOptions.IgnoreCase);
        worlds = Regex.Replace(worlds, @"\b(emergency|maintenance|scheduled|companion app|lodestone|mog station|website|square enix account|patch [\d.]+)\b", string.Empty, RegexOptions.IgnoreCase);
        worlds = Regex.Replace(worlds, @"\s+", " ").Trim();
        worlds = worlds.Equals("all worlds", StringComparison.OrdinalIgnoreCase) ? "All Worlds" : TidyWorlds(worlds);

        return new MaintenanceNotice(worlds, emergency, item.Start, item.End, title, item.Time);
    }
}
