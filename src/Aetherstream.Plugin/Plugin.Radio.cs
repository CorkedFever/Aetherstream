using Aetherstream.Playback;
using Aetherstream.Plugin.Video;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aetherstream.Plugin;

/// <summary>
/// Internet radio and podcasts behind the Sound tab: directory searches and feed reads in the
/// background, the chosen station or episode made the music source, and cover art decoded to
/// frame pixels for the Radio channel.
/// </summary>
public sealed partial class Plugin
{
    private RadioBrowser? radio;
    private PodcastFeed? podcasts;
    private PodcastFeed.Feed? podcastOpen;
    private string podcastOpenUrl = string.Empty;
    private readonly Dictionary<string, IconPixels?> coverPixels = [];
    private readonly HashSet<string> coverFetching = [];

    private RadioBrowser Radio => this.radio ??= new RadioBrowser(this.http);

    private PodcastFeed Podcasts => this.podcasts ??= new PodcastFeed(this.http);

    private void WireRadio()
    {
        this.window.Sound.Radio.Search = this.SearchRadio;
        this.window.Sound.Radio.Tune = this.TuneRadio;
        this.window.Sound.Podcasts.Search = this.SearchPodcasts;
        this.window.Sound.Podcasts.OpenFeed = this.OpenPodcast;
        this.window.Sound.Podcasts.Play = this.PlayPodcast;

        // A podcast chosen last time is read now, so the music comes back where it was.
        if (this.config.ChannelMusicSource == "podcast" && this.config.ChannelMusicPodcastFeed.Length > 0)
            this.OpenPodcast(this.config.ChannelMusicPodcastFeed);
    }

    private void SearchRadio(string name, string tag)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var stations = await this.Radio.SearchAsync(name, tag, 60, CancellationToken.None);
                this.window.Sound.Radio.SetStations(stations, stations.Count == 0 ? "Nothing in the directory for that." : string.Empty);
            }
            catch (Exception ex)
            {
                this.log.Warning($"[radio] search failed: {ex.Message}");
                this.window.Sound.Radio.SetStatus("The directory did not answer. Try again in a moment.");
            }
        });
    }

    /// <summary>Makes a station the music, or with an empty address puts the music back.</summary>
    private void TuneRadio(string name, string url, string logo)
    {
        if (url.Length == 0)
        {
            this.MusicChanged();
            return;
        }

        this.config.ChannelMusicSource = "radio";
        this.config.ChannelMusicRadioName = name;
        this.config.ChannelMusicRadioUrl = url;
        this.config.ChannelMusicRadioLogo = logo;
        this.config.ChannelMusic = true;
        this.SaveConfig();
        this.MusicChanged();
        this.log.Information($"[radio] tuned to {name}: {url}");
    }

    private void SearchPodcasts(string term)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var shows = await this.Podcasts.SearchAsync(term, CancellationToken.None);
                this.window.Sound.Podcasts.SetFound(shows, shows.Count == 0 ? "No shows by that name." : string.Empty);
            }
            catch (Exception ex)
            {
                this.log.Warning($"[podcast] search failed: {ex.Message}");
                this.window.Sound.Podcasts.SetStatus("The directory did not answer. Try again in a moment.");
            }
        });
    }

    private void OpenPodcast(string feedUrl)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var feed = await this.Podcasts.FetchAsync(feedUrl, CancellationToken.None);
                this.podcastOpen = feed;
                this.podcastOpenUrl = feedUrl;
                if (this.config.PodcastFeeds.All(p => p.Url != feedUrl))
                {
                    this.config.PodcastFeeds.Add(new PodcastRef { Title = feed.Title, Url = feedUrl, Image = feed.Image });
                    this.configDirty = true;
                }

                this.window.Sound.Podcasts.SetFeed(feedUrl, feed, feed.Episodes.Count == 0 ? "That feed has no episodes with audio." : string.Empty);
                this.log.Information($"[podcast] {feed.Title}: {feed.Episodes.Count} episodes");
            }
            catch (Exception ex)
            {
                this.log.Warning($"[podcast] could not read {feedUrl}: {ex.Message}");
                this.window.Sound.Podcasts.SetFeed(feedUrl, null, "That feed could not be read. Is it an RSS address?");
            }
        });
    }

    /// <summary>Plays an episode and the ones after it, in order, as the music.</summary>
    private void PlayPodcast(string feedUrl, string episodeUrl, string title)
    {
        this.config.ChannelMusicSource = "podcast";
        this.config.ChannelMusicPodcastFeed = feedUrl;
        this.config.ChannelMusicPodcastEpisode = episodeUrl;
        this.config.ChannelMusic = true;
        this.SaveConfig();
        this.MusicChanged();
        this.log.Information($"[podcast] playing '{title}'");
    }

    /// <summary>The tracks for the radio and podcast sources; empty when neither is chosen.</summary>
    private List<string> RadioOrPodcastTracks()
    {
        switch (this.config.ChannelMusicSource)
        {
            case "radio":
                return this.config.ChannelMusicRadioUrl.Length > 0 ? [this.config.ChannelMusicRadioUrl] : [];
            case "podcast":
                if (this.podcastOpen is not { } feed || this.podcastOpenUrl != this.config.ChannelMusicPodcastFeed)
                    return [];
                var urls = feed.Episodes.Select(e => e.Url).ToList();
                var at = urls.IndexOf(this.config.ChannelMusicPodcastEpisode);
                return at < 0 ? urls : urls.Skip(at).ToList();
            default:
                return [];
        }
    }

    /// <summary>A name for a track the jukebox would otherwise call by its file name.</summary>
    private string? MusicTitle(string track)
    {
        if (!this.UsesChosenSource())
            return null;
        if (this.config.ChannelMusicSource == "rolls")
            return this.RollTitle(track);
        if (this.config.ChannelMusicSource == "radio" && track == this.config.ChannelMusicRadioUrl)
            return this.config.ChannelMusicRadioName;
        if (this.config.ChannelMusicSource == "podcast" && this.podcastOpen is { } feed)
            return feed.Episodes.FirstOrDefault(e => e.Url == track).Title;
        return null;
    }

    /// <summary>The picture to show for what is playing: the station's logo or the show's art.</summary>
    private string MusicCoverUrl() => !this.UsesChosenSource() ? string.Empty : this.config.ChannelMusicSource switch
    {
        "radio" => this.config.ChannelMusicRadioLogo,
        "podcast" => this.podcastOpen?.Image ?? string.Empty,
        _ => string.Empty,
    };

    /// <summary>Cover art as frame pixels at 300 square, fetched once and kept; null until it arrives.</summary>
    private IconPixels? CoverPixels(string url)
    {
        if (url.Length == 0)
            return null;
        if (this.coverPixels.TryGetValue(url, out var have))
            return have;
        if (!this.coverFetching.Add(url))
            return null;

        _ = Task.Run(async () =>
        {
            IconPixels? result = null;
            try
            {
                var bytes = await this.http.GetByteArrayAsync(url);
                using var image = Image.Load<Rgba32>(bytes);
                image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(300, 300), Mode = ResizeMode.Crop }));
                var px = new uint[300 * 300];
                image.ProcessPixelRows(rows =>
                {
                    for (var y = 0; y < 300; y++)
                    {
                        var row = rows.GetRowSpan(y);
                        for (var x = 0; x < 300; x++)
                        {
                            var p = row[x];
                            px[(y * 300) + x] = ((uint)p.A << 24) | ((uint)p.B << 16) | ((uint)p.G << 8) | p.R;
                        }
                    }
                });
                result = new IconPixels(px, 300, 300);
            }
            catch (Exception ex)
            {
                this.log.Debug($"[music] cover {url}: {ex.Message}");
            }

            this.coverPixels[url] = result;
        });
        return null;
    }
}
