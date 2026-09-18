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
    public static List<Effect> List(string filter, int limit = 300)
    {
        var found = new List<Effect>();
        var all = Gather(filter);

        // Biggest first: a screen's texture is large, the sparks and glows around it are tiny,
        // so when the filter is empty or vague the one worth trying is near the top.
        foreach (var effect in all.OrderByDescending(e => (long)e.Width * e.Height))
        {
            if (found.Count >= limit)
                break;
            found.Add(effect);
        }

        return found;
    }

    private static List<Effect> Gather(string filter)
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
            // Every map the category keeps, not only the main one: a zone's own resources,
            // the bg category's models, textures and effects, are filed under the zone's
            // number in the other maps, and the main map alone never sees them.
            foreach (var mapPointer in container.CategoryMaps)
            {
            var map = mapPointer.Value;
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
}

        return found;
    }

    /// <summary>
    /// Writes what the resource graph holds: a count per file extension, then every path
    /// containing the filter whatever its extension, with a texture's size where it has one.
    /// For when a screen's texture cannot be found by any name guessed for it.
    /// </summary>
    public static void Dump(string filter, Action<string> write, int limit = 400)
    {
        var manager = ResourceManager.Instance();
        if (!SafeMemory.CanRead<ResourceManager>(manager))
        {
            write("[dump] no resource manager");
            return;
        }

        var graph = manager->ResourceGraph;
        if (!SafeMemory.CanRead<ResourceGraph>(graph))
        {
            write("[dump] no resource graph");
            return;
        }

        var byExtension = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var matched = new List<string>();
        var total = 0;

        foreach (var container in graph->Containers)
        {
            // Every map the category keeps, not only the main one: a zone's own resources,
            // the bg category's models, textures and effects, are filed under the zone's
            // number in the other maps, and the main map alone never sees them.
            foreach (var mapPointer in container.CategoryMaps)
            {
            var map = mapPointer.Value;
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

                    var path = handle->FileName.ToString();
                    if (path.Length == 0)
                        continue;

                    total++;
                    var dot = path.LastIndexOf('.');
                    var ext = dot >= 0 ? path[dot..] : "(none)";
                    byExtension[ext] = byExtension.GetValueOrDefault(ext) + 1;

                    if (filter.Length > 0 && !path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (matched.Count >= limit)
                        continue;

                    var size = string.Empty;
                    if (ext.Equals(".atex", StringComparison.OrdinalIgnoreCase) || ext.Equals(".tex", StringComparison.OrdinalIgnoreCase))
                    {
                        var texture = ((TextureResourceHandle*)handle)->Texture;
                        if (SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture>(texture))
                            size = $" {texture->ActualWidth}x{texture->ActualHeight}";
                    }

                    matched.Add($"{path}{size}");
                }
            }
        
            }
}

        write($"[dump] {total} resources loaded; " + string.Join(", ", byExtension.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{kv.Key} {kv.Value}")));
        write($"[dump] {matched.Count} paths contain '{filter}'" + (matched.Count >= limit ? " (list capped)" : string.Empty));
        foreach (var line in matched)
            write("[dump]   " + line);
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
            // Every map the category keeps, not only the main one: a zone's own resources,
            // the bg category's models, textures and effects, are filed under the zone's
            // number in the other maps, and the main map alone never sees them.
            foreach (var mapPointer in container.CategoryMaps)
            {
            var map = mapPointer.Value;
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
}

        return null;
    }
}
