namespace Aetherstream.Plugin.Video;

/// <summary>
/// The Eorzea clock: the time, the date in the game's own calendar, the moon, and where the sun
/// is. A day is seventy real minutes, a moon thirty-two days, a year twelve moons, and none of
/// it needs anything but the clock.
/// </summary>
internal sealed class ClockChannel(BitmapFont font) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;

    private static readonly string[] Phases =
    [
        "NEW MOON", "WAXING CRESCENT", "FIRST QUARTER", "WAXING GIBBOUS",
        "FULL MOON", "WANING GIBBOUS", "LAST QUARTER", "WANING CRESCENT",
    ];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);

        // Eorzean seconds since the epoch: 1440/70 of a real second each.
        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var etSeconds = unixMs * 144 / 7 / 1000;
        var etSecOfDay = etSeconds % 86400;
        var hour = (int)(etSecOfDay / 3600);
        var minute = (int)(etSecOfDay / 60 % 60);
        var second = (int)(etSecOfDay % 60);

        var dayOfMoon = (int)(etSeconds / 86400 % 32) + 1;
        var moon = (int)(etSeconds / (86400L * 32) % 12) + 1;
        var year = (int)(etSeconds / (86400L * 384)) + 1;

        // Night is a darker set; the tube warms through the day.
        var daylight = Daylight(hour + (minute / 60f));
        Canvas.Fill(span, 0, 0, W, H, Canvas.Lerp(Canvas.Glass, Canvas.Tube, daylight));

        // -- the big clock ---------------------------------------------------------------------------
        var time = $"{hour:00}:{minute:00}";
        var timeWidth = font.Measure(time, 4);
        var x = (W - timeWidth - 24 - font.Measure("00", 2)) / 2;
        font.Draw(span, W, time, x, 120, Canvas.White, 4, all);
        font.Draw(span, W, $"{second:00}", x + timeWidth + 24, 120 + 160 - 80, Canvas.Dim, 2, all);

        const string Label = "EORZEA TIME";
        font.Draw(span, W, Label, (W - font.Measure(Label)) / 2, 84, Canvas.Amber, 1, all);

        var local = now.ToString("h:mm:ss tt").ToUpperInvariant() + "  LOCAL";
        font.Draw(span, W, local, (W - font.Measure(local, 2)) / 2, 300, Canvas.Accent, 2, all);

        // -- the date ---------------------------------------------------------------------------------
        var date = $"THE {Ordinal(dayOfMoon)} SUN OF THE {Ordinal((moon + 1) / 2)} {(moon % 2 == 1 ? "ASTRAL" : "UMBRAL")} MOON";
        font.Draw(span, W, date, (W - font.Measure(date)) / 2, 400, Canvas.White, 1, all);

        var yearLine = $"YEAR {year} OF THE SEVENTH ASTRAL ERA";
        font.Draw(span, W, yearLine, (W - font.Measure(yearLine)) / 2, 436, Canvas.Faint, 1, all);

        // -- the sun's arc -------------------------------------------------------------------------
        const int BarLeft = 240, BarRight = W - 240, BarY = 520;
        Canvas.Fill(span, BarLeft, BarY, BarRight - BarLeft, 2, Canvas.Edge);

        // Ticks every six bells, labelled.
        for (var h = 0; h <= 24; h += 6)
        {
            var tx = BarLeft + ((BarRight - BarLeft) * h / 24);
            Canvas.Fill(span, tx, BarY - 6, 2, 14, Canvas.Edge);
            var t = $"{h % 24:00}";
            font.Draw(span, W, t, tx - (font.Measure(t) / 2), BarY + 14, Canvas.Faint, 1, all);
        }

        // Daylight runs six to eighteen: that stretch is lit.
        var dayStart = BarLeft + ((BarRight - BarLeft) * 6 / 24);
        var dayEnd = BarLeft + ((BarRight - BarLeft) * 18 / 24);
        Canvas.Fill(span, dayStart, BarY - 1, dayEnd - dayStart, 4, Canvas.Amber);

        var sunX = BarLeft + (int)((BarRight - BarLeft) * (hour + (minute / 60f)) / 24f);
        Canvas.Disc(span, sunX, BarY, 9, daylight > 0.5f ? Canvas.Rgb(0xFF, 0xD6, 0x4F) : Canvas.Dim);

        var sunLine = daylight > 0.5f ? "SUNSET AT 18:00" : "SUNRISE AT 06:00";
        font.Draw(span, W, sunLine, (W - font.Measure(sunLine)) / 2, 560, Canvas.Dim, 1, all);

        // -- the moon ---------------------------------------------------------------------------------
        var phase = (dayOfMoon - 1) / 4;
        var fraction = ((dayOfMoon - 1) + (etSecOfDay / 86400f)) / 32f;
        DrawMoon(span, 120, 200, 44, fraction);
        font.Draw(span, W, Phases[phase], 120 - (font.Measure(Phases[phase]) / 2), 262, Canvas.Dim, 1, all);

        // Mirror it on the right for balance: the same moon, the way it looks from the other side.
        DrawMoon(span, W - 120, 200, 44, fraction);
        var dayText = $"DAY {dayOfMoon} OF 32";
        font.Draw(span, W, dayText, W - 120 - (font.Measure(dayText) / 2), 262, Canvas.Dim, 1, all);

        // -- footer -------------------------------------------------------------------------------------
        Canvas.Fill(span, 0, 680, W, 40, Canvas.GlassLit);
        Canvas.Fill(span, 0, 680, W, 2, Canvas.Edge);
        font.Draw(span, W, "AETHERSTREAM CLOCK", 24, 680, Canvas.Accent, 1, all);
        const string Note = "ONE BELL IS 175 SECONDS";
        font.Draw(span, W, Note, W - 24 - font.Measure(Note), 680, Canvas.Faint, 1, all);
    }

    /// <summary>How lit the sky is: full from 6 to 18, with an hour of dusk and dawn either side.</summary>
    private static float Daylight(float hour) => hour switch
    {
        < 5f => 0f,
        < 6f => hour - 5f,
        < 18f => 1f,
        < 19f => 19f - hour,
        _ => 0f,
    };

    /// <summary>
    /// A moon at a phase from 0 (new) through 0.5 (full) back to 1. The terminator is an ellipse
    /// whose width follows the cosine of the phase, which is close enough for a clock.
    /// </summary>
    private static void DrawMoon(Span<uint> span, int cx, int cy, int r, float fraction)
    {
        var lit = Canvas.Rgb(0xE6, 0xF1, 0xFB);
        var dark = Canvas.Rgb(0x1A, 0x22, 0x33);
        var t = MathF.Cos(2f * MathF.PI * fraction);

        for (var y = -r; y <= r; y++)
        {
            var half = MathF.Sqrt((r * r) - (y * y));
            for (var x = (int)-half; x <= (int)half; x++)
            {
                var isLit = fraction < 0.5f ? x >= t * half : x <= -t * half;
                Canvas.Plot(span, cx + x, cy + y, isLit ? lit : dark);
            }
        }

        // A couple of maria on the lit face, so it reads as a moon and not a coin.
        Canvas.Disc(span, cx - (r / 3), cy - (r / 4), r / 6, Canvas.Lerp(lit, dark, 0.35f));
        Canvas.Disc(span, cx + (r / 4), cy + (r / 3), r / 8, Canvas.Lerp(lit, dark, 0.35f));
    }

    private static string Ordinal(int n) => n switch
    {
        11 or 12 or 13 => $"{n}TH",
        _ when n % 10 == 1 => $"{n}ST",
        _ when n % 10 == 2 => $"{n}ND",
        _ when n % 10 == 3 => $"{n}RD",
        _ => $"{n}TH",
    };
}
