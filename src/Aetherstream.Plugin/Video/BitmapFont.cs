using System.Buffers.Binary;
using System.IO.Compression;

using Dalamud.Plugin.Services;

namespace Aetherstream.Plugin.Video;

/// <summary>
/// The display face, pre-rasterised, for drawing text straight into a frame.
/// <para>
/// Nothing in the plugin can rasterise a TrueType font into our own pixels — ImGui draws to the
/// screen, not to a buffer we own — so the glyphs are rendered at build time into an alpha-only
/// atlas (see <c>tools/make_guidefont.py</c>) and blended here. VT323 is monospace, which keeps this to
/// one cell size and no kerning. Integer scaling gives the larger sizes; the face is pixel art
/// to begin with, so doubling it looks like it should.
/// </para>
/// </summary>
internal sealed class BitmapFont
{
    private readonly byte[]? coverage;
    private readonly int first;
    private readonly int count;

    public int CellWidth { get; }

    public int CellHeight { get; }

    public BitmapFont(string path, IPluginLog log)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var buffer = new MemoryStream();
            gzip.CopyTo(buffer);

            var bytes = buffer.ToArray();
            if (bytes.Length < 12 || bytes[0] != (byte)'A' || bytes[1] != (byte)'E' || bytes[2] != (byte)'F' || bytes[3] != (byte)'1')
            {
                log.Warning("Guide font atlas has the wrong header; not using it.");
                return;
            }

            this.first = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4));
            this.count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6));
            this.CellWidth = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8));
            this.CellHeight = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10));

            var expected = this.count * this.CellWidth * this.CellHeight;
            if (bytes.Length - 12 != expected)
            {
                log.Warning($"Guide font atlas is {bytes.Length - 12} bytes, expected {expected}; not using it.");
                return;
            }

            this.coverage = bytes[12..];
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not load the guide font.");
        }
    }

    public bool Available => this.coverage is not null;

    /// <summary>How wide a string draws at a scale, in pixels.</summary>
    public int Measure(string text, int scale = 1) => text.Length * this.CellWidth * scale;

    /// <summary>How many characters fit in a width at a scale.</summary>
    public int Fit(int width, int scale = 1) => Math.Max(0, width / (this.CellWidth * scale));

    /// <summary>
    /// Draws text with its top-left at (x, y), clipped to a rectangle, blending coverage over
    /// whatever is already there. Characters outside the atlas draw as nothing.
    /// </summary>
    public void Draw(Span<uint> target, int stride, string text, int x, int y, uint colour, int scale, in Clip clip)
    {
        if (this.coverage is null)
            return;

        var cw = this.CellWidth;
        var ch = this.CellHeight;

        var cr = (int)(colour & 0xFF);
        var cg = (int)((colour >> 8) & 0xFF);
        var cb = (int)((colour >> 16) & 0xFF);

        foreach (var c in text)
        {
            var glyph = c - this.first;
            if (glyph >= 0 && glyph < this.count && c != ' ')
            {
                var atlas = this.coverage.AsSpan(glyph * cw * ch, cw * ch);

                var top = Math.Max(y, clip.Top);
                var bottom = Math.Min(y + (ch * scale), clip.Bottom);
                var left = Math.Max(x, clip.Left);
                var right = Math.Min(x + (cw * scale), clip.Right);

                for (var py = top; py < bottom; py++)
                {
                    var atlasRow = atlas.Slice(((py - y) / scale) * cw, cw);
                    var line = target.Slice(py * stride, stride);

                    for (var px = left; px < right; px++)
                    {
                        int a = atlasRow[(px - x) / scale];
                        if (a == 0)
                            continue;

                        if (a == 255)
                        {
                            line[px] = colour | 0xFF000000u;
                            continue;
                        }

                        var d = line[px];
                        var dr = (int)(d & 0xFF);
                        var dg = (int)((d >> 8) & 0xFF);
                        var db = (int)((d >> 16) & 0xFF);

                        var r = dr + (((cr - dr) * a) >> 8);
                        var g = dg + (((cg - dg) * a) >> 8);
                        var b = db + (((cb - db) * a) >> 8);

                        line[px] = 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
                    }
                }
            }

            x += cw * scale;
            if (x >= clip.Right)
                return;
        }
    }

    /// <summary>A rectangle to draw within: left/top inclusive, right/bottom exclusive.</summary>
    public readonly record struct Clip(int Left, int Top, int Right, int Bottom);
}
