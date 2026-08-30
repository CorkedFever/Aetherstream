using System.Diagnostics;
using System.Drawing.Imaging;
using Aetherstream.Core;
using Aetherstream.Playback;

namespace Aetherstream.PoC;

/// <summary>
/// Polls the source for frames and blits them. Deliberately dumb: the point is to prove that
/// pixels arrive in *our* buffer in the pipeline's format, not to build a renderer.
/// </summary>
public sealed class PreviewForm : Form
{
    private readonly IFrameSource source;
    private readonly PlaybackStats? stats;
    private readonly bool proveBuffer;
    private readonly uint[] frame;
    private readonly Bitmap bitmap;
    private readonly System.Windows.Forms.Timer timer;
    private readonly Stopwatch uptime = Stopwatch.StartNew();
    private readonly Process self = Process.GetCurrentProcess();

    private TimeSpan lastCpu;
    private double lastCpuAt;
    private double cpuPercent;
    private int framesDrawn;
    private double lastFpsAt;
    private double fps;
    private double fetchMs;
    private double blitMs;
    private double drawMs;
    private long lastAudioFrames;
    private double lastAudioAt;
    private string title = "Aetherstream";

    public PreviewForm(IFrameSource source, PlaybackStats? stats, bool proveBuffer, string name)
    {
        this.source = source;
        this.stats = stats;
        this.proveBuffer = proveBuffer;
        this.frame = new uint[source.Width * source.Height];
        this.bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppRgb);
        this.title = $"Aetherstream — {name}";

        this.Text = this.title;
        this.ClientSize = new Size(source.Width, source.Height);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.Black;
        this.DoubleBuffered = true;
        this.lastCpu = this.self.TotalProcessorTime;

        // 15, not 16: the system tick is 15.6 ms, and a 16 ms request rounds up to two ticks —
        // exactly 32 ms, i.e. a hard 30 fps ceiling. 15 fits inside one tick and polls at ~64 Hz.
        this.timer = new System.Windows.Forms.Timer { Interval = 15 };
        this.timer.Tick += (_, _) => this.Invalidate();
        this.timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var t0 = Stopwatch.GetTimestamp();
        this.source.RenderFrame(this.frame);

        if (this.proveBuffer)
            this.DrawProofBorder();

        var t1 = Stopwatch.GetTimestamp();
        this.Blit();

        var t2 = Stopwatch.GetTimestamp();

        // Bilinear resampling costs more than the decode does at 720p. Nearest with the pixel-offset
        // mode set is a straight stretch-blit, and at the default 1:1 window size it is a plain copy.
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        e.Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        e.Graphics.DrawImage(this.bitmap, this.ClientRectangle);

        var t3 = Stopwatch.GetTimestamp();
        this.fetchMs += Ms(t0, t1);
        this.blitMs += Ms(t1, t2);
        this.drawMs += Ms(t2, t3);

        this.UpdateStats();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        this.timer.Stop();
        this.timer.Dispose();
        this.bitmap.Dispose();
        base.OnFormClosed(e);
    }

    /// <summary>
    /// A magenta frame drawn into the span the source just filled. If it shows on screen, the
    /// pixels demonstrably travelled through our own buffer rather than some internal fast path.
    /// </summary>
    private void DrawProofBorder()
    {
        var magenta = Rgba.Pack(255, 0, 255);
        var w = this.source.Width;
        var h = this.source.Height;

        for (var x = 0; x < w; x++)
        {
            this.frame[x] = magenta;
            this.frame[x + w] = magenta;
            this.frame[((h - 1) * w) + x] = magenta;
            this.frame[((h - 2) * w) + x] = magenta;
        }

        for (var y = 0; y < h; y++)
        {
            this.frame[y * w] = magenta;
            this.frame[(y * w) + 1] = magenta;
            this.frame[(y * w) + w - 1] = magenta;
            this.frame[(y * w) + w - 2] = magenta;
        }
    }

    /// <summary>
    /// Throwaway PoC glue: GDI's 32bpp is BGRA in memory while the pipeline's contract is
    /// RGBA-with-red-low, so the channels are swapped here at present time only. The pipeline
    /// buffer itself stays in Memoria's format — Phase 2 uploads it to an R8G8B8A8 texture with
    /// no swizzle at all, which is the whole point of keeping this ugliness out here.
    /// </summary>
    private unsafe void Blit()
    {
        // Width/Height are GDI+ P/Invokes, not fields. Reading them as loop bounds costs a native
        // call per pixel — which measured at 22 ms a frame here, i.e. the entire frame budget.
        var width = this.source.Width;
        var height = this.source.Height;

        var data = this.bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppRgb);

        try
        {
            fixed (uint* src = this.frame)
            {
                for (var y = 0; y < height; y++)
                {
                    var srcRow = src + (y * width);
                    var dstRow = (uint*)((byte*)data.Scan0 + (y * data.Stride));

                    for (var x = 0; x < width; x++)
                    {
                        var p = srcRow[x];
                        dstRow[x] = (p & 0xFF00FF00) | ((p & 0xFF) << 16) | ((p >> 16) & 0xFF);
                    }
                }
            }
        }
        finally
        {
            this.bitmap.UnlockBits(data);
        }
    }

    private static double Ms(long from, long to) =>
        (to - from) * 1000.0 / Stopwatch.Frequency;

    private void UpdateStats()
    {
        this.framesDrawn++;
        var now = this.uptime.Elapsed.TotalSeconds;

        if (now - this.lastFpsAt < 1.0)
            return;

        var drawn = this.framesDrawn;
        this.fps = drawn / (now - this.lastFpsAt);
        this.framesDrawn = 0;
        this.lastFpsAt = now;

        var cpu = this.self.TotalProcessorTime;
        var elapsed = now - this.lastCpuAt;
        if (elapsed > 0)
        {
            this.cpuPercent = (cpu - this.lastCpu).TotalSeconds
                / (elapsed * Environment.ProcessorCount) * 100.0;
        }

        this.lastCpu = cpu;
        this.lastCpuAt = now;

        var n = Math.Max(1, drawn);
        var text = $"{this.title} — {this.fps:F0} fps, CPU {this.cpuPercent:F1}%" +
            $", fetch {this.fetchMs / n:F1}ms blit {this.blitMs / n:F1}ms draw {this.drawMs / n:F1}ms";
        this.fetchMs = this.blitMs = this.drawMs = 0;
        if (this.stats is not null)
            text += $", presented {this.stats.FramesPresented}, dropped {this.stats.FramesDropped}";
        if (this.source is VlcStreamSource { Audio: { } audio } vlcSource && this.stats is not null)
        {
            // Delivered frames per second must land on the requested rate. A whole-number multiple
            // means libvlc ignored the requested format, which is the difference between "slightly
            // glitchy" and "full-scale noise".
            var deliveredPerSecond = (this.stats.AudioFramesDelivered - this.lastAudioFrames)
                / Math.Max(0.001, now - this.lastAudioAt);
            this.lastAudioFrames = this.stats.AudioFramesDelivered;
            this.lastAudioAt = now;

            text += $", audio {audio.Fill * 100:F0}% @{deliveredPerSecond / 1000:F1}k/s" +
                $" peak {vlcSource.AudioPeak:F2} bad {vlcSource.AudioBadSamples}" +
                $", under {audio.Underruns} over {audio.Overruns}";
        }

        this.Text = text;
    }
}
