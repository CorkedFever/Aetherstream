using System.Text;

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace Aetherstream.Plugin.Surfaces;

/// <summary>
/// Reports the materials and textures behind a chosen object, and changes nothing.
/// <para>
/// This is the reconnaissance step for painting video onto a real surface. Doing that means
/// pointing a material's texture handle at a texture of ours, which is a write into the game's own
/// memory — so the traversal that finds the right handle is proven read-only first, against real
/// objects. A wrong pointer here is a crash to desktop, not an exception.
/// </para>
/// </summary>
internal sealed unsafe class SurfaceInspector(IPluginLog log)
{
    /// <summary>The last report, ready to show in the window.</summary>
    public string Report { get; private set; } = "Nothing inspected yet.";

    public void Inspect(IGameObject? subject)
    {
        var report = new StringBuilder();

        try
        {
            if (subject is null || !subject.IsValid())
            {
                this.Report = "No object to inspect. Attach the screen to something first.";
                return;
            }

            var name = subject.Name.TextValue;
            report.AppendLine($"{(name.Length > 0 ? name : "(unnamed)")} — kind {subject.ObjectKind}, data id {subject.DataId}");

            var gameObject = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)subject.Address;
            var draw = gameObject->DrawObject;
            if (draw is null)
            {
                this.Report = report.AppendLine("No draw object — nothing is being rendered for it.").ToString();
                return;
            }

            // The vtable identifies what kind of drawable this is. Reported as an offset from the
            // module base so it can be matched against a known type without a debugger attached.
            var moduleBase = System.Diagnostics.Process.GetCurrentProcess().MainModule?.BaseAddress ?? 0;
            var vtable = (nint)draw->VirtualTable;
            report.AppendLine($"DrawObject 0x{(nint)draw:X}, vtable ffxiv_dx11.exe+0x{vtable - moduleBase:X}");

            DescribeAsCharacterBase(report, (CharacterBase*)draw);
        }
        catch (Exception ex)
        {
            report.AppendLine($"Inspection failed: {ex.Message}");
            log.Error(ex, "Surface inspection failed.");
        }

        this.Report = report.ToString();
        log.Information(this.Report);
    }

    /// <summary>
    /// Reads the object as a CharacterBase, which is the layout that carries models and materials.
    /// Furnishings may not use it, so the counts are sanity-checked before anything is followed:
    /// an implausible slot count means this is some other type and the walk stops there.
    /// </summary>
    private static void DescribeAsCharacterBase(StringBuilder report, CharacterBase* characterBase)
    {
        var slotCount = characterBase->SlotCount;
        report.AppendLine($"SlotCount: {slotCount}");

        if (slotCount is <= 0 or > 64)
        {
            report.AppendLine("Not a CharacterBase layout — it keeps its materials somewhere else.");
            report.AppendLine("Send me the vtable offset above and I will find the right type.");
            return;
        }

        var models = characterBase->ModelsSpan;
        report.AppendLine($"Models: {models.Length}");

        var materials = characterBase->MaterialsSpan;
        report.AppendLine($"Materials: {materials.Length}");

        for (var i = 0; i < materials.Length; i++)
        {
            var material = materials[i];
            if (material.Value is null)
                continue;

            DescribeMaterial(report, i, material.Value);
        }
    }

    private static void DescribeMaterial(StringBuilder report, int index, MaterialResourceHandle* material)
    {
        report.AppendLine($"  material[{index}] {material->FileName}");
        report.AppendLine($"    textures: {material->TextureCount}");

        var textures = material->TexturesSpan;
        for (var t = 0; t < textures.Length; t++)
        {
            var entry = textures[t];
            if (entry.TextureResourceHandle is null)
                continue;

            var handle = entry.TextureResourceHandle;
            var texturePath = handle->FileName.ToString();
            var texture = handle->Texture;

            if (texture is null)
            {
                report.AppendLine($"      [{t}] {texturePath} (not resident)");
                continue;
            }

            report.AppendLine(
                $"      [{t}] {texturePath}");
            report.AppendLine(
                $"           {texture->ActualWidth}x{texture->ActualHeight} " +
                $"fmt {texture->TextureFormat} srv 0x{(nint)texture->D3D11ShaderResourceView:X}");
        }
    }
}
