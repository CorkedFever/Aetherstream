// Copied from Memoria (FFXIV-rom-Emulator) src/RomEmulator.Core/IFrameSource.cs.
// Keep byte-compatible with the original contract — Phase 2 adapts this to Memoria's
// IFrameSource one-to-one. Do not diverge without updating both.

namespace Aetherstream.Core;

/// <summary>
/// Produces one frame of native-resolution video per call.
/// </summary>
public interface IFrameSource
{
    int Width { get; }

    int Height { get; }

    /// <summary>
    /// Frames a second this source is meant to run at. For a live stream this is a poll-rate
    /// hint for the driving clock, not a pacing contract — presentation timing lives inside
    /// the source, which repeats its latest frame until a new one is due.
    /// </summary>
    double FrameRate { get; }

    /// <summary>The shape of one pixel, width over height.</summary>
    double PixelAspect => 1.0;

    int OverscanRows => 0;

    int OverscanColumns => 0;

    /// <summary>
    /// Advances the source by exactly one frame and fills <paramref name="rgba"/> with
    /// <see cref="Width"/> * <see cref="Height"/> packed RGBA8888 pixels.
    /// Called on the driving thread; must not allocate.
    /// </summary>
    void RenderFrame(Span<uint> rgba);
}

public static class Rgba
{
    /// <summary>
    /// Packs a colour for DXGI_FORMAT_R8G8B8A8_UNORM. On little-endian the low byte lands
    /// first in memory, so red belongs in the low bits.
    /// </summary>
    public static uint Pack(byte r, byte g, byte b, byte a = 255) =>
        (uint)((a << 24) | (b << 16) | (g << 8) | r);
}
