namespace Aetherstream.Core;

/// <summary>
/// A moving gradient with a sweeping bar. Stands in for a decoder while the display path is being
/// built: if this animates smoothly in a window, the pipeline → poll → blit path is proven before
/// any network or codec code exists.
/// </summary>
public sealed class TestPatternSource(int width, int height) : IFrameSource
{
    private int tick;

    public int Width => width;

    public int Height => height;

    public double FrameRate => 60.0;

    public void RenderFrame(Span<uint> rgba)
    {
        var t = this.tick++;
        var barX = t * 4 % width;

        for (var y = 0; y < height; y++)
        {
            var row = rgba.Slice(y * width, width);
            var g = (byte)(y * 255 / height);

            for (var x = 0; x < width; x++)
            {
                var r = (byte)((x + t) * 255 / width);
                var b = (byte)(255 - r);
                row[x] = Rgba.Pack(r, g, b);
            }
        }

        // A bright vertical bar makes dropped or repeated frames visible at a glance.
        for (var y = 0; y < height; y++)
        {
            var row = rgba.Slice(y * width, width);
            for (var x = barX; x < Math.Min(barX + 8, width); x++)
                row[x] = Rgba.Pack(255, 255, 255);
        }
    }
}
