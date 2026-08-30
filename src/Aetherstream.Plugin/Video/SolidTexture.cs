using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherstream.Plugin.Video;

/// <summary>
/// A small texture of one flat colour, for painting onto a surface that is not meant to carry a
/// picture.
/// <para>
/// Effects rarely sample a single texture: one usually carries colour while another masks it, and
/// that mask is what fades a panel out and lets the wall show through. Replacing the mask with
/// solid white removes both. It is deliberately tiny — the shader samples it, so its content
/// matters and its resolution does not.
/// </para>
/// </summary>
internal sealed class SolidTexture : IFrameUploader
{
    private const int Size = 8;

    private readonly DynamicTextureUploader inner;

    public SolidTexture(ITextureProvider textures, uint rgba)
    {
        this.inner = new DynamicTextureUploader(textures, Size, Size);
        this.Fill(rgba);
    }

    public string Name => "solid colour";

    public int Width => Size;

    public int Height => Size;

    public bool HasFrame => this.inner.HasFrame;

    public bool HasStableHandle => true;

    public ImTextureID Handle => this.inner.Handle;

    /// <summary>Rewrites the colour. Render thread only.</summary>
    public void Fill(uint rgba)
    {
        var pixels = new uint[Size * Size];
        Array.Fill(pixels, rgba);
        this.inner.Upload(pixels);
    }

    /// <summary>Present for the interface; a flat colour has nothing to upload per frame.</summary>
    public void Upload(ReadOnlySpan<uint> rgba)
    {
    }

    public void Dispose() => this.inner.Dispose();
}
