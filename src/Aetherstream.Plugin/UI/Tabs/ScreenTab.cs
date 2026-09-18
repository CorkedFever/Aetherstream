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

    /// <summary>Waiting for a click in the world, to pick what is under it.</summary>
    private bool picking;
    private Vector3? pickedPoint;
    private long pickedAtTicks;
    private string pickReport = string.Empty;
    private bool mouseWasDown;

    /// <summary>Words to narrow the placed list and the slot list by; a space separates several, any of which matches.</summary>
    private string placedFilter = string.Empty;
    private string slotFilter = string.Empty;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

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
                ui.Config.RememberSurface();
                ui.Config.SurfaceModelPath = string.Empty;
                ui.Config.SurfaceMaterialIndex = -1;
                ui.Config.SurfaceTextureIndex = -1;
                ui.Config.SurfaceMaskPath = string.Empty;
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

        Ui.Hint("Click the thing you want to paint on, or stand next to it and scan.");

        this.DrawClickToPick();

        // Up to two hundred yalms: a furnishing sits where it stands, but a part of the zone
        // itself, a stadium's scoreboard say, is placed by its model's origin, which can be
        // half the building away from the face you are looking at.
        ImGui.SetNextItemWidth(130);
        ImGui.SliderFloat("##range", ref this.placedRange, 1f, 200f, "%.0f yalms");
        Ui.Tip("How far around you to look. A furnishing is close; a part of the building or the zone can be placed far from where it appears, so widen this for those.");

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

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##placedfilter", "filter: scr, screen, board, fx…", ref this.placedFilter, 64);
        Ui.Tip("Narrows the list to names containing any of these words, separated by spaces. \"fx\" keeps only effects; \"hou\" only furnishings.");

        var shownPlaced = this.placed.Where(p => Matches(p.Path, this.placedFilter)).ToList();
        if (shownPlaced.Count < this.placed.Count)
            ImGui.TextColored(Ui.Faint, $"{shownPlaced.Count} of {this.placed.Count} shown");

        // EndChild is required even when BeginChild returns false; ImRaii handles that, which is why
        // it is used here rather than the raw calls.
        using var child = ImRaii.Child("##placed", new Vector2(-1, 130), true);
        if (!child)
            return;

        foreach (var item in shownPlaced)
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

    /// <summary>
    /// A button that arms a pick, and the pick itself: the next click that lands in the world,
    /// not on a window, is cast through the game's collision to a point, and what is placed
    /// around that point is listed nearest first with the nearest chosen. Aiming at the thing
    /// beats standing near it: a part of a zone is placed by its model's origin, which can be
    /// nowhere near where it appears, but the ray lands on the face itself.
    /// </summary>
    private void DrawClickToPick()
    {
        if (this.picking)
        {
            ImGui.TextColored(Ui.Accent, "Now click the thing in the world. Escape cancels.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Cancel##pick"))
                this.picking = false;

            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                this.picking = false;

            // The button is read from Windows itself rather than through ImGui, which only sees
            // the clicks the game lets it see; a press that lands in the world is the game's, and
            // ImGui may never hear of it. The position is ImGui's, which is in the same space
            // the game's screen-to-world expects.
            var down = (GetAsyncKeyState(0x01) & 0x8000) != 0;
            var clicked = down && !this.mouseWasDown;
            this.mouseWasDown = down;
            var overWindow = ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow) || ImGui.IsAnyItemHovered();
            if (clicked && !overWindow)
            {
                this.picking = false;
                var mouse = ImGui.GetMousePos();
                ui.Log.Information($"[pick] click seen at {mouse.X:F0},{mouse.Y:F0}");
                if (ui.GameGui.ScreenToWorld(mouse, out var hit))
                {
                    this.pickReport = $"Click at {mouse.X:F0},{mouse.Y:F0} landed at {hit.X:F1}, {hit.Y:F1}, {hit.Z:F1}.";
                    this.PickAt(hit);
                }
                else
                {
                    this.pickReport = $"Click at {mouse.X:F0},{mouse.Y:F0} did not land on anything solid. Aim at a wall or floor behind the thing.";
                    ui.Log.Information($"[pick] {this.pickReport}");
                }
            }
            else if (clicked)
            {
                this.pickReport = "That click was on a window, not the world. Still waiting.";
            }
        }
        else if (ImGui.Button("Click to pick"))
        {
            this.picking = true;
            this.mouseWasDown = true;
            this.pickReport = string.Empty;
            ui.Log.Information("[pick] armed");
        }

        Ui.Tip("Then click the screen, wall or sign in the world. Whatever is placed nearest to where the click lands is chosen, and the rest are listed under it to try instead.");

        if (this.pickReport.Length > 0)
            ImGui.TextColored(Ui.Faint, this.pickReport);

        // A ring where the click landed, for a few seconds, so a miss is visible as a miss.
        if (this.pickedPoint is { } point && Environment.TickCount64 - this.pickedAtTicks < 4000 && ui.GameGui.WorldToScreen(point, out var screen))
        {
            var draw = ImGui.GetForegroundDrawList();
            draw.AddCircle(screen, 14f, Theme.U32(Theme.Accent), 24, 2f);
            draw.AddCircleFilled(screen, 3f, Theme.U32(Theme.Accent), 12);
        }
    }

    private void PickAt(Vector3 hit)
    {
        this.pickedPoint = hit;
        this.pickedAtTicks = Environment.TickCount64;

        // Listed by distance from the click, not from the player: the thing under the cursor
        // is the one that was meant.
        this.placed = LayoutLookup.ListNearby(hit, 15f);
        this.furnitureCount = this.placed.Count(p => p.Path.StartsWith("bgcommon/hou", StringComparison.OrdinalIgnoreCase));
        this.surfaces = [];
        this.surfacesScanned = false;

        ui.Log.Information($"[pick] click landed at {hit.X:F1},{hit.Y:F1},{hit.Z:F1}: {this.placed.Count} placed within 15y");
        foreach (var item in this.placed.Take(30))
            ui.Log.Information($"[pick]   {item.Distance:F1}y {item.Path}");

        if (this.placed.Count == 0)
        {
            this.pickReport += " Nothing is placed within fifteen yalms of that point; try scanning with a wide range instead.";
            return;
        }

        this.pickReport += $" {this.placed.Count} placed nearby; chose {Path.GetFileName(this.placed[0].Path)}.";
        this.Choose(this.placed[0], hit);
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

        foreach (var item in this.placed.Take(60))
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

        if (!ui.Config.SurfaceModelPath.Equals(item.Path, StringComparison.OrdinalIgnoreCase))
        {
            ui.UnbindSurface();
            ui.Config.SwitchSurface(item.Path);
        }

        ui.Config.SurfacePosition = anchorPos;
        this.surfaces = SurfaceBinding.Enumerate(anchorPos, item.Path, out this.surfaceReport);
        this.surfacesScanned = true;
        ui.SaveConfig();

        // What the model carries, so the right slot can be worked out from the log when the
        // list on screen is not enough: a screen face is a large texture, a light strip is not.
        ui.Log.Information($"[surface] {this.surfaceReport}");
        foreach (var slot in this.surfaces)
            ui.Log.Information($"[surface]   mat {slot.MaterialIndex} tex {slot.TextureIndex} {slot.Width}x{slot.Height} {slot.TexturePath}");
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

        if (this.surfaces.Count > 6)
        {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##slotfilter", "filter the textures by name", ref this.slotFilter, 64);
        }

        var shownSlots = this.surfaces
            .Where(slot => Matches(slot.TexturePath, this.slotFilter))
            .OrderByDescending(slot => (long)slot.Width * slot.Height)
            .ToList();

        using var child = ImRaii.Child("##surfacelist", new Vector2(-1, 130), true);
        if (!child)
            return;

        foreach (var slot in shownSlots)
        {
            var selected = ui.Config.SurfaceMaterialIndex == slot.MaterialIndex
                && ui.Config.SurfaceTextureIndex == slot.TextureIndex;

            var name = Path.GetFileName(slot.TexturePath);
            if (name.Length == 0)
                name = "(no path)";

            var isMaskSlot = ui.Config.SurfaceMaskPath == slot.ModelPath
                && ui.Config.SurfaceMaskMaterialIndex == slot.MaterialIndex
                && ui.Config.SurfaceMaskTextureIndex == slot.TextureIndex;

            if (ImGui.SmallButton($"{(isMaskSlot ? "unmask" : "mask")}##ms{slot.MaterialIndex}_{slot.TextureIndex}"))
            {
                ui.Config.SurfaceMaskPath = isMaskSlot ? string.Empty : slot.ModelPath;
                ui.Config.SurfaceMaskMaterialIndex = slot.MaterialIndex;
                ui.Config.SurfaceMaskTextureIndex = slot.TextureIndex;
                if (!isMaskSlot)
                    ui.Config.MaskColour = 0xFF000000u;
                ui.SaveConfig();
            }

            Ui.Tip(
                "Fill this texture with a flat colour. Black on a shine map (_s) or a glow map takes " +
                "the shine off a screen so the picture reads; white on a mask makes a panel solid.");

            if (isMaskSlot)
            {
                ImGui.SameLine();
                this.DrawMaskColour();
            }

            ImGui.SameLine();
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
        if (ImGui.Button("Dump to log"))
        {
            VfxLookup.Dump(this.effectFilter, line => ui.Log.Information(line));
            this.surfaceReport = $"Everything loaded whose path contains '{this.effectFilter}' is in the Dalamud log, under [dump].";
        }

        Ui.Tip("Writes every loaded resource whose path contains the filter, any kind of file, to the Dalamud log, with a count of what is loaded by type. For when a screen's texture cannot be found by name.");

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
                if (!ui.Config.SurfaceModelPath.Equals(tagged, StringComparison.OrdinalIgnoreCase))
                {
                    ui.UnbindSurface();
                    ui.Config.SwitchSurface(tagged);
                }

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
                ui.Config.SurfaceMaskMaterialIndex = 0;
                ui.Config.SurfaceMaskTextureIndex = 0;
                ui.SaveConfig();
            }

            Ui.Tip(
                "Fill this texture with a flat colour instead of the picture. White makes a fading " +
                "panel solid; black switches off a glow or scanline layer drawn over the picture.");

            if (isMask)
            {
                ImGui.SameLine();
                this.DrawMaskColour();
            }
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

        ImGui.TextColored(Ui.Faint, "Turn");
        ImGui.SameLine();
        if (ImGui.SmallButton("<##turnleft"))
        {
            ui.Config.FitRotation = (ui.Config.FitRotation + 3) % 4;
            ui.SaveConfig();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(ui.Config.FitRotation switch { 1 => "90° right", 2 => "upside down", 3 => "90° left", _ => "upright" });
        ImGui.SameLine();
        if (ImGui.SmallButton(">##turnright"))
        {
            ui.Config.FitRotation = (ui.Config.FitRotation + 1) % 4;
            ui.SaveConfig();
        }

        Ui.Tip("For a surface that wraps the texture on its side or upside down: turn the picture until it reads.");

        ImGui.SameLine(0f, 16f);
        if (ImGui.SmallButton("Reset fit"))
        {
            ui.Config.FitScaleX = 1f;
            ui.Config.FitScaleY = 1f;
            ui.Config.FitOffsetX = 0f;
            ui.Config.FitOffsetY = 0f;
            ui.Config.FitRotation = 0;
            ui.SaveConfig();
        }

        this.DrawFitPreview();
    }

    /// <summary>
    /// The whole texture as the surface receives it, small: the picture inside it at the fit's
    /// size and place, black around. Moving a slider moves it here at once, so the fit can be
    /// worked out without craning at the furnishing.
    /// </summary>
    private void DrawFitPreview()
    {
        if (ui.Session.Uploader is not { HasFrame: true } uploader)
            return;

        ImGui.Spacing();
        ImGui.TextColored(Ui.Faint, "What the surface receives");
        var width = Math.Min(320f, ImGui.GetContentRegionAvail().X);
        var size = new Vector2(width, width * 9f / 16f);
        var origin = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + size, Theme.U32(Theme.Bezel));
        draw.AddImage(uploader.Handle, origin, origin + size);
        draw.AddRect(origin, origin + size, Theme.U32(Theme.GlassEdge));
        ImGui.Dummy(size);
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

        var card = ui.Config.IdleCard;
        if (ImGui.Checkbox("Test card when nothing's on", ref card))
        {
            ui.Config.IdleCard = card;
            ui.SaveConfig();
        }

        Ui.Tip("Colour bars, the mark and a clock, the way a set looks between programmes.");

        ImGui.SameLine();
        var retro = ui.Config.RetroMode;
        if (ImGui.Checkbox("Retro look", ref retro))
        {
            ui.Config.RetroMode = retro;
            ui.SaveConfig();
        }

        Ui.Tip("Scanlines and a soft vignette on every frame. Costs a couple of milliseconds a frame.");

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
    /// <summary>
    /// The filter that finds an effect's textures. Tried from the most specific: the effect's
    /// whole name, then the name without its last piece (x6t1_scrn1_y shares x6t1_scrn1 with its
    /// textures), then the digits alone, which is how a furnishing's effect names itself
    /// (igene_1604_c1 and its 1604 textures). The first that matches anything wins.
    /// </summary>
    /// <summary>
    /// Whether a path's file name contains any of the filter's words. "fx" and "hou" are read as
    /// kinds rather than words, since an effect's name rarely says "fx" and a furnishing's never
    /// says "hou" outside its folder.
    /// </summary>
    private static bool Matches(string path, string filter)
    {
        if (filter.Trim().Length == 0)
            return true;

        var name = Path.GetFileName(path);
        foreach (var word in filter.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (word.Equals("fx", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".avfx", StringComparison.OrdinalIgnoreCase))
                return true;
            if (word.Equals("hou", StringComparison.OrdinalIgnoreCase) && path.StartsWith("bgcommon/hou", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.Contains(word, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Black or white, for whichever texture the mask is on.</summary>
    private void DrawMaskColour()
    {
        var black = ui.Config.MaskColour == 0xFF000000u;
        if (ImGui.SmallButton(black ? "black##maskcolour" : "white##maskcolour"))
        {
            ui.Config.MaskColour = black ? 0xFFFFFFFFu : 0xFF000000u;
            ui.SaveConfig();
        }

        Ui.Tip("The colour the mask is filled with. Press to switch.");
    }

    private static string DeriveEffectFilter(string avfxPath)
    {
        // The name, then the name with pieces taken off its end one at a time: x6t1_scrn1_y,
        // x6t1_scrn1, x6t1. The last of those is the zone's own prefix, which matches every
        // texture the zone loaded; that is a long list, but it is the right list, and it sorts
        // biggest first. The digits alone come last, for furnishings, whose effect is named
        // by their number.
        var name = Path.GetFileNameWithoutExtension(avfxPath);
        var candidates = new List<string> { name };
        var trimmed = name;
        while (trimmed.LastIndexOf('_') is var cut && cut > 0)
        {
            trimmed = trimmed[..cut];
            candidates.Add(trimmed);
        }

        var digits = new string(name.Where(char.IsDigit).ToArray());
        if (digits.Length >= 3)
            candidates.Add(digits);

        foreach (var candidate in candidates)
        {
            if (candidate.Length >= 3 && VfxLookup.List(candidate, 1).Count > 0)
                return candidate;
        }

        return name;
    }
}
