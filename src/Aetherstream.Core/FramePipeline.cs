// Copied from Memoria (FFXIV-rom-Emulator) src/RomEmulator.Core/FramePipeline.cs.
// Keep byte-compatible with the original contract. Do not diverge without updating both.

namespace Aetherstream.Core;

/// <summary>
/// Lock-free handoff of finished frames from the producer thread to the render thread.
/// <para>
/// Three buffers: the producer owns one, the consumer owns one, and a single shared slot holds
/// the most recently finished frame. Both sides trade through that slot with an atomic exchange,
/// so neither ever blocks and the two can never hold the same buffer. The producer overwrites an
/// unconsumed frame rather than waiting — dropping a frame is always better than stalling either
/// side.
/// </para>
/// <para>
/// Nearest-neighbour upscaling happens here, on the producer side. That keeps the render thread's
/// job down to a single memcpy. Video sources use scale 1, which takes the row-copy fast path.
/// </para>
/// </summary>
public sealed class FramePipeline
{
    private readonly uint[][] buffers;
    private readonly uint[] native;
    private readonly int nativeWidth;
    private readonly int nativeHeight;

    private int writeIndex;
    private int readyIndex;
    private int readIndex;
    private long sequence;
    private long lastConsumedSnapshot;
    // Starts level with the unpublished sequence, so the first acquire reports nothing rather than
    // handing out a buffer no frame has been written into yet.
    private long lastConsumed;

    /// <summary>Rows and columns trimmed from the edges, and what remains.</summary>
    public int OverscanRows { get; }

    public int OverscanColumns { get; }

    public int VisibleHeight { get; }

    public int VisibleWidth { get; }

    public FramePipeline(
        int nativeWidth,
        int nativeHeight,
        int scale,
        float scanlineStrength = 0f,
        int overscanRows = 0,
        int overscanColumns = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(overscanRows);
        ArgumentOutOfRangeException.ThrowIfNegative(overscanColumns);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overscanRows * 2, nativeHeight);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overscanColumns * 2, nativeWidth);

        this.nativeWidth = nativeWidth;
        this.nativeHeight = nativeHeight;
        this.OverscanRows = overscanRows;
        this.OverscanColumns = overscanColumns;
        this.VisibleHeight = nativeHeight - (overscanRows * 2);
        this.VisibleWidth = nativeWidth - (overscanColumns * 2);
        this.Scale = scale;
        this.ScanlineStrength = Math.Clamp(scanlineStrength, 0f, 1f);
        this.Width = this.VisibleWidth * scale;
        this.Height = this.VisibleHeight * scale;

        this.native = new uint[nativeWidth * nativeHeight];
        this.buffers =
        [
            new uint[this.Width * this.Height],
            new uint[this.Width * this.Height],
            new uint[this.Width * this.Height],
        ];

        // The three indices must start distinct; the exchanges preserve that invariant.
        this.writeIndex = 0;
        this.readyIndex = 1;
        this.readIndex = 2;
    }

    public int Scale { get; }

    /// <summary>Gets how far the darkened rows are dimmed, 0 to 1. Zero disables scanlines.</summary>
    public float ScanlineStrength { get; }

    /// <summary>Gets the scaled width, i.e. what the texture must be sized to.</summary>
    public int Width { get; }

    /// <summary>Gets the scaled height.</summary>
    public int Height { get; }

    /// <summary>Gets the number of finished frames the consumer never picked up.</summary>
    public long DroppedFrames { get; private set; }

    /// <summary>
    /// Renders one frame from <paramref name="source"/>, upscales it, and publishes it.
    /// Producer thread only.
    /// </summary>
    public void Produce(IFrameSource source)
    {
        source.RenderFrame(this.native);

        var target = this.buffers[this.writeIndex];

        // The source renders every pixel; only the visible region is scaled out. The source span
        // starts at the visible region's top-left corner, and the stride stays the full native
        // width so each row skips the cropped columns on both sides.
        Upscale(
            this.native.AsSpan((this.OverscanRows * this.nativeWidth) + this.OverscanColumns),
            this.nativeWidth,
            this.VisibleWidth,
            this.VisibleHeight,
            target,
            this.Scale,
            this.ScanlineStrength);

        // Publish the buffer, then the sequence number. Exchange is a full fence, so the pixel
        // writes above are visible to whoever picks this buffer up.
        var previouslyReady = Interlocked.Exchange(ref this.readyIndex, this.writeIndex);
        this.writeIndex = previouslyReady;

        var published = Interlocked.Increment(ref this.sequence);
        if (published - Volatile.Read(ref this.lastConsumedSnapshot) > 1)
            this.DroppedFrames++;
    }

    /// <summary>
    /// Takes the newest finished frame if one has arrived since the last call. Render thread only.
    /// Returns <see langword="false"/> when nothing new is ready, in which case the caller should
    /// keep showing whatever it drew last.
    /// </summary>
    public bool TryAcquire(out ReadOnlySpan<uint> frame)
    {
        var published = Volatile.Read(ref this.sequence);
        if (published == this.lastConsumed)
        {
            frame = default;
            return false;
        }

        this.lastConsumed = published;
        Volatile.Write(ref this.lastConsumedSnapshot, published);
        this.readIndex = Interlocked.Exchange(ref this.readyIndex, this.readIndex);
        frame = this.buffers[this.readIndex];
        return true;
    }

    private static void Upscale(
        ReadOnlySpan<uint> src,
        int srcStride,
        int srcWidth,
        int srcHeight,
        Span<uint> dst,
        int scale,
        float scanlineStrength)
    {
        if (scale == 1 && scanlineStrength <= 0f)
        {
            for (var y = 0; y < srcHeight; y++)
                src.Slice(y * srcStride, srcWidth).CopyTo(dst.Slice(y * srcWidth, srcWidth));

            return;
        }

        // Scanlines are free here. Each source row is already being replicated `scale` times, so
        // darkening the last copy costs one extra pass over rows that were going to be written
        // anyway — and it happens on the producer thread, not the render thread's.
        var dim = scanlineStrength > 0f && scale > 1;
        var dstWidth = srcWidth * scale;

        for (var y = 0; y < srcHeight; y++)
        {
            var srcRow = src.Slice(y * srcStride, srcWidth);
            var dstRow = dst.Slice(y * scale * dstWidth, dstWidth);

            for (var x = 0; x < srcWidth; x++)
                dstRow.Slice(x * scale, scale).Fill(srcRow[x]);

            // Replicate the finished row instead of re-expanding it.
            for (var r = 1; r < scale; r++)
                dstRow.CopyTo(dst.Slice(((y * scale) + r) * dstWidth, dstWidth));

            if (!dim)
                continue;

            var darkRow = dst.Slice(((y * scale) + scale - 1) * dstWidth, dstWidth);
            Darken(darkRow, scanlineStrength);
        }
    }

    private static void Darken(Span<uint> row, float strength)
    {
        var keep = (uint)Math.Clamp((int)((1f - strength) * 255f), 0, 255);

        for (var i = 0; i < row.Length; i++)
        {
            var colour = row[i];
            var r = ((colour & 0xFF) * keep) / 255;
            var g = (((colour >> 8) & 0xFF) * keep) / 255;
            var b = (((colour >> 16) & 0xFF) * keep) / 255;

            row[i] = (colour & 0xFF000000) | (b << 16) | (g << 8) | r;
        }
    }
}
