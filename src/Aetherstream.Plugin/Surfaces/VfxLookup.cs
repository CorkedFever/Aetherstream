using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace Aetherstream.Plugin.Surfaces;

/// <summary>
/// Finds the textures a visual effect draws with.
/// <para>
/// Some furnishings render their screen as a VFX rather than as a model surface — the Everkeep
/// Monitor is one: its only model is the base, and the lit panel is an effect. A VFX samples its
/// own textures (.atex) instead of a material's, so the model walk finds nothing to swap. Those
/// textures are ordinary loaded resources though, and an ApricotTextureResourceHandle points at
/// the same kind of Kernel.Texture a material does — so once found, the swap is identical.
/// </para>
/// <para>
/// They are located by walking every loaded resource rather than by walking down from the object:
/// the effect's own structures do not expose their texture list, but the resource graph knows
/// everything the game currently has open.
/// </para>
/// </summary>
internal static unsafe class VfxLookup
{
    /// <summary>Marks a surface path as a VFX texture rather than a model material.</summary>
    public const string Prefix = "atex:";

    /// <summary>One effect texture that could carry a picture.</summary>
    public readonly record struct Effect(string Path, int Width, int Height);

    /// <summary>
    /// Lists loaded effect textures whose path contains <paramref name="filter"/>.
    /// <para>
    /// A filter is required in practice: the game keeps thousands of these open, and the useful one
    /// is identified by the furnishing's id — the Everkeep Monitor's effect is igene_1604_c1.avfx,
    /// so "1604" narrows it to that piece of furniture.
    /// </para>
    /// </summary>
    public static List<Effect> List(string filter, int limit = 200)
    {
        var found = new List<Effect>();

        var manager = ResourceManager.Instance();
        if (!SafeMemory.CanRead<ResourceManager>(manager))
            return found;

        var graph = manager->ResourceGraph;
        if (!SafeMemory.CanRead<ResourceGraph>(graph))
            return found;

        foreach (var container in graph->Containers)
        {
            var map = container.MainMap;
            if (map is null)
                continue;

            // The category container holds a map of maps: an outer map keyed by resource type,
            // each pointing at the handles of that type.
            foreach (var byType in *map)
            {
                var inner = byType.Item2.Value;
                if (inner is null)
                    continue;

            foreach (var entry in *inner)
            {
                if (found.Count >= limit)
                    return found;

                var handle = entry.Item2.Value;
                if (!SafeMemory.CanRead<ResourceHandle>(handle))
                    continue;

                var path = handle->FileName.ToString();
                if (!path.EndsWith(".atex", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (filter.Length > 0 && !path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var texture = ((ApricotTextureResourceHandle*)handle)->Texture;
                if (!SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture>(texture))
                    continue;

                found.Add(new Effect(path, (int)texture->ActualWidth, (int)texture->ActualHeight));
            }
            }
        }

        return found;
    }

    /// <summary>Resolves an effect texture by path. Re-walked each frame, like the model path is.</summary>
    public static FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture* FindByPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var manager = ResourceManager.Instance();
        if (!SafeMemory.CanRead<ResourceManager>(manager))
            return null;

        var graph = manager->ResourceGraph;
        if (!SafeMemory.CanRead<ResourceGraph>(graph))
            return null;

        foreach (var container in graph->Containers)
        {
            var map = container.MainMap;
            if (map is null)
                continue;

            foreach (var byType in *map)
            {
                var inner = byType.Item2.Value;
                if (inner is null)
                    continue;

            foreach (var entry in *inner)
            {
                var handle = entry.Item2.Value;
                if (!SafeMemory.CanRead<ResourceHandle>(handle))
                    continue;

                if (!handle->FileName.ToString().Equals(path, StringComparison.OrdinalIgnoreCase))
                    continue;

                var texture = ((ApricotTextureResourceHandle*)handle)->Texture;
                return SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture>(texture)
                    ? texture
                    : null;
            }
            }
        }

        return null;
    }
}
