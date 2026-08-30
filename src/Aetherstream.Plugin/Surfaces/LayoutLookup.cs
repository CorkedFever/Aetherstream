using System.Numerics;

using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Node;

namespace Aetherstream.Plugin.Surfaces;

/// <summary>
/// Finds the drawable behind a piece of housing furniture.
/// <para>
/// Furnishings are not drawn as game objects — their entry in the object table exists for
/// targeting and has no draw object at all. The model belongs to the layout engine, which keeps
/// every placed instance in the active layout. So the route is: ask the layout for its indoor and
/// outdoor object instances, find the one standing where the furniture is, and take its graphics.
/// </para>
/// </summary>
internal static unsafe class LayoutLookup
{
    /// <summary>How far from the recorded position an instance may be and still be the same thing.</summary>
    private const float MatchRadius = 5f;

    /// <summary>What the search saw, so a failure says why instead of just coming back empty.</summary>
    public sealed class Diagnostics
    {
        public bool LayoutAvailable { get; set; }

        public int TypesScanned { get; set; }

        public int InstancesScanned { get; set; }

        public int WithGraphics { get; set; }

        public float NearestDistance { get; set; } = float.MaxValue;

        public string NearestPath { get; set; } = string.Empty;

        public InstanceType NearestType { get; set; }

        public override string ToString()
        {
            if (!this.LayoutAvailable)
                return "No active layout — the game has no zone layout loaded.";

            if (this.InstancesScanned == 0)
                return $"Layout has no instances in {this.TypesScanned} searched types.";

            var nearest = this.NearestDistance is float.MaxValue
                ? "none had graphics"
                : $"nearest {this.NearestDistance:F2}y ({this.NearestType}) {this.NearestPath}";

            return $"{this.InstancesScanned} instances across {this.TypesScanned} types, " +
                $"{this.WithGraphics} drawable; {nearest}.";
        }
    }

    /// <summary>
    /// Returns the background object standing nearest <paramref name="position"/>, or null when the
    /// layout has nothing there.
    /// <para>
    /// Every instance type is searched rather than a guessed shortlist. Furniture is expected under
    /// the indoor and outdoor object types, but a wrong guess there is indistinguishable from "not
    /// found", and that ambiguity has already cost a round trip.
    /// </para>
    /// </summary>
    /// <summary>One placed piece of the layout, named by its model so it can be told apart.</summary>
    public readonly record struct Placed(string Path, float Distance, InstanceType Type);

    /// <summary>
    /// Lists what is placed around a point, nearest first. "Nearest" alone is not enough to pick
    /// the right object — a monitor against a wall is metres from the wall itself, and the wall may
    /// well win — so the choice belongs to whoever can see the room.
    /// </summary>
    public static List<Placed> ListNearby(Vector3 position, float radius)
    {
        var found = new List<Placed>();

        foreach (var layoutPtr in AllLayouts())
        {
        var layout = (LayoutManager*)layoutPtr;
        foreach (var pair in layout->InstancesByType)
        {
            var pool = pair.Item2.Value;
            if (pool is null)
                continue;

            foreach (var entry in *pool)
            {
                var instance = entry.Item2.Value;
                if (!SafeMemory.CanRead<ILayoutInstance>(instance))
                    continue;

                Vector3 translation;
                instance->GetTranslation(&translation);

                var d = Vector3.Distance(translation, position);
                if (d > radius)
                    continue;

                // Furniture is placed as a shared group: a container with no graphics of its own,
                // whose children hold the actual models. Skipping anything without graphics — as
                // this did — therefore skipped every furnishing in the room and left only the
                // building itself.
                if (pair.Item1 == InstanceType.SharedGroup)
                {
                    CollectChildren(instance, position, d, found);
                    continue;
                }

                if (instance->GetGraphics() is null)
                    continue;

                var path = instance->GetPrimaryPath().ToString();
                if (path.Length == 0)
                    continue;

                found.Add(new Placed(path, d, pair.Item1));
            }
        }
        }

        // Same model placed twice nearby is genuinely ambiguous; keep the closer one.
        return found
            .GroupBy(p => p.Path)
            .Select(g => g.OrderBy(p => p.Distance).First())
            .OrderBy(p => p.Distance)
            .ToList();
    }

    /// <summary>
    /// Adds the drawable children of a shared group. One level deep: furnishings are a group of
    /// parts, not a tree of groups.
    /// </summary>
    private static void CollectChildren(
        ILayoutInstance* group,
        Vector3 position,
        float groupDistance,
        List<Placed> into)
    {
        var shared = (SharedGroupLayoutInstance*)group;
        if (!SafeMemory.CanRead<SharedGroupLayoutInstance>(shared))
            return;

        foreach (var node in shared->Instances.Instances)
        {
            var child = node.Value;
            if (!SafeMemory.CanRead<ChildNodeInstance>(child))
                continue;

            var instance = child->Instance;
            if (!SafeMemory.CanRead<ILayoutInstance>(instance))
                continue;

            if (instance->GetGraphics() is null)
                continue;

            var path = instance->GetPrimaryPath().ToString();
            if (path.Length == 0)
                continue;

            into.Add(new Placed(path, groupDistance, InstanceType.SharedGroup));
        }
    }

    /// <summary>
    /// Finds the drawable for a specific model path, nearest to a point. This is what a chosen
    /// binding resolves through, so it keeps pointing at the object the user picked rather than
    /// whatever happens to be closest that frame.
    /// </summary>
    public static BgObject* FindByPath(Vector3 position, string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        BgObject* best = null;
        var bestDistance = float.MaxValue;

        foreach (var layoutPtr in AllLayouts())
        {
        var layout = (LayoutManager*)layoutPtr;
        foreach (var pair in layout->InstancesByType)
        {
            var pool = pair.Item2.Value;
            if (pool is null)
                continue;

            foreach (var entry in *pool)
            {
                var instance = entry.Item2.Value;
                if (!SafeMemory.CanRead<ILayoutInstance>(instance))
                    continue;

                Vector3 translation;
                instance->GetTranslation(&translation);

                var d = Vector3.Distance(translation, position);
                if (d >= bestDistance || d > 40f)
                    continue;

                // Shared groups carry furnishings in their children, so the match has to look
                // inside them — the group itself has no model to compare against.
                if (pair.Item1 == InstanceType.SharedGroup)
                {
                    var fromChild = FindInChildren(instance, path);
                    if (fromChild is not null)
                    {
                        bestDistance = d;
                        best = fromChild;
                    }

                    continue;
                }

                var graphics = instance->GetGraphics();
                if (!SafeMemory.CanRead<BgObject>(graphics))
                    continue;

                if (!instance->GetPrimaryPath().ToString().Equals(path, StringComparison.OrdinalIgnoreCase))
                    continue;

                bestDistance = d;
                best = (BgObject*)graphics;
            }
        }
        }

        return best;
    }

    /// <summary>
    /// Collects the drawables of the layout instance with a given id, descending into shared groups.
    /// This is the exact lookup — no distances, no nearest-wins — for when the caller already knows
    /// which instance it wants.
    /// </summary>
    public static void CollectById(uint layoutId, List<Placed> into)
    {
        foreach (var layoutPtr in AllLayouts())
        {
        var layout = (LayoutManager*)layoutPtr;
        foreach (var pair in layout->InstancesByType)
        {
            var pool = pair.Item2.Value;
            if (pool is null)
                continue;

            foreach (var entry in *pool)
            {
                var instance = entry.Item2.Value;
                if (!SafeMemory.CanRead<ILayoutInstance>(instance))
                    continue;

                if (instance->Id.InstanceKey != layoutId)
                    continue;

                if (pair.Item1 == InstanceType.SharedGroup)
                {
                    CollectChildren(instance, default, 0f, into);
                    continue;
                }

                if (instance->GetGraphics() is null)
                    continue;

                var path = instance->GetPrimaryPath().ToString();
                if (path.Length > 0)
                    into.Add(new Placed(path, 0f, pair.Item1));
            }
        }
        }
    }

    /// <summary>
    /// Every layout the world currently holds, not just the active one.
    /// <para>
    /// A house keeps its shell and its contents in different layouts: ActiveLayout held only
    /// bg/…/bgparts — the building — which is why searching it returned walls and pillars and never
    /// a single furnishing. Furniture models live under bgcommon/hou, in another loaded layout.
    /// </para>
    /// </summary>
    private static List<nint> AllLayouts()
    {
        // Built eagerly rather than yielded: an iterator cannot hold pointers.
        var layouts = new List<nint>();

        var world = LayoutWorld.Instance();
        if (world is null)
            return layouts;

        void Add(nint candidate)
        {
            if (candidate != 0 && !layouts.Contains(candidate) && SafeMemory.IsReadable(candidate, sizeof(nint)))
                layouts.Add(candidate);
        }

        Add((nint)world->ActiveLayout);
        Add((nint)world->GlobalLayout);

        foreach (var entry in world->LoadedLayouts)
            Add((nint)entry.Item2.Value);

        return layouts;
    }

    /// <summary>
    /// Dumps what the layout actually contains, with no distance or graphics filtering at all.
    /// <para>
    /// Every "nothing found" so far has been read as "the furniture is somewhere else", and each
    /// time that led to another guess. This answers the prior question instead: is the layout being
    /// walked correctly in the first place? A room with one instance in it is not a room.
    /// </para>
    /// </summary>
    public static void DumpLayout(Vector3 position, Action<string> write)
    {
        var totalTypes = 0;
        var totalInstances = 0;

        foreach (var layoutPtr in AllLayouts())
        {
        var layout = (LayoutManager*)layoutPtr;
        write($"[layout] === layout 0x{layoutPtr:X} terrain {layout->TerritoryTypeId} init {layout->InitState}");
        foreach (var pair in layout->InstancesByType)
        {
            var pool = pair.Item2.Value;
            if (pool is null)
            {
                write($"[layout]   {pair.Item1}: pool null");
                continue;
            }

            totalTypes++;

            var count = 0;
            var drawable = 0;
            var nearest = float.MaxValue;

            foreach (var entry in *pool)
            {
                var instance = entry.Item2.Value;
                if (!SafeMemory.CanRead<ILayoutInstance>(instance))
                    continue;

                count++;

                Vector3 translation;
                instance->GetTranslation(&translation);
                nearest = MathF.Min(nearest, Vector3.Distance(translation, position));

                if (instance->GetGraphics() is not null)
                    drawable++;
            }

            totalInstances += count;
            if (count > 0)
            {
                write($"[layout]   {pair.Item1}: {count} instances, {drawable} drawable, " +
                    $"nearest {nearest:F1}y");
            }
        }

        }

        write($"[layout] total {totalInstances} instances across {totalTypes} type-pools");
    }

    /// <summary>The drawable child of a shared group matching a model path, if it has one.</summary>
    private static BgObject* FindInChildren(ILayoutInstance* group, string path)
    {
        var shared = (SharedGroupLayoutInstance*)group;
        if (!SafeMemory.CanRead<SharedGroupLayoutInstance>(shared))
            return null;

        foreach (var node in shared->Instances.Instances)
        {
            var child = node.Value;
            if (!SafeMemory.CanRead<ChildNodeInstance>(child))
                continue;

            var instance = child->Instance;
            if (!SafeMemory.CanRead<ILayoutInstance>(instance))
                continue;

            var graphics = instance->GetGraphics();
            if (!SafeMemory.CanRead<BgObject>(graphics))
                continue;

            if (instance->GetPrimaryPath().ToString().Equals(path, StringComparison.OrdinalIgnoreCase))
                return (BgObject*)graphics;
        }

        return null;
    }

    public static BgObject* FindNearest(Vector3 position, Diagnostics? diagnostics = null)
    {
        var report = diagnostics ?? new Diagnostics();

        var world = LayoutWorld.Instance();
        if (world is null)
            return null;

        var layout = world->ActiveLayout;
        if (layout is null)
            return null;

        report.LayoutAvailable = true;

        BgObject* best = null;
        var bestDistance = float.MaxValue;

        foreach (var pair in layout->InstancesByType)
        {
            var pool = pair.Item2.Value;
            if (pool is null)
                continue;

            report.TypesScanned++;

            foreach (var entry in *pool)
            {
                var instance = entry.Item2.Value;
                if (instance is null)
                    continue;

                report.InstancesScanned++;

                Vector3 translation;
                instance->GetTranslation(&translation);

                var d = Vector3.Distance(translation, position);
                if (d > MatchRadius)
                    continue;

                var graphics = instance->GetGraphics();
                if (graphics is null)
                    continue;

                report.WithGraphics++;
                if (d >= bestDistance)
                    continue;

                bestDistance = d;
                best = (BgObject*)graphics;
                report.NearestDistance = d;
                report.NearestType = pair.Item1;
                report.NearestPath = instance->GetPrimaryPath().ToString();
            }
        }

        return best;
    }
}
