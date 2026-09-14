namespace Aetherstream.Plugin.Video;

/// <summary>One line of the guide: a channel, a party, or something on demand.</summary>
internal sealed record GuideRow(
    string Number,
    string Name,
    string Detail,
    bool Current = false,
    bool Offline = false,
    bool Live = false);

/// <summary>Everything the guide shows this second, gathered by the plugin from what it knows.</summary>
internal sealed record GuideSnapshot(
    IReadOnlyList<GuideRow> Rows,
    string NowPlaying,
    string Ticker);

/// <summary>
/// The guide channel: the 90s TV listings channel, on your own set.
/// <para>
/// A picture in the top-left corner, the time and what is on beside it, and underneath, a grid
/// of everything you could be watching, scrolling up on its own. Nobody scrolls it; you wait
/// for your row to come round, the way it was. Drawn straight into the frame, so it shows
/// wherever the picture does — in the window and on a furnishing alike — and costs nothing
/// while it is off.
/// </para>
/// </summary>
internal sealed class GuideChannel(BitmapFont font, Func<GuideSnapshot> data) : IFrameChannel
{
    public const int Width = 1280;
    public const int Height = 720;

    // Colours as libvlc-order RGBA bytes read little-endian: 0xAABBGGRR.
    private const uint Tube = 0xFF26150Au;       // 0A1526, the set's deep blue
    private const uint Glass = 0xFF1A0F0Au;      // 0A0F1A
    private const uint GlassLit = 0xFF38240Fu;   // 0F2438
    private const uint RowA = 0xFF2E1C10u;       // 101C2E
    private const uint RowB = 0xFF241608u;       // 081624
    private const uint Edge = 0xFF44281Cu;       // 1C2A44
    private const uint Accent = 0xFFFFC76Bu;     // 6BC7FF
    private const uint AccentDeep = 0xFF6B4A1Eu; // 1E4A6B, the current row
    private const uint White = 0xFFFBF1E6u;      // E6F1FB
    private const uint Dim = 0xFFC8B09Fu;        // 9FB0C8
    private const uint Faint = 0xFF806E5Fu;      // 5F6E80
    private const uint Amber = 0xFF279FEFu;      // EF9F27
    private const uint Good = 0xFFA5CA5Du;       // 5DCAA5
    private const uint Bad = 0xFF4A4BE2u;        // E24B4A
    private const uint Black = 0xFF000000u;

    // The picture box in the top band.
    private const int PicLeft = 16, PicTop = 12, PicWidth = 400, PicHeight = 225;

    // The grid: a header row, then rows that scroll under it, then the ticker.
    private const int GridTop = 240;
    private const int RowHeight = 40;
    private const int RowsTop = GridTop + RowHeight;
    private const int RowsBottom = 680;
    private const int TickerTop = RowsBottom;

    private const int ColNumber = 0, ColName = 96, ColSlots = 480;
    private const int SlotWidth = (Width - ColSlots) / 3;

    /// <summary>Pixels per second the rows climb, and the ticker crawls.</summary>
    private const double RowSpeed = 22.0;
    private const double TickerSpeed = 90.0;

    private readonly int[] picX = BuildMap(PicWidth, Width);
    private readonly int[] picY = BuildMap(PicHeight, Height);

    public bool Available => font.Available;

    public bool WantsPicture => true;

    public bool WantsMusic => true;

    /// <summary>
    /// Paints the guide into <paramref name="target"/>. <paramref name="picture"/> is the frame
    /// to show small in the corner — the live video, or the test card — or null for nothing on.
    /// <paramref name="seconds"/> drives the scrolling and only needs to climb steadily.
    /// </summary>
    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds) =>
        this.Render(target, picture, data(), now, seconds);

    /// <summary>The same, with the listing handed in — for the offline preview.</summary>
    public void Render(uint[] target, uint[]? picture, GuideSnapshot data, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, Width, Height);

        Fill(span, 0, 0, Width, GridTop, Tube);

        this.DrawPicture(span, picture);
        this.DrawHeadline(span, data, now, all);
        this.DrawGrid(span, data, now, seconds);
        this.DrawTicker(span, data, seconds);
    }

    private void DrawPicture(Span<uint> span, uint[]? picture)
    {
        Fill(span, PicLeft - 2, PicTop - 2, PicWidth + 4, PicHeight + 4, Edge);

        if (picture is null)
        {
            Fill(span, PicLeft, PicTop, PicWidth, PicHeight, Black);

            var clip = new BitmapFont.Clip(PicLeft, PicTop, PicLeft + PicWidth, PicTop + PicHeight);
            const string Mark = "AETHERSTREAM";
            const string Off = "NOTHING ON";
            font.Draw(span, Width, Mark, PicLeft + ((PicWidth - font.Measure(Mark, 2)) / 2), PicTop + 70, Accent, 2, clip);
            font.Draw(span, Width, Off, PicLeft + ((PicWidth - font.Measure(Off)) / 2), PicTop + 130, Faint, 1, clip);
            return;
        }

        // Nearest-neighbour: 90,000 pixels, a fraction of a millisecond, and the slight
        // harshness suits a picture-in-picture on a set like this.
        for (var y = 0; y < PicHeight; y++)
        {
            var src = picture.AsSpan(this.picY[y] * Width, Width);
            var dst = span.Slice(((PicTop + y) * Width) + PicLeft, PicWidth);
            for (var x = 0; x < PicWidth; x++)
                dst[x] = src[this.picX[x]] | 0xFF000000u;
        }
    }

    private void DrawHeadline(Span<uint> span, GuideSnapshot data, DateTime now, in BitmapFont.Clip clip)
    {
        const int Left = PicLeft + PicWidth + 28;
        var width = Width - Left - 16;

        font.Draw(span, Width, "AETHERSTREAM GUIDE", Left, 14, Accent, 2, clip);

        var clock = now.ToString("h:mm tt").ToUpperInvariant();
        var date = now.ToString("ddd d MMM").ToUpperInvariant();
        font.Draw(span, Width, clock, Left, 100, White, 2, clip);
        font.Draw(span, Width, date, Left + font.Measure(clock, 2) + 32, 118, Dim, 1, clip);

        font.Draw(span, Width, "NOW", Left, 184, Amber, 1, clip);
        var title = Ellipsis(data.NowPlaying.ToUpperInvariant(), font.Fit(width - 64));
        font.Draw(span, Width, title, Left + 64, 184, White, 1, clip);
    }

    private void DrawGrid(Span<uint> span, GuideSnapshot data, DateTime now, double seconds)
    {
        // -- header --------------------------------------------------------------------------
        Fill(span, 0, GridTop, Width, RowHeight, GlassLit);
        var header = new BitmapFont.Clip(0, GridTop, Width, RowsTop);

        font.Draw(span, Width, "CH", ColNumber + 16, GridTop, Amber, 1, header);
        font.Draw(span, Width, "CHANNEL", ColName + 8, GridTop, Amber, 1, header);

        // Half-hour slots from the current one, as a real grid does.
        var slot = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute >= 30 ? 30 : 0, 0);
        for (var i = 0; i < 3; i++)
        {
            var label = slot.AddMinutes(30 * i).ToString("h:mm tt").ToUpperInvariant();
            font.Draw(span, Width, label, ColSlots + (i * SlotWidth) + 8, GridTop, Amber, 1, header);
        }

        // -- rows ----------------------------------------------------------------------------
        Fill(span, 0, RowsTop, Width, RowsBottom - RowsTop, Glass);
        var rows = data.Rows;
        if (rows.Count == 0)
            return;

        var region = new BitmapFont.Clip(0, RowsTop, Width, RowsBottom);
        var visible = (RowsBottom - RowsTop) / RowHeight;
        var total = rows.Count * RowHeight;

        // Fewer rows than fit: nothing moves. More: they climb, and wrap seamlessly.
        var offset = rows.Count <= visible ? 0 : (int)((seconds * RowSpeed) % total);

        for (var pass = 0; pass < 2; pass++)
        {
            var baseY = RowsTop - offset + (pass * total);
            if (pass == 1 && rows.Count <= visible)
                break;

            for (var i = 0; i < rows.Count; i++)
            {
                var y = baseY + (i * RowHeight);
                if (y + RowHeight <= RowsTop || y >= RowsBottom)
                    continue;

                this.DrawRow(span, rows[i], y, i, region);
            }
        }

        // Column rules over everything, so they never scroll. Two wide, so a squashed surface keeps them.
        for (var y = RowsTop; y < RowsBottom; y++)
        {
            var line = span.Slice(y * Width, Width);
            foreach (var x in (ReadOnlySpan<int>)[ColName - 1, ColSlots - 1, ColSlots + SlotWidth - 1, ColSlots + (2 * SlotWidth) - 1])
            {
                line[x] = Edge;
                line[x - 1] = Edge;
            }
        }
    }

    private void DrawRow(Span<uint> span, GuideRow row, int y, int index, in BitmapFont.Clip region)
    {
        var top = Math.Max(y, region.Top);
        var bottom = Math.Min(y + RowHeight, region.Bottom);

        var bg = row.Current ? AccentDeep : (index % 2 == 0 ? RowA : RowB);
        Fill(span, 0, top, Width, bottom - top, bg);

        var textY = y;
        var clip = new BitmapFont.Clip(0, top, Width, bottom);

        var numberColour = row.Current ? White : row.Live ? Good : Accent;
        font.Draw(span, Width, row.Number, ColNumber + 16, textY, numberColour, 1, clip);

        var name = Ellipsis(row.Name.ToUpperInvariant(), font.Fit(ColSlots - ColName - 16));
        font.Draw(span, Width, name, ColName + 8, textY, row.Offline ? Bad : White, 1, clip);

        var detail = row.Offline ? "OFF AIR" : row.Detail.ToUpperInvariant();
        detail = Ellipsis(detail, font.Fit(Width - ColSlots - 16));
        var detailColour = row.Offline ? Bad : row.Current ? White : row.Live ? Good : Dim;
        font.Draw(span, Width, detail, ColSlots + 8, textY, detailColour, 1, clip);
    }

    private void DrawTicker(Span<uint> span, GuideSnapshot data, double seconds)
    {
        Fill(span, 0, TickerTop, Width, Height - TickerTop, GlassLit);
        Fill(span, 0, TickerTop, Width, 2, Edge);

        var text = Plain(data.Ticker).ToUpperInvariant();
        if (text.Length == 0)
            return;

        var clip = new BitmapFont.Clip(0, TickerTop, Width, Height);
        var width = font.Measure(text);

        // Crawls in from the right, and comes round again after it has gone.
        var cycle = width + Width;
        var x = Width - (int)((seconds * TickerSpeed) % cycle);
        font.Draw(span, Width, text, x, TickerTop, Amber, 1, clip);
    }

    // -- helpers ---------------------------------------------------------------------------------

    private static void Fill(Span<uint> target, int x, int y, int w, int h, uint colour)
    {
        var x0 = Math.Max(0, x);
        var x1 = Math.Min(Width, x + w);
        if (x1 <= x0)
            return;

        for (var row = Math.Max(0, y); row < Math.Min(Height, y + h); row++)
            target.Slice((row * Width) + x0, x1 - x0).Fill(colour);
    }

    /// <summary>
    /// Cuts text to fit, the way a listings grid did — no ellipsis, the cell simply ends — and
    /// folds it to what the atlas can draw: accents stripped, the odd typographic mark swapped
    /// for its plain cousin. Anything else outside ASCII draws as a blank cell.
    /// </summary>
    private static string Ellipsis(string text, int max)
    {
        if (max <= 0)
            return string.Empty;

        var plain = Plain(text);
        return plain.Length <= max ? plain : plain[..max];
    }

    private static string Plain(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(c switch
            {
                '·' or '•' => '/',
                '—' or '–' => '-',
                '‘' or '’' => '\'',
                '“' or '”' => '"',
                '…' => '.',
                _ => c,
            });
        }

        return sb.ToString();
    }

    private static int[] BuildMap(int size, int from)
    {
        var map = new int[size];
        for (var i = 0; i < size; i++)
            map[i] = Math.Min(from - 1, (int)((i + 0.5) * from / size));

        return map;
    }
}
