namespace Aetherstream.Plugin.Video;

/// <summary>
/// Channel 99. The premium channel you did not subscribe to, as cable delivered it: the picture
/// underneath torn sideways in rolling bands, the colour smeared, the odd frame of static, and
/// every half minute a slate telling you to call your cable operator. Whatever is on — a film, a
/// live channel, the test card — is what gets scrambled, so there is nothing under it that was
/// not already there; the sound leaks through as it always did.
/// </summary>
internal sealed class ScrambledChannel(BitmapFont font) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;

    // The cycle: scrambled, then the slate, then scrambled again.
    private const double Cycle = 34.0;
    private const double SlateFor = 3.5;

    private static readonly uint Slate = Canvas.Rgb(0x00, 0x00, 0xA8);

    public bool Available => font.Available;

    public bool WantsPicture => true;

    public bool WantsMusic => false;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var phase = seconds % Cycle;

        if (phase > Cycle - SlateFor)
        {
            this.DrawSlate(span, phase - (Cycle - SlateFor));
            return;
        }

        // A whole frame of static now and then: the decoder losing the signal entirely.
        var frame = (int)(seconds * 30);
        var rng = (uint)(frame * 2654435761u) | 1u;
        if (frame % 173 == 0 || frame % 173 == 1)
        {
            Static(span, ref rng);
            return;
        }

        // Every nine seconds or so the scramble eases for a moment, and you almost make it out.
        var ease = 1.0 - Math.Clamp(1.0 - (Math.Abs((seconds % 9.3) - 4.6) * 2.5), 0.0, 0.85);

        // The tear: a band rolling up the picture where the sync goes completely.
        var tearY = (H + 160) - ((seconds * 150.0) % (H + 160)) - 80;

        for (var y = 0; y < H; y++)
        {
            var drift = (Math.Sin((seconds * 0.7) + (y * 0.013)) * 26.0) + (Math.Sin((seconds * 3.1) + (y * 0.05)) * 7.0);
            var fromTear = Math.Abs(y - tearY);
            if (fromTear < 48)
                drift += (48 - fromTear) * 4.0 * Math.Sign(Math.Sin(seconds * 5.0));

            var shift = (int)(drift * ease);
            var chroma = (int)((7 + (5 * Math.Sin((seconds * 2.0) + (y * 0.02)))) * ease);

            // Colour bands drift down the picture; each swaps the channels a different way.
            var band = (int)Math.Floor((y + (seconds * 55.0)) / 44.0) % 3;
            if (band < 0)
                band += 3;

            var dim = (y & 1) == 0 ? 1.0f : 0.78f;
            var row = span.Slice(y * W, W);

            if (picture is null)
            {
                for (var x = 0; x < W; x++)
                {
                    rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                    var n = (byte)(rng & 0x3F);
                    row[x] = 0xFF000000u | ((uint)n << 16) | ((uint)n << 8) | n;
                }

                continue;
            }

            var src = picture.AsSpan(y * W, W);
            for (var x = 0; x < W; x++)
            {
                var xr = Wrap(x + shift + chroma);
                var xg = Wrap(x + shift);
                var xb = Wrap(x + shift - chroma);

                // Each channel from a different column: the smear.
                var r = src[xr] & 0xFF;
                var g = (src[xg] >> 8) & 0xFF;
                var b = (src[xb] >> 16) & 0xFF;

                var (r2, g2, b2) = band switch
                {
                    0 => (r, g, b),
                    1 => (g, b, r),
                    _ => (b, r, g),
                };

                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                var noise = (int)(rng & 0x1F) - 16;

                var rr = (uint)Math.Clamp((int)(r2 * dim) + noise, 0, 255);
                var gg = (uint)Math.Clamp((int)(g2 * dim) + noise, 0, 255);
                var bb = (uint)Math.Clamp((int)(b2 * dim) + noise, 0, 255);
                row[x] = 0xFF000000u | (bb << 16) | (gg << 8) | rr;
            }
        }

        // A thin bright line rides the tear, the way the sync pulse showed through.
        var line = (int)tearY;
        if (line >= 0 && line < H)
            Canvas.Fill(span, 0, line, W, 2, Canvas.Rgb(0xC0, 0xC0, 0xC0));
    }

    private static int Wrap(int x)
    {
        x %= W;
        return x < 0 ? x + W : x;
    }

    private static void Static(Span<uint> span, ref uint rng)
    {
        for (var i = 0; i < span.Length; i++)
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            var n = (byte)(rng & 0xFF);
            span[i] = 0xFF000000u | ((uint)n << 16) | ((uint)n << 8) | n;
        }
    }

    private void DrawSlate(Span<uint> span, double into)
    {
        Canvas.Fill(span, 0, 0, W, H, Slate);
        var all = new BitmapFont.Clip(0, 0, W, H);

        // The slate comes in with a beat of black, as the box switched over.
        if (into < 0.25)
        {
            Canvas.Fill(span, 0, 0, W, H, Canvas.Black);
            return;
        }

        const string Head = "CHANNEL 99";
        font.Draw(span, W, Head, (W - font.Measure(Head, 2)) / 2, 200, Canvas.White, 2, all);

        var lines = new[]
        {
            "THIS CHANNEL REQUIRES A SUBSCRIPTION.",
            "PLEASE CONTACT YOUR CABLE OPERATOR.",
            string.Empty,
            "AETHERSTREAM CABLE  -  CUSTOMER SERVICE  -  1-800-555-0199",
        };

        var y = 320;
        foreach (var text in lines)
        {
            if (text.Length > 0)
                font.Draw(span, W, text, (W - font.Measure(text)) / 2, y, Canvas.White, 1, all);
            y += 44;
        }
    }
}
