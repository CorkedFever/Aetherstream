namespace Aetherstream.Plugin.Video;

/// <summary>
/// The 3D maze from Windows 95: brick corridors, a checkered floor, and a walk through them
/// that never ends. A raycaster at a quarter of the size, scaled up, which is how it looked
/// then too.
/// </summary>
internal sealed class MazeChannel(BitmapFont font) : AmbienceChannel(font)
{
    public override string Name => "3D Maze";

    private const int Scale = 4;
    private const int SW = W / Scale, SH = H / Scale;
    private const int Cells = 21;
    private const int Tex = 32;

    private readonly uint[] small = new uint[SW * SH];
    private readonly byte[] maze = new byte[Cells * Cells];
    private readonly uint[] brick = BuildBrick();

    private readonly float[] depth = new float[SW];
    private readonly uint[] rolled = new uint[SW * SH];
    private readonly List<(int X, int Y)> smileys = [];
    private float roll, rollTarget;

    // The rat: a second walker, quicker, keeping to the right-hand wall so it takes other turns.
    private float ratX, ratY;
    private int ratTargetX, ratTargetY, ratFacing;
    private bool ratLeft;

    private float px, py, angle;
    private int targetX, targetY;
    private float targetAngle;
    private bool seeded;
    private int facing;

    private static readonly (int Dx, int Dy)[] Dirs = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    private static readonly string[] Smiley =
    [
        ".....yyyyyy.....",
        "...yyyyyyyyyy...",
        "..yyyyyyyyyyyy..",
        ".yyyyyyyyyyyyyy.",
        ".yyykkyyyykkyyy.",
        "yyyykkyyyykkyyyy",
        "yyyyyyyyyyyyyyyy",
        "yyyyyyyyyyyyyyyy",
        "yyyyyyyyyyyyyyyy",
        "yykyyyyyyyyyykyy",
        ".yykkyyyyyykkyy.",
        ".yyyykkkkkkyyyy.",
        "..yyyyyyyyyyyy..",
        "...yyyyyyyyyy...",
        ".....yyyyyy.....",
        "................",
    ];

    private static readonly string[] Rat =
    [
        "................",
        "................",
        "................",
        "................",
        "..........gg....",
        ".......ggggggg..",
        "......gggggggggp",
        "t....ggggggggkg.",
        ".tt.gggggggggg..",
        "..ttgggggggggg..",
        "....gggggggggg..",
        ".....gg....gg...",
        "................",
        "................",
        "................",
        "................",
    ];

    private static uint[] BuildBrick()
    {
        var t = new uint[Tex * Tex];
        var brickA = Canvas.Rgb(0xA8, 0x3A, 0x2A);
        var brickB = Canvas.Rgb(0x96, 0x32, 0x24);
        var mortar = Canvas.Rgb(0xC8, 0xB8, 0xA0);
        for (var y = 0; y < Tex; y++)
        {
            var row = y / 4;
            var shift = row % 2 == 0 ? 0 : 4;
            for (var x = 0; x < Tex; x++)
            {
                var bx = (x + shift) % 8;
                var isMortar = y % 4 == 0 || bx == 0;
                t[(y * Tex) + x] = isMortar ? mortar : ((x + y) % 5 == 0 ? brickB : brickA);
            }
        }

        return t;
    }

    private void Generate()
    {
        Array.Fill(this.maze, (byte)1);
        var stack = new Stack<(int X, int Y)>();
        var start = (X: 1, Y: 1);
        this.maze[(start.Y * Cells) + start.X] = 0;
        stack.Push(start);

        while (stack.Count > 0)
        {
            var (x, y) = stack.Peek();
            var options = new List<(int X, int Y)>();
            foreach (var (dx, dy) in Dirs)
            {
                var nx = x + (dx * 2);
                var ny = y + (dy * 2);
                if (nx > 0 && ny > 0 && nx < Cells - 1 && ny < Cells - 1 && this.maze[(ny * Cells) + nx] == 1)
                    options.Add((nx, ny));
            }

            if (options.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var (tx, ty) = options[this.Rng.Next(options.Count)];
            this.maze[(((y + ty) / 2) * Cells) + ((x + tx) / 2)] = 0;
            this.maze[(ty * Cells) + tx] = 0;
            stack.Push((tx, ty));
        }

        this.px = 1.5f;
        this.py = 1.5f;
        this.facing = 0;
        this.angle = 0f;
        this.targetAngle = 0f;
        this.targetX = 1;
        this.targetY = 1;
        this.PickNext();

        this.smileys.Clear();
        for (var i = 0; i < 14; i++)
            this.PlaceSmiley();

        // The rat starts a few cells along, ahead of the walker.
        this.ratTargetX = 1;
        this.ratTargetY = 1;
        this.ratX = 1.5f;
        this.ratY = 1.5f;
        this.ratFacing = 0;
        this.PickRatNext();
        for (var i = 0; i < 6; i++)
        {
            this.ratX = this.ratTargetX + 0.5f;
            this.ratY = this.ratTargetY + 0.5f;
            this.PickRatNext();
        }
    }

    /// <summary>Puts a smiley in a random open cell that is not where the walker is.</summary>
    private void PlaceSmiley()
    {
        for (var tries = 0; tries < 200; tries++)
        {
            var x = this.Rng.Next(1, Cells - 1);
            var y = this.Rng.Next(1, Cells - 1);
            if (!this.Open(x, y) || (x == (int)this.px && y == (int)this.py) || this.smileys.Contains((x, y)))
                continue;

            this.smileys.Add((x, y));
            return;
        }
    }

    /// <summary>Right-hand rule for the rat, so it and the walker part ways at junctions.</summary>
    private void PickRatNext()
    {
        var cx = this.ratTargetX;
        var cy = this.ratTargetY;
        foreach (var turn in new[] { 1, 0, -1, 2 })
        {
            var f = ((this.ratFacing + turn) % 4 + 4) % 4;
            var (dx, dy) = Dirs[f];
            if (this.Open(cx + dx, cy + dy))
            {
                this.ratFacing = f;
                this.ratTargetX = cx + dx;
                this.ratTargetY = cy + dy;
                this.ratLeft = dx < 0;
                return;
            }
        }
    }

    private bool Open(int x, int y) => x >= 0 && y >= 0 && x < Cells && y < Cells && this.maze[(y * Cells) + x] == 0;

    /// <summary>Left-hand rule: turn left if you can, else straight, else right, else back.</summary>
    private void PickNext()
    {
        var cx = this.targetX;
        var cy = this.targetY;
        foreach (var turn in new[] { -1, 0, 1, 2 })
        {
            var f = ((this.facing + turn) % 4 + 4) % 4;
            var (dx, dy) = Dirs[f];
            if (this.Open(cx + dx, cy + dy))
            {
                this.facing = f;
                this.targetX = cx + dx;
                this.targetY = cy + dy;
                this.targetAngle = f * MathF.PI / 2f;
                return;
            }
        }
    }

    protected override void Frame(Span<uint> span, float dt, double seconds)
    {
        if (!this.seeded)
        {
            this.Generate();
            this.seeded = true;
        }

        // Turn first, then walk to the middle of the next cell.
        var diff = this.targetAngle - this.angle;
        while (diff > MathF.PI) diff -= 2f * MathF.PI;
        while (diff < -MathF.PI) diff += 2f * MathF.PI;

        if (MathF.Abs(diff) > 0.02f)
        {
            var step = 2.2f * dt;
            this.angle += MathF.Abs(diff) < step ? diff : MathF.Sign(diff) * step;
        }
        else
        {
            var gx = this.targetX + 0.5f;
            var gy = this.targetY + 0.5f;
            var dx = gx - this.px;
            var dy = gy - this.py;
            var dist = MathF.Sqrt((dx * dx) + (dy * dy));
            var step = 1.6f * dt;
            if (dist <= step)
            {
                this.px = gx;
                this.py = gy;

                // Walking into a smiley turns the world over — as it did — and the smiley moves on.
                var here = (this.targetX, this.targetY);
                if (this.smileys.Remove(here))
                {
                    this.rollTarget = this.rollTarget < 0.5f ? MathF.PI : 0f;
                    this.PlaceSmiley();
                }

                this.PickNext();
            }
            else
            {
                this.px += dx / dist * step;
                this.py += dy / dist * step;
            }
        }

        // The rat scurries: straight to the next cell, no turning animation, quicker than us.
        {
            var gx = this.ratTargetX + 0.5f;
            var gy = this.ratTargetY + 0.5f;
            var dx = gx - this.ratX;
            var dy = gy - this.ratY;
            var dist = MathF.Sqrt((dx * dx) + (dy * dy));
            var step = 2.6f * dt;
            if (dist <= step)
            {
                this.ratX = gx;
                this.ratY = gy;
                this.PickRatNext();
            }
            else
            {
                this.ratX += dx / dist * step;
                this.ratY += dy / dist * step;
            }
        }

        // The roll eases toward its target over about a second and a half.
        var rollDiff = this.rollTarget - this.roll;
        if (MathF.Abs(rollDiff) > 0.005f)
            this.roll += MathF.Sign(rollDiff) * MathF.Min(MathF.Abs(rollDiff), 2.1f * dt);
        else
            this.roll = this.rollTarget;

        this.Raycast();
        this.DrawSprites();

        if (this.roll == 0f)
        {
            Canvas.Upscale(this.small, SW, SH, Scale, span);
        }
        else
        {
            this.Rotate(this.roll);
            Canvas.Upscale(this.rolled, SW, SH, Scale, span);
        }

        this.Font.Draw(span, W, "3D MAZE", 24, 8, Canvas.Faint, 1, new BitmapFont.Clip(0, 0, W, H));
    }

    /// <summary>
    /// Sprites, drawn after the walls and only where they are nearer than the wall in that
    /// column. Farthest first, so a near one covers a far one.
    /// </summary>
    private void DrawSprites()
    {
        var dirX = MathF.Cos(this.angle);
        var dirY = MathF.Sin(this.angle);
        var planeX = -dirY * 0.66f;
        var planeY = dirX * 0.66f;
        var inv = 1f / ((planeX * dirY) - (dirX * planeY));

        var yellow = Canvas.Rgb(0xFF, 0xD6, 0x4F);
        var black = Canvas.Rgb(0x1A, 0x14, 0x08);
        var grey = Canvas.Rgb(0x8A, 0x86, 0x80);
        var pink = Canvas.Rgb(0xE0, 0x90, 0x90);

        var sprites = new List<(float X, float Y, string[] Art, Func<char, uint> Palette, float Height, float Lift, bool Flip)>();
        foreach (var (cx, cy) in this.smileys)
            sprites.Add((cx + 0.5f, cy + 0.5f, Smiley, c => c == 'y' ? yellow : c == 'k' ? black : 0u, 0.6f, 0.15f, false));

        sprites.Add((this.ratX, this.ratY, Rat, c => c switch { 'g' => grey, 'p' => pink, 't' => pink, 'k' => black, _ => 0u }, 0.5f, 0.42f, this.ratLeft));

        foreach (var sprite in sprites.OrderByDescending(m => ((m.X - this.px) * (m.X - this.px)) + ((m.Y - this.py) * (m.Y - this.py))))
        {
            var sx = sprite.X - this.px;
            var sy = sprite.Y - this.py;
            var tx = inv * ((dirY * sx) - (dirX * sy));
            var ty = inv * ((-planeY * sx) + (planeX * sy));
            if (ty <= 0.1f)
                continue;

            var screenX = (int)((SW / 2f) * (1f + (tx / ty)));
            var size = (int)(SH / ty * sprite.Height);
            if (size < 2)
                continue;

            var left = screenX - (size / 2);
            var top = (SH / 2) - (size / 2) + (int)(SH / ty * sprite.Lift);
            var shade = Math.Clamp(1.1f - (ty / 9f), 0.15f, 1f);
            this.Blit(sprite.Art, sprite.Palette, left, top, size, ty, shade, sprite.Flip);
        }
    }

    private void Blit(string[] art, Func<char, uint> palette, int left, int top, int size, float ty, float shade, bool flip)
    {
        var s = this.small.AsSpan();
        for (var x = Math.Max(0, left); x < Math.Min(SW, left + size); x++)
        {
            if (ty >= this.depth[x])
                continue;

            var ax = (x - left) * 16 / size;
            if (flip)
                ax = 15 - ax;

            for (var y = Math.Max(0, top); y < Math.Min(SH, top + size); y++)
            {
                var ay = (y - top) * 16 / size;
                var colour = palette(art[ay][ax]);
                if (colour == 0)
                    continue;

                s[(y * SW) + x] = Canvas.Lerp(Canvas.Black, colour, shade);
            }
        }
    }

    /// <summary>Rotates the small picture about its centre into the rolled buffer, black where nothing lands.</summary>
    private void Rotate(float angle)
    {
        var src = this.small.AsSpan();
        var dst = this.rolled.AsSpan();
        var c = MathF.Cos(angle);
        var sn = MathF.Sin(angle);
        var cx = SW / 2f;
        var cy = SH / 2f;

        for (var y = 0; y < SH; y++)
        {
            var dy = y - cy;
            for (var x = 0; x < SW; x++)
            {
                var dx = x - cx;
                var ux = (int)(cx + (dx * c) - (dy * sn));
                var uy = (int)(cy + (dx * sn) + (dy * c));
                dst[(y * SW) + x] = (uint)ux < SW && (uint)uy < SH ? src[(uy * SW) + ux] : Canvas.Black;
            }
        }
    }

    private void Raycast()
    {
        var s = this.small.AsSpan();
        var dirX = MathF.Cos(this.angle);
        var dirY = MathF.Sin(this.angle);
        var planeX = -dirY * 0.66f;
        var planeY = dirX * 0.66f;

        var ceiling = Canvas.Rgb(0x2A, 0x2E, 0x3A);
        var floorA = Canvas.Rgb(0x3A, 0x3E, 0x48);
        var floorB = Canvas.Rgb(0x22, 0x25, 0x2E);

        // Floor and ceiling by row: a checker on the floor, flat above, both fading with distance.
        for (var y = SH / 2; y < SH; y++)
        {
            var rowDist = (SH / 2f) / (y - (SH / 2f) + 0.5f);
            var fade = Math.Clamp(1.2f - (rowDist / 10f), 0.15f, 1f);
            var stepX = rowDist * (2f * planeX) / SW;
            var stepY = rowDist * (2f * planeY) / SW;
            var fx = this.px + (rowDist * (dirX - planeX));
            var fy = this.py + (rowDist * (dirY - planeY));
            var row = y * SW;
            var ceil = (SH - 1 - y) * SW;
            var ceilColour = Canvas.Lerp(Canvas.Black, ceiling, fade);
            for (var x = 0; x < SW; x++)
            {
                var checker = (((int)MathF.Floor(fx * 2f)) + ((int)MathF.Floor(fy * 2f))) & 1;
                s[row + x] = Canvas.Lerp(Canvas.Black, checker == 0 ? floorA : floorB, fade);
                s[ceil + x] = ceilColour;
                fx += stepX;
                fy += stepY;
            }
        }

        for (var x = 0; x < SW; x++)
        {
            var cameraX = (2f * x / SW) - 1f;
            var rayX = dirX + (planeX * cameraX);
            var rayY = dirY + (planeY * cameraX);

            var mapX = (int)this.px;
            var mapY = (int)this.py;
            var deltaX = rayX == 0 ? 1e30f : MathF.Abs(1f / rayX);
            var deltaY = rayY == 0 ? 1e30f : MathF.Abs(1f / rayY);
            int stepX, stepY;
            float sideX, sideY;

            if (rayX < 0) { stepX = -1; sideX = (this.px - mapX) * deltaX; }
            else { stepX = 1; sideX = (mapX + 1f - this.px) * deltaX; }
            if (rayY < 0) { stepY = -1; sideY = (this.py - mapY) * deltaY; }
            else { stepY = 1; sideY = (mapY + 1f - this.py) * deltaY; }

            var side = 0;
            for (var i = 0; i < 64; i++)
            {
                if (sideX < sideY) { sideX += deltaX; mapX += stepX; side = 0; }
                else { sideY += deltaY; mapY += stepY; side = 1; }

                if (mapX < 0 || mapY < 0 || mapX >= Cells || mapY >= Cells || this.maze[(mapY * Cells) + mapX] == 1)
                    break;
            }

            var perp = side == 0 ? sideX - deltaX : sideY - deltaY;
            if (perp < 0.05f)
                perp = 0.05f;

            this.depth[x] = perp;

            var lineH = (int)(SH / perp);
            var drawStart = Math.Max(0, (-lineH / 2) + (SH / 2));
            var drawEnd = Math.Min(SH - 1, (lineH / 2) + (SH / 2));

            var wallX = side == 0 ? this.py + (perp * rayY) : this.px + (perp * rayX);
            wallX -= MathF.Floor(wallX);
            var texX = (int)(wallX * Tex) & (Tex - 1);

            var shade = Math.Clamp(1.1f - (perp / 9f), 0.12f, 1f) * (side == 1 ? 0.72f : 1f);
            var texStep = (float)Tex / lineH;
            var texPos = (drawStart - (SH / 2f) + (lineH / 2f)) * texStep;

            for (var y = drawStart; y <= drawEnd; y++)
            {
                var texY = (int)texPos & (Tex - 1);
                texPos += texStep;
                s[(y * SW) + x] = Canvas.Lerp(Canvas.Black, this.brick[(texY * Tex) + texX], shade);
            }
        }
    }
}

/// <summary>
/// Pipes, from Windows NT: plumbing grows through an empty room until it fills, then starts
/// over. A grid in three dimensions drawn back to front with a slowly turning camera.
/// </summary>
internal sealed class PipesChannel(BitmapFont font) : AmbienceChannel(font)
{
    public override string Name => "Pipes";

    private const int GX = 22, GY = 14, GZ = 22;
    private const float Focal = 900f;
    private const float Distance = 31f;

    private readonly record struct Segment(int X0, int Y0, int Z0, int X1, int Y1, int Z1, uint Colour, bool Joint, bool Teapot = false);

    // The Utah teapot, as the original hid at the odd joint. Sixteen by twelve, sideways.
    private static readonly string[] Teapot =
    [
        "......cccc......",
        ".......cc.......",
        "....cccccccc....",
        "c..cccccccccc.c.",
        "cc.cccccccccc.cc",
        ".c.ccccccccccccc",
        ".c.cccccccccccc.",
        ".cccccccccccccc.",
        "..cccccccccccc..",
        "...cccccccccc...",
        "....cccccccc....",
        "................",
    ];

    private readonly bool[] occupied = new bool[GX * GY * GZ];
    private readonly List<Segment> segments = [];
    private readonly List<(float Depth, int Index)> order = [];

    private int headX, headY, headZ, dir = -1;
    private int pipeLength, pipeLimit;
    private uint colour;
    private float stepTimer;
    private float resetTimer;
    private bool resetting;

    private static readonly (int X, int Y, int Z)[] Dirs =
        [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    private static readonly uint[] Palette =
    [
        Canvas.Rgb(0xE2, 0x4B, 0x4A), Canvas.Rgb(0x5D, 0xCA, 0xA5), Canvas.Rgb(0x6B, 0xC7, 0xFF),
        Canvas.Rgb(0xEF, 0x9F, 0x27), Canvas.Rgb(0xB0, 0x7A, 0xE0), Canvas.Rgb(0xFF, 0xD6, 0x4F),
        Canvas.Rgb(0xE6, 0xF1, 0xFB),
    ];

    private bool Free(int x, int y, int z) =>
        x >= 0 && y >= 0 && z >= 0 && x < GX && y < GY && z < GZ && !this.occupied[(z * GY * GX) + (y * GX) + x];

    private void StartPipe()
    {
        for (var tries = 0; tries < 200; tries++)
        {
            var x = this.Rng.Next(GX);
            var y = this.Rng.Next(GY);
            var z = this.Rng.Next(GZ);
            if (!this.Free(x, y, z))
                continue;

            this.headX = x;
            this.headY = y;
            this.headZ = z;
            this.occupied[(z * GY * GX) + (y * GX) + x] = true;
            this.colour = Palette[this.Rng.Next(Palette.Length)];
            this.dir = -1;
            this.pipeLength = 0;
            this.pipeLimit = 40 + this.Rng.Next(80);
            this.segments.Add(new Segment(x, y, z, x, y, z, this.colour, true));
            return;
        }

        // Nowhere left to start: the room is full. Let it sit, then clear it.
        this.resetting = true;
        this.resetTimer = 4f;
    }

    private void Step()
    {
        if (this.dir < 0 && this.segments.Count == 0)
        {
            this.StartPipe();
            return;
        }

        // A pipe runs its length and then stops, so the room fills with many colours, not one.
        if (this.dir >= 0 && ++this.pipeLength > this.pipeLimit)
        {
            this.StartPipe();
            return;
        }

        // Mostly straight on; sometimes a turn; when blocked, any free way; when boxed in, a new pipe.
        var candidates = new List<int>();
        if (this.dir >= 0 && this.Rng.NextDouble() < 0.72)
        {
            var (dx, dy, dz) = Dirs[this.dir];
            if (this.Free(this.headX + dx, this.headY + dy, this.headZ + dz))
                candidates.Add(this.dir);
        }

        if (candidates.Count == 0)
        {
            for (var d = 0; d < Dirs.Length; d++)
            {
                var (dx, dy, dz) = Dirs[d];
                if (this.Free(this.headX + dx, this.headY + dy, this.headZ + dz))
                    candidates.Add(d);
            }
        }

        if (candidates.Count == 0)
        {
            this.StartPipe();
            return;
        }

        var next = candidates[this.Rng.Next(candidates.Count)];
        var turned = this.dir >= 0 && next != this.dir;
        var (mx, my, mz) = Dirs[next];

        var nx = this.headX + mx;
        var ny = this.headY + my;
        var nz = this.headZ + mz;

        // One turn in twenty-five gets a teapot instead of a ball. Everyone who knows, knows.
        if (turned)
            this.segments.Add(new Segment(this.headX, this.headY, this.headZ, this.headX, this.headY, this.headZ, this.colour, true, Teapot: this.Rng.Next(25) == 0));

        this.segments.Add(new Segment(this.headX, this.headY, this.headZ, nx, ny, nz, this.colour, false));
        this.occupied[(nz * GY * GX) + (ny * GX) + nx] = true;
        this.headX = nx;
        this.headY = ny;
        this.headZ = nz;
        this.dir = next;

        if (this.segments.Count > 2600)
        {
            this.resetting = true;
            this.resetTimer = 3f;
        }
    }

    protected override void Frame(Span<uint> span, float dt, double seconds)
    {
        if (this.resetting)
        {
            this.resetTimer -= dt;
            if (this.resetTimer <= 0f)
            {
                this.segments.Clear();
                Array.Clear(this.occupied);
                this.dir = -1;
                this.resetting = false;
            }
        }
        else
        {
            this.stepTimer += dt;
            while (this.stepTimer >= 0.05f)
            {
                this.stepTimer -= 0.05f;
                this.Step();
            }
        }

        span.Fill(Canvas.Black);

        // The camera circles the room slowly, looking at its middle.
        var yaw = (float)(seconds * 0.12);
        var pitch = 0.35f;
        var cy = MathF.Cos(yaw); var sy = MathF.Sin(yaw);
        var cp = MathF.Cos(pitch); var sp = MathF.Sin(pitch);

        (float X, float Y, float Z) View(float x, float y, float z)
        {
            x -= GX / 2f; y -= GY / 2f; z -= GZ / 2f;
            var rx = (x * cy) - (z * sy);
            var rz = (x * sy) + (z * cy);
            var ry = (y * cp) - (rz * sp);
            rz = (y * sp) + (rz * cp);
            return (rx, ry, rz + Distance);
        }

        this.order.Clear();
        for (var i = 0; i < this.segments.Count; i++)
        {
            var sgm = this.segments[i];
            var mid = View((sgm.X0 + sgm.X1) / 2f, (sgm.Y0 + sgm.Y1) / 2f, (sgm.Z0 + sgm.Z1) / 2f);
            this.order.Add((mid.Z, i));
        }

        this.order.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        foreach (var (depth, index) in this.order)
        {
            var sgm = this.segments[index];
            var a = View(sgm.X0, sgm.Y0, sgm.Z0);
            var b = View(sgm.X1, sgm.Y1, sgm.Z1);
            if (a.Z <= 1f || b.Z <= 1f)
                continue;

            var ax = (W / 2f) + (Focal * a.X / a.Z);
            var ay = (H / 2f) - (Focal * a.Y / a.Z);
            var bx = (W / 2f) + (Focal * b.X / b.Z);
            var by = (H / 2f) - (Focal * b.Y / b.Z);
            var radius = Math.Clamp((int)(0.34f * Focal / depth), 2, 22);

            // Far pipes fade, and a joint is a slightly larger ball.
            var fade = Math.Clamp(1.3f - (depth / (Distance * 1.6f)), 0.25f, 1f);
            var body = Canvas.Lerp(Canvas.Black, sgm.Colour, fade);
            var shine = Canvas.Lerp(body, Canvas.White, 0.45f);

            if (sgm.Teapot)
            {
                var scale = Math.Max(1, (radius * 3) / 8);
                Canvas.Sprite(span, Teapot, c => c == 'c' ? body : 0u, (int)ax - (8 * scale), (int)ay - (6 * scale), scale);
                Canvas.Fill(span, (int)ax - (4 * scale), (int)ay - (3 * scale), 3 * scale, scale, shine);
                continue;
            }

            if (sgm.Joint)
            {
                Canvas.Disc(span, (int)ax, (int)ay, radius + 2, body);
                Canvas.Disc(span, (int)ax - (radius / 3), (int)ay - (radius / 3), Math.Max(1, radius / 3), shine);
                continue;
            }

            var length = MathF.Sqrt(((bx - ax) * (bx - ax)) + ((by - ay) * (by - ay)));
            var steps = Math.Max(1, (int)(length / Math.Max(1f, radius * 0.5f)));
            for (var t = 0; t <= steps; t++)
            {
                var f = t / (float)steps;
                var x = (int)(ax + ((bx - ax) * f));
                var y = (int)(ay + ((by - ay) * f));
                Canvas.Disc(span, x, y, radius, body);
            }

            // A highlight along the top edge sells the cylinder.
            var hx = -(by - ay) / Math.Max(1f, length) * (radius * 0.45f);
            var hy = (bx - ax) / Math.Max(1f, length) * (radius * 0.45f);
            Canvas.Line(span, (int)(ax + hx), (int)(ay - MathF.Abs(hy)), (int)(bx + hx), (int)(by - MathF.Abs(hy)), shine);
        }

        this.Font.Draw(span, W, "PIPES", 24, 8, Canvas.Faint, 1, new BitmapFont.Clip(0, 0, W, H));
    }
}
