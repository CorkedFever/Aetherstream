namespace Aetherstream.Plugin.Video;

/// <summary>
/// Scales one RGBA buffer into another with a bilinear filter. Used both ways: the 720 canvas
/// up to a 1080 output, and a 1080 frame down to the canvas for a channel that shows the picture
/// in a corner. Runs on the render thread, so it is written for speed: the column weights are
/// worked out once per pair of sizes, the four taps are blended two channels at a time in one
/// register, and the rows are spread over the thread pool.
/// </summary>
internal static unsafe class Resample
{
    private static (int SourceWidth, int TargetWidth, int[] X0, int[] X1, int[] Fx)? columns;

    public static void Bilinear(uint[] source, int sourceWidth, int sourceHeight, uint[] target, int targetWidth, int targetHeight)
    {
        if (sourceWidth == targetWidth && sourceHeight == targetHeight)
        {
            Array.Copy(source, target, targetWidth * targetHeight);
            return;
        }

        var (x0s, x1s, fxs) = Columns(sourceWidth, targetWidth);
        var scaleY = (sourceHeight - 1) / (float)Math.Max(1, targetHeight - 1);

        fixed (uint* src = source)
        fixed (uint* dst = target)
        fixed (int* px0 = x0s)
        fixed (int* px1 = x1s)
        fixed (int* pfx = fxs)
        {
            var s = src;
            var d = dst;
            var cx0 = px0;
            var cx1 = px1;
            var cfx = pfx;

            // Rows in strips of sixteen: enough work per task to be worth the hand-off, few
            // enough strips that every core gets some.
            var strips = (targetHeight + 15) / 16;
            Parallel.For(0, strips, strip =>
            {
                var yEnd = Math.Min(targetHeight, (strip + 1) * 16);
                for (var y = strip * 16; y < yEnd; y++)
                {
                    var sy = y * scaleY;
                    var y0 = (int)sy;
                    var y1 = Math.Min(sourceHeight - 1, y0 + 1);
                    var fy = (uint)((sy - y0) * 256f);
                    var row0 = s + (y0 * sourceWidth);
                    var row1 = s + (y1 * sourceWidth);
                    var outRow = d + (y * targetWidth);

                    for (var x = 0; x < targetWidth; x++)
                    {
                        var x0 = cx0[x];
                        var x1 = cx1[x];
                        var fx = (uint)cfx[x];

                        var top = Lerp(row0[x0], row0[x1], fx);
                        var bottom = Lerp(row1[x0], row1[x1], fx);
                        outRow[x] = Lerp(top, bottom, fy) | 0xFF000000u;
                    }
                }
            });
        }
    }

    /// <summary>
    /// Blends two packed pixels, red and blue in one multiply and green in another: the channels
    /// are spread apart so an 8-bit product cannot carry into its neighbour.
    /// </summary>
    private static uint Lerp(uint a, uint b, uint f)
    {
        var inverse = 256 - f;
        var rb = ((((a & 0x00FF00FFu) * inverse) + ((b & 0x00FF00FFu) * f)) >> 8) & 0x00FF00FFu;
        var g = ((((a & 0x0000FF00u) * inverse) + ((b & 0x0000FF00u) * f)) >> 8) & 0x0000FF00u;
        return rb | g;
    }

    private static (int[] X0, int[] X1, int[] Fx) Columns(int sourceWidth, int targetWidth)
    {
        var cached = columns;
        if (cached is { } c && c.SourceWidth == sourceWidth && c.TargetWidth == targetWidth)
            return (c.X0, c.X1, c.Fx);

        var x0s = new int[targetWidth];
        var x1s = new int[targetWidth];
        var fxs = new int[targetWidth];
        var scaleX = (sourceWidth - 1) / (float)Math.Max(1, targetWidth - 1);
        for (var x = 0; x < targetWidth; x++)
        {
            var sx = x * scaleX;
            var x0 = (int)sx;
            x0s[x] = x0;
            x1s[x] = Math.Min(sourceWidth - 1, x0 + 1);
            fxs[x] = (int)((sx - x0) * 256f);
        }

        columns = (sourceWidth, targetWidth, x0s, x1s, fxs);
        return (x0s, x1s, fxs);
    }
}
