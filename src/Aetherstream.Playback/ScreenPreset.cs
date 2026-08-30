namespace Aetherstream.Playback;

/// <summary>
/// Which in-game surface the picture goes on, and how it sits there.
/// <para>
/// Every field is <b>asset-relative</b> — it names a model or effect texture that exists in the
/// game's own files, so it means the same thing in anyone's house. World placement is deliberately
/// absent: position, facing, territory and the anchor object are coordinates in the host's instance,
/// and in someone else's they name a spot inside a wall. Sharing those would half-work whenever two
/// people happened to share a layout, and silent half-working is worse than never working.
/// </para>
/// </summary>
public readonly record struct ScreenPreset(
    string SurfacePath,
    int MaterialIndex,
    int TextureIndex,
    string MaskPath,
    float Brightness,
    float FitScaleX,
    float FitScaleY,
    float FitOffsetX,
    float FitOffsetY)
{
    public bool IsUsable => this.SurfacePath.Length > 0 && this.MaterialIndex >= 0;

    /// <summary>The furnishing's file name, which is the only part worth showing a person.</summary>
    public string DisplayName
    {
        get
        {
            var path = this.SurfacePath;
            var colon = path.IndexOf(':');
            if (colon >= 0)
                path = path[(colon + 1)..];

            var slash = path.LastIndexOf('/');
            return slash >= 0 ? path[(slash + 1)..] : path;
        }
    }
}
