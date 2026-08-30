using System.Numerics;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace Aetherstream.Plugin.Surfaces;

/// <summary>
/// Finds the drawable behind a piece of player-placed furniture.
/// <para>
/// Furnishings are not in the zone layout — that holds the building itself, which is why searching
/// it only ever turned up walls and pillars. Placed furniture belongs to the housing furniture
/// manager, as its own array of game objects, and those do carry draw objects.
/// </para>
/// </summary>
internal static unsafe class HousingLookup
{
    /// <summary>One placed furnishing, named by the model it draws.</summary>
    public readonly record struct Placed(string Path, float Distance, nint DrawObject);

    /// <summary>
    /// Bridges the two systems that describe one furnishing.
    /// <para>
    /// The object table entry you can target and the renderable instance in the layout are separate
    /// things, and matching them by position is guesswork — a monitor is metres from the wall, and
    /// the wall wins. The object carries a LayoutId, which is the game's own explicit link between
    /// the two, so it is used instead.
    /// </para>
    /// </summary>
    public static List<LayoutLookup.Placed> FromObject(Dalamud.Game.ClientState.Objects.Types.IGameObject? subject)
    {
        var found = new List<LayoutLookup.Placed>();
        if (subject is null || !subject.IsValid())
            return found;

        var housing = (FFXIVClientStructs.FFXIV.Client.Game.Object.HousingObject*)subject.Address;
        if (!SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Game.Object.HousingObject>(housing))
            return found;

        var layoutId = housing->LayoutId;
        if (layoutId == 0)
            return found;

        LayoutLookup.CollectById(layoutId, found);
        return found;
    }

    /// <summary>The layout id an object claims, or 0 when it has none. Diagnostics.</summary>
    public static uint LayoutIdOf(Dalamud.Game.ClientState.Objects.Types.IGameObject? subject)
    {
        if (subject is null || !subject.IsValid())
            return 0;

        var housing = (FFXIVClientStructs.FFXIV.Client.Game.Object.HousingObject*)subject.Address;
        return SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Game.Object.HousingObject>(housing)
            ? housing->LayoutId
            : 0;
    }

    /// <summary>Lists placed furniture within <paramref name="radius"/>, nearest first.</summary>
    public static List<Placed> ListNearby(Vector3 position, float radius)
    {
        var found = new List<Placed>();

        var manager = HousingManager.Instance();
        if (!SafeMemory.CanRead<HousingManager>(manager))
            return found;

        var furniture = manager->GetFurnitureManager();
        if (!SafeMemory.CanRead<HousingFurnitureManager>(furniture))
            return found;

        var objects = furniture->ObjectManager.ObjectArray.Objects;

        for (var i = 0; i < objects.Length; i++)
        {
            var obj = objects[i].Value;
            if (!SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject>(obj))
                continue;

            var distance = Vector3.Distance(obj->Position, position);
            if (distance > radius)
                continue;

            var draw = obj->DrawObject;
            if (!SafeMemory.CanRead<BgObject>(draw))
                continue;

            var path = ModelPath((BgObject*)draw);
            if (path.Length == 0)
                continue;

            found.Add(new Placed(path, distance, (nint)draw));
        }

        found.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return found;
    }

    /// <summary>
    /// Finds the furnishing drawing <paramref name="modelPath"/>, nearest to a point. Bindings
    /// resolve through this every frame, so they follow the object that was chosen rather than
    /// whatever happens to be closest.
    /// </summary>
    public static BgObject* FindByPath(Vector3 position, string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
            return null;

        var manager = HousingManager.Instance();
        if (!SafeMemory.CanRead<HousingManager>(manager))
            return null;

        var furniture = manager->GetFurnitureManager();
        if (!SafeMemory.CanRead<HousingFurnitureManager>(furniture))
            return null;

        var objects = furniture->ObjectManager.ObjectArray.Objects;

        BgObject* best = null;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < objects.Length; i++)
        {
            var obj = objects[i].Value;
            if (!SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject>(obj))
                continue;

            var distance = Vector3.Distance(obj->Position, position);
            if (distance >= bestDistance || distance > 40f)
                continue;

            var draw = obj->DrawObject;
            if (!SafeMemory.CanRead<BgObject>(draw))
                continue;

            var bg = (BgObject*)draw;
            if (!ModelPath(bg).Equals(modelPath, StringComparison.OrdinalIgnoreCase))
                continue;

            bestDistance = distance;
            best = bg;
        }

        return best;
    }

    /// <summary>The model file a background object is drawing, or empty when it has none loaded.</summary>
    private static string ModelPath(BgObject* bg)
    {
        var model = bg->ModelResourceHandle;
        return SafeMemory.CanRead<FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ModelResourceHandle>(model)
            ? model->FileName.ToString()
            : string.Empty;
    }
}
