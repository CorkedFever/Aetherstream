// Adapted from Memoria (FFXIV-rom-Emulator) src/RomEmulator/Video/DynamicTextureUploader.cs.

using System.Runtime.InteropServices;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherstream.Plugin.Video;

/// <summary>
/// The fast path: one D3D11_USAGE_DYNAMIC texture, allocated once, rewritten in place every frame
/// with MAP_WRITE_DISCARD. No per-frame allocation and no GPU-side churn.
/// </summary>
internal sealed unsafe class DynamicTextureUploader : IFrameUploader
{
    private readonly IDalamudTextureWrap wrap;
    private readonly nint resource;
    private bool disposed;

    public DynamicTextureUploader(ITextureProvider textures, int width, int height)
    {
        this.Width = width;
        this.Height = height;

        // cpuWrite: true is what gets us a DYNAMIC texture we are allowed to Map.
        this.wrap = textures.CreateEmpty(
            RawImageSpecification.Rgba32(width, height),
            cpuRead: false,
            cpuWrite: true,
            debugName: "Aetherstream.Screen");

        try
        {
            var srv = (nint)this.wrap.Handle.Handle;
            if (srv == 0)
                throw new InvalidOperationException("Texture wrap produced a null handle.");

            this.resource = D3D11.GetResource(srv);
            if (this.resource == 0)
                throw new InvalidOperationException("Shader resource view has no backing resource.");
        }
        catch
        {
            this.wrap.Dispose();
            throw;
        }
    }

    public string Name => "dynamic (D3D11 map)";

    public int Width { get; }

    public int Height { get; }

    public bool HasFrame { get; private set; }

    /// <summary>One texture, allocated once and rewritten in place.</summary>
    public bool HasStableHandle => true;

    public ImTextureID Handle => this.wrap.Handle;

    public void Upload(ReadOnlySpan<uint> rgba)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        var expected = this.Width * this.Height;
        if (rgba.Length < expected)
            throw new ArgumentException($"Frame is {rgba.Length} pixels, expected {expected}.", nameof(rgba));

        var context = D3D11.ImmediateContext;
        if (context == 0)
            throw new InvalidOperationException("Graphics device is not available.");

        var hr = D3D11.Map(context, this.resource, D3D11.MapWriteDiscard, out var mapped);
        if (hr < 0)
            throw new InvalidOperationException($"ID3D11DeviceContext::Map failed with 0x{hr:X8}.");

        try
        {
            var source = MemoryMarshal.AsBytes(rgba);
            var rowBytes = this.Width * sizeof(uint);
            var destination = (byte*)mapped.Data;

            if (mapped.RowPitch == rowBytes)
            {
                // Contiguous — one copy for the whole frame.
                source[..(rowBytes * this.Height)].CopyTo(new Span<byte>(destination, rowBytes * this.Height));
            }
            else
            {
                // The driver padded each row; copy row by row into the pitch it gave us.
                for (var y = 0; y < this.Height; y++)
                {
                    source.Slice(y * rowBytes, rowBytes)
                          .CopyTo(new Span<byte>(destination + (y * mapped.RowPitch), rowBytes));
                }
            }
        }
        finally
        {
            D3D11.Unmap(context, this.resource);
        }

        this.HasFrame = true;
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        D3D11.Release(this.resource);
        this.wrap.Dispose();
    }
}
