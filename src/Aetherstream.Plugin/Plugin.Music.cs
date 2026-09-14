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

    private void WireMusic()
    {
        this.window.Sound.LoadPlexPlaylists = this.LoadPlexPlaylists;
        this.window.Sound.MusicChanged = this.MusicChanged;
        this.session.MusicTracks = this.MusicTracks;
        this.session.MusicTitles = this.MusicTitle;
        this.session.MusicShuffle = () => this.config.ChannelMusicSource != "podcast";
        this.WireRadio();

        // A Plex playlist chosen last time is fetched now, so the first forecast has its music.
        if (this.config.ChannelMusicSource == "plex" && this.config.ChannelMusicPlexPlaylist.Length > 0)
            this.LoadPlexMusic();
    }

    /// <summary>
    /// The current list, rebuilt when the source changes or a minute has passed. The same list
    /// instance is returned until then, which is how the jukebox knows nothing changed.
    /// </summary>
    private IReadOnlyList<string> MusicTracks()
    {
        var key = $"{this.config.ChannelMusicSource}|{this.config.ChannelMusicFolder}|{this.config.ChannelMusicPlexPlaylist}|{this.plexMusicTracks.Count}|{this.config.ChannelMusicRadioUrl}|{this.config.ChannelMusicPodcastFeed}|{this.config.ChannelMusicPodcastEpisode}|{this.podcastOpen?.Episodes.Count ?? 0}";
        var now = Environment.TickCount64;
        if (key == this.musicKey && now - this.musicScannedAtMs < 60_000)
            return this.musicTracks;

        this.musicKey = key;
        this.musicScannedAtMs = now;

        var found = new List<string>();
        try
        {
            switch (this.config.ChannelMusicSource)
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

        // Only replaced when the contents differ, so a rescan that finds the same files does not
        // restart the music.
        if (!found.SequenceEqual(this.musicTracks))
        {
            this.musicTracks = found;
            this.log.Information($"[music] {found.Count} tracks from {this.config.ChannelMusicSource}");
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
