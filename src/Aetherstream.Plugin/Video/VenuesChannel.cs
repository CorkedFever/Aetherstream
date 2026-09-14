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
    uint[]? Banner);

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
            font.Draw(span, W, line, Left, ty, Canvas.White, 2, all);
            ty += 84;
        }

        font.Draw(span, W, Canvas.Cut($"{venue.Location}  /  {venue.World}".ToUpperInvariant(), font.Fit(BannerWidth)), Left, ty, Canvas.Dim, 1, all);
        ty += 36;

        var hours = venue.OpenNow
            ? venue.ClosesUtc is { } c ? $"OPEN NOW UNTIL {c.ToLocalTime():h:mm tt}" : "OPEN NOW"
            : venue.OpensUtc is { } o ? $"OPENS {WhenText(o, now)}" : "HOURS NOT LISTED";
        font.Draw(span, W, hours.ToUpperInvariant(), Left, ty, venue.OpenNow ? Canvas.Good : Canvas.Amber, 1, all);
        ty += 36;

        if (venue.Tags.Length > 0)
        {
            font.Draw(span, W, Canvas.Cut(venue.Tags.ToUpperInvariant(), font.Fit(BannerWidth)), Left, ty, Canvas.Faint, 1, all);
            ty += 36;
        }

        foreach (var line in Canvas.Wrap(venue.Description, font.Fit(BannerWidth), Math.Max(0, (636 - ty) / 34)))
        {
            font.Draw(span, W, line, Left, ty, Canvas.Dim, 1, all);
            ty += 34;
        }

        // -- the lists ----------------------------------------------------------------------------------
        const int ListLeft = 620;
        Canvas.Fill(span, ListLeft - 16, 72, 2, 560, Canvas.Edge);
        var region = new BitmapFont.Clip(ListLeft, 72, W, 636);

        var entries = new List<(string Text, string Time, uint Colour, bool Featured)>();
        entries.Add(("OPEN NOW", string.Empty, Canvas.Amber, false));
        if (open.Count == 0)
            entries.Add(("nobody yet", string.Empty, Canvas.Faint, false));
        foreach (var v in open)
            entries.Add((v.Name, v.ClosesUtc is { } closes ? $"til {closes.ToLocalTime():h:mm tt}" : string.Empty, Canvas.Good, ReferenceEquals(v, venue)));

        entries.Add((string.Empty, string.Empty, 0, false));
        entries.Add(("COMING UP", string.Empty, Canvas.Amber, false));
        if (soon.Count == 0)
            entries.Add(("nothing in the next day", string.Empty, Canvas.Faint, false));
        foreach (var v in soon)
            entries.Add((v.Name, v.OpensUtc is { } opens ? WhenText(opens, now) : string.Empty, Canvas.White, ReferenceEquals(v, venue)));

        const int RowHeight = 36;
        var visible = (636 - 72) / RowHeight;
        var total = entries.Count * RowHeight;
        var offset = entries.Count <= visible ? 0 : (int)((seconds * 16.0) % total);

        for (var pass = 0; pass < (entries.Count <= visible ? 1 : 2); pass++)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var y = 72 - offset + (pass * total) + (i * RowHeight);
                if (y + RowHeight <= 72 || y >= 636)
                    continue;

                var (text, time, colour, isFeatured) = entries[i];
                if (text.Length == 0)
                    continue;

                if (isFeatured)
                    Canvas.Fill(span, ListLeft - 8, Math.Max(72, y), 4, Math.Min(636, y + RowHeight) - Math.Max(72, y), Canvas.Rgb(0xED, 0x93, 0xB1));

                var timeWidth = time.Length > 0 ? font.Measure(time.ToUpperInvariant()) + 16 : 0;
                font.Draw(span, W, Canvas.Cut(text.ToUpperInvariant(), font.Fit(W - ListLeft - 40 - timeWidth)), ListLeft, y, colour, 1, region);
                if (time.Length > 0)
                    font.Draw(span, W, time.ToUpperInvariant(), W - 40 - font.Measure(time.ToUpperInvariant()), y, Canvas.Dim, 1, region);
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
    }

    private static string WhenText(DateTime utc, DateTime now)
    {
        var local = utc.ToLocalTime();
        var until = utc - DateTime.UtcNow;
        var when = local.Date == now.Date ? local.ToString("h:mm tt") : local.ToString("ddd h:mm tt");
        var inText = until.TotalMinutes < 1 ? string.Empty
            : until.TotalHours < 1 ? $" (in {(int)until.TotalMinutes} min)"
            : until.TotalHours < 24 ? $" (in {(int)until.TotalHours}h {until.Minutes:00}m)"
            : string.Empty;
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
