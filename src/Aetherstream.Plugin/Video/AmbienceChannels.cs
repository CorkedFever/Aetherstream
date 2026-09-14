namespace Aetherstream.Plugin.Video;

/// <summary>
/// The atmosphere channels: nothing to read, just something on. Each keeps its own small state
/// and draws every frame; the expensive ones work at a fraction of the size and are scaled up,
/// which is faster and looks like the hardware that inspired them.
/// </summary>
internal abstract class AmbienceChannel(BitmapFont font) : IFrameChannel
{
    protected const int W = Canvas.Width;
    protected const int H = Canvas.Height;

    protected readonly BitmapFont Font = font;
    protected readonly Random Rng = new();

    private double lastSeconds = -1;

    public bool Available => true;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public abstract string Name { get; }

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var dt = this.lastSeconds < 0 ? 0.033 : Math.Clamp(seconds - this.lastSeconds, 0.0, 0.1);
        this.lastSeconds = seconds;
        this.Frame(target.AsSpan(), (float)dt, seconds);
    }

    protected abstract void Frame(Span<uint> span, float dt, double seconds);
}

/// <summary>A tank of pixel-art fish, the odd Namazu, sand, weeds and bubbles.</summary>
internal sealed class AquariumChannel(BitmapFont font) : AmbienceChannel(font)
{
    public override string Name => "Aquarium";

    private const int Scale = 4;
    private const int SW = W / Scale, SH = H / Scale;
    private readonly uint[] small = new uint[SW * SH];
    private readonly uint[] water = BuildWater();

    private sealed class Fish
    {
        public float X, Y, Speed, Wobble;
        public int Kind, Size;
        public bool Left;
        public uint Colour;
    }

    private sealed class Bubble
    {
        public float X, Y, Speed;
    }

    private readonly List<Fish> fish = [];
    private readonly List<Bubble> bubbles = [];
    private float spawnTimer;

    private static readonly string[] Small =
    [
        "....bbb.....",
        "...bbbbbb..b",
        "bbbbbeb.bbbb",
        "...bbbbbb..b",
        "....bbb.....",
    ];

    private static readonly string[] Long =
    [
        ".....bbbbbb.......",
        "...bbbbbbbbbb....b",
        "bbbbbbbbebbbbbbbbb",
        "...bbbbbbbbbb....b",
        ".....bbbbbb.......",
    ];

    private static readonly string[] Namazu =
    [
        "......nnnnnnnn......",
        "...w.nnnnnnnnnnnn...",
        "....nnnnennnnennnn.n",
        "...wnnnnnnnnnnnnnnnn",
        "....nnnnnnnnnnnnnn.n",
        "......nnnnnnnnnnn...",
        "........nn..nn......",
    ];

    private static uint[] BuildWater()
    {
        var w = new uint[SW * SH];
        var top = Canvas.Rgb(0x14, 0x5A, 0x8C);
        var bottom = Canvas.Rgb(0x06, 0x1E, 0x3C);
        for (var y = 0; y < SH; y++)
        {
            var colour = Canvas.Lerp(top, bottom, y / (float)SH);
            w.AsSpan(y * SW, SW).Fill(colour);
        }

        return w;
    }

    protected override void Frame(Span<uint> span, float dt, double seconds)
    {
        var s = this.small.AsSpan();
        this.water.AsSpan().CopyTo(s);

        // Light rays: a few slow diagonal bands, brighter near the surface.
        for (var i = 0; i < 4; i++)
        {
            var phase = (float)((seconds * 0.08) + (i * 1.7));
            var x0 = (int)((SW / 4f * i) + (MathF.Sin(phase) * 30f)) + 40;
            for (var y = 0; y < SH * 2 / 3; y++)
            {
                var x = x0 + (y / 3);
                var fade = 1f - (y / (SH * 0.66f));
                for (var k = 0; k < 6; k++)
                {
                    var px = x + k;
                    if ((uint)px < SW)
                        s[(y * SW) + px] = Canvas.Lerp(s[(y * SW) + px], Canvas.Rgb(0x6B, 0xC7, 0xFF), 0.12f * fade);
                }
            }
        }

        // Sand, and weeds swaying in it.
        var sand = Canvas.Rgb(0xC9, 0xA8, 0x6A);
        var sandDark = Canvas.Rgb(0x9A, 0x7B, 0x48);
        for (var x = 0; x < SW; x++)
        {
            var top = SH - 14 + (int)(MathF.Sin(x * 0.15f) * 2f);
            for (var y = top; y < SH; y++)
                s[(y * SW) + x] = (x + y) % 7 == 0 ? sandDark : sand;
        }

        var weed = Canvas.Rgb(0x2E, 0x8B, 0x57);
        var weedLight = Canvas.Rgb(0x5D, 0xCA, 0xA5);
        for (var i = 0; i < 9; i++)
        {
            var baseX = 20 + (i * 37) + ((i * 13) % 11);
            var height = 30 + ((i * 7) % 25);
            for (var y = 0; y < height; y++)
            {
                var sway = MathF.Sin((float)(seconds * 1.2) + (i * 0.9f) + (y * 0.12f)) * (y / 6f);
                var x = baseX + (int)sway;
                var py = SH - 14 - y;
                if ((uint)x < SW && (uint)py < SH)
                {
                    s[(py * SW) + x] = y % 5 == 0 ? weedLight : weed;
                    if ((uint)(x + 1) < SW)
                        s[(py * SW) + x + 1] = weed;
                }
            }
        }

        // Fish: keep a shoal of about nine, spawning off one edge and swimming to the other.
        this.spawnTimer -= dt;
        if (this.fish.Count < 8 && this.spawnTimer <= 0f)
        {
            this.spawnTimer = 1.5f + (float)this.Rng.NextDouble() * 2f;
            var left = this.Rng.Next(2) == 0;
            var roll = this.Rng.NextDouble();
            var kind = roll < 0.06 ? 2 : roll < 0.5 ? 1 : 0;
            var size = kind == 2 ? 3 : 2 + this.Rng.Next(2);
            uint[] palette = [Canvas.Rgb(0xEF, 0x9F, 0x27), Canvas.Rgb(0xE2, 0x4B, 0x4A), Canvas.Rgb(0x6B, 0xC7, 0xFF), Canvas.Rgb(0xB0, 0x7A, 0xE0), Canvas.Rgb(0xFF, 0xD6, 0x4F), Canvas.Rgb(0x5D, 0xCA, 0xA5)];

            this.fish.Add(new Fish
            {
                X = left ? SW + 20 : -40,
                Y = 12 + (float)this.Rng.NextDouble() * (SH - 50),
                Speed = (kind == 2 ? 10f : 16f + (float)this.Rng.NextDouble() * 16f) * 2f / size,
                Wobble = (float)this.Rng.NextDouble() * 6.28f,
                Kind = kind,
                Size = size,
                Left = left,
                Colour = kind == 2 ? Canvas.Rgb(0xE8, 0xD8, 0x9A) : palette[this.Rng.Next(palette.Length)],
            });
        }

        for (var i = this.fish.Count - 1; i >= 0; i--)
        {
            var f = this.fish[i];
            f.X += (f.Left ? -f.Speed : f.Speed) * dt;
            f.Wobble += dt * 2f;
            var y = (int)(f.Y + (MathF.Sin(f.Wobble) * 3f));

            var art = f.Kind == 2 ? Namazu : f.Kind == 1 ? Long : Small;
            var body = f.Colour;
            var eye = Canvas.White;
            var whisker = Canvas.Rgb(0x80, 0x6E, 0x5F);
            Canvas_Sprite(s, art, c => c switch { 'b' or 'n' => body, 'e' => eye, 'w' => whisker, _ => 0u }, (int)f.X, y, f.Size, flip: !f.Left);

            if (f.X < -60 || f.X > SW + 60)
                this.fish.RemoveAt(i);
        }

        // Bubbles from the bottom.
        // A few streams of bubbles from fixed spots on the sand, as from a pump, not a snowfall.
        if (this.Rng.NextDouble() < dt * 1.2)
        {
            var spot = new[] { 60, SW / 2 + 20, SW - 70 }[this.Rng.Next(3)];
            this.bubbles.Add(new Bubble { X = spot + this.Rng.Next(-3, 4), Y = SH - 16, Speed = 18f + (float)this.Rng.NextDouble() * 14f });
        }

        var bubble = Canvas.Rgb(0xC8, 0xE8, 0xFF);
        for (var i = this.bubbles.Count - 1; i >= 0; i--)
        {
            var b = this.bubbles[i];
            b.Y -= b.Speed * dt;
            var bx = (int)(b.X + (MathF.Sin(b.Y * 0.2f) * 2f));
            var by = (int)b.Y;
            if (by < 2)
            {
                this.bubbles.RemoveAt(i);
                continue;
            }

            if ((uint)bx < SW && (uint)by < SH)
            {
                s[(by * SW) + bx] = bubble;
                if ((uint)(bx + 1) < SW) s[(by * SW) + bx + 1] = bubble;
                if ((uint)(by + 1) < SH) s[((by + 1) * SW) + bx] = bubble;
            }
        }

        Canvas.Upscale(this.small, SW, SH, Scale, span);

        // A glass edge and a tiny label, so it reads as a tank on a set.
        Canvas.Rect(span, 0, 0, W, H, Canvas.Edge);
        this.Font.Draw(span, W, "AQUARIUM", 24, 8, Canvas.Faint, 1, new BitmapFont.Clip(0, 0, W, H));
    }

    /// <summary>Sprite into the small buffer, whose width is not the frame's.</summary>
    private static void Canvas_Sprite(Span<uint> s, string[] art, Func<char, uint> palette, int x, int y, int scale, bool flip)
    {
        var w = art[0].Length;
        for (var ay = 0; ay < art.Length; ay++)
        {
            var row = art[ay];
            for (var ax = 0; ax < w; ax++)
            {
                var colour = palette(row[flip ? w - 1 - ax : ax]);
                if (colour == 0)
                    continue;

                for (var dy = 0; dy < scale; dy++)
                {
                    var py = y + (ay * scale) + dy;
                    if ((uint)py >= SH)
                        continue;

                    for (var dx = 0; dx < scale; dx++)
                    {
                        var px = x + (ax * scale) + dx;
                        if ((uint)px < SW)
                            s[(py * SW) + px] = colour;
                    }
                }
            }
        }
    }
}

/// <summary>The fire from Doom, at an eighth of the size, with a hearth drawn round it.</summary>
internal sealed class FireplaceChannel(BitmapFont font) : AmbienceChannel(font)
{
    public override string Name => "Fireplace";

    private const int Scale = 4;
    private const int SW = W / Scale, SH = H / Scale;
    private readonly byte[] heat = new byte[SW * SH];
    private readonly uint[] small = new uint[SW * SH];
    private readonly uint[] palette = BuildPalette();
    private float accumulator;

    private static uint[] BuildPalette()
    {
        // Black through deep red, orange, yellow to near white, 64 steps.
        var p = new uint[64];
        for (var i = 0; i < 64; i++)
        {
            var t = i / 63f;
            uint c;
            if (t < 0.25f) c = Canvas.Lerp(Canvas.Rgb(6, 4, 4), Canvas.Rgb(120, 10, 0), t / 0.25f);
            else if (t < 0.55f) c = Canvas.Lerp(Canvas.Rgb(120, 10, 0), Canvas.Rgb(230, 90, 0), (t - 0.25f) / 0.3f);
            else if (t < 0.85f) c = Canvas.Lerp(Canvas.Rgb(230, 90, 0), Canvas.Rgb(255, 200, 40), (t - 0.55f) / 0.3f);
            else c = Canvas.Lerp(Canvas.Rgb(255, 200, 40), Canvas.Rgb(255, 245, 200), (t - 0.85f) / 0.15f);
            p[i] = c;
        }

        return p;
    }

    protected override void Frame(Span<uint> span, float dt, double seconds)
    {
        // The fire burns in the middle two-thirds, from a bed of embers a few rows up.
        const int Left = SW / 6, Right = SW * 5 / 6, Bed = SH - 30;

        // Step the simulation at a fixed rate, whatever the frame rate.
        this.accumulator += dt;
        while (this.accumulator >= 1f / 30f)
        {
            this.accumulator -= 1f / 30f;

            for (var x = Left; x < Right; x++)
            {
                // Embers flicker; the edges of the bed burn lower than the middle.
                var edge = MathF.Min(x - Left, Right - x) / 24f;
                var strength = Math.Clamp(edge, 0.3f, 1f);
                this.heat[(Bed * SW) + x] = (byte)(this.Rng.Next(64) < 60 * strength ? 63 : 40);
            }

            for (var y = Bed - 1; y >= 0; y--)
            {
                for (var x = Left; x < Right; x++)
                {
                    var below = this.heat[((y + 1) * SW) + x];
                    var decay = this.Rng.Next(3);
                    var drift = this.Rng.Next(3) - 1;
                    var dx = Math.Clamp(x + drift, Left, Right - 1);
                    this.heat[(y * SW) + dx] = (byte)Math.Max(0, below - decay);
                }
            }
        }

        var s = this.small.AsSpan();
        s.Fill(Canvas.Rgb(6, 4, 4));

        for (var y = 0; y <= Bed; y++)
        {
            for (var x = Left; x < Right; x++)
            {
                var h = this.heat[(y * SW) + x];
                if (h > 0)
                    s[(y * SW) + x] = this.palette[h];
            }
        }

        // Logs.
        var log = Canvas.Rgb(0x4A, 0x2E, 0x1A);
        var logLight = Canvas.Rgb(0x6B, 0x45, 0x28);
        for (var i = 0; i < 3; i++)
        {
            var lx = Left + 20 + (i * 60);
            var ly = Bed - 2 + (i % 2 * 4);
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 70; x++)
                {
                    var px = lx + x + (y / 2);
                    if ((uint)px < SW && (uint)(ly + y) < SH)
                        s[((ly + y) * SW) + px] = y is 1 or 2 ? logLight : log;
                }
            }
        }

        Canvas.Upscale(this.small, SW, SH, Scale, span);

        // The hearth: stone either side and a mantel, warmed by the glow.
        var glow = 0.5f + (0.5f * MathF.Sin((float)(seconds * 3.1)));
        var stone = Canvas.Lerp(Canvas.Rgb(0x2A, 0x24, 0x22), Canvas.Rgb(0x4A, 0x36, 0x2A), glow * 0.4f);
        var mortar = Canvas.Rgb(0x18, 0x14, 0x12);
        Canvas.Fill(span, 0, 0, Left * Scale, H, stone);
        Canvas.Fill(span, Right * Scale, 0, W - (Right * Scale), H, stone);
        Canvas.Fill(span, 0, 0, W, 60, stone);
        for (var y = 0; y < H; y += 36)
        {
            Canvas.Fill(span, 0, y, Left * Scale, 2, mortar);
            Canvas.Fill(span, Right * Scale, y, W - (Right * Scale), 2, mortar);
            var stagger = (y / 36) % 2 == 0 ? 0 : 60;
            for (var x = stagger; x < Left * Scale; x += 120)
                Canvas.Fill(span, x, y, 2, 36, mortar);
            for (var x = (Right * Scale) + stagger; x < W; x += 120)
                Canvas.Fill(span, x, y, 2, 36, mortar);
        }

        Canvas.Fill(span, 0, 60, W, 6, Canvas.Rgb(0x5A, 0x3A, 0x22));
        this.Font.Draw(span, W, "FIREPLACE", 24, 12, Canvas.Faint, 1, new BitmapFont.Clip(0, 0, W, H));
    }
}

/// <summary>Stars streaming past, the screensaver everyone had.</summary>
internal sealed class StarfieldChannel(BitmapFont font) : AmbienceChannel(font)
{
    public override string Name => "Starfield";

    private const int Count = 500;
    private readonly float[] x = new float[Count];
    private readonly float[] y = new float[Count];
    private readonly float[] z = new float[Count];
    private bool seeded;

    protected override void Frame(Span<uint> span, float dt, double seconds)
    {
        if (!this.seeded)
        {
            for (var i = 0; i < Count; i++)
                this.Reset(i, (float)this.Rng.NextDouble());
            this.seeded = true;
        }

        span.Fill(Canvas.Black);

        // A slow drift of the vanishing point, so it is not nailed to the centre.
        var cx = (W / 2f) + (MathF.Sin((float)(seconds * 0.11)) * 120f);
        var cy = (H / 2f) + (MathF.Cos((float)(seconds * 0.07)) * 60f);

        for (var i = 0; i < Count; i++)
        {
            this.z[i] -= dt * 0.35f;
            if (this.z[i] <= 0.02f)
                this.Reset(i, 1f);

            var px = cx + (this.x[i] / this.z[i] * 400f);
            var py = cy + (this.y[i] / this.z[i] * 400f);

            if (px < 0 || px >= W || py < 0 || py >= H)
            {
                this.Reset(i, 1f);
                continue;
            }

            var near = 1f - this.z[i];
            var bright = (int)(60 + (195 * near));
            var colour = Canvas.Rgb(bright, bright, Math.Min(255, bright + 20));
            var size = near > 0.8f ? 3 : near > 0.5f ? 2 : 1;
            Canvas.Fill(span, (int)px, (int)py, size, size, colour);

            // Fast, close stars leave a short streak toward where they came from.
            if (near > 0.7f)
            {
                var ox = cx + (this.x[i] / (this.z[i] + 0.04f) * 400f);
                var oy = cy + (this.y[i] / (this.z[i] + 0.04f) * 400f);
                Canvas.Line(span, (int)ox, (int)oy, (int)px, (int)py, Canvas.Lerp(Canvas.Black, colour, 0.5f));
            }
        }

        this.Font.Draw(span, W, "STARFIELD", 24, 8, Canvas.Faint, 1, new BitmapFont.Clip(0, 0, W, H));
    }

    private void Reset(int i, float depth)
    {
        this.x[i] = (float)(this.Rng.NextDouble() * 2 - 1);
        this.y[i] = (float)(this.Rng.NextDouble() * 2 - 1);
        this.z[i] = depth;
    }
}

/// <summary>Plasma: the demoscene's favourite, from four sine tables and a palette.</summary>
internal sealed class PlasmaChannel(BitmapFont font) : AmbienceChannel(font)
{
    public override string Name => "Plasma";

    private const int Scale = 4;
    private const int SW = W / Scale, SH = H / Scale;
    private readonly uint[] small = new uint[SW * SH];
    private readonly int[] sin = BuildSin();
    private readonly int[] dist = BuildDist();
    private readonly uint[] palette = BuildPalette();

    private static int[] BuildSin()
    {
        var t = new int[1024];
        for (var i = 0; i < 1024; i++)
            t[i] = (int)(MathF.Sin(i * MathF.PI * 2f / 1024f) * 255f);
        return t;
    }

    private static int[] BuildDist()
    {
        var d = new int[SW * SH];
        for (var y = 0; y < SH; y++)
            for (var x = 0; x < SW; x++)
                d[(y * SW) + x] = (int)(MathF.Sqrt(((x - (SW / 2)) * (x - (SW / 2))) + ((y - (SH / 2)) * (y - (SH / 2)))) * 6f);
        return d;
    }

    private static uint[] BuildPalette()
    {
        // The set's own colours, cycling: deep blue, cyan, white, amber, and back.
        var p = new uint[256];
        uint[] stops = [Canvas.Tube, Canvas.Rgb(0x1E, 0x4A, 0x6B), Canvas.Accent, Canvas.White, Canvas.Amber, Canvas.Rgb(0x6B, 0x2A, 0x4A), Canvas.Tube];
        for (var i = 0; i < 256; i++)
        {
            var f = i / 256f * (stops.Length - 1);
            var a = (int)f;
            p[i] = Canvas.Lerp(stops[a], stops[Math.Min(a + 1, stops.Length - 1)], f - a);
        }

        return p;
    }

    protected override void Frame(Span<uint> span, float dt, double seconds)
    {
        var t1 = (int)(seconds * 90) & 1023;
        var t2 = (int)(seconds * 60) & 1023;
        var t3 = (int)(seconds * 40) & 1023;
        var t4 = (int)(seconds * 25) & 1023;
        var s = this.small.AsSpan();

        for (var y = 0; y < SH; y++)
        {
            var row = y * SW;
            var sy = this.sin[((y * 9) + t2) & 1023];
            for (var x = 0; x < SW; x++)
            {
                var v = this.sin[((x * 7) + t1) & 1023]
                    + sy
                    + this.sin[(((x + y) * 5) + t3) & 1023]
                    + this.sin[(this.dist[row + x] + t4) & 1023];

                // v is in [-1020, 1020]; fold to a palette index.
                s[row + x] = this.palette[((v + 1020) >> 3) & 255];
            }
        }

        Canvas.Upscale(this.small, SW, SH, Scale, span);
        this.Font.Draw(span, W, "PLASMA", 24, 8, Canvas.Faint, 1, new BitmapFont.Clip(0, 0, W, H));
    }
}

/// <summary>Two polygons bouncing round the screen, trailing fading copies. Mystify, more or less.</summary>
internal sealed class MystifyChannel(BitmapFont font) : AmbienceChannel(font)
{
    public override string Name => "Mystify";

    private const int Shapes = 2, Points = 4, Trail = 12;
    private readonly float[,,] pos = new float[Shapes, Points, 2];
    private readonly float[,,] vel = new float[Shapes, Points, 2];
    private readonly int[,,,] history = new int[Shapes, Trail, Points, 2];
    private int head;
    private float accumulator;
    private bool seeded;

    private static readonly uint[] Colours = [Canvas.Accent, Canvas.Amber];

    protected override void Frame(Span<uint> span, float dt, double seconds)
    {
        if (!this.seeded)
        {
            for (var s = 0; s < Shapes; s++)
                for (var p = 0; p < Points; p++)
                {
                    this.pos[s, p, 0] = this.Rng.Next(W);
                    this.pos[s, p, 1] = this.Rng.Next(H);
                    this.vel[s, p, 0] = (float)(this.Rng.NextDouble() * 240 + 120) * (this.Rng.Next(2) == 0 ? -1 : 1);
                    this.vel[s, p, 1] = (float)(this.Rng.NextDouble() * 240 + 120) * (this.Rng.Next(2) == 0 ? -1 : 1);
                }

            for (var i = 0; i < Trail; i++)
                this.Record();

            this.seeded = true;
        }

        // Move the corners; record a trail entry every few frames so the copies spread out.
        for (var s = 0; s < Shapes; s++)
            for (var p = 0; p < Points; p++)
                for (var axis = 0; axis < 2; axis++)
                {
                    this.pos[s, p, axis] += this.vel[s, p, axis] * dt;
                    var limit = axis == 0 ? W : H;
                    if (this.pos[s, p, axis] < 0) { this.pos[s, p, axis] = 0; this.vel[s, p, axis] = -this.vel[s, p, axis]; }
                    if (this.pos[s, p, axis] >= limit) { this.pos[s, p, axis] = limit - 1; this.vel[s, p, axis] = -this.vel[s, p, axis]; }
                }

        this.accumulator += dt;
        if (this.accumulator >= 0.05f)
        {
            this.accumulator = 0f;
            this.Record();
        }

        span.Fill(Canvas.Black);

        for (var s = 0; s < Shapes; s++)
        {
            for (var t = 0; t < Trail; t++)
            {
                var slot = (this.head + 1 + t) % Trail;
                var colour = Canvas.Lerp(Canvas.Black, Colours[s], (t + 1) / (float)Trail);
                for (var p = 0; p < Points; p++)
                {
                    var q = (p + 1) % Points;
                    Canvas.Line(span, this.history[s, slot, p, 0], this.history[s, slot, p, 1], this.history[s, slot, q, 0], this.history[s, slot, q, 1], colour);
                }
            }
        }

        this.Font.Draw(span, W, "MYSTIFY", 24, 8, Canvas.Faint, 1, new BitmapFont.Clip(0, 0, W, H));
    }

    private void Record()
    {
        this.head = (this.head + 1) % Trail;
        for (var s = 0; s < Shapes; s++)
            for (var p = 0; p < Points; p++)
            {
                this.history[s, this.head, p, 0] = (int)this.pos[s, p, 0];
                this.history[s, this.head, p, 1] = (int)this.pos[s, p, 1];
            }
    }
}
