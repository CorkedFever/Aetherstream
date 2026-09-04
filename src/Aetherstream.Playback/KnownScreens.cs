namespace Aetherstream.Playback;

/// <summary>
/// Furnishings that are known to work as screens, with the setup already worked out.
/// <para>
/// Finding a surface by hand is a scan, a filter and a list of texture names, and the answer is
/// the same for everyone because it names files in the game's own data. Once one has been worked
/// out it belongs here, so the next person picks it from a list instead of repeating the search.
/// </para>
/// </summary>
public static class KnownScreens
{
    public static readonly IReadOnlyList<(string Name, string Note, ScreenPreset Screen)> All =
    [
        (
            "Everkeep Monitor",
            "The glowing panel is an effect texture. Its lower part fades out, so the picture is " +
            "fitted into the top half where it stays solid.",
            new ScreenPreset(
                SurfacePath: "atex:bgcommon/hou/common/vfx_hou_ind1/texture/i1604_c1_bc7.atex",
                MaterialIndex: 0,
                TextureIndex: 0,
                MaskPath: string.Empty,
                Brightness: 1f,
                FitScaleX: 1f,
                FitScaleY: 0.47f,
                FitOffsetX: 0f,
                FitOffsetY: -0.27f)),
    ];

    /// <summary>The known screen a setup matches, if it is one of them.</summary>
    public static string? NameOf(string surfacePath, int materialIndex, int textureIndex)
    {
        foreach (var (name, _, screen) in All)
        {
            if (string.Equals(screen.SurfacePath, surfacePath, StringComparison.Ordinal)
                && screen.MaterialIndex == materialIndex
                && screen.TextureIndex == textureIndex)
            {
                return name;
            }
        }

        return null;
    }
}
