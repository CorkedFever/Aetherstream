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

    /// <summary>
    /// Whether the whole window is shrunk to fit, or a piece of it shown at its own pixels. A
    /// window wider than the picture loses its text to the shrink; at actual pixels it reads.
    /// </summary>
    public bool ActualPixels { get; set; }

    /// <summary>Where the actual-pixels view sits in the window, 0 to 1 each way; a half is the middle.</summary>
    public float PanX { get; set; } = 0.5f;

    public float PanY { get; set; } = 0.5f;

    /// <summary>The captured window's size, for the app to say how it relates to the picture.</summary>
    public (int Width, int Height) Size => this.capture.TryLatest(out _, out var w, out var h) ? (w, h) : (0, 0);

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

        if (this.ActualPixels)
        {
            RenderActual(target, pixels, width, height, this.PanX, this.PanY);
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

        if (width <= drawW && height <= drawH)
        {
            // Growing, or the same: a straight sample.
            for (var y = 0; y < drawH; y++)
            {
                var row = (y * height / drawH) * width;
                var outRow = ((top + y) * W) + left;
                for (var x = 0; x < drawW; x++)
                    target[outRow + x] = pixels[row + (x * width / drawW)];
            }

            return;
        }

        // Shrinking: every source pixel under a picture pixel is averaged, so a line of small
        // text becomes soft grey rather than a scatter of whichever pixels were sampled. The
        // footprint is capped so a very large window costs a bounded amount per frame.
        var boxW = Math.Clamp(width / drawW, 1, 4);
        var boxH = Math.Clamp(height / drawH, 1, 4);
        var count = (uint)(boxW * boxH);
        for (var y = 0; y < drawH; y++)
        {
            var sy = Math.Min(height - boxH, y * height / drawH);
            var outRow = ((top + y) * W) + left;
            for (var x = 0; x < drawW; x++)
            {
                var sx = Math.Min(width - boxW, x * width / drawW);
                uint r = 0, g = 0, b = 0;
                for (var yy = 0; yy < boxH; yy++)
                {
                    var row = ((sy + yy) * width) + sx;
                    for (var xx = 0; xx < boxW; xx++)
                    {
                        var p = pixels[row + xx];
                        r += p & 0xFF;
                        g += (p >> 8) & 0xFF;
                        b += (p >> 16) & 0xFF;
                    }
                }

                target[outRow + x] = 0xFF000000u | ((b / count) << 16) | ((g / count) << 8) | (r / count);
            }
        }
    }

    /// <summary>A picture-sized piece of the window, pixel for pixel, placed by the pan; a smaller window sits centred.</summary>
    private static void RenderActual(uint[] target, uint[] pixels, int width, int height, float panX, float panY)
    {
        var copyW = Math.Min(W, width);
        var copyH = Math.Min(H, height);
        var srcX = (int)Math.Round((width - copyW) * Math.Clamp(panX, 0f, 1f));
        var srcY = (int)Math.Round((height - copyH) * Math.Clamp(panY, 0f, 1f));
        var dstX = (W - copyW) / 2;
        var dstY = (H - copyH) / 2;

        if (copyW < W || copyH < H)
            Canvas.Fill(target, 0, 0, W, H, Canvas.Black);

        for (var y = 0; y < copyH; y++)
            Array.Copy(pixels, ((srcY + y) * width) + srcX, target, ((dstY + y) * W) + dstX, copyW);
    }

    public void Dispose() => this.capture.Dispose();
}
