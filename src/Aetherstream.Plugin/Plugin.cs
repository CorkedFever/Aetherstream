using System.Numerics;

using Aetherstream.Playback;
using Aetherstream.Plugin.Playback;
using Aetherstream.Plugin.Surfaces;
using Aetherstream.Plugin.Video;
using Aetherstream.Plugin.Weather;
using Aetherstream.Plugin.UI;

using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using LibVLCSharp.Shared;

namespace Aetherstream.Plugin;

public sealed partial class Plugin : IDalamudPlugin
{
    private const string CommandName = "/aether";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly IObjectTable objects;
    private readonly IPluginLog log;
    private readonly IChatGui chat;
    private readonly WindowSystem windows = new("Aetherstream");
    private readonly FileDialogManager fileDialogs = new();
    private readonly Configuration config;
    private readonly LibVLC vlc;
    private readonly StreamSession session;
    private readonly WorldScreen screen;
    private readonly IGameGui gameGuiRef;
    private readonly SurfaceBinding binding;
    private readonly SurfaceBinding maskBinding;
    private readonly ITextureProvider textures;
    private SolidTexture? mask;
    private readonly ControlWindow window;
    private readonly MirrorChannel mirror;
    private readonly PlexArt art;
    private readonly BroadcastSession broadcast = new();
    private readonly UiContext uiContext;
    private readonly PartyDirectory directory;
    private CancellationTokenSource? partyLoop;

    /// <summary>
    /// Cancelled first thing on unload, so background work that outlives the teardown — the
    /// lineup health check, most of all — stops writing into the configuration while it is
    /// being saved. The same lock guards those writes and the save.
    /// </summary>
    private readonly CancellationTokenSource unloading = new();
    private readonly object configLock = new();

    /// <summary>Stream path of the group currently selected to broadcast to.</summary>
    private string currentStreamPath = string.Empty;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly HlsRelay relay;

    /// <summary>Set off the render thread to ask <see cref="OnDraw"/> to persist the config.</summary>
    private volatile bool configDirty;

    private CancellationTokenSource? resolving;
    private readonly EorzeaWeather weather;
    private readonly string fishDataPath;
    private readonly GuideChannel guideChannel;
    private readonly WeatherChannel weatherChannel;
    private readonly (string Name, string Blurb, string Group, IFrameChannel Channel)[] channels;
    private PlexAccount plex = null!;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commands,
        ITextureProvider textures,
        IGameGui gameGui,
        IClientState clientState,
        IObjectTable objects,
        ITargetManager targets,
        IDataManager data,
        IChatGui chat,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.clientState = clientState;
        this.objects = objects;
        this.dataManager = data;
        this.log = log;
        this.chat = chat;

        this.config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (this.config.Migrate())
            pluginInterface.SavePluginConfig(this.config);

        // A plugin's base directory is the game's, not ours, so libvlc has to be pointed at the
        // natives that shipped beside this assembly.
        var natives = Path.Combine(
            pluginInterface.AssemblyLocation.Directory?.FullName ?? AppContext.BaseDirectory,
            "libvlc",
            "win-x64");

        // Fully qualified: our own Aetherstream.Core namespace shadows LibVLCSharp's Core class.
        LibVLCSharp.Shared.Core.Initialize(natives);
        this.vlc = new LibVLC();

        // libvlc says exactly why a stream failed, and until now the plugin discarded it — leaving
        // "it froze after ten seconds" with nothing behind it, diagnosable only by reproducing the
        // same stream in a desktop harness. Warnings and above only: at full verbosity libvlc emits
        // thousands of lines a minute.
        this.vlc.Log += this.OnVlcLog;

        var images = Path.Combine(
            pluginInterface.AssemblyLocation.Directory?.FullName ?? AppContext.BaseDirectory,
            "images");
        this.fishDataPath = Path.Combine(
            pluginInterface.AssemblyLocation.Directory?.FullName ?? AppContext.BaseDirectory,
            "data", "fish.json.gz");

        this.session = new StreamSession(this.vlc, textures, log, this.config)
        {
            IdleCard = new TestCard(Path.Combine(images, "testcard.rgba.gz"), log),
        };

        // The drawn channels share one face. Each is handed the plugin's view of its data and
        // asks for it when it paints.
        var face = new BitmapFont(Path.Combine(images, "guidefont.a8.gz"), log);
        this.weather = new EorzeaWeather(data, log);
        this.guideChannel = new GuideChannel(face, this.GuideSnapshot);
        this.weatherChannel = new WeatherChannel(face, this.WeatherSnapshot);
        var breaks = new CommercialBreak(
            face,
            () => this.channels!.Where(c => c.Group == "Shows").Select(c => (c.Name, c.Blurb)).ToList(),
            this.AdItems,
            this.ItemIcon);
        this.channels =
        [
            ("Guide", "What's on, scrolling. The picture stays in the corner.", "Info", this.guideChannel),
            ("Weather", "Local on the 8s, for the zone you are in.", "Info", this.weatherChannel),
            ("Clock", "Eorzea time, the date, the moon, the sun.", "Info", new ClockChannel(face)),
            ("News", "Lodestone headlines, one story at a time.", "Info", new NewsChannel(face, this.NewsSnapshot)),
            ("Market", "Your watch list, priced by Universalis.", "Info", new MarketChannel(face, this.MarketSnapshot)),
            ("Gathering", "Timed nodes: what is up, and how long until each pops.", "Info", new GatheringChannel(face, this.GatheringSnapshot)),
            ("Fishing", "Fish you still need, up now and next; the boats along the top.", "Info", new FishingChannel(face, this.FishingSnapshot)),
            ("Almanac", "Resets, the boat, the cactpot, your retainers, today's roulettes.", "Info", new TimersChannel(face, this.TimersSnapshot)),
            ("Venues", "Who is open in your region tonight, via ffxivvenues.com.", "Info", new VenuesChannel(face, this.VenuesSnapshot)),
            ("Aquarium", "Fish. The odd Namazu. Nothing to read.", "Ambience", new AquariumChannel(face)),
            ("Fireplace", "A fire in a hearth, for the winter.", "Ambience", new FireplaceChannel(face)),
            ("Starfield", "The screensaver everyone had.", "Ambience", new StarfieldChannel(face)),
            ("Plasma", "The demoscene's favourite, in the set's colours.", "Ambience", new PlasmaChannel(face)),
            ("Mystify", "Two polygons and their ghosts.", "Ambience", new MystifyChannel(face)),
            ("3D Maze", "Brick corridors, walked forever. Windows 95.", "Ambience", new MazeChannel(face)),
            ("Pipes", "Plumbing fills the room, then starts over. Windows NT.", "Ambience", new PipesChannel(face)),
            ("Horoscope", "A Sharlayan reading a day, for those born under your guardian.", "Info", new HoroscopeChannel(face, this.HoroscopeSnapshot)),
            ("Kitchen", "A cooking show with a Namazu chef. Every episode a real recipe from your log.", "Shows", breaks.Wrap("Kitchen", new CookingChannel(face, this.Dishes, this.Icon))),
            ("Wildlife", "Stevie Mogwyn gets far too close to the hunting log, one beast at a time.", "Shows", breaks.Wrap("Wildlife", new NatureChannel(face, this.Creatures, this.Icon))),
            ("Stories", "The Tonberry's Lantern: the tales of the Twelve, one at a time, by the fire.", "Shows", breaks.Wrap("Stories", new BardChannel(face, this.Twelve))),
            ("Shopping", "The Eorzean Shopping Network: your market watch, one item at a time, sold hard.", "Shows", breaks.Wrap("Shopping", new ShoppingChannel(face, this.MarketSnapshot, this.ItemIcon))),
            ("Radio", "Aether FM: the set's own jukebox on screen, with a spectrum that moves to it.", "Music", new MusicChannel(face, () => new MusicState(this.session.MusicPlaying, this.session.MusicNowPlaying, this.session.MusicUpNext, this.session.MusicPosition.Index, this.session.MusicPosition.Count, this.session.MusicLiveTitle, this.MusicCoverUrl()), buf => this.session.MusicTap(buf), this.CoverPixels)),
            ("Housing", "Loporrit Estates: the open plots on your world, one at a time, by a Loporrit.", "Info", new HousingChannel(face, this.HousingSnapshot)),
            ("Painting", "The Kupo of Painting: Pom Ross paints a vista from the Sightseeing Log, stroke by stroke.", "Shows", breaks.Wrap("Painting", new PaintingChannel(face, this.Vistas, this.Icon, this.VistaFound))),
            ("Forecast", "The Forecast with Nimbly: every zone's sky, now and for the next three bells.", "Shows", breaks.Wrap("Forecast", new ForecastChannel(face, this.ForecastSnapshot))),
            ("Saucer", "Saucer Tonight with Ko Bi: your MGP, the cactpot, the next GATE, and a card you lack.", "Shows", breaks.Wrap("Saucer", new SaucerChannel(face, this.SaucerSnapshot, this.Icon))),
            ("Sea News", "Sea News with Wavv: the ocean fishing boats, and the conditions on the coasts.", "Shows", breaks.Wrap("Sea News", new CruiseChannel(face, this.CruiseSnapshot))),
            ("Adverts", "The commercial breaks, back to back, every kind in turn. For the connoisseur.", "Ambience", breaks.Reel()),
            ("Allagans", "Ancient Allagans: a mandragora proves a real place, beast or item was the Allagans.", "Shows", breaks.Wrap("Allagans", new ConspiracyChannel(face, this.ConspiracySubjects, this.SubjectPicture))),
            ("Cinema", "Aetherstream Cinema: a whole film every five minutes. None of them exist.", "Shows", breaks.Wrap("Cinema", new MovieChannel(face))),
            ("Scrambled", "Channel 99. You did not subscribe. Nothing to see, and you keep looking.", "Ambience", new ScrambledChannel(face)),
        ];
        this.screen = new WorldScreen(gameGui);
        this.gameGuiRef = gameGui;
        this.binding = new SurfaceBinding(log);
        this.maskBinding = new SurfaceBinding(log);
        this.textures = textures;

        // The moment the session is about to release its texture, the surface has to stop pointing
        // at it — otherwise the game draws from freed GPU memory and the driver takes the process
        // down with it.
        this.session.UploaderReleasing += () => this.binding.Unbind(this.SurfaceAnchorPosition());

        this.art = new PlexArt(textures, this.http, log);

        // Its own client: the shared one has a twenty-second timeout, and a relayed segment is
        // read to completion through this process rather than handed off.
        this.relay = new HlsRelay(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, message => log.Information(message));

        this.uiContext = new UiContext
        {
            Config = this.config,
            Session = this.session,
            ClientState = clientState,
            Objects = objects,
            Targets = targets,
            Log = log,
            Art = this.art,
            Inspector = new SurfaceInspector(log),
            SaveConfig = this.SaveConfig,
            Play = this.PlayAsync,
            PlayResolved = this.PlayResolved,
            FindAnchor = () => this.FindAnchor(this.config.Placement),
            UnbindSurface = this.UnbindSurfaces,
            LocateYtDlp = () => YtDlpResolver.Locate(this.config.YtDlpPath, this.ToolDirectories()),
            MusicFellBack = () => this.musicFellBack,
            FileDialogs = this.fileDialogs,
            Guide = this.guideChannel,
            Weather = this.weatherChannel,
            Channels = this.channels,
        };

        // The display face is loaded before the window so the first frame is drawn in it.
        Theme.Display = new DisplayFont(pluginInterface, log);
        this.window = new ControlWindow(this.uiContext, this.SaveConfig);
        this.mirror = new MirrorChannel(face);
        this.window.Mirror.Channel = this.mirror;
        this.window.Mirror.Show = (handle, title) =>
        {
            this.mirror.Show(handle, title);
            this.session.Channel = this.mirror;
        };
        this.window.Mirror.Stop = () =>
        {
            if (ReferenceEquals(this.session.Channel, this.mirror))
                this.session.Channel = null;
            this.mirror.Stop();
        };
        this.window.Mirror.State = () => (this.mirror.Status, this.mirror.Window, ReferenceEquals(this.session.Channel, this.mirror));

        // The account calls are network work and must not run inside Draw.
        this.plex = new PlexAccount(this.http, this.config.ClientId);
        this.window.Screen.ResumeStalled = this.ResumeStalled;
        this.window.Library.BeginSignIn = this.BeginPlexSignIn;
        this.window.Library.CompleteSignIn = this.CompletePlexSignIn;
        this.window.Library.Browse = this.BrowsePlex;
        this.window.Library.OpenSection = this.OpenPlexSection;
        this.window.Library.OpenItem = this.OpenPlexItem;
        this.window.Library.OpenAllEpisodes = this.OpenAllPlexEpisodes;
        this.window.Library.Archive.Search = this.SearchArchive;
        this.window.Library.Archive.Open = this.OpenArchiveFilm;
        this.window.Library.Local.Scan = this.ScanLocal;
        this.window.Library.YouTube.Browse = this.BrowseYouTube;
        this.window.Library.Twitch.Browse = this.BrowseTwitch;
        this.window.Library.Pluto.Browse = this.BrowsePluto;
        this.window.Library.Pluto.Search = this.SearchPluto;
        this.window.Library.Pluto.OpenSeries = this.OpenPlutoSeries;
        this.window.Library.Pluto.Play = this.PlayPluto;
        this.window.Library.RedBull.Browse = this.BrowseRedBull;
        this.window.Library.RedBull.OpenShelf = this.OpenRedBullShelf;
        this.window.Library.RedBull.Search = this.SearchRedBull;
        this.window.Library.RedBull.Play = this.PlayRedBull;
        this.window.Library.Pbs.Open = this.OpenPbs;
        this.window.Library.MediaServer.SignIn = this.SignInMediaServer;
        this.window.Library.MediaServer.Browse = this.BrowseMediaServer;
        this.window.Library.MediaServer.Open = this.OpenMediaServer;
        this.window.Library.MediaServer.OpenSeries = this.OpenMediaServerSeries;
        this.window.Library.MediaServer.Addresses = this.MediaServerAddresses;
        this.window.Setup.SignOutMediaServer = this.SignOutMediaServer;
        this.window.Library.Nasa.Browse = this.BrowseNasa;
        this.window.Library.Nasa.Open = this.OpenNasa;
        this.window.Library.Ted.Browse = this.BrowseTed;
        this.window.Library.Ted.Open = this.OpenTed;
        this.window.Library.Dailymotion.Browse = this.BrowseDailymotion;
        this.window.Library.Dailymotion.Open = this.OpenDailymotion;
        this.broadcast.Log = message => log.Information(message);
        this.window.Share.Session = () => this.broadcast;
        this.window.Share.StartBroadcast = this.StartBroadcast;
        this.window.Share.StopBroadcast = this.StopBroadcast;
        this.directory = new PartyDirectory(this.http);
        this.WireParty();
        this.WireLiveTv();
        this.WireMusic();
        this.window.Dial.IsDead = this.IsDead;
        this.window.Setup.CheckChannels = () => this.CheckLineupHealth(force: true);
        this.window.Setup.HealthStatus = () => this.healthStatus;
        this.window.Setup.FindItem = this.FindMarketItem;
        this.window.Setup.WatchListChanged = this.WatchListChanged;
        this.window.Setup.TestBanner = this.TestMaintenanceBanner;
        this.session.Overlay = new MaintenanceOverlay(new MaintenanceBanner(face), this.CurrentNotice);
        this.chat.ChatMessage += this.OnChatMessage;

        this.windows.AddWindow(this.window);

        this.commands.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Aetherstream. \"/aether play <source>\" starts one straight away, "
                + "\"/aether stop\" ends it, \"/aether here\" moves the screen in front of you.",
        });

        // Scroll Lock hides the game's UI, and Dalamud then stops calling Draw — which would freeze
        // the picture on the furnishing, since the frame uploads ride on that callback. Keep it
        // coming; the plugin's own window is hidden by hand below when the game's UI is.
        pluginInterface.UiBuilder.DisableUserUiHide = true;
        pluginInterface.UiBuilder.DisableCutsceneUiHide = true;
        pluginInterface.UiBuilder.Draw += this.OnDraw;
        pluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
        pluginInterface.UiBuilder.OpenConfigUi += this.OpenMainUi;
    }

    public void Dispose()
    {
        this.unloading.Cancel();

        // First, and before anything else is torn down: the LibVLC instance outlives the plugin
        // deliberately (see the note at the end of this method), so a subscription left behind here
        // would call into a log this unload is about to invalidate.
        this.vlc.Log -= this.OnVlcLog;
        this.chat.ChatMessage -= this.OnChatMessage;
        this.log.Information("[unload] hooks off");

        this.pluginInterface.UiBuilder.Draw -= this.OnDraw;
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenMainUi;

        this.commands.RemoveHandler(CommandName);
        this.windows.RemoveAllWindows();
        this.log.Information("[unload] windows down");

        this.resolving?.Cancel();
        this.resolving?.Dispose();

        // Put the object's own texture back before anything else goes away, while the object and
        // our texture are both still alive.
        this.log.Information("[unload] unbinding the surface");
        this.binding.Unbind(this.SurfaceAnchorPosition());
        this.binding.Dispose();
        this.maskBinding.Unbind(this.SurfaceAnchorPosition());
        this.maskBinding.Dispose();
        this.log.Information("[unload] surface unbound");

        // Ends the push before anything else goes away. A child process outlives its parent on
        // Windows, so skipping this would leave ffmpeg broadcasting after the plugin unloaded.
        this.partyLoop?.Cancel();
        this.partyLoop?.Dispose();

        this.broadcast.Dispose();
        this.log.Information("[unload] broadcast down");

        this.session.Dispose();
        this.log.Information("[unload] session down");
        this.mirror.Dispose();

        // Poster textures are ours alone — the game never sees them, only our own draw lists — so
        // unlike the video texture they can be released outright once drawing has stopped.
        this.art.Dispose();

        Theme.Display?.Dispose();
        Theme.Display = null;

        // The LibVLC instance is deliberately left alive. Disposing it unloads libvlc's native
        // modules — including its own Direct3D plugins — from inside the game's process, and doing
        // that while the display driver still has work queued faults on a driver thread. It is
        // released when the process exits, which is soon enough.
        this.relay.Dispose();
        this.http.Dispose();

        this.SaveConfig();
        this.log.Information("[unload] done");
    }

    private void OnDraw()
    {
        // A mirror nobody is looking at is put away, so a game is not captured for nothing
        // behind a film; three seconds covers a channel change that comes straight back.
        if (this.mirror.Showing && !ReferenceEquals(this.session.Channel, this.mirror) && this.mirror.IdleMs > 3000)
            this.mirror.Stop();

        // Queued starts, stops and texture work are applied before anything draws, so no draw list
        // can be holding a texture that is about to be released.
        this.session.Update();

        // Checked here so a stall is noticed whether or not the control window is open.
        this.RetryStalledThroughRelay();
        this.WatchForBlack();
        this.WatchForFailure();

        this.ApplyVolumeForFrame();
        this.RecordProgress();

        // A finished episode hands over to the next one in the list it was picked from — and a
        // finished anything is not something to resume into.
        if (this.session.ConsumeEnded())
        {
            if (this.config.ResumePositions.Remove(this.config.Source))
                this.configDirty = true;

            if (this.uiContext.PlayNext())
                this.log.Information("[playback] next up");
        }

        // Driven from here rather than from the window, because a closed or collapsed window does
        // not draw — and retired poster textures would then sit un-released until it was reopened.
        this.art.Update();

        // Config writes asked for by background work happen here, on the render thread, because the
        // serializer walks collections that Draw mutates. Saving from a worker could catch Recents
        // mid-edit and throw inside the serializer, which surfaced as an unrelated failure message.
        if (this.configDirty)
        {
            this.configDirty = false;
            this.SaveConfig();
        }

        this.DrawWorldScreen();

        // The set keeps playing with the UI hidden; the remote does not need to be on screen for that.
        if (!this.gameGuiRef.GameUiHidden)
            this.windows.Draw();

        // Drawn outside the window system so an open dialog survives the window being closed.
        this.fileDialogs.Draw();
    }

    /// <summary>
    /// Works out where the screen actually is this frame. An anchored screen takes its position and
    /// facing from the object it follows, so placing it means placing that object — which is what
    /// the game's own housing tools are for. Falls back to the fixed coordinates when the anchor is
    /// not around, rather than hiding the screen with no explanation.
    /// </summary>
    private ScreenQuad? ResolveQuad()
    {
        var placement = this.config.Placement;

        if (placement.IsAnchored)
        {
            var anchor = this.FindAnchor(placement);
            if (anchor is not null)
            {
                var facing = anchor.Rotation;
                var forward = new Vector3(MathF.Sin(facing), 0f, MathF.Cos(facing));
                var right = new Vector3(forward.Z, 0f, -forward.X);

                var centre = anchor.Position
                    + (forward * placement.OffsetForward)
                    + (right * placement.OffsetRight)
                    + new Vector3(0f, placement.OffsetUp, 0f);

                var yaw = (facing * (180f / MathF.PI)) + 90f + placement.AnchorYawOffset;
                return new ScreenQuad(centre, yaw, placement.Width, placement.Height);
            }

            // Anchor gone (different zone, unloaded, furniture removed). Fixed coordinates are a
            // better fallback than nothing, and the window says which is in use.
        }

        if (placement.TerritoryType != 0 && placement.TerritoryType != this.clientState.TerritoryType)
            return null;

        return new ScreenQuad(placement.Position, placement.Yaw, placement.Width, placement.Height);
    }

    /// <summary>
    /// Finds the anchored object, preferring the exact instance but falling back to "the same
    /// furnishing, in the same spot" so an anchor keeps working after a relog.
    /// </summary>
    private Dalamud.Game.ClientState.Objects.Types.IGameObject? FindAnchor(ScreenPlacement placement)
    {
        if (placement.AnchorObjectId != 0 &&
            this.objects.SearchById(placement.AnchorObjectId) is { } exact &&
            exact.IsValid())
        {
            return exact;
        }

        if (placement.AnchorDataId == 0)
            return null;

        // Several identical furnishings can share a data id, so the remembered position picks the
        // right one. A generous radius covers the object settling slightly differently on load.
        Dalamud.Game.ClientState.Objects.Types.IGameObject? best = null;
        var bestDistance = float.MaxValue;

        foreach (var candidate in this.objects)
        {
            if (!candidate.IsValid() || candidate.DataId != placement.AnchorDataId)
                continue;

            var distance = Vector3.Distance(candidate.Position, placement.AnchorPosition);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        if (best is null || bestDistance > 5f)
            return null;

        // Cache the new runtime handle so later frames take the cheap path.
        placement.AnchorObjectId = best.GameObjectId;
        return best;
    }

    private const int MaxOccluders = 16;

    /// <summary>
    /// Builds the screen-space regions where characters stand in front of the screen, so those
    /// cells can be left undrawn.
    /// <para>
    /// Whether someone is genuinely between you and the screen is decided by which side of the
    /// screen's plane they are on — no camera position needed, since the screen is only drawn at
    /// all when its front face is toward you. Anyone on that front side is a candidate; anyone
    /// behind it is not, so a screen on a wall is not punctured by people standing behind it.
    /// </para>
    /// </summary>
    private int CollectOccluders(ScreenQuad quad, Span<WorldScreen.Occluder> into)
    {
        var yaw = quad.Yaw * (MathF.PI / 180f);
        var right = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
        var normal = new Vector3(-right.Z, 0f, right.X);

        var count = 0;

        foreach (var obj in this.objects)
        {
            if (count == into.Length)
                break;

            if (!obj.IsValid())
                continue;

            // Only things with a body worth cutting around.
            if (obj.ObjectKind is not (ObjectKind.Pc or ObjectKind.BattleNpc or ObjectKind.EventNpc))
                continue;

            var toObject = obj.Position - quad.Centre;
            if (toObject.LengthSquared() > 40f * 40f)
                continue;

            var isLocalPlayer = obj.GameObjectId == this.objects.LocalPlayer?.GameObjectId;

            // The plane test can go either way depending on how the screen was turned, so it is
            // used to reject only when we are confident: the local player is always cut out, since
            // a screen you cannot see past is the whole complaint.
            if (!isLocalPlayer && Vector3.Dot(normal, toObject) < 0f && toObject.Length() > 2f)
                continue;

            // Approximate the body as an upright ellipse: project the feet and a point overhead,
            // and take the height between them. Width follows from human proportions rather than
            // the hitbox, which is a gameplay radius and far too wide.
            if (!gameGuiRef.WorldToScreen(obj.Position, out var feet) ||
                !gameGuiRef.WorldToScreen(obj.Position + new Vector3(0f, 2.1f, 0f), out var head))
            {
                continue;
            }

            var halfHeight = MathF.Abs(head.Y - feet.Y) * 0.5f;
            if (halfHeight < 1f)
                continue;

            into[count++] = new WorldScreen.Occluder(
                new Vector2((feet.X + head.X) * 0.5f, (feet.Y + head.Y) * 0.5f),
                halfHeight * 0.45f,
                halfHeight * 1.05f);
        }

        return count;
    }

    /// <summary>
    /// Paints the video onto the anchored object's own surface. When this is in use the overlay
    /// panel is not drawn at all — the picture is part of the world instead of on top of it.
    /// </summary>
    private bool DrawOnSurface()
    {
        if (!this.config.PaintOnSurface
            || this.config.SurfaceMaterialIndex < 0
            || this.config.SurfaceModelPath.Length == 0)
        {
            return false;
        }

        var anchorPosition = this.SurfaceAnchorPosition();
        var slot = new SurfaceSlot(
            this.config.SurfaceMaterialIndex,
            this.config.SurfaceTextureIndex,
            this.config.SurfaceModelPath,
            string.Empty,
            string.Empty,
            0,
            0);

        // The model path is part of the identity, not just the indices. Comparing indices alone made
        // picking a different object a no-op whenever the new one happened to land on the same slot
        // numbers — which is always, for effects, since those are fixed at 0/0. The binding then
        // kept painting the previous object and nothing appeared to happen.
        if (this.binding.Bound is not { } bound
            || bound.MaterialIndex != slot.MaterialIndex
            || bound.TextureIndex != slot.TextureIndex
            || !string.Equals(bound.ModelPath, slot.ModelPath, StringComparison.Ordinal))
        {
            this.binding.Bind(slot);
        }

        this.binding.Apply(anchorPosition, this.session.Uploader);
        this.ApplyMask(anchorPosition);
        return true;
    }

    /// <summary>
    /// Puts both painted surfaces back.
    /// <para>
    /// The mask has to be included. It is only ever unbound from <see cref="ApplyMask"/>, which
    /// <see cref="DrawOnSurface"/> stops calling the moment painting is switched off — so unbinding
    /// just the picture left the mask surface filled with flat white for the rest of the session,
    /// with no way to clear it short of reloading the plugin.
    /// </para>
    /// </summary>
    private void UnbindSurfaces()
    {
        var position = this.SurfaceAnchorPosition();
        this.binding.Unbind(position);
        this.maskBinding.Unbind(position);
    }

    /// <summary>
    /// Where to look for the furnishing in the layout. The object table entry is only a proxy, but
    /// it stands in the right place, which is all the layout lookup needs.
    /// </summary>
    /// <summary>
    /// Applies volume, mute and the distance fade every frame, whichever way the picture is shown.
    /// <para>
    /// This used to sit at the end of the floating-panel draw, which the painted-surface path
    /// returned before reaching — so on a painted furnishing the mute button did nothing, the
    /// volume slider only took effect at the next play, and the distance fade never ran at all.
    /// Sound is not a drawing concern; it belongs beside the session update, and it has to run
    /// with the screen hidden or the player absent too, or a mute pressed on a loading screen
    /// would be ignored.
    /// </para>
    /// </summary>
    private void ApplyVolumeForFrame()
    {
        // Music under a channel needs the room's rules too, even with no picture playing.
        if (!this.session.IsPlaying && !this.session.MusicPlaying)
            return;

        var distance = 0f;
        var pan = 0f;

        if (this.objects.LocalPlayer is { } player)
        {
            Vector3? at = this.config.PaintOnSurface
                ? this.SurfaceAnchorPosition()
                : this.ResolveQuad()?.Centre;

            if (at is { } centre)
            {
                distance = Vector3.Distance(player.Position, centre);

                // Where the screen sits on the display is where the sound should come from. The
                // camera does the projection for us: a set that lands left of centre is on the
                // left. Behind the camera it cannot be placed, so it stays centred.
                if (this.config.SpatialSound && this.gameGuiRef.WorldToScreen(centre, out var onScreen))
                {
                    var viewport = ImGui.GetMainViewport();
                    var half = viewport.Size.X * 0.5f;
                    if (half > 1f)
                        pan = Math.Clamp((onScreen.X - viewport.Pos.X - half) / half, -1f, 1f);
                }
            }
        }

        this.session.ApplyVolume(distance, pan);
    }

    private long lastProgressTicks;
    private long lastProgressSaveTicks;

    /// <summary>
    /// Notes where a seekable source has reached, every few seconds, so it can be picked up
    /// later. The first half-minute and the last minute are not worth resuming into, so those
    /// clear the entry instead — a film watched to the credits starts from the top next time.
    /// </summary>
    private void RecordProgress()
    {
        var now = Environment.TickCount64;
        if (now - this.lastProgressTicks < 5000)
            return;

        this.lastProgressTicks = now;

        var source = this.config.Source;
        var duration = this.session.DurationMs;
        if (source.Length == 0 || !this.session.IsPlaying || duration <= 0 || !this.session.IsSeekable)
            return;

        var position = this.session.PositionMs;
        var table = this.config.ResumePositions;

        if (position < 30_000 || position > duration - 60_000)
        {
            if (table.Remove(source))
                this.configDirty = true;

            return;
        }

        table[source] = position;

        // Bounded: nobody needs to resume into something they last touched two hundred films ago.
        if (table.Count > 200)
            table.Remove(table.Keys.First());

        // Written every half minute rather than every sample; a crash loses at most that.
        if (now - this.lastProgressSaveTicks > 30_000)
        {
            this.lastProgressSaveTicks = now;
            this.configDirty = true;
        }
    }

    private long ResumePointFor(string source) =>
        this.config.ResumePositions.TryGetValue(source, out var ms) ? ms : 0;

    private Vector3 SurfaceAnchorPosition() =>
        this.config.SurfacePosition != Vector3.Zero
            ? this.config.SurfacePosition
            : this.FindAnchor(this.config.Placement)?.Position ?? this.config.Placement.AnchorPosition;

    /// <summary>
    /// Paints a flat colour onto a second surface — the effect's mask — so an additively blended
    /// panel reads as solid instead of letting the wall through.
    /// </summary>
    private void ApplyMask(Vector3 position)
    {
        if (this.config.SurfaceMaskPath.Length == 0)
        {
            if (this.maskBinding.Bound is not null)
                this.maskBinding.Unbind(position);

            return;
        }

        this.mask ??= new SolidTexture(this.textures, this.config.MaskColour);

        var slot = new SurfaceSlot(0, 0, this.config.SurfaceMaskPath, string.Empty, string.Empty, 0, 0);
        if (this.maskBinding.Bound?.ModelPath != slot.ModelPath)
            this.maskBinding.Bind(slot);

        this.maskBinding.Apply(position, this.mask);
    }

    private void DrawWorldScreen()
    {
        if (!this.config.ScreenVisible || !this.clientState.IsLoggedIn)
            return;

        if (this.DrawOnSurface())
            return;

        if (this.ResolveQuad() is not { } quad)
            return;

        // The background draw list keeps the screen behind every plugin window, so the controls
        // stay usable while it plays.
        var drawList = ImGui.GetBackgroundDrawList();

        var drew = false;
        if (this.session.Uploader is { } uploader)
        {
            Span<WorldScreen.Occluder> occluders = stackalloc WorldScreen.Occluder[MaxOccluders];
            var count = this.config.CutOutCharacters
                ? this.CollectOccluders(quad, occluders)
                : 0;

            drew = this.screen.Draw(drawList, uploader, quad, this.config.Opacity, occluders[..count]);
        }

        if (this.config.ShowOutline && !drew)
            this.screen.DrawOutline(drawList, quad);
    }

    /// <summary>
    /// The config folder first: it is the same for the life of the install, whereas Dalamud puts
    /// each plugin version in its own numbered folder, so a file left beside the DLL is gone on
    /// the next update.
    /// </summary>
    private string[] ToolDirectories() =>
    [
        this.pluginInterface.GetPluginConfigDirectory(),
        this.pluginInterface.AssemblyLocation.Directory?.FullName ?? AppContext.BaseDirectory,
    ];

    private StreamResolvers.Tools Tools() =>
        new(this.config.YtDlpPath, this.ToolDirectories(), this.config.YtDlpCookiesBrowser, this.config.YtDlpCookiesFile);

    private void OnCommand(string command, string arguments)
    {
        var trimmed = arguments.Trim();

        if (trimmed.Length == 0)
        {
            this.window.Toggle();
            return;
        }

        var space = trimmed.IndexOf(' ');
        var verb = (space < 0 ? trimmed : trimmed[..space]).ToLowerInvariant();
        var rest = space < 0 ? string.Empty : trimmed[(space + 1)..].Trim();

        switch (verb)
        {
            case "play":
                this.PlayFromCommand(rest.Length > 0 ? rest : this.config.Source);
                break;

            case "stop":
                this.session.RequestStop();
                break;

            case "here":
                this.PlaceInFrontOfPlayer();
                break;

            default:
                // Anything else is taken as a source, so "/aether xenosysvex" just works.
                this.PlayFromCommand(trimmed);
                break;
        }
    }

    /// <summary>
    /// Plays and records it in the history, so a source started from chat appears alongside the ones
    /// started from the window. The window's own buttons record a proper title first and then call
    /// through to <see cref="PlayAsync"/>, which is why the recording is not done there.
    /// </summary>
    private void PlayFromCommand(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        // Keeps a title the library already established. Replaying a film from chat should not
        // rename it to "Plex" in the history.
        var known = this.config.Recents.FirstOrDefault(
            r => string.Equals(r.Source, source, StringComparison.OrdinalIgnoreCase));

        this.config.Remember(source, known?.Label ?? Ui.Pretty(source), known?.Thumb ?? string.Empty);

        this.PlayAsync(source);
    }

    /// <summary>
    /// A recent is remembered before resolution, under the best name known then — often just
    /// the host. Once the resolver has the real title, the entry takes it, so the history and the
    /// guide say "Frieren" rather than "youtube.com".
    /// </summary>
    private void RememberTitle(string source, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var recent = this.config.Recents.FirstOrDefault(r => string.Equals(r.Source, source, StringComparison.OrdinalIgnoreCase));
        if (recent is null || recent.Label == title)
            return;

        // Only a placeholder is replaced; a name the library gave is kept.
        if (recent.Label.Length == 0 || recent.Label == Ui.Pretty(source) || recent.Label.Contains('.') && !recent.Label.Contains(' '))
        {
            recent.Label = title;
            this.configDirty = true;
        }
    }

    private void PlaceInFrontOfPlayer()
    {
        if (this.objects.LocalPlayer is not { } player)
            return;

        var placement = this.config.Placement;
        var facing = player.Rotation;
        var forward = new Vector3(MathF.Sin(facing), 0f, MathF.Cos(facing));

        placement.Position = player.Position + (forward * 3f) + new Vector3(0f, 1.2f, 0f);
        placement.Yaw = (facing * (180f / MathF.PI)) + 90f;
        placement.TerritoryType = this.clientState.TerritoryType;
        this.SaveConfig();
    }

    /// <summary>
    /// Resolves off the render thread — resolution shells out to yt-dlp and talks to the network,
    /// neither of which belongs anywhere near a frame.
    /// </summary>
    private void PlayAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        this.config.Source = source;
        this.SaveConfig();

        this.resolving?.Cancel();
        this.resolving?.Dispose();
        this.resolving = new CancellationTokenSource();
        var token = this.resolving.Token;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    var resolver = StreamResolvers.For(
                        source,
                        this.http,
                        out var via,
                        new StreamResolvers.PlexSettings(
                            this.config.PlexServer,
                            this.config.PlexToken,
                            this.config.PlexMaxKilobits,
                            this.config.ClientId),
                        new StreamResolvers.PartySettings(this.config.PartyApiHost, this.config.PartyKey),
                        this.Tools());
                    var stream = await resolver.ResolveAsync(source, token);
                    if (token.IsCancellationRequested)
                        return;

                    this.log.Information($"Resolved '{source}' via {via}.");
                    this.RememberTitle(source, stream.DisplayName);

                    // An origin that wants a header libvlc cannot send is relayed from the start;
                    // played directly it would only ever show the origin's refusal.
                    if (stream.RelayRequired)
                        stream = this.relay.Publish(stream);

                    this.session.RequestStart(stream, this.ResumePointFor(source));
                }
                catch (OperationCanceledException)
                {
                    // Superseded by a newer request; nothing to report.
                }
                catch (Exception ex)
                {
                    this.log.Error(ex, $"Could not resolve '{source}'.");

                    // The first line is what the screen shows; the log keeps the whole thing.
                    this.session.Fail(ex.Message.Split('\n')[0].Trim());
                }
            },
            token);
    }

    /// <summary>
    /// Asks Plex for a link code. The user enters it on plex.tv, so no password is handled here.
    /// </summary>
    private void BeginPlexSignIn()
    {
        this.window.Library.SetStatus("Asking Plex for a code…");

        _ = Task.Run(async () =>
        {
            try
            {
                var pin = await this.plex.BeginSignInAsync(CancellationToken.None);
                this.window.Library.SetPin(pin);
                this.window.Library.SetStatus("Go to plex.tv/link and enter the code, then press \"I've entered it\".");
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "Plex sign-in could not start.");
                this.window.Library.SetStatus($"Could not reach Plex: {ex.Message}");
            }
        });
    }

    /// <summary>Collects the token once the code has been entered, then lists the account's servers.</summary>
    private void CompletePlexSignIn(PlexAccount.Pin pin)
    {
        this.window.Library.SetStatus("Checking…");

        _ = Task.Run(async () =>
        {
            try
            {
                var token = await this.plex.PollSignInAsync(pin, CancellationToken.None);
                if (token is null)
                {
                    this.window.Library.SetStatus("Not linked yet — enter the code at plex.tv/link, then try again.");
                    return;
                }

                this.config.PlexToken = token;
                this.configDirty = true;

                var servers = await this.plex.ListServersAsync(token, CancellationToken.None);
                this.window.Library.SetServers(
                    servers,
                    servers.Count > 0
                        ? "Signed in. Pick the server to use — remote ones are listed first."
                        : "Signed in, but the account reports no servers.");
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "Plex sign-in failed.");
                this.window.Library.SetStatus($"Sign-in failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Restarts what was playing, at the point it stopped. Re-uses the already-resolved stream so a
    /// dropped connection does not also mean a fresh round trip to Plex or Twitch.
    /// </summary>
    private void ResumeStalled()
    {
        if (this.session.Current is { } stream && this.session.StalledAtMs > 0)
            this.session.RequestStart(stream, this.session.StalledAtMs);
    }

    /// <summary>
    /// Probes the source and starts the push, both off the render thread — ffprobe is a process
    /// launch and a file read, neither of which belongs in a frame.
    /// </summary>
    private void StartBroadcast(string input, string startAt)
    {
        // The path belongs to the group; the key is who we are. The relay asks the service whether
        // that user owns that path before it accepts a byte.
        var target = BroadcastTarget.ForGroup(
            this.config.PartyServer,
            this.currentStreamPath,
            this.config.PartyKey,
            this.config.PartySrtPassphrase,
            this.config.PartyWatchHost);

        // Once it is up, our own screen moves to the relay output.
        this.watchOwnPartyWhenLive = true;

        TimeSpan? from = TimeSpan.TryParse(startAt, out var parsed) && parsed > TimeSpan.Zero
            ? parsed
            : null;

        _ = Task.Run(async () =>
        {
            try
            {
                var probe = await BroadcastSession.ProbeAsync(input, CancellationToken.None);

                this.log.Information(
                    $"[broadcast] {input}: video={probe.VideoCodec} audio={probe.AudioCodec} " +
                    $"keyframes~{probe.KeyframeGapSeconds:F1}s -> {(probe.CanCopy ? "copy" : "re-encode")}");

                if (probe.KeyframesTooFarApart)
                {
                    this.log.Information(
                        "[broadcast] keyframes are further apart than the segment duration, so the "
                        + "streams cannot be copied — re-encoding instead.");
                }

                this.broadcast.Start(target, input, probe, from);
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "Could not start the broadcast.");
            }
        });
    }

    private PlexLibrary Library() =>
        new(this.http, this.config.PlexServer, this.config.PlexToken);

    private void BrowsePlex() => this.PlexWork(
        "Reading your libraries…",
        async library =>
        {
            var sections = await library.ListSectionsAsync(CancellationToken.None);
            this.window.Library.SetSections(
                sections,
                sections.Count > 0 ? "Pick a library." : "That server reports no libraries.");
        });

    private void OpenPlexSection(string sectionKey, string filter) => this.PlexWork(
        filter.Length > 0 ? $"Searching for \"{filter}\"…" : "Reading the library…",
        async library =>
        {
            var items = await library.ListItemsAsync(sectionKey, filter, 300, CancellationToken.None);
            this.window.Library.SetItems(items);
            this.window.Library.SetStatus(
                items.Count > 0 ? string.Empty
                : filter.Length > 0 ? $"Nothing matching \"{filter}\"."
                : "Nothing in that library.");
        });

    /// <summary>
    /// Opens a show or a season, one level at a time: a show lists its seasons, a season lists its
    /// episodes. Flattening a show straight to every episode — which is what this used to do — turns
    /// a long-running series into several hundred tiles with no season to tell them apart.
    /// </summary>
    private void OpenPlexItem(PlexLibrary.Item item) => this.PlexWork(
        $"Opening {item.Title}…",
        async library =>
        {
            var children = await library.ListChildrenAsync(item.RatingKey, CancellationToken.None);

            // Most anime is a single season, and making someone click through "Season 1" every time
            // to reach it is a step that never carries information.
            if (item.IsShow && children.Count == 1 && children[0].IsSeason)
                children = await library.ListChildrenAsync(children[0].RatingKey, CancellationToken.None);

            this.window.Library.SetItems(children);
            this.window.Library.SetStatus(children.Count > 0 ? string.Empty : "Nothing inside that.");
        });

    /// <summary>Every episode of a show at once, past its seasons. Asked for explicitly.</summary>
    private void OpenAllPlexEpisodes(PlexLibrary.Item show) => this.PlexWork(
        $"Reading every episode of {show.Title}…",
        async library =>
        {
            var episodes = await library.ListEpisodesAsync(show.RatingKey, CancellationToken.None);
            this.window.Library.SetItems(episodes);
            this.window.Library.SetStatus(episodes.Count > 0 ? string.Empty : "Nothing inside that.");
        });

    /// <summary>
    /// Runs library work off the render thread and reports failures where they can be seen. Every
    /// one of these is an HTTP round trip to a server that may be on the other side of the world.
    /// </summary>
    private void PlexWork(string busy, Func<PlexLibrary, Task> work)
    {
        if (this.config.PlexServer.Length == 0 || this.config.PlexToken.Length == 0)
        {
            this.window.Library.SetStatus("Sign in and pick a server first.");
            return;
        }

        this.window.Library.SetStatus(busy);
        var library = this.Library();

        _ = Task.Run(async () =>
        {
            try
            {
                await work(library);
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "Plex library request failed.");
                this.window.Library.SetStatus($"Plex request failed: {ex.Message}");
            }
        });
    }

    private void OpenMainUi() => this.window.IsOpen = true;

    private void SaveConfig()
    {
        lock (this.configLock)
            this.pluginInterface.SavePluginConfig(this.config);
    }
}
