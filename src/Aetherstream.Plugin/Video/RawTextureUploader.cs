// Adapted from Memoria (FFXIV-rom-Emulator) src/RomEmulator/Video/RawTextureUploader.cs.

using System.Runtime.InteropServices;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherstream.Plugin.Video;

/// <summary>
/// The boring path: build a brand new texture every frame. Wasteful, but it touches nothing except
/// public Dalamud API, so it is the fallback when the dynamic uploader cannot get at the D3D11
/// resource.
/// <para>
/// The previous frame's texture is held for one extra frame before release, since ImGui's draw
/// list from the last frame may still reference it.
/// </para>
/// </summary>
internal sealed class RawTextureUploader : IFrameUploader
{
    private readonly ITextureProvider textures;
    private readonly RawImageSpecification spec;

    private IDalamudTextureWrap? current;
    private IDalamudTextureWrap? retired;
    private bool disposed;

    public RawTextureUploader(ITextureProvider textures, int width, int height)
    {
        this.textures = textures;
        this.Width = width;
        this.Height = height;
        this.spec = RawImageSpecification.Rgba32(width, height);
    }

    public string Name => "raw (texture per frame)";

    public int Width { get; }

    public int Height { get; }

    public bool HasFrame => this.current is not null;

    /// <summary>A new texture every frame, so the handle must never be lent out.</summary>
    public bool HasStableHandle => false;

    public ImTextureID Handle => this.current?.Handle ?? default;

    public void Upload(ReadOnlySpan<uint> rgba)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        this.retired?.Dispose();
        this.retired = this.current;
        this.current = this.textures.CreateFromRaw(this.spec, MemoryMarshal.AsBytes(rgba));
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.retired?.Dispose();
        this.current?.Dispose();
        this.retired = null;
        this.current = null;
    }
}
