namespace Aetherstream.Plugin.Video;

/// <summary>
/// Scales one RGBA buffer into another with a bilinear filter. Used both ways: the 720 canvas
/// up to a 1080 output, and a 1080 frame down to the canvas for a channel that shows the picture
/// in a corner. Plain loops on the render thread; a 1080 frame is under three milliseconds.
/// </summary>
internal static class Resample
{
    public static void Bilinear(uint[] source, int sourceWidth, int sourceHeight, uint[] target, int targetWidth, int targetHeight)
    {
        if (sourceWidth == targetWidth && sourceHeight == targetHeight)
        {
            Array.Copy(source, target, targetWidth * targetHeight);
            return;
        }

        var scaleX = (sourceWidth - 1) / (float)Math.Max(1, targetWidth - 1);
        var scaleY = (sourceHeight - 1) / (float)Math.Max(1, targetHeight - 1);

        for (var y = 0; y < targetHeight; y++)
        {
            var sy = y * scaleY;
            var y0 = (int)sy;
            var y1 = Math.Min(sourceHeight - 1, y0 + 1);
            var fy = (int)((sy - y0) * 256f);
            var row0 = y0 * sourceWidth;
            var row1 = y1 * sourceWidth;
            var outRow = y * targetWidth;

            for (var x = 0; x < targetWidth; x++)
            {
                var sx = x * scaleX;
                var x0 = (int)sx;
                var x1 = Math.Min(sourceWidth - 1, x0 + 1);
                var fx = (int)((sx - x0) * 256f);

                var a = source[row0 + x0];
                var b = source[row0 + x1];
                var c = source[row1 + x0];
                var d = source[row1 + x1];

                var r = Mix(a & 0xFF, b & 0xFF, c & 0xFF, d & 0xFF, fx, fy);
                var g = Mix((a >> 8) & 0xFF, (b >> 8) & 0xFF, (c >> 8) & 0xFF, (d >> 8) & 0xFF, fx, fy);
                var bl = Mix((a >> 16) & 0xFF, (b >> 16) & 0xFF, (c >> 16) & 0xFF, (d >> 16) & 0xFF, fx, fy);
                target[outRow + x] = 0xFF000000u | (bl << 16) | (g << 8) | r;
            }
        }
    }

    private static uint Mix(uint a, uint b, uint c, uint d, int fx, int fy)
    {
        var top = (a * (256 - fx)) + (b * fx);
        var bottom = (c * (256 - fx)) + (d * fx);
        return (uint)(((top * (256 - fy)) + (bottom * fy)) >> 16);
    }
}
