// Adapted from Memoria (FFXIV-rom-Emulator) src/RomEmulator/Video/IFrameUploader.cs.

using Dalamud.Bindings.ImGui;

namespace Aetherstream.Plugin.Video;

/// <summary>
/// Gets a finished frame onto the GPU. Every member is render-thread only.
/// </summary>
internal interface IFrameUploader : IDisposable
{
    /// <summary>Gets a short name for the strategy, shown in the debug readout.</summary>
    string Name { get; }

    int Width { get; }

    int Height { get; }

    /// <summary>Gets a value indicating whether a frame has been uploaded and <see cref="Handle"/> is drawable.</summary>
    bool HasFrame { get; }

    /// <summary>Gets the handle to hand to the ImGui draw list. Only valid once <see cref="HasFrame"/> is set.</summary>
    ImTextureID Handle { get; }

    /// <summary>
    /// Whether <see cref="Handle"/> stays the same object across frames.
    /// <para>
    /// Only a stable handle may be lent to the game. A strategy that allocates a fresh texture each
    /// frame hands out a pointer that is freed a frame later, and the game rendering from it faults
    /// inside the display driver.
    /// </para>
    /// </summary>
    bool HasStableHandle { get; }

    /// <summary>Uploads one full frame of packed RGBA8888 pixels.</summary>
    void Upload(ReadOnlySpan<uint> rgba);
}
