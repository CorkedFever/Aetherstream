namespace Aetherstream.Plugin.Video;

/// <summary>What the music channel needs from the set: what is on, what is next, and the sound itself.</summary>
internal sealed record MusicState(bool Playing, string NowPlaying, string UpNext, int Index, int Count);

/// <summary>
/// Aether FM: the jukebox as a channel. The track in big letters with a generated sleeve beside
/// it, what is up next, a spectrum that moves with the sound, a waveform under it, and a pair of
/// VU meters. Something to leave on. The sleeve is drawn from the title, so every track has one.
/// </summary>
internal sealed class MusicChannel(BitmapFont font, Func<MusicState> state, Action<float[]> tap) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const int Bins = 48;

    private static readonly uint Night = Canvas.Rgb(0x0C, 0x0A, 0x16);
    private static readonly uint Panel = Canvas.Rgb(0x16, 0x12, 0x26);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Neon = Canvas.Rgb(0xFF, 0x5A, 0xA0);
    private static readonly uint Cyan = Canvas.Rgb(0x5A, 0xE0, 0xFF);
    private static readonly uint Lime = Canvas.Rgb(0xB0, 0xFF, 0x6A);
    private static readonly uint Dim = Canvas.Rgb(0x6A, 0x62, 0x88);

    private readonly float[] samples = new float[2048];
    private readonly float[] levels = new float[Bins];
    private readonly float[] peaks = new float[Bins];
    private readonly float[] window = BuildWindow();
    private readonly (float[] Cos, float[] Sin)[] twiddle = BuildTwiddle();
    private float vuLeft;
    private float vuRight;

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var s = state();

        Canvas.Fill(span, 0, 0, W, H, Night);

        // The header.
        Canvas.Fill(span, 0, 0, W, 56, Panel);
        Canvas.Fill(span, 0, 56, W, 2, Neon);
        font.Draw(span, W, "AETHER FM", 24, 8, Neon, 1, all);
        var local = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, local, W - 24 - font.Measure(local), 8, Cream, 1, all);
        var onAir = s.Playing ? "ON AIR" : "OFF AIR";
        font.Draw(span, W, onAir, (W - font.Measure(onAir)) / 2, 8, s.Playing ? Lime : Dim, 1, all);

        // The sound: pull the tap, measure, and analyse.
        tap(this.samples);
        this.Analyse();

        // The sleeve, generated from the title.
        var title = s.NowPlaying.Length > 0 ? s.NowPlaying : "NOTHING ON";
        this.DrawSleeve(span, 40, 90, 300, title, seconds, s.Playing);

        // The title, the next, the position.
        const int TextX = 500;
        font.Draw(span, W, "NOW PLAYING", TextX, 96, Dim, 1, all);
        var y = 136;
        foreach (var line in Canvas.Wrap(Canvas.Plain(title).ToUpperInvariant(), font.Fit(W - TextX - 40, 2), 2))
        {
            font.Draw(span, W, line, TextX, y, Cream, 2, all);
            y += 88;
        }

        if (!s.Playing)
        {
            font.Draw(span, W, "PUT MUSIC ON THE SET IN THE SOUND TAB:", TextX, y + 12, Dim, 1, all);
            font.Draw(span, W, "THE BUNDLED TRACKS, A FOLDER, OR A PLEX PLAYLIST.", TextX, y + 52, Dim, 1, all);
        }
        else
        {
            if (s.UpNext.Length > 0)
            {
                font.Draw(span, W, "UP NEXT", TextX, y + 12, Dim, 1, all);
                font.Draw(span, W, Canvas.Cut(Canvas.Plain(s.UpNext).ToUpperInvariant(), font.Fit(W - TextX - 40)), TextX, y + 52, Cyan, 1, all);
            }

            if (s.Count > 0)
            {
                var pos = $"TRACK {s.Index} OF {s.Count}";
                font.Draw(span, W, pos, W - 40 - font.Measure(pos), 96, Dim, 1, all);
            }
        }

        // The VU meters, right of the sleeve's row.
        this.DrawVu(span, W - 40 - 180, 300, all);

        // The spectrum across the middle, the waveform under it.
        this.DrawSpectrum(span, 40, 420, W - 80, 160);
        this.DrawWaveform(span, 40, 600, W - 80, 60);

        Canvas.Fill(span, 0, 680, W, 40, Panel);
        font.Draw(span, W, "AETHERSTREAM RADIO", 24, 680, Neon, 1, all);
        const string Note = "THE SET'S OWN JUKEBOX / CHANGE THE MUSIC IN SOUND";
        font.Draw(span, W, Note, W - 24 - font.Measure(Note), 680, Dim, 1, all);
    }

    // -- the sound --------------------------------------------------------------------------------------------

    private void Analyse()
    {
        // Levels: a plain DFT at a handful of frequencies, log-spaced from 40 Hz to 12 kHz over a
        // windowed 1024-sample tail. Cheap enough for a frame, and it moves the way a spectrum should.
        const int N = 1024;
        var tail = this.samples.AsSpan(this.samples.Length - N, N);
        var left = 0f;
        var right = 0f;
        for (var i = 0; i < N; i++)
        {
            var v = Math.Abs(tail[i]);
            if (i % 2 == 0)
                left = Math.Max(left, v);
            else
                right = Math.Max(right, v);
        }

        this.vuLeft = Math.Max(left, this.vuLeft * 0.85f);
        this.vuRight = Math.Max(right, this.vuRight * 0.85f);

        for (var b = 0; b < Bins; b++)
        {
            var (cos, sin) = this.twiddle[b];
            var re = 0f;
            var im = 0f;
            for (var i = 0; i < N; i++)
            {
                var v = tail[i] * this.window[i];
                re += v * cos[i];
                im += v * sin[i];
            }

            var mag = MathF.Sqrt((re * re) + (im * im)) / (N / 8f);
            var level = MathF.Min(1f, MathF.Pow(mag, 0.6f) * (1f + (b / (float)Bins)));
            this.levels[b] = Math.Max(level, this.levels[b] * 0.82f);
            this.peaks[b] = Math.Max(this.levels[b], this.peaks[b] - 0.012f);
        }
    }

    private static float[] BuildWindow()
    {
        var w = new float[1024];
        for (var i = 0; i < w.Length; i++)
            w[i] = 0.5f - (0.5f * MathF.Cos(2f * MathF.PI * i / (w.Length - 1)));
        return w;
    }

    private static (float[] Cos, float[] Sin)[] BuildTwiddle()
    {
        const int N = 1024;
        const float Rate = 48000f;
        var t = new (float[], float[])[Bins];
        for (var b = 0; b < Bins; b++)
        {
            var hz = 40f * MathF.Pow(300f, b / (float)(Bins - 1));
            var cos = new float[N];
            var sin = new float[N];
            for (var i = 0; i < N; i++)
            {
                var a = 2f * MathF.PI * hz * i / Rate;
                cos[i] = MathF.Cos(a);
                sin[i] = MathF.Sin(a);
            }

            t[b] = (cos, sin);
        }

        return t;
    }

    // -- the pictures ---------------------------------------------------------------------------------------

    private void DrawSleeve(Span<uint> span, int x, int y, int size, string title, double seconds, bool playing)
    {
        // A sleeve from the title: its hash picks two colours and a pattern; the record turns behind it.
        var seed = 0;
        foreach (var ch in title)
            seed = (seed * 31) + ch;
        var a = Hue(seed);
        var b = Hue(seed * 7 + 3);
        var pattern = Math.Abs(seed) % 4;

        // The record peeking out the right, turning when there is music.
        var spin = playing ? seconds * 2.0 : 0.0;
        var rx = x + size + 40;
        var ry = y + (size / 2);
        Canvas.Disc(span, rx, ry, size / 2 - 10, Canvas.Rgb(0x14, 0x12, 0x1A));
        for (var r = size / 2 - 20; r > size / 8; r -= 10)
            Canvas.Disc(span, rx, ry, r, (r / 10) % 2 == 0 ? Canvas.Rgb(0x1E, 0x1A, 0x26) : Canvas.Rgb(0x14, 0x12, 0x1A));
        Canvas.Disc(span, rx, ry, size / 8, b);
        Canvas.Disc(span, rx, ry, 5, Night);
        Canvas.Line(span, rx, ry, rx + (int)(Math.Cos(spin) * (size / 8)), ry + (int)(Math.Sin(spin) * (size / 8)), a);
        this.SleeveFace(span, x, y, size, a, b, pattern);
    }

    private void SleeveFace(Span<uint> span, int x, int y, int size, uint a, uint b, int pattern)
    {
        Canvas.Fill(span, x, y, size, size, a);
        switch (pattern)
        {
            case 0:
                for (var i = 0; i < 8; i++)
                    Canvas.Fill(span, x, y + (i * size / 8), size, size / 16, b);
                break;
            case 1:
                for (var r = size / 2; r > 0; r -= size / 10)
                    Canvas.Disc(span, x + (size / 2), y + (size / 2), r, (r / (size / 10)) % 2 == 0 ? a : b);
                break;
            case 2:
                for (var i = 0; i < 6; i++)
                    Canvas.Disc(span, x + (size / 6) + ((i % 3) * (size / 3)), y + (size / 4) + ((i / 3) * (size / 2)), size / 9, b);
                break;
            default:
                for (var i = 0; i < size; i += 4)
                    Canvas.Fill(span, x + i, y + (int)((Math.Sin(i * 0.05) + 1) * size / 4), 4, size / 3, b);
                break;
        }

        Canvas.Rect(span, x, y, size, size, Canvas.Lerp(a, Canvas.Black, 0.5f), 4);
    }

    private static uint Hue(int seed)
    {
        var h = ((seed % 360) + 360) % 360 / 60f;
        var i = (int)h;
        var f = h - i;
        var (r, g, b) = i switch
        {
            0 => (1f, f, 0f),
            1 => (1f - f, 1f, 0f),
            2 => (0f, 1f, f),
            3 => (0f, 1f - f, 1f),
            4 => (f, 0f, 1f),
            _ => (1f, 0f, 1f - f),
        };
        return Canvas.Rgb((int)(60 + (r * 160)), (int)(60 + (g * 160)), (int)(60 + (b * 160)));
    }

    private void DrawVu(Span<uint> span, int x, int y, in BitmapFont.Clip all)
    {
        font.Draw(span, W, "L", x - 24, y - 4, Dim, 1, all);
        font.Draw(span, W, "R", x - 24, y + 40, Dim, 1, all);
        for (var ch = 0; ch < 2; ch++)
        {
            var level = ch == 0 ? this.vuLeft : this.vuRight;
            var yy = y + (ch * 44);
            for (var seg = 0; seg < 18; seg++)
            {
                var on = level * 18 > seg;
                var colour = seg < 12 ? Lime : seg < 16 ? Canvas.Rgb(0xFF, 0xD1, 0x5C) : Neon;
                Canvas.Fill(span, x + (seg * 10), yy, 8, 28, on ? colour : Canvas.Lerp(colour, Night, 0.8f));
            }
        }
    }

    private void DrawSpectrum(Span<uint> span, int x, int y, int width, int height)
    {
        Canvas.Fill(span, x, y, width, height, Panel);
        var barW = width / Bins;
        for (var b = 0; b < Bins; b++)
        {
            var h = (int)(this.levels[b] * (height - 8));
            var colour = Canvas.Lerp(Cyan, Neon, b / (float)Bins);
            Canvas.Fill(span, x + (b * barW) + 2, y + height - 4 - h, barW - 4, h, colour);
            var ph = (int)(this.peaks[b] * (height - 8));
            Canvas.Fill(span, x + (b * barW) + 2, y + height - 6 - ph, barW - 4, 3, Cream);
        }
    }

    private void DrawWaveform(Span<uint> span, int x, int y, int width, int height)
    {
        Canvas.Fill(span, x, y + (height / 2), width, 1, Dim);
        var n = this.samples.Length;
        for (var i = 0; i < width; i++)
        {
            var v = this.samples[i * n / width];
            var h = Math.Max(1, (int)(Math.Abs(v) * height));
            Canvas.Fill(span, x + i, y + (height / 2) - (h / 2), 1, h, Lime);
        }
    }
}
