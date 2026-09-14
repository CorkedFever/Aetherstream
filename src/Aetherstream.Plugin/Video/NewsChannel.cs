namespace Aetherstream.Plugin.Video;

internal sealed record NewsItem(string Title, string Description, DateTime Time, string Kind);

/// <summary>The headlines as last fetched. <paramref name="Status"/> says why the list might be short.</summary>
internal sealed record NewsSnapshot(IReadOnlyList<NewsItem> Items, DateTime FetchedAt, string Status);

/// <summary>
/// Lodestone news as a rolling news channel: one story big, the rest listed, a ticker of every
/// headline. Stories change every ten seconds. Everything shown comes from the plugin's cached
/// fetch, so this draws happily with no network at all.
/// </summary>
internal sealed class NewsChannel(BitmapFont font, Func<NewsSnapshot?> data) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double HoldSeconds = 10.0;
    private const double TickerSpeed = 90.0;

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);

        Canvas.Fill(span, 0, 0, W, H, Canvas.Tube);

        // Header.
        Canvas.Fill(span, 0, 0, W, 56, Canvas.Bad);
        font.Draw(span, W, "LODESTONE", 24, 8, Canvas.White, 1, all);
        var local = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, local, W - 24 - font.Measure(local), 8, Canvas.White, 1, all);

        var snapshot = data();
        var items = snapshot?.Items ?? [];

        if (items.Count == 0)
        {
            var line = snapshot?.Status is { Length: > 0 } s ? s.ToUpperInvariant() : "CHECKING THE LODESTONE";
            font.Draw(span, W, line, (W - font.Measure(line, 2)) / 2, 330, Canvas.Faint, 2, all);
            this.DrawFooter(span, snapshot, all);
            return;
        }

        // -- the story --------------------------------------------------------------------------------
        var index = (int)(seconds / HoldSeconds) % items.Count;
        var story = items[index];

        const int Left = 24;
        const int StoryWidth = 780;

        var kind = story.Kind.ToUpperInvariant();
        var when = story.Time.ToLocalTime().ToString("ddd d MMM").ToUpperInvariant();
        font.Draw(span, W, $"{kind}   {when}", Left, 76, Canvas.Amber, 1, all);

        var y = 112;
        foreach (var line in Canvas.Wrap(story.Title.ToUpperInvariant(), font.Fit(StoryWidth, 2), 2))
        {
            font.Draw(span, W, line, Left, y, Canvas.White, 2, all);
            y += 84;
        }

        y += 8;
        foreach (var line in Canvas.Wrap(story.Description, font.Fit(StoryWidth), 8))
        {
            font.Draw(span, W, line, Left, y, Canvas.Dim, 1, all);
            y += 40;
        }

        // A progress bar along the bottom of the story: how long until the next one.
        var progress = (seconds % HoldSeconds) / HoldSeconds;
        Canvas.Fill(span, Left, 636, StoryWidth, 4, Canvas.Edge);
        Canvas.Fill(span, Left, 636, (int)(StoryWidth * progress), 4, Canvas.Accent);

        // -- the others -------------------------------------------------------------------------------
        const int ListLeft = 840;
        Canvas.Fill(span, ListLeft - 16, 72, 2, 560, Canvas.Edge);
        font.Draw(span, W, "MORE", ListLeft, 76, Canvas.Amber, 1, all);

        var ly = 116;
        var shown = 0;
        for (var i = 1; i < items.Count && shown < 12; i++)
        {
            var other = items[(index + i) % items.Count];
            var title = Canvas.Cut(other.Title.ToUpperInvariant(), font.Fit(W - ListLeft - 24));
            font.Draw(span, W, title, ListLeft, ly, shown == 0 ? Canvas.White : Canvas.Dim, 1, all);
            ly += 40;
            shown++;
        }

        // -- ticker -----------------------------------------------------------------------------------
        Canvas.Fill(span, 0, 640, W, 40, Canvas.GlassLit);
        var ticker = string.Join("   +++   ", items.Select(i => Canvas.Plain(i.Title).ToUpperInvariant()));
        var width = font.Measure(ticker);
        var tx = W - (int)((seconds * TickerSpeed) % (width + W));
        font.Draw(span, W, ticker, tx, 640, Canvas.Amber, 1, new BitmapFont.Clip(0, 640, W, 680));

        this.DrawFooter(span, snapshot, all);
    }

    private void DrawFooter(Span<uint> span, NewsSnapshot? snapshot, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 680, W, 40, Canvas.Glass);
        Canvas.Fill(span, 0, 680, W, 2, Canvas.Edge);
        font.Draw(span, W, "AETHERSTREAM NEWS", 24, 680, Canvas.Accent, 1, all);

        var right = snapshot is { FetchedAt: var at } && at > DateTime.MinValue
            ? $"UPDATED {at.ToLocalTime():h:mm tt}".ToUpperInvariant()
            : "VIA LODESTONENEWS.COM";
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Canvas.Faint, 1, all);
    }
}
