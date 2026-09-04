using System.Numerics;

using Aetherstream.Playback;
using Aetherstream.Plugin.Surfaces;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Where the picture goes and what it looks like when it gets there.
/// <para>
/// The one decision that matters is at the top: a floating panel, or the object's own surface. Every
/// other control on this tab only applies to one of those two, so they are shown accordingly rather
/// than all at once.
/// </para>
/// </summary>
internal sealed class ScreenTab(UiContext ui)
{
    private float nearbyRange = 15f;
    private bool showUnnamed = true;
    private List<SurfaceSlot> surfaces = [];
    private bool surfacesScanned;
    private string surfaceReport = string.Empty;
    private List<LayoutLookup.Placed> placed = [];
    private float placedRange = 6f;
    private int furnitureCount;
    private List<VfxLookup.Effect> effects = [];
    private string effectFilter = "1604";

    public void Draw()
    {
        this.DrawMode();
        this.DrawKnownScreens();

        if (ui.Config.PaintOnSurface)
            this.DrawSurfaceWorkflow();
        else
            this.DrawPanelPlacement();

        this.DrawAppearance();
    }

    private void DrawMode()
    {
        Ui.Section("How it is drawn");

        var painting = ui.Config.PaintOnSurface;

        if (ImGui.RadioButton("Floating panel", !painting) && painting)
        {
            ui.Config.PaintOnSurface = false;
            ui.UnbindSurface();
            ui.SaveConfig();
        }

        Ui.Tip(
            "Drawn over the world after the game has finished its frame. It works anywhere, needs " +
            "no furniture, and has no depth — it covers your character and the walls.");

        ImGui.SameLine();
        if (ImGui.RadioButton("On a real surface", painting) && !painting)
        {
            ui.Config.PaintOnSurface = true;
            ui.SaveConfig();
        }

        Ui.Tip(
            "Hands the picture to the game's own renderer as a texture on an object. It is lit, " +
            "occluded and depth-sorted like anything else in the room — your character stands in " +
            "front of it properly.");
    }

    /// <summary>
    /// Furnishings with the setup already worked out. This is the way in for almost everyone: the
    /// scan-and-pick workflow underneath exists for finding a new screen, not for using a known one.
    /// </summary>
    private void DrawKnownScreens()
    {
        Ui.Section("Known screens");

        var current = KnownScreens.NameOf(
            ui.Config.SurfaceModelPath,
            ui.Config.SurfaceMaterialIndex,
            ui.Config.SurfaceTextureIndex);

        foreach (var (name, note, screen) in KnownScreens.All)
        {
            var active = ui.Config.PaintOnSurface && current == name;

            using (ImRaii.PushColor(ImGuiCol.Button, Theme.GlassLit, active)
                .Push(ImGuiCol.Border, Theme.Accent, active))
            {
                if (ImGui.Button(name, new Vector2(180, ImGui.GetFrameHeight() + 6)) && !active)
                    ui.ApplyScreen(screen);
            }

            Ui.Tip($"{note}\n\nStand next to the furnishing, then pick it: the picture goes on the nearest one.");

            if (active)
            {
                ImGui.SameLine();
                Ui.Dot(Theme.Good, "painting on it now");
                ImGui.SameLine();
                ImGui.TextColored(Theme.TextDim, "painting on it now");
            }
        }

        Ui.Hint("Stand next to it first — the picture lands on the nearest one.");
    }

    // -- Painted surface ---------------------------------------------------------------------------

    private void DrawSurfaceWorkflow()
    {
        if (ui.Config.SurfaceModelPath.Length > 0)
        {
            Ui.Section("Currently painting");

            var isEffect = ui.Config.SurfaceModelPath.StartsWith(VfxLookup.Prefix, StringComparison.Ordinal);
            Ui.Dot(Ui.Good, isEffect ? "an effect texture" : "a model surface");
            ImGui.SameLine();
            ImGui.TextColored(Ui.Accent, Path.GetFileName(ui.Config.SurfaceModelPath));
            Ui.Tip(ui.Config.SurfaceModelPath);

            ImGui.SameLine();
            if (Ui.IconButton(FontAwesomeIcon.Times, "Stop painting and put the surface back", "##clearsurface"))
            {
                ui.UnbindSurface();
                ui.Config.SurfaceModelPath = string.Empty;
                ui.Config.SurfaceMaterialIndex = -1;
                ui.Config.SurfaceTextureIndex = -1;
                ui.Config.PaintOnSurface = false;
                ui.SaveConfig();
            }
        }

        Ui.Section("1 · Find the object");
        this.DrawObjectScan();

        Ui.Section("2 · Pick its screen");
        this.DrawSurfacePicker();
        this.DrawEffects();

        if (ui.Config.SurfaceModelPath.Length > 0)
        {
            Ui.Section("3 · Fit the picture");
            this.DrawFit();
        }
    }

    private void DrawObjectScan()
    {
        // Searched around YOU, not around the anchor. Tying it to the anchor meant walking up to a
        // different object and still being shown whatever sat near the anchored one.
        var anchorPos = ui.Objects.LocalPlayer?.Position ?? ui.Config.Placement.AnchorPosition;

        Ui.Hint("Stand next to the thing you want to paint on, then scan.");

        ImGui.SetNextItemWidth(130);
        ImGui.SliderFloat("##range", ref this.placedRange, 1f, 25f, "%.0f yalms");

        ImGui.SameLine();
        if (ImGui.Button("Scan"))
            this.Scan(anchorPos);

        if (this.placed.Count > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(
                Ui.Faint,
                $"{this.furnitureCount} furnishings, {this.placed.Count - this.furnitureCount} room parts");
        }

        if (this.placed.Count == 0)
            return;

        // EndChild is required even when BeginChild returns false; ImRaii handles that, which is why
        // it is used here rather than the raw calls.
        using var child = ImRaii.Child("##placed", new Vector2(-1, 130), true);
        if (!child)
            return;

        foreach (var item in this.placed)
        {
            var selected = ui.Config.SurfaceModelPath == item.Path;
            var isEffect = item.Path.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase);
            var file = Path.GetFileName(item.Path);

            if (isEffect)
            {
                ImGui.TextColored(Ui.Warn, "fx");
                ImGui.SameLine();
            }

            if (ImGui.Selectable($"{file}  ·  {item.Distance:F1}y##{item.Path}", selected))
                this.Choose(item, anchorPos);

            Ui.Tip(item.Path);
        }
    }

    private void Scan(Vector3 anchorPos)
    {
        this.placed = LayoutLookup.ListNearby(anchorPos, this.placedRange);

        // Furniture models live under bgcommon/hou; the building is under bg/. Counting them apart
        // makes it obvious at a glance whether furnishings are being seen at all.
        this.furnitureCount = this.placed.Count(p =>
            p.Path.StartsWith("bgcommon/hou", StringComparison.OrdinalIgnoreCase));

        this.surfacesScanned = false;
        this.surfaces = [];

        ui.Log.Information(
            $"[scan] anchor {anchorPos.X:F1},{anchorPos.Y:F1},{anchorPos.Z:F1} " +
            $"range {this.placedRange:F0} — {this.furnitureCount} furnishings, " +
            $"{this.placed.Count - this.furnitureCount} layout parts. " +
            $"Currently painting: '{ui.Config.SurfaceModelPath}' " +
            $"mat {ui.Config.SurfaceMaterialIndex} tex {ui.Config.SurfaceTextureIndex}, " +
            $"enabled {ui.Config.PaintOnSurface}");

        if (ui.FindAnchor() is { } anchorObject)
        {
            ui.Log.Information(
                $"[scan] anchor object '{anchorObject.Name}' kind {anchorObject.ObjectKind} " +
                $"data {anchorObject.DataId} layoutId {HousingLookup.LayoutIdOf(anchorObject)}");
        }

        LayoutLookup.DumpLayout(anchorPos, line => ui.Log.Information(line));

        foreach (var item in this.placed.Take(20))
            ui.Log.Information($"[scan]   placed     {item.Distance:F1}y {item.Path}");
    }

    private void Choose(LayoutLookup.Placed item, Vector3 anchorPos)
    {
        if (item.Path.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase))
        {
            // An effect has no materials to read. Jump straight to its textures instead of walking a
            // model path that was never going to find anything: the file name is the filter that
            // narrows thousands of loaded textures down to this one furnishing's.
            this.effectFilter = DeriveEffectFilter(item.Path);
            this.effects = VfxLookup.List(this.effectFilter);
            this.surfaces = [];
            this.surfacesScanned = true;
            this.surfaceReport =
                $"{Path.GetFileName(item.Path)} is an effect, not a model — its " +
                $"{this.effects.Count} texture(s) are listed below.";

            ui.Log.Information($"[vfx] '{item.Path}' -> filter '{this.effectFilter}', {this.effects.Count} textures");
            return;
        }

        ui.Config.SurfaceModelPath = item.Path;
        ui.Config.SurfacePosition = anchorPos;
        this.surfaces = SurfaceBinding.Enumerate(anchorPos, item.Path, out this.surfaceReport);
        this.surfacesScanned = true;
        ui.SaveConfig();
    }

    private void DrawSurfacePicker()
    {
        // Deliberately not an early return: returning here also skipped the effect list below, which
        // made a screen drawn as an effect unreachable the moment you rescanned.
        if (!this.surfacesScanned)
        {
            Ui.Hint("Scan, then pick the object above.");
            return;
        }

        if (this.surfaces.Count == 0)
        {
            ImGui.TextColored(Ui.Warn, "No model surfaces on that one.");
            Ui.Hint(this.surfaceReport);

            if (ImGui.SmallButton("Copy this"))
                ImGui.SetClipboardText(this.surfaceReport);

            return;
        }

        Ui.Hint("Try them one at a time — the screen face is usually the largest.");

        using var child = ImRaii.Child("##surfacelist", new Vector2(-1, 130), true);
        if (!child)
            return;

        foreach (var slot in this.surfaces)
        {
            var selected = ui.Config.SurfaceMaterialIndex == slot.MaterialIndex
                && ui.Config.SurfaceTextureIndex == slot.TextureIndex;

            var name = Path.GetFileName(slot.TexturePath);
            if (name.Length == 0)
                name = "(no path)";

            if (ImGui.Selectable(
                $"{name}  ·  {slot.Width}x{slot.Height}##{slot.MaterialIndex}_{slot.TextureIndex}",
                selected))
            {
                ui.Config.SurfaceMaterialIndex = slot.MaterialIndex;
                ui.Config.SurfaceTextureIndex = slot.TextureIndex;
                ui.Config.SurfacePosition = ui.Objects.LocalPlayer?.Position ?? ui.Config.SurfacePosition;
                ui.Config.PaintOnSurface = true;
                ui.SaveConfig();
            }

            Ui.Tip(slot.TexturePath);
        }
    }

    /// <summary>
    /// Some furnishings draw their screen as a VFX rather than a model surface — the Everkeep
    /// Monitor's panel is one — and those sample .atex textures the model walk cannot see. They are
    /// found by name, since the effect does not publish its texture list.
    /// </summary>
    private void DrawEffects()
    {
        ImGui.Spacing();
        ImGui.TextColored(Ui.Faint, "Or an effect texture, for screens that glow");

        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##vfxfilter", "furnishing id, e.g. 1604", ref this.effectFilter, 64);
        Ui.Tip(
            "The monitor's effect is igene_1604_c1.avfx, so \"1604\" finds its textures.\n" +
            "Leave it empty to list everything loaded — there are thousands.");

        ImGui.SameLine();
        if (ImGui.Button("Find effects"))
        {
            this.effects = VfxLookup.List(this.effectFilter);
            ui.Log.Information($"[vfx] filter '{this.effectFilter}' matched {this.effects.Count} textures");

            foreach (var effect in this.effects.Take(30))
                ui.Log.Information($"[vfx]   {effect.Width}x{effect.Height} {effect.Path}");
        }

        if (this.effects.Count == 0)
            return;

        using var child = ImRaii.Child("##effectlist", new Vector2(-1, 130), true);
        if (!child)
            return;

        foreach (var effect in this.effects)
        {
            var tagged = VfxLookup.Prefix + effect.Path;
            var selected = ui.Config.SurfaceModelPath == tagged;

            if (ImGui.Selectable(
                $"{Path.GetFileName(effect.Path)}  ·  {effect.Width}x{effect.Height}##{effect.Path}",
                selected))
            {
                ui.Config.SurfaceModelPath = tagged;
                ui.Config.SurfaceMaterialIndex = 0;
                ui.Config.SurfaceTextureIndex = 0;
                ui.Config.PaintOnSurface = true;
                ui.SaveConfig();
            }

            ImGui.SameLine();

            var isMask = ui.Config.SurfaceMaskPath == tagged;
            if (ImGui.SmallButton($"{(isMask ? "unmask" : "mask")}##m{effect.Path}"))
            {
                ui.Config.SurfaceMaskPath = isMask ? string.Empty : tagged;
                ui.SaveConfig();
            }

            Ui.Tip(
                "Fill this texture with flat white instead of the picture. An effect's mask is what " +
                "fades the panel out and lets the wall through; whiting it out makes it solid.");
        }
    }

    /// <summary>
    /// A surface does not necessarily show its whole texture — the Everkeep Monitor's panel fades
    /// out toward the bottom, so anything filling the texture loses its lower edge.
    /// </summary>
    private void DrawFit()
    {
        var brightness = ui.Config.SurfaceBrightness;
        if (ImGui.SliderFloat("Brightness", ref brightness, 1f, 3f, "%.2fx"))
        {
            ui.Config.SurfaceBrightness = brightness;
            ui.SaveConfig();
        }

        Ui.Tip(
            "The picture is already forced fully opaque. If the surface still looks see-through it " +
            "blends additively — dark pixels stay transparent — and brightening is the only fix.\n\n" +
            "Recolouring the actual in-game wall behind the screen to black helps far more than " +
            "anything here.");

        var scaleX = ui.Config.FitScaleX;
        if (ImGui.SliderFloat("Width##fit", ref scaleX, 0.2f, 1f, "%.2f"))
        {
            ui.Config.FitScaleX = scaleX;
            ui.SaveConfig();
        }

        var scaleY = ui.Config.FitScaleY;
        if (ImGui.SliderFloat("Height##fit", ref scaleY, 0.2f, 1f, "%.2f"))
        {
            ui.Config.FitScaleY = scaleY;
            ui.SaveConfig();
        }

        Ui.Tip("Shrink the picture so it clears the part of the surface that fades out.");

        var offsetX = ui.Config.FitOffsetX;
        if (ImGui.SliderFloat("Left / right##fit", ref offsetX, -0.5f, 0.5f, "%.2f"))
        {
            ui.Config.FitOffsetX = offsetX;
            ui.SaveConfig();
        }

        var offsetY = ui.Config.FitOffsetY;
        if (ImGui.SliderFloat("Up / down##fit", ref offsetY, -0.5f, 0.5f, "%.2f"))
        {
            ui.Config.FitOffsetY = offsetY;
            ui.SaveConfig();
        }

        if (ImGui.SmallButton("Reset fit"))
        {
            ui.Config.FitScaleX = 1f;
            ui.Config.FitScaleY = 1f;
            ui.Config.FitOffsetX = 0f;
            ui.Config.FitOffsetY = 0f;
            ui.SaveConfig();
        }
    }

    // -- Floating panel ----------------------------------------------------------------------------

    private void DrawPanelPlacement()
    {
        var placement = ui.Config.Placement;

        Ui.Section("Where it sits");

        if (placement.IsAnchored)
        {
            Ui.Dot(Ui.Good, "following an object");
            ImGui.SameLine();
            ImGui.TextColored(
                Ui.Accent,
                placement.AnchorLabel.Length > 0 ? placement.AnchorLabel : "an object");

            ImGui.SameLine();
            if (Ui.IconButton(FontAwesomeIcon.Times, "Stop following it", "##unanchor"))
            {
                placement.AnchorObjectId = 0;
                placement.AnchorDataId = 0;
                placement.AnchorLabel = string.Empty;
                ui.SaveConfig();
            }
        }

        if (ImGui.Button(placement.IsAnchored ? "Follow my target instead" : "Attach to my target"))
        {
            if (ui.Targets.Target is { } target)
            {
                Bind(placement, target, target.Name.TextValue);
                ui.SaveConfig();
            }
        }

        Ui.Tip(
            "Attaches the screen to whatever you have targeted — a furnishing, an NPC, a friend. " +
            "Move the object and the screen goes with it.");

        ImGui.SameLine();
        if (ImGui.Button("Attach to me"))
        {
            if (ui.Objects.LocalPlayer is { } self)
            {
                Bind(placement, self, "you");

                // Following yourself needs completely different offsets from a wall mount. The
                // furnishing default sits centimetres off the surface, which with your own character
                // as the anchor means "inside your head" — so push it out to arm's length and shrink
                // it to something that does not swallow the view.
                placement.OffsetForward = 3.5f;
                placement.OffsetUp = 1.5f;
                placement.OffsetRight = 0f;
                placement.SetWidthKeepingAspect(3.2f);
                ui.SaveConfig();
            }
        }

        Ui.Tip("Needs no furniture and no housing rights, so it is the quickest way to try this anywhere.");

        if (!placement.IsAnchored)
        {
            ImGui.SameLine();
            if (ImGui.Button("Put it in front of me"))
                this.PlaceAhead(placement);
        }

        this.DrawNearbyPicker(placement);

        Ui.Section("Position");

        if (placement.IsAnchored)
        {
            this.Drag("Out from it", () => placement.OffsetForward, v => placement.OffsetForward = v, -10f, 10f);
            this.Drag("Up from it", () => placement.OffsetUp, v => placement.OffsetUp = v, -10f, 20f);
            this.Drag("Sideways", () => placement.OffsetRight, v => placement.OffsetRight = v, -20f, 20f);
            this.Drag("Turn", () => placement.AnchorYawOffset, v => placement.AnchorYawOffset = v, -180f, 180f, "%.0f°", 1f);
        }
        else
        {
            var position = placement.Position;
            if (ImGui.DragFloat3("Coordinates", ref position, 0.05f))
            {
                placement.Position = position;
                ui.SaveConfig();
            }

            this.Drag("Facing", () => placement.Yaw, v => placement.Yaw = v, -360f, 360f, "%.0f°", 1f);

            if (placement.TerritoryType != 0 && placement.TerritoryType != ui.ClientState.TerritoryType)
            {
                ImGui.TextColored(Ui.Warn, "This screen belongs to another zone, so it is hidden here.");
                if (ImGui.Button("Move it to this zone"))
                {
                    placement.TerritoryType = ui.ClientState.TerritoryType;
                    ui.SaveConfig();
                }
            }
        }

        Ui.Section("Size");

        var width = placement.Width;
        if (ImGui.DragFloat("Width", ref width, 0.1f, 0.5f, 60f, "%.1f yalms"))
        {
            placement.SetWidthKeepingAspect(Math.Clamp(width, 0.5f, 60f));
            ui.SaveConfig();
        }

        Ui.Tip("Height follows at 16:9 unless you set it yourself below.");

        var height = placement.Height;
        if (ImGui.DragFloat("Height", ref height, 0.1f, 0.3f, 40f, "%.1f yalms"))
        {
            placement.Height = Math.Clamp(height, 0.3f, 40f);
            ui.SaveConfig();
        }
    }

    private void PlaceAhead(ScreenPlacement placement)
    {
        if (ui.Objects.LocalPlayer is not { } player)
            return;

        // A couple of yalms ahead and lifted to eye level, so it lands somewhere visible rather than
        // inside the character.
        var facing = player.Rotation;
        var forward = new Vector3(MathF.Sin(facing), 0f, MathF.Cos(facing));

        placement.Position = player.Position + (forward * 3f) + new Vector3(0f, 1.2f, 0f);
        placement.Yaw = (facing * (180f / MathF.PI)) + 90f;
        placement.TerritoryType = ui.ClientState.TerritoryType;
        ui.SaveConfig();
    }

    private void Drag(
        string label,
        Func<float> get,
        Action<float> set,
        float min,
        float max,
        string format = "%.2f yalms",
        float speed = 0.02f)
    {
        var value = get();
        if (!ImGui.DragFloat(label, ref value, speed, min, max, format))
            return;

        set(value);
        ui.SaveConfig();
    }

    /// <summary>
    /// Furniture in someone else's house cannot be targeted at all, but it is still in the object
    /// table — so picking from a list works where clicking does not.
    /// </summary>
    private void DrawNearbyPicker(ScreenPlacement placement)
    {
        if (!ImGui.CollapsingHeader("Attach to something nearby"))
            return;

        if (ui.Objects.LocalPlayer is not { } player)
        {
            Ui.Hint("Not in the world.");
            return;
        }

        ImGui.SetNextItemWidth(130);
        ImGui.SliderFloat("##within", ref this.nearbyRange, 2f, 60f, "%.0f yalms");

        ImGui.SameLine();
        ImGui.Checkbox("Unnamed too", ref this.showUnnamed);
        Ui.Tip(
            "Furnishings often have no name in the object table. Turn this on to see them, then use " +
            "the distance to work out which is which — the outline moves as you pick.");

        var nearby = ui.Objects
            .Where(o => o.IsValid())
            .Select(o => (Object: o, Distance: Vector3.Distance(player.Position, o.Position)))
            .Where(x => x.Distance <= this.nearbyRange)
            .Where(x => this.showUnnamed || x.Object.Name.TextValue.Length > 0)
            .OrderBy(x => x.Distance)
            .Take(40)
            .ToList();

        if (nearby.Count == 0)
        {
            Ui.Hint("Nothing in range.");
            return;
        }

        using var child = ImRaii.Child("##nearby", new Vector2(-1, 160), true);
        if (!child)
            return;

        foreach (var (obj, distance) in nearby)
        {
            var name = obj.Name.TextValue;
            if (name.Length == 0)
                name = "(unnamed)";

            if (ImGui.Selectable(
                $"{name}  ·  {obj.ObjectKind}  ·  {distance:F1}y  ·  id {obj.DataId}##{obj.GameObjectId}",
                placement.AnchorObjectId == obj.GameObjectId))
            {
                Bind(placement, obj, name);
                ui.SaveConfig();
            }
        }
    }

    // -- Appearance --------------------------------------------------------------------------------

    private void DrawAppearance()
    {
        Ui.Section("Appearance");

        var visible = ui.Config.ScreenVisible;
        if (ImGui.Checkbox("Show the screen", ref visible))
        {
            ui.Config.ScreenVisible = visible;
            ui.SaveConfig();
        }

        if (ui.Config.PaintOnSurface)
            return;

        ImGui.SameLine();
        var outline = ui.Config.ShowOutline;
        if (ImGui.Checkbox("Outline it", ref outline))
        {
            ui.Config.ShowOutline = outline;
            ui.SaveConfig();
        }

        Ui.Tip("Useful while positioning, and shows where the screen is before it plays.");

        var opacity = ui.Config.Opacity;
        if (ImGui.SliderFloat("Opacity", ref opacity, 0.05f, 1f, "%.2f"))
        {
            ui.Config.Opacity = opacity;
            ui.SaveConfig();
        }

        var cutOut = ui.Config.CutOutCharacters;
        if (ImGui.Checkbox("Punch holes for characters", ref cutOut))
        {
            ui.Config.CutOutCharacters = cutOut;
            ui.SaveConfig();
        }

        Ui.Tip(
            "Not recommended. It deletes part of the picture where someone stands in front of it, " +
            "which reads as a ragged hole rather than as occlusion. Paint on a real surface instead " +
            "and the game handles it properly.");
    }

    /// <summary>
    /// Records everything needed to find this object again, including next session: the runtime
    /// handle for speed now, and the data id plus position for when that handle is stale.
    /// </summary>
    private static void Bind(
        ScreenPlacement placement,
        Dalamud.Game.ClientState.Objects.Types.IGameObject anchor,
        string label)
    {
        placement.AnchorObjectId = anchor.GameObjectId;
        placement.AnchorDataId = anchor.DataId;
        placement.AnchorPosition = anchor.Position;
        placement.AnchorLabel = label.Length > 0 ? label : $"object {anchor.DataId}";
    }

    /// <summary>
    /// Turns an effect's path into a filter that finds its textures. They are not named after the
    /// effect, but they share the furnishing's number — igene_1604_c1.avfx and its textures both
    /// carry 1604 — so the digits are what narrows the search.
    /// </summary>
    private static string DeriveEffectFilter(string avfxPath)
    {
        var name = Path.GetFileNameWithoutExtension(avfxPath);
        var digits = new string(name.Where(char.IsDigit).ToArray());
        return digits.Length >= 3 ? digits : name;
    }
}
