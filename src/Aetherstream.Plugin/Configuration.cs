using System.Numerics;

using Dalamud.Configuration;

namespace Aetherstream.Plugin;

/// <summary>Where a screen stands in the world, and how big it is. Sizes are in yalms.</summary>
[Serializable]
public sealed class ScreenPlacement
{
    public float PositionX { get; set; }

    public float PositionY { get; set; }

    public float PositionZ { get; set; }

    /// <summary>Rotation about the vertical axis, in degrees.</summary>
    public float Yaw { get; set; }

    public float Width { get; set; } = 6f;

    public float Height { get; set; } = 3.375f;

    /// <summary>
    /// The zone this placement belongs to. A position only means anything within its own territory,
    /// so the screen stays hidden elsewhere rather than floating somewhere arbitrary.
    /// </summary>
    public uint TerritoryType { get; set; }

    public Vector3 Position
    {
        get => new(this.PositionX, this.PositionY, this.PositionZ);
        set
        {
            this.PositionX = value.X;
            this.PositionY = value.Y;
            this.PositionZ = value.Z;
        }
    }

    /// <summary>
    /// When set, the screen follows this object instead of standing at fixed coordinates. Placement
    /// then means "put the furnishing where you want it" — the game's own placement tools do the
    /// work, and the screen moves with the object if it is ever moved again.
    /// </summary>
    public ulong AnchorObjectId { get; set; }

    /// <summary>
    /// The anchor's data id — which piece of furniture it is, rather than which instance.
    /// <para>
    /// <see cref="AnchorObjectId"/> is a runtime handle: it changes every time the zone reloads, so
    /// an anchor stored only by that id quietly stops working after a relog. The data id plus where
    /// the object stood when it was bound survives, and re-finds the same object next session.
    /// </para>
    /// </summary>
    public uint AnchorDataId { get; set; }

    public float AnchorPositionX { get; set; }

    public float AnchorPositionY { get; set; }

    public float AnchorPositionZ { get; set; }

    public Vector3 AnchorPosition
    {
        get => new(this.AnchorPositionX, this.AnchorPositionY, this.AnchorPositionZ);
        set
        {
            this.AnchorPositionX = value.X;
            this.AnchorPositionY = value.Y;
            this.AnchorPositionZ = value.Z;
        }
    }

    /// <summary>What the anchor was called when it was bound, so the UI can name it.</summary>
    public string AnchorLabel { get; set; } = string.Empty;

    /// <summary>Offset from the anchor, along its own facing. Forward lifts the screen off the object.</summary>
    public float OffsetForward { get; set; } = 0.05f;

    public float OffsetUp { get; set; } = 1.2f;

    public float OffsetRight { get; set; }

    /// <summary>Rotation relative to the anchor's own facing, in degrees.</summary>
    public float AnchorYawOffset { get; set; }

    public bool IsAnchored => this.AnchorObjectId != 0 || this.AnchorDataId != 0;

    /// <summary>Resizes about the centre, keeping 16:9.</summary>
    public void SetWidthKeepingAspect(float width)
    {
        this.Width = width;
        this.Height = width * 9f / 16f;
    }
}

/// <summary>A party someone gave you the code for. Saved once, watched whenever it is live.</summary>
[Serializable]
public sealed class Followed
{
    /// <summary>The directory to ask. A code alone cannot say where it lives.</summary>
    public string Host { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    /// <summary>Last name the directory reported, so the list reads properly while offline.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>Something played before, so it can be started again without being typed again.</summary>
[Serializable]
public sealed class Recent
{
    public string Source { get; set; } = string.Empty;

    /// <summary>What to call it in the list. A title where one is known, a host name otherwise.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Plex thumbnail path, when it came from the library. Empty for everything else.</summary>
    public string Thumb { get; set; } = string.Empty;

    public long PlayedAtUnix { get; set; }
}

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public const int CurrentVersion = 4;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// Your Plex server, e.g. http://192.168.1.20:32400. The token is yours and is only ever sent
    /// to this address.
    /// </summary>
    public string PlexServer { get; set; } = string.Empty;

    public string PlexToken { get; set; } = string.Empty;

    /// <summary>
    /// Transcode ceiling in kilobits. Zero streams the original file, which is right on a LAN and
    /// impractical from a remote server.
    /// </summary>
    public int PlexMaxKilobits { get; set; }

    /// <summary>What to play: a channel name, a page URL, or a direct media URL.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Most recent first.</summary>
    public List<Recent> Recents { get; set; } = [];

    // -- Live TV -----------------------------------------------------------------------------------

    /// <summary>
    /// An M3U playlist of live channels. Defaults to the iptv-org directory, which indexes publicly
    /// available streams; any other extended-M3U list works the same way.
    /// </summary>
    public string LiveTvPlaylistUrl { get; set; } = "https://iptv-org.github.io/iptv/index.m3u";

    /// <summary>Last group and country filters, so the tab opens where you left it.</summary>
    public string LiveTvGroup { get; set; } = string.Empty;

    public string LiveTvCountry { get; set; } = string.Empty;

    /// <summary>
    /// Channel URLs you have pinned. Keyed by URL rather than name because names are not unique in
    /// these lists — there are a dozen "News" in any given one.
    /// </summary>
    public List<string> LiveTvFavourites { get; set; } = [];

    // -- Parties -----------------------------------------------------------------------------------
    //
    // Everything below the key is told to us by the service on sign-in. None of it is typed, and
    // none of it is worth backing up: sign in again and it refills.

    /// <summary>Relay host, from the service.</summary>
    public string PartyServer { get; set; } = string.Empty;

    /// <summary>
    /// Host and port the room watches on. Carries the port deliberately: the stream is served over
    /// an HTTP/1.1-only listener, because libvlc cannot fetch HLS through Caddy over HTTP/2.
    /// </summary>
    public string PartyWatchHost { get; set; } = string.Empty;

    /// <summary>Encrypts the SRT ingest. From the service.</summary>
    public string PartySrtPassphrase { get; set; } = string.Empty;

    /// <summary>What was last broadcast, so movie night resumes without retyping a path.</summary>
    public string PartyInput { get; set; } = string.Empty;

    /// <summary>The party service everyone connects to, e.g. party.example.com.</summary>
    public string PartyApiHost { get; set; } = string.Empty;

    /// <summary>
    /// This install's identity. Generated here, never chosen, and the server only ever stores a
    /// hash of it — so there is no account, no password and nothing to recover. Copy it to another
    /// machine and that machine is you.
    /// </summary>
    public string PartyKey { get; set; } = string.Empty;

    /// <summary>Which of your groups a broadcast goes to.</summary>
    public string PartyCodeInUse { get; set; } = string.Empty;


    /// <summary>
    /// Put the screen setup in the invite code, so guests can land the picture on the same
    /// furnishing instead of working the surface scan out for themselves. Only asset-relative
    /// fields travel; world placement never does.
    /// </summary>
    public bool PartyShareScreen { get; set; } = true;

    /// <summary>
    /// Records a play, newest first, without duplicating what is already there. Capped at a screenful
    /// — a history longer than the panel that shows it is just a bigger config file.
    /// </summary>
    public void Remember(string source, string label, string thumb = "")
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        this.Recents.RemoveAll(r => string.Equals(r.Source, source, StringComparison.OrdinalIgnoreCase));

        this.Recents.Insert(0, new Recent
        {
            Source = source,
            Label = label.Length > 0 ? label : source,
            Thumb = thumb,
            PlayedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });

        if (this.Recents.Count > 12)
            this.Recents.RemoveRange(12, this.Recents.Count - 12);
    }

    public ScreenPlacement Placement { get; set; } = new();

    /// <summary>Whether the screen is currently shown in the world.</summary>
    public bool ScreenVisible { get; set; } = true;

    /// <summary>Outline the screen's bounds even when nothing is playing, to help place it.</summary>
    public bool ShowOutline { get; set; } = true;

    public float Opacity { get; set; } = 1f;

    /// <summary>
    /// Punch holes in the picture where characters stand in front of it.
    /// <para>
    /// Off, and it should stay off. It does not put anyone behind the screen — it deletes part of
    /// the picture, which reads as a ragged hole rather than as occlusion, and looks worse than the
    /// problem it was meant to solve. Painting on a real surface is the answer instead; there the
    /// game draws the whole picture and hides it correctly.
    /// </para>
    /// </summary>
    public bool CutOutCharacters { get; set; }

    /// <summary>Muted by default: a screen that starts talking the moment it loads is rude.</summary>
    public bool AudioEnabled { get; set; }

    public float Volume { get; set; } = 0.6f;

    /// <summary>
    /// Milliseconds to shift the sound against the picture, applied by libvlc at the source.
    /// Positive holds the sound back, negative brings it forward.
    /// <para>
    /// Defaults to zero deliberately. Which way this pipeline drifts is not something to assume —
    /// guessing a direction and shipping it as a default just moves the problem and makes the
    /// symptom harder to describe. Tune it by ear against a talking head.
    /// </para>
    /// </summary>
    public int AudioOffsetMs { get; set; }

    /// <summary>
    /// Fade the sound out with distance, so the screen behaves like something in the room rather
    /// than something in your head. Zero disables it.
    /// </summary>
    public float AudioFalloffYalms { get; set; } = 25f;

    /// <summary>
    /// Off by default in-process: hardware decoding spins up a second D3D11 video device inside the
    /// game, and that combination with libvlc's memory-output path is a known source of instability.
    /// Software decode of one 720p stream is a few percent of a modern CPU.
    /// </summary>
    public bool UseHardwareDecode { get; set; }

    public bool UseDynamicTexture { get; set; } = true;

    /// <summary>
    /// How much libvlc buffers before showing anything, in milliseconds.
    /// <para>
    /// Raised from libvlc's 1.5s default because a live relay over a home connection is not a CDN:
    /// one late segment inside a thin buffer is a visible freeze. The cost is startup latency, which
    /// does not matter when the whole room is a few seconds behind the broadcast regardless.
    /// </para>
    /// </summary>
    public int NetworkCachingMs { get; set; } = 8000;

    /// <summary>
    /// Paint onto the anchored object's own surface instead of drawing a panel over the world.
    /// This is the real thing: the game renders it, so it is occluded and lit correctly.
    /// </summary>
    public bool PaintOnSurface { get; set; }

    /// <summary>
    /// The model of the placed object carrying the picture. Named by path rather than by nearest,
    /// because the nearest drawable to a monitor is frequently the wall behind it.
    /// </summary>
    public string SurfaceModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Where the surface object stood when it was chosen. The binding resolves near this each
    /// frame, so it stays on the object you picked rather than tracking wherever you walk.
    /// </summary>
    public float SurfaceX { get; set; }

    public float SurfaceY { get; set; }

    public float SurfaceZ { get; set; }

    public Vector3 SurfacePosition
    {
        get => new(this.SurfaceX, this.SurfaceY, this.SurfaceZ);
        set
        {
            this.SurfaceX = value.X;
            this.SurfaceY = value.Y;
            this.SurfaceZ = value.Z;
        }
    }

    /// <summary>
    /// How the picture sits inside the surface's texture, as fractions of it.
    /// <para>
    /// Some surfaces do not show their whole texture: the Everkeep Monitor's panel is an effect
    /// with an alpha gradient baked in, so the lower part of anything painted on it fades out.
    /// Shrinking the picture and sliding it up puts it in the part that is actually visible; the
    /// rest of the texture is left black.
    /// </para>
    /// </summary>
    /// <summary>
    /// A second surface painted with a flat colour rather than the picture.
    /// <para>
    /// An effect that blends additively cannot be made opaque by any amount of alpha on the colour
    /// texture. Its mask, though, is usually a separate texture — filling that with white removes
    /// both the fade and the translucency.
    /// </para>
    /// </summary>
    public string SurfaceMaskPath { get; set; } = string.Empty;

    /// <summary>Packed RGBA, red in the low bits. White by default.</summary>
    public uint MaskColour { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Brightens the picture before it goes onto a surface. An effect that blends additively shows
    /// dark pixels as see-through, and brightness is the only lever against that.
    /// </summary>
    public float SurfaceBrightness { get; set; } = 1f;

    public float FitScaleX { get; set; } = 1f;

    public float FitScaleY { get; set; } = 1f;

    public float FitOffsetX { get; set; }

    public float FitOffsetY { get; set; }

    /// <summary>True when the picture is not simply filling the whole texture.</summary>
    public bool HasFit =>
        Math.Abs(this.FitScaleX - 1f) > 0.001f
        || Math.Abs(this.FitScaleY - 1f) > 0.001f
        || Math.Abs(this.FitOffsetX) > 0.001f
        || Math.Abs(this.FitOffsetY) > 0.001f;

    /// <summary>Which material on the object carries the picture. -1 when nothing is chosen.</summary>
    public int SurfaceMaterialIndex { get; set; } = -1;

    public int SurfaceTextureIndex { get; set; } = -1;

    /// <summary>Applies stepwise upgrades; returns whether anything changed and needs saving.</summary>
    public bool Migrate()
    {
        if (this.Version >= CurrentVersion)
            return false;

        if (this.Version < 2)
        {
            // Retire the hole-punching for anyone who already has it on.
            this.CutOutCharacters = false;
        }

        if (this.Version < 3)
        {
            // Changing the default was not enough: a config saved before that keeps its old value.
            // Hardware decoding makes libvlc build a second D3D11 device and video decoder inside
            // the game, and tearing that down is a plausible cause of faults on a driver thread.
            this.UseHardwareDecode = false;
        }

        if (this.Version < 4)
        {
            // A 4s buffer shipped briefly and was not enough for a live relay over a home
            // connection. Raising the default alone would not have reached anyone, because a value
            // already in the saved config wins over it — which is the whole reason this exists.
            if (this.NetworkCachingMs <= 4000)
                this.NetworkCachingMs = 8000;
        }

        this.Version = CurrentVersion;
        return true;
    }
}
