using Aetherstream.Playback;
using Aetherstream.Plugin.Playback;
using Aetherstream.Plugin.Surfaces;

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// What every panel needs. Passing one of these keeps a panel's constructor to a single argument, so
/// splitting the window further later costs nothing.
/// </summary>
internal sealed class UiContext
{
    public required Configuration Config { get; init; }

    public required StreamSession Session { get; init; }

    public required IClientState ClientState { get; init; }

    public required IObjectTable Objects { get; init; }

    public required ITargetManager Targets { get; init; }

    public required IPluginLog Log { get; init; }

    public required PlexArt Art { get; init; }

    public required SurfaceInspector Inspector { get; init; }

    public required Action SaveConfig { get; init; }

    /// <summary>Starts a source. Resolution happens off the render thread; this only queues it.</summary>
    public required Action<string> Play { get; init; }

    /// <summary>
    /// Starts something already resolved, skipping the resolver chain.
    /// <para>
    /// A live-TV channel carries its own user agent and referrer, and those would be lost if the URL
    /// went back through resolution as a bare string — 665 channels in the default playlist do not
    /// serve without them.
    /// </para>
    /// </summary>
    public required Action<Aetherstream.Core.ResolvedStream> PlayResolved { get; init; }

    public required Func<IGameObject?> FindAnchor { get; init; }

    public required Action UnbindSurface { get; init; }

    /// <summary>Where yt-dlp would be found right now, or null. For the Setup tab's readout.</summary>
    public required Func<string?> LocateYtDlp { get; init; }


    /// <summary>
    /// A screen setup offered by the party code just played, when it differs from what is already
    /// configured. Held rather than applied: it replaces the viewer's own screen binding, so it is
    /// theirs to accept.
    /// </summary>
    public ScreenPreset? OfferedScreen { get; private set; }

    /// <summary>Plays something and records it in the history, which is almost always what is wanted.</summary>
    public void PlayAndRemember(string source, string label, string thumb = "")
    {
        this.Config.Source = source;
        this.Config.Remember(source, label, thumb);
        this.SaveConfig();
        this.Play(source);
    }

    /// <summary>
    /// Notes a screen the party is using, so it can be offered once playback starts. Ignored when it
    /// already matches — there is nothing to ask about.
    /// </summary>
    public void OfferScreen(ScreenPreset? screen)
    {
        if (screen is { } offered && offered.IsUsable && !this.MatchesCurrentScreen(offered))
            this.OfferedScreen = offered;
    }

    private bool MatchesCurrentScreen(ScreenPreset screen) =>
        this.Config.PaintOnSurface
        && string.Equals(this.Config.SurfaceModelPath, screen.SurfacePath, StringComparison.Ordinal)
        && this.Config.SurfaceMaterialIndex == screen.MaterialIndex
        && this.Config.SurfaceTextureIndex == screen.TextureIndex;

    /// <summary>Takes the host's screen.</summary>
    public void AcceptOfferedScreen()
    {
        if (this.OfferedScreen is not { } screen)
            return;

        this.ApplyScreen(screen);
        this.OfferedScreen = null;
    }

    /// <summary>
    /// Puts the picture on a described surface. Placement is untouched on purpose — where the
    /// furnishing stands is this install's business, and a preset or a host's coordinates would be
    /// meaningless in a different house.
    /// </summary>
    public void ApplyScreen(ScreenPreset screen)
    {
        this.UnbindSurface();

        this.Config.SurfaceModelPath = screen.SurfacePath;
        this.Config.SurfaceMaterialIndex = screen.MaterialIndex;
        this.Config.SurfaceTextureIndex = screen.TextureIndex;
        this.Config.SurfaceMaskPath = screen.MaskPath;
        this.Config.SurfaceBrightness = screen.Brightness;
        this.Config.FitScaleX = screen.FitScaleX;
        this.Config.FitScaleY = screen.FitScaleY;
        this.Config.FitOffsetX = screen.FitOffsetX;
        this.Config.FitOffsetY = screen.FitOffsetY;

        // The whole point is that it shows up without a second step.
        this.Config.PaintOnSurface = true;

        // The surface is resolved near where you are standing, so the viewer has to be by their own
        // copy of the furnishing — which they will be, since that is where they want to watch.
        this.Config.SurfacePosition = this.Objects.LocalPlayer?.Position ?? this.Config.SurfacePosition;

        this.SaveConfig();
    }

    public void DeclineOfferedScreen() => this.OfferedScreen = null;

    /// <summary>The screen this install is using, to put in an invite code.</summary>
    public ScreenPreset? CurrentScreen =>
        this.Config.PaintOnSurface && this.Config.SurfaceModelPath.Length > 0
            ? new ScreenPreset(
                this.Config.SurfaceModelPath,
                this.Config.SurfaceMaterialIndex,
                this.Config.SurfaceTextureIndex,
                this.Config.SurfaceMaskPath,
                this.Config.SurfaceBrightness,
                this.Config.FitScaleX,
                this.Config.FitScaleY,
                this.Config.FitOffsetX,
                this.Config.FitOffsetY)
            : null;
}
