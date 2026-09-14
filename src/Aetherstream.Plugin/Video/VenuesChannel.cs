namespace Aetherstream.Plugin.Video;

/// <summary>A venue as the channel shows it. <paramref name="Banner"/> is 560x280 RGBA (the site's 2:1 banners) or null while it loads.</summary>
internal sealed record VenueRow(
    string Id,
    string Name,
    string Location,
    string World,
    string Description,
    string Tags,
    bool OpenNow,
    DateTime? OpensUtc,
    DateTime? ClosesUtc,
    uint[]? Banner,
    bool Sfw = true);

internal sealed record VenuesSnapshot(
    string DataCenter,
    IReadOnlyList<VenueRow> OpenNow,
    IReadOnlyList<VenueRow> Soon,
    DateTime FetchedAt,
    string Status);

/// <summary>
/// The venues channel: who is open on your datacenter tonight, from ffxivvenues.com. One venue
/// featured with its banner and description, the open list and the coming-up list beside it,
/// and a crawl of everyone open along the bottom. The feature turns every twelve seconds.
/// </summary>
internal sealed class VenuesChannel(BitmapFont font, Func<VenuesSnapshot?> data) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double FeatureSeconds = 12.0;
    private const double TickerSpeed = 90.0;

    /// <summary>Adult venues, when the filter lets them through: purple, instead of a label on every line.</summary>
    private static readonly uint Adult = Canvas.Rgb(0xC9, 0x8A, 0xF0);

    public const int BannerWidth = 560;
    public const int BannerHeight = 280;

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var utc = DateTime.UtcNow;

        Canvas.Fill(span, 0, 0, W, H, Canvas.Glass);

        var snapshot = data();
        var dc = snapshot?.DataCenter is { Length: > 0 } d ? d.ToUpperInvariant() : "NO REGION";

        // Header, in the venue-neon pink of a good evening.
        Canvas.Fill(span, 0, 0, W, 56, Canvas.Rgb(0x4B, 0x15, 0x28));
        Canvas.Fill(span, 0, 56, W, 2, Canvas.Rgb(0xD4, 0x53, 0x7E));
        font.Draw(span, W, "VENUES", 24, 8, Canvas.Rgb(0xED, 0x93, 0xB1), 1, all);
        font.Draw(span, W, dc, (W - font.Measure(dc)) / 2, 8, Canvas.White, 1, all);
        var local = now.ToString("ddd h:mm tt").ToUpperInvariant();
        font.Draw(span, W, local, W - 24 - font.Measure(local), 8, Canvas.White, 1, all);

        var open = snapshot?.OpenNow ?? [];
        var soon = snapshot?.Soon ?? [];
        var featured = open.Count > 0 ? open : soon;

        if (featured.Count == 0)
        {
            var line = snapshot?.Status is { Length: > 0 } s ? s.ToUpperInvariant() : "READING THE LISTINGS";
            font.Draw(span, W, line, (W - font.Measure(line, 2)) / 2, 330, Canvas.Faint, 2, all);
            this.DrawFooter(span, snapshot, all);
            return;
        }

        // -- the featured venue -----------------------------------------------------------------------
        var venue = featured[(int)(seconds / FeatureSeconds) % featured.Count];
        const int Left = 24;

        if (venue.Banner is { } banner)
        {
            for (var y = 0; y < BannerHeight; y++)
                banner.AsSpan(y * BannerWidth, BannerWidth).CopyTo(span.Slice(((72 + y) * W) + Left, BannerWidth));
        }
        else
        {
            Canvas.Fill(span, Left, 72, BannerWidth, BannerHeight, Canvas.Tube);
            const string NoArt = "NO BANNER";
            font.Draw(span, W, NoArt, Left + ((BannerWidth - font.Measure(NoArt)) / 2), 72 + 140, Canvas.Faint, 1, all);
        }

        Canvas.Rect(span, Left - 3, 69, BannerWidth + 6, BannerHeight + 6, Canvas.Edge);

        var ty = 72 + BannerHeight + 14;
        foreach (var line in Canvas.Wrap(venue.Name.ToUpperInvariant(), font.Fit(BannerWidth, 2), 1))
        {
            font.Draw(span, W, line, Left, ty, !venue.Sfw ? Adult : venue.OpenNow ? Canvas.Good : Canvas.White, 2, all);
            ty += 84;
        }

        // Where: the plot on the left and the world on the right of the same line when both fit,
        // otherwise the world drops to a line of its own.
        var place = Canvas.Cut(venue.Location.ToUpperInvariant(), font.Fit(BannerWidth));
        var world = Canvas.Cut(venue.World.ToUpperInvariant(), font.Fit(BannerWidth));
        font.Draw(span, W, place, Left, ty, Canvas.White, 1, all);
        if (font.Measure(place) + 32 + font.Measure(world) <= BannerWidth)
        {
            font.Draw(span, W, world, Left + BannerWidth - font.Measure(world), ty, Canvas.Dim, 1, all);
            ty += 40;
        }
        else
        {
            ty += 40;
            font.Draw(span, W, world, Left, ty, Canvas.Dim, 1, all);
            ty += 40;
        }

        var hours = venue.OpenNow
            ? venue.ClosesUtc is { } c ? $"OPEN NOW UNTIL {c.ToLocalTime():h:mm tt}" : "OPEN NOW"
            : venue.OpensUtc is { } o ? $"OPENS {WhenText(o, now)}" : "HOURS NOT LISTED";
        font.Draw(span, W, hours.ToUpperInvariant(), Left, ty, venue.OpenNow ? Canvas.Good : Canvas.Amber, 1, all);
        ty += 40;

        // The description, or the tags when there is none; the space fits two lines either way.
        var blurb = venue.Description.Length > 0 ? venue.Description : venue.Tags;
        foreach (var line in Canvas.Wrap(blurb, font.Fit(BannerWidth), Math.Max(0, (640 - ty) / 40)))
        {
            font.Draw(span, W, line, Left, ty, Canvas.Dim, 1, all);
            ty += 40;
        }

        // -- the lists ----------------------------------------------------------------------------------
        const int ListLeft = 620;
        Canvas.Fill(span, ListLeft - 16, 72, 2, 560, Canvas.Edge);
        var region = new BitmapFont.Clip(ListLeft, 72, W, 636);

        // Two lines a venue: the name and its time, then where it is.
        var entries = new List<(string Text, string Detail, string Time, uint Colour, bool Featured, bool Heading, bool Adult)>();
        entries.Add(("OPEN NOW", string.Empty, string.Empty, Canvas.Amber, false, true, false));
        if (open.Count == 0)
            entries.Add(("nobody yet", string.Empty, string.Empty, Canvas.Faint, false, true, false));
        foreach (var v in open)
            entries.Add((v.Name, $"{v.Location}  /  {v.World}", v.ClosesUtc is { } closes ? $"til {closes.ToLocalTime():h:mm tt}" : string.Empty, v.Sfw ? Canvas.Good : Adult, ReferenceEquals(v, venue), false, !v.Sfw));

        entries.Add(("COMING UP", string.Empty, string.Empty, Canvas.Amber, false, true, false));
        if (soon.Count == 0)
            entries.Add(("nothing in the next day", string.Empty, string.Empty, Canvas.Faint, false, true, false));
        foreach (var v in soon)
            entries.Add((v.Name, $"{v.Location}  /  {v.World}", v.OpensUtc is { } opens ? WhenText(opens, now) : string.Empty, v.Sfw ? Canvas.White : Adult, ReferenceEquals(v, venue), false, !v.Sfw));

        // Headings take one line, venues two; the list climbs when it does not fit.
        var heights = entries.Select(e => e.Heading ? 44 : 76).ToList();
        var total = heights.Sum();
        var fits = total <= 636 - 72;
        var offset = fits ? 0 : (int)((seconds * 18.0) % total);

        for (var pass = 0; pass < (fits ? 1 : 2); pass++)
        {
            var y = 72 - offset + (pass * total);
            for (var i = 0; i < entries.Count; i++)
            {
                var height = heights[i];
                var top = y;
                y += height;
                if (top + height <= 72 || top >= 636)
                    continue;

                var (text, detail, time, colour, isFeatured, heading, adult) = entries[i];

                if (isFeatured)
                    Canvas.Fill(span, ListLeft - 8, Math.Max(72, top), 4, Math.Min(636, top + height - 8) - Math.Max(72, top), Canvas.Rgb(0xED, 0x93, 0xB1));

                var timeText = time.ToUpperInvariant();
                var timeWidth = timeText.Length > 0 ? font.Measure(timeText) + 16 : 0;
                var nameText = Canvas.Cut(text.ToUpperInvariant(), font.Fit(W - ListLeft - 40 - timeWidth));
                font.Draw(span, W, nameText, ListLeft, top, colour, 1, region);
                if (timeText.Length > 0)
                    font.Draw(span, W, timeText, W - 40 - font.Measure(timeText), top, Canvas.Dim, 1, region);

                if (!heading && detail.Length > 0)
                    font.Draw(span, W, Canvas.Cut(detail.ToUpperInvariant(), font.Fit(W - ListLeft - 40)), ListLeft, top + 34, Canvas.Faint, 1, region);
            }
        }

        // -- ticker ----------------------------------------------------------------------------------
        Canvas.Fill(span, 0, 640, W, 40, Canvas.GlassLit);
        if (open.Count > 0)
        {
            var ticker = "OPEN NOW:   " + string.Join("     ", open.Select(v => $"{Canvas.Plain(v.Name).ToUpperInvariant()} ({Canvas.Plain(v.Location).ToUpperInvariant()})"));
            var width = font.Measure(ticker);
            var tx = W - (int)((seconds * TickerSpeed) % (width + W));
            font.Draw(span, W, ticker, tx, 640, Canvas.Rgb(0xED, 0x93, 0xB1), 1, new BitmapFont.Clip(0, 640, W, 680));
        }

        this.DrawFooter(span, snapshot, all);

        if (open.Concat(soon).Any(v => !v.Sfw))
            font.Draw(span, W, "PURPLE: 18+", 24 + font.Measure("AETHERSTREAM VENUES") + 32, 680, Adult, 1, all);
    }

    private static string WhenText(DateTime utc, DateTime now)
    {
        var local = utc.ToLocalTime();
        var until = utc - DateTime.UtcNow;
        var when = local.Date == now.Date ? local.ToString("h:mm tt") : local.ToString("ddd h:mm tt");
        // Only the near ones get a countdown; further out, the clock time says enough and the
        // list keeps room for the name.
        var inText = until.TotalMinutes >= 1 && until.TotalHours < 1 ? $" (in {(int)until.TotalMinutes} min)" : string.Empty;
        return when + inText;
    }

    private void DrawFooter(Span<uint> span, VenuesSnapshot? snapshot, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 680, W, 40, Canvas.Glass);
        Canvas.Fill(span, 0, 680, W, 2, Canvas.Edge);
        font.Draw(span, W, "AETHERSTREAM VENUES", 24, 680, Canvas.Accent, 1, all);

        var right = snapshot is { FetchedAt: var at } && at > DateTime.MinValue
            ? $"FFXIVVENUES.COM, UPDATED {at.ToLocalTime():h:mm tt}".ToUpperInvariant()
            : "VIA FFXIVVENUES.COM";
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Canvas.Faint, 1, all);
    }
}
