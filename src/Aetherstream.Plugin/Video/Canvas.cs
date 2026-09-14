namespace Aetherstream.Plugin.Video;

/// <summary>
/// Drawing primitives for the drawn channels: fills, lines, sprites, upscaling, and text
/// folding. Everything works on a 1280x720 span of libvlc-order RGBA pixels, which is what
/// the session hands every channel.
/// </summary>
internal static class Canvas
{
    public const int Width = 1280;
    public const int Height = 720;

    // The set's palette as 0xAABBGGRR, libvlc order.
    public const uint Tube = 0xFF26150Au;       // 0A1526
    public const uint Glass = 0xFF1A0F0Au;      // 0A0F1A
    public const uint GlassLit = 0xFF38240Fu;   // 0F2438
    public const uint Edge = 0xFF44281Cu;       // 1C2A44
    public const uint Accent = 0xFFFFC76Bu;     // 6BC7FF
    public const uint AccentDeep = 0xFF6B4A1Eu; // 1E4A6B
    public const uint White = 0xFFFBF1E6u;      // E6F1FB
    public const uint Dim = 0xFFC8B09Fu;        // 9FB0C8
    public const uint Faint = 0xFF806E5Fu;      // 5F6E80
    public const uint Amber = 0xFF279FEFu;      // EF9F27
    public const uint Good = 0xFFA5CA5Du;       // 5DCAA5
    public const uint Bad = 0xFF4A4BE2u;        // E24B4A
    public const uint Black = 0xFF000000u;

    /// <summary>Packs an R,G,B triple into the frame's byte order.</summary>
    public static uint Rgb(int r, int g, int b) =>
        0xFF000000u | ((uint)Math.Clamp(b, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(r, 0, 255);

    /// <summary>Blends two packed colours, t from 0 (a) to 1 (b).</summary>
    public static uint Lerp(uint a, uint b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        var ar = (int)(a & 0xFF); var ag = (int)((a >> 8) & 0xFF); var ab = (int)((a >> 16) & 0xFF);
        var br = (int)(b & 0xFF); var bg = (int)((b >> 8) & 0xFF); var bb = (int)((b >> 16) & 0xFF);
        return Rgb(ar + (int)((br - ar) * t), ag + (int)((bg - ag) * t), ab + (int)((bb - ab) * t));
    }

    public static void Fill(Span<uint> target, int x, int y, int w, int h, uint colour)
    {
        var x0 = Math.Max(0, x);
        var x1 = Math.Min(Width, x + w);
        if (x1 <= x0)
            return;

        for (var row = Math.Max(0, y); row < Math.Min(Height, y + h); row++)
            target.Slice((row * Width) + x0, x1 - x0).Fill(colour);
    }

    /// <summary>An outline. Three pixels thick by default: a thinner line falls between the rows a squashed surface samples.</summary>
    public static void Rect(Span<uint> target, int x, int y, int w, int h, uint colour, int thickness = 3)
    {
        Fill(target, x, y, w, thickness, colour);
        Fill(target, x, y + h - thickness, w, thickness, colour);
        Fill(target, x, y, thickness, h, colour);
        Fill(target, x + w - thickness, y, thickness, h, colour);
    }

    public static void Plot(Span<uint> target, int x, int y, uint colour)
    {
        if ((uint)x < Width && (uint)y < Height)
            target[(y * Width) + x] = colour;
    }

    /// <summary>Bresenham, clipped per pixel; fine for the few hundred lines a frame ever needs.</summary>
    public static void Line(Span<uint> target, int x0, int y0, int x1, int y1, uint colour)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;

        while (true)
        {
            Plot(target, x0, y0, colour);
            if (x0 == x1 && y0 == y1)
                break;

            var e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public static void Disc(Span<uint> target, int cx, int cy, int r, uint colour)
    {
        for (var y = -r; y <= r; y++)
        {
            var half = (int)Math.Sqrt((r * r) - (y * y));
            Fill(target, cx - half, cy + y, (2 * half) + 1, 1, colour);
        }
    }

    /// <summary>
    /// Draws pixel art: rows of characters, one per art pixel, each mapped to a colour by the
    /// palette (0 for transparent), scaled up by an integer, optionally mirrored.
    /// </summary>
    public static void Sprite(Span<uint> target, string[] art, Func<char, uint> palette, int x, int y, int scale, bool flip = false)
    {
        var w = art[0].Length;
        for (var ay = 0; ay < art.Length; ay++)
        {
            var row = art[ay];
            for (var ax = 0; ax < w; ax++)
            {
                var colour = palette(row[flip ? w - 1 - ax : ax]);
                if (colour == 0)
                    continue;

                Fill(target, x + (ax * scale), y + (ay * scale), scale, scale, colour);
            }
        }
    }

    /// <summary>
    /// Scales a small buffer up to the full frame by an integer factor, nearest-neighbour.
    /// The channels that compute every pixel work at a quarter or an eighth of the size and let
    /// this do the rest — it is faster, and it looks more like the hardware that inspired them.
    /// </summary>
    public static void Upscale(ReadOnlySpan<uint> source, int sourceWidth, int sourceHeight, int scale, Span<uint> target)
    {
        for (var sy = 0; sy < sourceHeight; sy++)
        {
            var src = source.Slice(sy * sourceWidth, sourceWidth);
            var firstRow = target.Slice(sy * scale * Width, Width);

            for (var sx = 0; sx < sourceWidth; sx++)
                firstRow.Slice(sx * scale, scale).Fill(src[sx]);

            for (var dy = 1; dy < scale; dy++)
                firstRow.CopyTo(target.Slice(((sy * scale) + dy) * Width, Width));
        }
    }

    /// <summary>Folds text to what the atlas can draw: accents stripped, typographic marks swapped.</summary>
    public static string Plain(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(c switch
            {
                '·' or '•' => '/',
                '—' or '–' => '-',
                '‘' or '’' => '\'',
                '“' or '”' => '"',
                '…' => '.',
                '\n' or '\r' or '\t' => ' ',
                _ => c,
            });
        }

        return sb.ToString();
    }

    /// <summary>Word-wraps to a column count, at most a number of lines; the last line is cut if needed.</summary>
    public static List<string> Wrap(string text, int columns, int maxLines)
    {
        var lines = new List<string>();
        if (columns <= 0 || maxLines <= 0)
            return lines;

        var line = new System.Text.StringBuilder();
        foreach (var word in Plain(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var piece = word.Length > columns ? word[..columns] : word;
            if (line.Length > 0 && line.Length + 1 + piece.Length > columns)
            {
                lines.Add(line.ToString());
                line.Clear();
                if (lines.Count == maxLines)
                    return lines;
            }

            if (line.Length > 0)
                line.Append(' ');

            line.Append(piece);
        }

        if (line.Length > 0 && lines.Count < maxLines)
            lines.Add(line.ToString());

        return lines;
    }

    public static string Cut(string text, int max)
    {
        var plain = Plain(text);
        return max <= 0 ? string.Empty : plain.Length <= max ? plain : plain[..max];
    }
}
