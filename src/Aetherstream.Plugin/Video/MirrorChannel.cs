namespace Aetherstream.Plugin.Video;

/// <summary>
/// Mirror: another window's picture on the set. A game running beside the game, a video in any
/// player, a second monitor's desktop, whatever <see cref="WindowCapture"/> can see. The newest
/// captured frame is scaled to fit the picture with black bars where the shapes differ, and
/// until one arrives the screen says what it is waiting for.
/// </summary>
internal sealed class MirrorChannel(BitmapFont font) : IFrameChannel, IDisposable
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;

    private readonly WindowCapture capture = new();
    private string title = string.Empty;
    private long lastRenderTicks;

    public bool Available => WindowCapture.Supported;

    public bool WantsPicture => false;

    /// <summary>The other window's sound is its own; nothing plays underneath.</summary>
    public bool WantsMusic => false;

    /// <summary>Whether a window is being captured.</summary>
    public bool Showing => this.capture.Window != 0;

    public string Title => this.title;

    /// <summary>The window being captured, or zero.</summary>
    public nint Window => this.capture.Window;

    /// <summary>A line for the app: what is captured and how it is going.</summary>
    public string Status =>
        this.capture.Window == 0 ? "Nothing mirrored."
        : this.capture.Error is { } error ? $"Stopped: {error}"
        : this.capture.Frames == 0 ? $"Waiting for {this.title}…"
        : $"Mirroring {this.title}";

    /// <summary>Milliseconds since the set last painted this channel; large when it is not up.</summary>
    public long IdleMs => this.lastRenderTicks == 0 ? long.MaxValue : Environment.TickCount64 - this.lastRenderTicks;

    public void Show(nint window, string windowTitle)
    {
        this.title = windowTitle;
        this.lastRenderTicks = Environment.TickCount64;
        this.capture.Start(window);
    }

    public void Stop()
    {
        this.capture.Stop();
        this.title = string.Empty;
    }

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        this.lastRenderTicks = Environment.TickCount64;
        var span = target.AsSpan();

        if (!this.capture.TryLatest(out var pixels, out var width, out var height) || width <= 0 || height <= 0)
        {
            Canvas.Fill(span, 0, 0, W, H, Canvas.Black);
            var all = new BitmapFont.Clip(0, 0, W, H);
            var word = this.capture.Error is not null ? "NO PICTURE" : this.capture.Window == 0 ? "MIRROR" : "WAITING";
            font.Draw(span, W, word, (W - font.Measure(word, 4)) / 2, 236, this.capture.Error is not null ? Canvas.Bad : Canvas.Accent, 4, all);
            var line = this.capture.Error ?? (this.capture.Window == 0 ? "pick a window in the Mirror app" : this.title);
            if (line.Length > 70)
                line = line[..69] + "…";
            font.Draw(span, W, line, (W - font.Measure(line, 1)) / 2, 372, Canvas.Dim, 1, all);
            return;
        }

        // Fit the window inside the frame, keeping its shape.
        var scale = Math.Min((float)W / width, (float)H / height);
        var drawW = Math.Max(1, (int)(width * scale));
        var drawH = Math.Max(1, (int)(height * scale));
        var left = (W - drawW) / 2;
        var top = (H - drawH) / 2;

        if (drawW < W || drawH < H)
            Canvas.Fill(span, 0, 0, W, H, Canvas.Black);

        // Shrinking by two or more averages a 2x2 block so text and edges do not shimmer; at
        // less than that a straight sample is close enough and half the work.
        var average = width >= drawW * 2 || height >= drawH * 2;
        for (var y = 0; y < drawH; y++)
        {
            var sy = y * height / drawH;
            var sy2 = average ? Math.Min(height - 1, sy + 1) : sy;
            var rowA = sy * width;
            var rowB = sy2 * width;
            var outRow = ((top + y) * W) + left;
            for (var x = 0; x < drawW; x++)
            {
                var sx = x * width / drawW;
                if (!average)
                {
                    target[outRow + x] = pixels[rowA + sx];
                    continue;
                }

                var sx2 = Math.Min(width - 1, sx + 1);
                var a = pixels[rowA + sx];
                var b = pixels[rowA + sx2];
                var c = pixels[rowB + sx];
                var d = pixels[rowB + sx2];
                var r = ((a & 0xFF) + (b & 0xFF) + (c & 0xFF) + (d & 0xFF)) >> 2;
                var g = (((a >> 8) & 0xFF) + ((b >> 8) & 0xFF) + ((c >> 8) & 0xFF) + ((d >> 8) & 0xFF)) >> 2;
                var bl = (((a >> 16) & 0xFF) + ((b >> 16) & 0xFF) + ((c >> 16) & 0xFF) + ((d >> 16) & 0xFF)) >> 2;
                target[outRow + x] = 0xFF000000u | (bl << 16) | (g << 8) | r;
            }
        }
    }

    public void Dispose() => this.capture.Dispose();
}
