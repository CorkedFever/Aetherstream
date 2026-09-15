namespace Aetherstream.Plugin;

/// <summary>
/// The music under the drawn channels: which tracks, from where. The jukebox takes a list and
/// does not care where it came from; this is the part that cares.
/// </summary>
public sealed partial class Plugin
{
    private static readonly string[] MusicExtensions = [".mp3", ".flac", ".ogg", ".m4a", ".wav", ".opus", ".aac"];

    private IReadOnlyList<string> musicTracks = [];
    private string musicKey = string.Empty;
    private long musicScannedAtMs = -1;
    private List<string> plexMusicTracks = [];

    /// <summary>Whether the chosen source had nothing and the bundled tracks are standing in.</summary>
    private bool musicFellBack;

    private void WireMusic()
    {
        this.window.Sound.LoadPlexPlaylists = this.LoadPlexPlaylists;
        this.window.Sound.MusicChanged = this.MusicChanged;
        this.session.MusicTracks = this.MusicTracks;
        this.session.MusicTitles = this.MusicTitle;
        this.session.MusicShuffle = () => !this.UsesChosenSource() || this.config.ChannelMusicSource != "podcast";
        this.WireRadio();
        this.WireOrchestrion();

        // A Plex playlist chosen last time is fetched now, so the first forecast has its music.
        if (this.config.ChannelMusicSource == "plex" && this.config.ChannelMusicPlexPlaylist.Length > 0)
            this.LoadPlexMusic();
    }

    /// <summary>
    /// The current list, rebuilt when the source changes or a minute has passed. The same list
    /// instance is returned until then, which is how the jukebox knows nothing changed.
    /// </summary>
    /// <summary>
    /// Which channels get the chosen source. The shows and the info channels keep the bundled
    /// lounge tracks whatever is chosen: they are programmes with their own sound, and a podcast
    /// under the kitchen is two people talking at once. The Radio channel and the ambience
    /// channels play whatever the Music tab says.
    /// </summary>
    private bool UsesChosenSource()
    {
        var up = this.session.Channel;
        if (up is null)
            return true;
        var group = this.channels.FirstOrDefault(c => ReferenceEquals(c.Channel, up)).Group;
        return group is not ("Info" or "Shows");
    }

    private IReadOnlyList<string> MusicTracks()
    {
        var source = this.UsesChosenSource() ? this.config.ChannelMusicSource : "bundled";
        var key = $"{source}|{this.config.ChannelMusicFolder}|{this.config.ChannelMusicPlexPlaylist}|{this.plexMusicTracks.Count}|{this.config.ChannelMusicRadioUrl}|{this.config.ChannelMusicPodcastFeed}|{this.config.ChannelMusicPodcastEpisode}|{this.podcastOpen?.Episodes.Count ?? 0}|{string.Join(',', this.config.ChannelMusicRolls)}|{this.rollFiles.Count}";
        var now = Environment.TickCount64;
        if (key == this.musicKey && now - this.musicScannedAtMs < 60_000)
            return this.musicTracks;

        this.musicKey = key;
        this.musicScannedAtMs = now;

        var found = new List<string>();
        try
        {
            switch (source)
            {
                case "folder":
                    if (Directory.Exists(this.config.ChannelMusicFolder))
                        found.AddRange(ScanFolder(this.config.ChannelMusicFolder, 500));
                    break;

                case "plex":
                    found.AddRange(this.plexMusicTracks);
                    break;

                case "radio":
                case "podcast":
                    found.AddRange(this.RadioOrPodcastTracks());
                    break;

                case "rolls":
                    found.AddRange(this.RollTracks());
                    break;

                default:
                    var bundled = Path.Combine(
                        this.pluginInterface.AssemblyLocation.Directory?.FullName ?? AppContext.BaseDirectory,
                        "music");
                    if (Directory.Exists(bundled))
                        found.AddRange(ScanFolder(bundled, 50));
                    break;
            }
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "[music] could not list the tracks.");
        }

        // A source with nothing in it, no rolls ticked, a folder gone, a feed not read yet, falls
        // back to the bundled tracks rather than to silence; the Music tab says so.
        this.musicFellBack = false;
        if (found.Count == 0 && source != "bundled")
        {
            try
            {
                var bundled = Path.Combine(this.pluginInterface.AssemblyLocation.Directory?.FullName ?? AppContext.BaseDirectory, "music");
                if (Directory.Exists(bundled))
                    found.AddRange(ScanFolder(bundled, 50));
                this.musicFellBack = found.Count > 0;
            }
            catch (Exception ex)
            {
                this.log.Warning(ex, "[music] could not list the bundled tracks.");
            }
        }

        // Only replaced when the contents differ, so a rescan that finds the same files does not
        // restart the music.
        if (!found.SequenceEqual(this.musicTracks))
        {
            this.musicTracks = found;
            this.log.Information($"[music] {found.Count} tracks from {source}");
        }

        return this.musicTracks;
    }

    private static IEnumerable<string> ScanFolder(string folder, int cap) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => MusicExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Take(cap);

    private void MusicChanged()
    {
        this.musicKey = string.Empty;
        if (this.config.ChannelMusicSource == "plex")
            this.LoadPlexMusic();
        this.ShowRadioChannel();
    }

    /// <summary>
    /// Puts the Radio channel up when music is chosen: picking a station, a podcast or a playlist
    /// from the Music tab means you want to hear it, and the Radio channel is the screen that
    /// plays it and shows it. A film that is on is left alone; the music would not play over it.
    /// </summary>
    private void ShowRadioChannel()
    {
        if (this.session.IsPlaying && this.session.Channel is null)
            return;

        var radio = this.channels.FirstOrDefault(c => c.Name == "Radio").Channel;
        if (radio is not null && !ReferenceEquals(this.session.Channel, radio))
            this.session.Channel = radio;
    }

    private void LoadPlexPlaylists()
    {
        if (this.config.PlexServer.Length == 0 || this.config.PlexToken.Length == 0)
        {
            this.window.Sound.SetPlexPlaylists([], "sign in to Plex in Library first");
            return;
        }

        var library = this.Library();
        _ = Task.Run(async () =>
        {
            try
            {
                var playlists = await library.ListAudioPlaylistsAsync(CancellationToken.None);
                this.window.Sound.SetPlexPlaylists(playlists, playlists.Count == 0 ? "no audio playlists on this server" : string.Empty);
            }
            catch (Exception ex)
            {
                this.log.Warning(ex, "[music] could not list Plex playlists.");
                this.window.Sound.SetPlexPlaylists([], $"could not reach Plex: {ex.Message}");
            }
        });
    }

    private void LoadPlexMusic()
    {
        var key = this.config.ChannelMusicPlexPlaylist;
        if (key.Length == 0 || this.config.PlexServer.Length == 0 || this.config.PlexToken.Length == 0)
        {
            this.plexMusicTracks = [];
            this.musicKey = string.Empty;
            return;
        }

        var library = this.Library();
        _ = Task.Run(async () =>
        {
            try
            {
                var urls = await library.ListPlaylistTrackUrlsAsync(key, CancellationToken.None);
                this.plexMusicTracks = urls;
                this.musicKey = string.Empty;
                this.log.Information($"[music] Plex playlist has {urls.Count} tracks");
            }
            catch (Exception ex)
            {
                this.log.Warning(ex, "[music] could not read the Plex playlist.");
            }
        });
    }
}
