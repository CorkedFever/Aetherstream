using System.Numerics;
using System.Linq;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

using Aetherstream.Plugin.Video;

namespace Aetherstream.Plugin.Surfaces;

/// <summary>One texture on an object's material, described well enough to pick from a list.</summary>
internal readonly record struct SurfaceSlot(
    int MaterialIndex,
    int TextureIndex,
    string ModelPath,
    string MaterialPath,
    string TexturePath,
    int Width,
    int Height);

/// <summary>
/// Paints the video onto a real object's surface, by pointing one of its material textures at our
/// texture instead of the one the game loaded.
/// <para>
/// This is what an overlay cannot do. The picture becomes part of the object, so the game renders
/// it in its normal pass: it is lit like the surface, it is hidden by anything in front of it, and
/// a character standing between you and it occludes it per pixel. No cut-outs, no approximation.
/// </para>
/// <para>
/// Only the shader resource view is exchanged — the pointer the shader samples from — and the
/// original is kept so it can be put back exactly. The swap is re-applied every frame rather than
/// once, because the game rebuilds these when an object streams in or out; re-applying is a couple
/// of pointer writes and makes the binding survive that on its own.
/// </para>
/// </summary>
internal sealed unsafe class SurfaceBinding(IPluginLog log) : IDisposable
{
    /// <summary>
    /// Every game texture we have written our view into, with whatever was there first.
    /// <para>
    /// One (target, original) pair cannot describe reality: trying several textures in turn leaves
    /// more than one game texture holding our view at the same time, and the ones no longer bound
    /// were silently abandoned still pointing at us. That is what the display driver was faulting
    /// on. Every slot is remembered instead, and every one is put back.
    /// </para>
    /// </summary>
    private readonly List<PaintedSlot> painted = [];
    private bool disposed;

    private sealed class PaintedSlot
    {
        public nint Target;

        public nint Ours;

        /// <summary>Zero when we never saw the game's own view and so cannot restore it.</summary>
        public nint Original;
    }

    /// <summary>Which slot is bound, if any. Stored by index and re-walked every frame.</summary>
    public SurfaceSlot? Bound { get; private set; }

    public string? Error { get; private set; }

    /// <summary>True while the video is actually reaching the surface.</summary>
    public bool IsApplied { get; private set; }

    /// <summary>
    /// Lists the textures on an object that could carry a picture. Read-only.
    /// </summary>
    /// <summary>
    /// Materials are read out of the model's own handle rather than a runtime array, and that array
    /// carries no count — so it is walked with a cap and every entry validated before it is
    /// followed. Furniture has a handful of materials, never dozens.
    /// </summary>
    private const int MaxMaterials = 12;

    public static List<SurfaceSlot> Enumerate(Vector3 position, string modelPath, out string report)
    {
        var slots = new List<SurfaceSlot>();
        var bg = HousingLookup.FindByPath(position, modelPath);
        if (bg is null)
            bg = LayoutLookup.FindByPath(position, modelPath);

        if (bg is null)
        {
            report = $"Could not find '{modelPath}' near the anchor.";
            return slots;
        }

        var model = bg->ModelResourceHandle;
        if (!SafeMemory.CanRead<ModelResourceHandle>(model))
        {
            report = $"Found '{modelPath}' but its model is not loaded yet.";
            return slots;
        }

        for (var m = 0; m < MaxMaterials; m++)
        {
            if (!SafeMemory.IsReadable(model->MaterialResourceHandles + m, sizeof(nint)))
                break;

            var material = model->MaterialResourceHandles[m];
            if (!IsPlausibleMaterial(material))
                break;

            var textures = material->TexturesSpan;
            for (var t = 0; t < textures.Length; t++)
            {
                var handle = textures[t].TextureResourceHandle;
                if (!SafeMemory.CanRead<TextureResourceHandle>(handle))
                    continue;

                var texture = handle->Texture;
                if (!SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture>(texture))
                    continue;
                slots.Add(new SurfaceSlot(
                    m,
                    t,
                    modelPath,
                    material->FileName.ToString(),
                    handle->FileName.ToString(),
                    (int)texture->ActualWidth,
                    (int)texture->ActualHeight));
            }
        }

        report = slots.Count > 0
            ? $"{slots.Count} textures on {modelPath}"
            : $"'{modelPath}' is loaded but none of its materials were readable.";

        return slots;
    }

    /// <summary>
    /// Guards the uncounted material array. A real entry points at a loaded material with a sane
    /// texture count; anything else means we have walked off the end.
    /// </summary>
    private static bool IsPlausibleMaterial(MaterialResourceHandle* material)
    {
        // The array this comes from carries no count, so the pointer is checked with the kernel
        // before it is followed rather than merely looking sensible. Walking off the end of an
        // uncounted array is otherwise a crash, not an error.
        if (!SafeMemory.CanRead<MaterialResourceHandle>(material))
            return false;

        return material->TextureCount is > 0 and <= 16;
    }

    public void Bind(SurfaceSlot slot)
    {
        // Put every previously painted texture back BEFORE adopting a new one. Dropping the old
        // binding without restoring it is what left abandoned materials pointing at our view.
        this.RestoreAll();
        this.Bound = slot;
        this.Error = null;
    }

    /// <summary>
    /// Stops painting and puts the object's own texture back. Pass the object so the original can
    /// actually be restored; without it the surface would keep showing the last video frame until
    /// the game next reloaded it.
    /// </summary>
    public void Unbind(Vector3 position)
    {
        this.RestoreAll();
        this.Bound = null;
        this.Error = null;
    }

    /// <summary>
    /// Re-applies the swap for this frame. Render thread only.
    /// </summary>
    public void Apply(Vector3 position, IFrameUploader? uploader)
    {
        if (this.disposed || this.Bound is not { } slot)
            return;

        if (uploader is null || !uploader.HasFrame)
        {
            this.IsApplied = false;
            return;
        }

        if (!uploader.HasStableHandle)
        {
            // Lending the game a texture that is replaced every frame would leave it drawing from
            // freed memory within one frame.
            this.Error = "This graphics path allocates a new texture per frame and cannot be "
                + "painted onto an object. Enable the dynamic texture path.";
            this.IsApplied = false;
            return;
        }

        var target = Resolve(position, slot);
        if (target is null)
        {
            // Object streamed out. The bookkeeping is deliberately left alone: that slot may still
            // hold our view, and forgetting its original is precisely what makes it unrestorable.
            this.IsApplied = false;
            return;
        }

        var ours = (nint)uploader.Handle.Handle;
        if (ours == 0)
        {
            this.IsApplied = false;
            return;
        }

        var current = (nint)target->D3D11ShaderResourceView;
        this.Track((nint)target, ours, current);

        if (current != ours)
            target->D3D11ShaderResourceView = (void*)ours;

        this.IsApplied = true;
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.RestoreAll();
    }

    /// <summary>
    /// Walks from the object to the exact texture the slot names. Re-walked every frame rather than
    /// cached: the pointers are the game's, and it frees them whenever an object streams out.
    /// </summary>
    private static FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture* Resolve(
        Vector3 position,
        SurfaceSlot slot)
    {
        // A VFX texture is not reached through a model at all — it is a loaded resource, found by
        // path. Everything downstream is identical, because both end at the same Kernel.Texture.
        if (slot.ModelPath.StartsWith(VfxLookup.Prefix, StringComparison.Ordinal))
            return VfxLookup.FindByPath(slot.ModelPath[VfxLookup.Prefix.Length..]);

        var bg = HousingLookup.FindByPath(position, slot.ModelPath);
        if (bg is null)
            bg = LayoutLookup.FindByPath(position, slot.ModelPath);

        if (bg is null)
            return null;

        var model = bg->ModelResourceHandle;
        if (!SafeMemory.CanRead<ModelResourceHandle>(model) || slot.MaterialIndex is < 0 or >= MaxMaterials)
            return null;

        if (!SafeMemory.IsReadable(model->MaterialResourceHandles + slot.MaterialIndex, sizeof(nint)))
            return null;

        var material = model->MaterialResourceHandles[slot.MaterialIndex];
        if (!IsPlausibleMaterial(material))
            return null;

        var textures = material->TexturesSpan;
        if (slot.TextureIndex < 0 || slot.TextureIndex >= textures.Length)
            return null;

        var handle = textures[slot.TextureIndex].TextureResourceHandle;
        if (!SafeMemory.CanRead<TextureResourceHandle>(handle))
            return null;

        var texture = handle->Texture;
        return SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture>(texture)
            ? texture
            : null;
    }

    /// <summary>
    /// Records a texture we are about to paint, taking exactly one reference per slot.
    /// <para>
    /// That reference is never given back. Every crash this produced was a game material still
    /// pointing at a view whose last reference we had dropped; holding one for the life of the
    /// process turns any bookkeeping mistake into a frozen picture and a few leaked megabytes
    /// instead of an access violation on a driver thread.
    /// </para>
    /// </summary>
    private void Track(nint target, nint ours, nint current)
    {
        foreach (var existing in this.painted)
        {
            if (existing.Target != target || existing.Ours != ours)
                continue;

            // Fill in an original we could not see the first time round.
            if (existing.Original == 0 && current != 0 && current != ours)
                existing.Original = current;

            return;
        }

        D3D11.AddRef(ours);
        this.painted.Add(new PaintedSlot
        {
            Target = target,
            Ours = ours,
            Original = current != ours ? current : 0,
        });

        log.Information(
            $"[surface] painted target=0x{target:X} ours=0x{ours:X} original=0x{current:X}, " +
            $"{this.painted.Count} slot(s) tracked");
    }

    /// <summary>
    /// Puts the game's own view back into every texture we painted, newest first so a texture
    /// painted more than once ends on its true original. Nothing is ever released.
    /// </summary>
    private void RestoreAll()
    {
        for (var i = this.painted.Count - 1; i >= 0; i--)
        {
            var entry = this.painted[i];
            var target = (FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture*)entry.Target;

            if (entry.Original != 0
                && SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture>(target)
                && (nint)target->D3D11ShaderResourceView == entry.Ours)
            {
                target->D3D11ShaderResourceView = (void*)entry.Original;
            }
        }

        this.painted.Clear();
        this.IsApplied = false;
    }
}
