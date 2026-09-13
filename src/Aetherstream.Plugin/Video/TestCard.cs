using System.IO.Compression;
using System.Runtime.InteropServices;

using Dalamud.Plugin.Services;

namespace Aetherstream.Plugin.Video;

/// <summary>
/// The picture a set shows when nothing is on: colour bars, the mark, and a clock.
/// <para>
/// The card itself is artwork rendered once at build time (with the display face, which no
/// software rasteriser here could draw) and shipped as raw RGBA pixels, gzipped to a few tens of
/// kilobytes. Only the clock is drawn at runtime, as seven-segment digits — which is not a
/// compromise; it is what a test card's clock looks like.
/// </para>
/// </summary>
internal sealed class TestCard
{
    public const int Width = 1280;
    public const int Height = 720;

    /// <summary>Aether cyan as libvlc-order RGBA bytes read as a little-endian uint.</summary>
    private const uint Accent = 0xFFFFC76Bu;

    // The clock box the artwork leaves empty: 260x64 at right 140, bottom 44.
    private const int BoxLeft = 880;
    private const int BoxTop = 612;
    private const int BoxWidth = 260;
    private const int BoxHeight = 64;

    private readonly uint[]? pixels;

    public TestCard(string path, IPluginLog log)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            var bytes = new byte[Width * Height * sizeof(uint)];

            var read = 0;
            while (read < bytes.Length)
            {
                var n = gzip.Read(bytes, read, bytes.Length - read);
                if (n == 0)
                    break;

                read += n;
            }

            if (read != bytes.Length)
            {
                log.Warning($"Test card is {read} bytes, expected {bytes.Length}; not using it.");
                return;
            }

            // Same memory layout libvlc hands us for chroma "RGBA": R,G,B,A per pixel, read as
            // one little-endian uint each. No conversion, so the card and the video agree.
            this.pixels = MemoryMarshal.Cast<byte, uint>(bytes).ToArray();
        }
        catch (Exception ex)
        {
            // A missing card is cosmetic: the set simply shows nothing, as it did before.
            log.Warning(ex, "Could not load the test card.");
        }
    }

    public bool Available => this.pixels is not null;

    /// <summary>Paints the card into a 1280x720 frame with the current time on its clock.</summary>
    public void Render(Span<uint> target, DateTime now)
    {
        if (this.pixels is null)
            return;

        this.pixels.AsSpan().CopyTo(target);

        // HH:MM, centred in the box. Digit 22 wide, 40 tall, 10 apart; the colon takes 10.
        const int DigitW = 22, DigitH = 40, Gap = 10, ColonW = 10;
        const int Total = (DigitW * 4) + (Gap * 4) + ColonW;

        var x = BoxLeft + ((BoxWidth - Total) / 2);
        var y = BoxTop + ((BoxHeight - DigitH) / 2);

        var hh = now.Hour;
        var mm = now.Minute;

        Digit(target, x, y, hh / 10); x += DigitW + Gap;
        Digit(target, x, y, hh % 10); x += DigitW + Gap;

        // Two squares, on the digit's second and fifth stripe.
        Fill(target, x + 2, y + 9, 6, 6);
        Fill(target, x + 2, y + DigitH - 15, 6, 6);
        x += ColonW + Gap;

        Digit(target, x, y, mm / 10); x += DigitW + Gap;
        Digit(target, x, y, mm % 10);
    }

    // Segments a–g as bits: a top, b top-right, c bottom-right, d bottom, e bottom-left,
    // f top-left, g middle — the classic seven-segment encoding.
    private static readonly byte[] Segments =
    [
        0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110,
        0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111,
    ];

    private static void Digit(Span<uint> target, int x, int y, int value)
    {
        const int W = 22, H = 40, T = 5;
        var s = Segments[value];

        if ((s & 0b0000001) != 0) Fill(target, x + T, y, W - (2 * T), T);                         // a
        if ((s & 0b0000010) != 0) Fill(target, x + W - T, y + T, T, (H / 2) - T);                // b
        if ((s & 0b0000100) != 0) Fill(target, x + W - T, y + (H / 2), T, (H / 2) - T);          // c
        if ((s & 0b0001000) != 0) Fill(target, x + T, y + H - T, W - (2 * T), T);                // d
        if ((s & 0b0010000) != 0) Fill(target, x, y + (H / 2), T, (H / 2) - T);                  // e
        if ((s & 0b0100000) != 0) Fill(target, x, y + T, T, (H / 2) - T);                        // f
        if ((s & 0b1000000) != 0) Fill(target, x + T, y + (H / 2) - (T / 2), W - (2 * T), T);    // g
    }

    private static void Fill(Span<uint> target, int x, int y, int w, int h)
    {
        for (var row = y; row < y + h && row < Height; row++)
        {
            var line = target.Slice(row * Width, Width);
            for (var col = x; col < x + w && col < Width; col++)
                line[col] = Accent;
        }
    }
}
