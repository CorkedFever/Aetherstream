using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

using Aetherstream.Plugin.Video;

namespace Aetherstream.Plugin;

/// <summary>
/// A screen resolved to concrete world coordinates, whether it stands at fixed coordinates or is
/// following an object. Everything downstream draws this and never asks which it was.
/// </summary>
internal readonly record struct ScreenQuad(Vector3 Centre, float Yaw, float Width, float Height);

/// <summary>
/// Draws a flat rectangular screen standing in the world.
/// <para>
/// The screen is defined in world space — a centre, a facing, and a size in yalms — and every
/// vertex is projected through the game's own camera with <see cref="IGameGui.WorldToScreen"/>.
/// That is what makes it behave like an object: walk closer and it grows, orbit it and it turns,
/// because the game's camera matrix is doing the work rather than any guesswork here.
/// </para>
/// <para>
/// It is subdivided into a grid rather than drawn as one quad. ImGui interpolates texture
/// coordinates linearly across a quad, which is wrong under perspective — a screen seen at an
/// angle would visibly skew, with the far half stretched. Splitting it into many small quads makes
/// the error per cell small enough to vanish. This is the standard fix for drawing a textured
/// plane on a 2D draw list.
/// </para>
/// <para>
/// What it cannot do is hide behind things: this paints over the world, with no depth buffer, so
/// walls and people in front of the screen do not occlude it.
/// </para>
/// </summary>
internal sealed class WorldScreen(IGameGui gameGui)
{
    /// <summary>
    /// Cells per side. Eight would be enough for perspective alone, but the cut-out for characters
    /// standing in front is done by dropping whole cells, and its edge is only as fine as this grid.
    /// Twenty keeps the silhouette close to the body and still costs a few hundred quads a frame.
    /// </summary>
    private const int Subdivisions = 20;

    /// <summary>
    /// How far behind the screen's plane the camera may be before it stops drawing. Without this a
    /// screen viewed from behind still paints, mirrored, which reads as a bug.
    /// </summary>
    private const float BackfaceEpsilon = 0.02f;

    /// <summary>
    /// Projects and draws the screen. Render thread only. Returns false when nothing was drawn,
    /// which the caller can report rather than leaving the user wondering.
    /// </summary>
    /// <summary>
    /// A region of the screen a character occupies, in screen pixels. Cells covered by one are left
    /// undrawn, so the character shows through instead of being painted over.
    /// </summary>
    internal readonly record struct Occluder(Vector2 Centre, float RadiusX, float RadiusY);

    public bool Draw(
        ImDrawListPtr drawList,
        IFrameUploader uploader,
        ScreenQuad quad,
        float opacity,
        ReadOnlySpan<Occluder> occluders = default)
    {
        if (!uploader.HasFrame)
            return false;

        // Right and up vectors for the screen's plane. Yaw turns it about the vertical axis, which
        // is the only rotation a screen on a wall actually needs.
        var yaw = quad.Yaw * (MathF.PI / 180f);
        var right = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
        var up = Vector3.UnitY;

        if (this.IsFacingAway(quad, right))
            return false;

        var halfWidth = quad.Width * 0.5f;
        var halfHeight = quad.Height * 0.5f;
        var origin = quad.Centre;

        // Project every grid vertex once, then draw the cells between them.
        var points = new Vector2[Subdivisions + 1, Subdivisions + 1];
        var visible = new bool[Subdivisions + 1, Subdivisions + 1];

        for (var row = 0; row <= Subdivisions; row++)
        {
            // Row 0 is the top of the picture, so v runs downward in world space.
            var v = row / (float)Subdivisions;
            var y = halfHeight - (v * quad.Height);

            for (var column = 0; column <= Subdivisions; column++)
            {
                var u = column / (float)Subdivisions;
                var x = -halfWidth + (u * quad.Width);

                var world = origin + (right * x) + (up * y);
                visible[row, column] = gameGui.WorldToScreen(world, out var screen);
                points[row, column] = screen;
            }
        }

        var tint = new Vector4(1f, 1f, 1f, Math.Clamp(opacity, 0f, 1f));
        var colour = ImGui.ColorConvertFloat4ToU32(tint);
        var drewAnything = false;

        for (var row = 0; row < Subdivisions; row++)
        {
            for (var column = 0; column < Subdivisions; column++)
            {
                // WorldToScreen reports false for anything behind the camera, where the projected
                // coordinates are meaningless. Skipping the cell clips the screen against the view
                // instead of smearing it across the display.
                if (!visible[row, column] || !visible[row, column + 1] ||
                    !visible[row + 1, column + 1] || !visible[row + 1, column])
                {
                    continue;
                }

                // Leave a hole where a character stands in front. Without a depth buffer this is
                // the only occlusion available: an ellipse per character, tested against the cell's
                // centre. Finer subdivision makes the cut-out follow the body more closely.
                if (occluders.Length > 0 && IsCovered(occluders, points, row, column))
                    continue;

                var u0 = column / (float)Subdivisions;
                var u1 = (column + 1) / (float)Subdivisions;
                var v0 = row / (float)Subdivisions;
                var v1 = (row + 1) / (float)Subdivisions;

                drawList.AddImageQuad(
                    uploader.Handle,
                    points[row, column],
                    points[row, column + 1],
                    points[row + 1, column + 1],
                    points[row + 1, column],
                    new Vector2(u0, v0),
                    new Vector2(u1, v0),
                    new Vector2(u1, v1),
                    new Vector2(u0, v1),
                    colour);

                drewAnything = true;
            }
        }

        return drewAnything;
    }

    /// <summary>
    /// True when a cell's centre falls inside any occluder's ellipse.
    /// </summary>
    private static bool IsCovered(ReadOnlySpan<Occluder> occluders, Vector2[,] points, int row, int column)
    {
        var centre = (points[row, column] + points[row, column + 1]
            + points[row + 1, column + 1] + points[row + 1, column]) * 0.25f;

        foreach (var occluder in occluders)
        {
            if (occluder.RadiusX <= 0f || occluder.RadiusY <= 0f)
                continue;

            var dx = (centre.X - occluder.Centre.X) / occluder.RadiusX;
            var dy = (centre.Y - occluder.Centre.Y) / occluder.RadiusY;
            if ((dx * dx) + (dy * dy) <= 1f)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Draws a wireframe outline plus the centre point, so the screen can be positioned before
    /// anything is playing.
    /// </summary>
    public void DrawOutline(ImDrawListPtr drawList, ScreenQuad quad)
    {
        var yaw = quad.Yaw * (MathF.PI / 180f);
        var right = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
        var halfWidth = quad.Width * 0.5f;
        var halfHeight = quad.Height * 0.5f;

        Span<Vector3> corners =
        [
            quad.Centre + (right * -halfWidth) + (Vector3.UnitY * halfHeight),
            quad.Centre + (right * halfWidth) + (Vector3.UnitY * halfHeight),
            quad.Centre + (right * halfWidth) + (Vector3.UnitY * -halfHeight),
            quad.Centre + (right * -halfWidth) + (Vector3.UnitY * -halfHeight),
        ];

        Span<Vector2> projected = stackalloc Vector2[4];
        for (var i = 0; i < 4; i++)
        {
            if (!gameGui.WorldToScreen(corners[i], out projected[i]))
                return;
        }

        var colour = ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.8f, 1f, 0.9f));
        for (var i = 0; i < 4; i++)
            drawList.AddLine(projected[i], projected[(i + 1) % 4], colour, 2f);
    }

    /// <summary>
    /// True when we are looking at the back of the screen.
    /// <para>
    /// This is decided from the winding order of the projected corners rather than from the
    /// camera's position: projection flips the handedness of a quad seen from behind, so the signed
    /// area of the projected rectangle changes sign. That keeps the test to arithmetic on points we
    /// already have, with no reading of the game's camera out of memory.
    /// </para>
    /// </summary>
    private bool IsFacingAway(ScreenQuad quad, Vector3 right)
    {
        var halfWidth = quad.Width * 0.5f;
        var left = quad.Centre - (right * halfWidth);
        var farSide = quad.Centre + (right * halfWidth);
        var top = quad.Centre + (Vector3.UnitY * (quad.Height * 0.5f));

        if (!gameGui.WorldToScreen(left, out var a) ||
            !gameGui.WorldToScreen(farSide, out var b) ||
            !gameGui.WorldToScreen(top, out var c))
        {
            // Partly off-camera: leave the decision to the per-cell visibility test.
            return false;
        }

        var signedArea = ((b.X - a.X) * (c.Y - a.Y)) - ((c.X - a.X) * (b.Y - a.Y));
        return signedArea > -BackfaceEpsilon;
    }
}
