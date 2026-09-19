using Aetherstream.Playback;
using Aetherstream.Plugin.UI;

namespace Aetherstream.Plugin;

/// <summary>The Library's free shelves without a session of their own to keep: Red Bull TV, PBS, NASA+, TED and Dailymotion. Listings off the render thread; playback through the resolvers.</summary>
public sealed partial class Plugin
{
    private RedBullTv? redBull;
    private RedBullTv.Session? redBullSession;
    private CancellationTokenSource? redBullWork;
    private PbsShows? pbs;
    private CancellationTokenSource? pbsWork;
    private NasaPlus? nasa;
    private CancellationTokenSource? nasaWork;
    private TedTalks? ted;
    private CancellationTokenSource? tedWork;
    private Dailymotion? dailymotion;
    private CancellationTokenSource? dailymotionWork;
    private SouthParkStudios? southPark;
    private CancellationTokenSource? southParkWork;

    private RedBullTv RedBull => this.redBull ??= new RedBullTv(this.http);

    private PbsShows Pbs => this.pbs ??= new PbsShows(this.http);

    private NasaPlus Nasa => this.nasa ??= new NasaPlus(this.http);

    private TedTalks Ted => this.ted ??= new TedTalks(this.http);

    private Dailymotion Daily => this.dailymotion ??= new Dailymotion(this.http);

    private SouthParkStudios SouthPark => this.southPark ??= new SouthParkStudios(this.http);

    private void BrowseSouthPark() =>
        Background(ref this.southParkWork, async ct =>
        {
            var seasons = await this.SouthPark.SeasonsAsync(ct);
            this.window.Library.SouthPark.SetSeasons(seasons, seasons.Count == 0 ? "South Park Studios listed no seasons." : string.Empty);
            this.log.Information($"[southpark] {seasons.Count} seasons");
        }, ex =>
        {
            this.log.Warning($"[southpark] seasons: {ex.Message}");
            this.window.Library.SouthPark.SetStatus("South Park Studios did not answer: " + Ui.Ellipsis(ex.Message, 80));
        });

    private void OpenSouthParkSeason(SouthParkStudios.Season season) =>
        Background(ref this.southParkWork, async ct =>
        {
            var episodes = await this.SouthPark.EpisodesAsync(season, ct);
            var locked = episodes.Count(e => e.Locked);
            this.window.Library.SouthPark.SetEpisodes(season, episodes, episodes.Count == 0 ? "The site lists no episodes for it." : locked > 0 ? $"{locked} of {episodes.Count} locked by the site right now." : string.Empty);
            this.log.Information($"[southpark] season {season.Number}: {episodes.Count} episodes, {locked} locked");
        }, ex =>
        {
            this.log.Warning($"[southpark] season {season.Number}: {ex.Message}");
            this.window.Library.SouthPark.SetEpisodes(season, [], "South Park Studios did not answer: " + Ui.Ellipsis(ex.Message, 80));
        });

    private async Task<RedBullTv.Session> RedBullSessionAsync(CancellationToken ct)
    {
        if (this.redBullSession is { } s && DateTime.UtcNow - s.MadeUtc < TimeSpan.FromMinutes(50))
            return s;
        var made = await this.RedBull.SessionAsync(ct);
        this.redBullSession = made;
        return made;
    }

    private void BrowseRedBull() => this.RedBullWork(async ct =>
    {
        var s = await this.RedBullSessionAsync(ct);
        var shelves = await this.RedBull.ShelvesAsync(s, ct);
        this.window.Library.RedBull.SetShelves(shelves, shelves.Count == 0 ? "Red Bull TV listed no shelves." : string.Empty);
        this.log.Information($"[redbull] {shelves.Count} shelves");
    });

    private void OpenRedBullShelf(RedBullTv.Shelf shelf) => this.RedBullWork(async ct =>
    {
        var s = await this.RedBullSessionAsync(ct);
        var items = await this.RedBull.ShelfAsync(s, shelf.Id, ct);
        this.window.Library.RedBull.SetItems(shelf.Id, items, items.Count == 0 ? "Nothing on that shelf right now." : string.Empty);
    });

    private void SearchRedBull(string query) => this.RedBullWork(async ct =>
    {
        var s = await this.RedBullSessionAsync(ct);
        var items = await this.RedBull.SearchAsync(s, query, ct);
        this.window.Library.RedBull.SetItems("search:" + query, items, items.Count == 0 ? $"Nothing on Red Bull TV for \"{query}\"." : string.Empty);
    });

    private void PlayRedBull(RedBullTv.Item item) => this.RedBullWork(async ct =>
    {
        var s = await this.RedBullSessionAsync(ct);
        var id = item.IsShow ? await this.RedBull.PlayIdOfAsync(s, item.Id, ct) : item.Id;
        if (id is null)
        {
            this.window.Library.RedBull.SetStatus($"\"{item.Title}\" has nothing to play yet.");
            return;
        }

        var url = RedBullTv.StreamUrl(s, id);
        this.config.Source = url;
        this.config.Remember(url, item.Title, item.Image);
        this.RememberTitle(url, item.Title);
        this.SaveConfig();
        this.PlayAsync(url);
        this.log.Information($"[redbull] playing '{item.Title}'");
    });

    private void RedBullWork(Func<CancellationToken, Task> work) =>
        Background(ref this.redBullWork, work, ex =>
        {
            this.log.Warning($"[redbull] {ex.Message}");
            this.redBullSession = null;
            this.window.Library.RedBull.SetStatus("Red Bull TV did not answer: " + Ui.Ellipsis(ex.Message, 80));
        });

    private void OpenPbs(string slug) =>
        Background(ref this.pbsWork, async ct =>
        {
            var episodes = await this.Pbs.EpisodesAsync(slug, ct);
            this.window.Library.Pbs.SetEpisodes(slug, episodes, episodes.Count == 0 ? "PBS's page listed no episodes." : string.Empty);
            this.log.Information($"[pbs] {episodes.Count} episodes for {slug}");
        }, ex =>
        {
            this.log.Warning($"[pbs] {slug}: {ex.Message}");
            this.window.Library.Pbs.SetEpisodes(slug, [], "PBS did not answer: " + Ui.Ellipsis(ex.Message, 80));
        });

    private void BrowseNasa() =>
        Background(ref this.nasaWork, async ct =>
        {
            var topics = await this.Nasa.TopicsAsync(ct);
            this.window.Library.Nasa.SetTopics(topics);
            this.log.Information($"[nasa] {topics.Count} topics");
        }, ex => this.log.Warning($"[nasa] topics: {ex.Message}"));

    private void OpenNasa(int topic, string search)
    {
        var listing = search.Length > 0 ? "search:" + search : "topic:" + topic;
        Background(ref this.nasaWork, async ct =>
        {
            var items = await this.Nasa.VideosAsync(topic, search, ct);
            this.window.Library.Nasa.SetItems(listing, items, items.Count == 0 ? "NASA+ has nothing there." : string.Empty);
        }, ex =>
        {
            this.log.Warning($"[nasa] {ex.Message}");
            this.window.Library.Nasa.SetItems(listing, [], "NASA+ did not answer: " + Ui.Ellipsis(ex.Message, 80));
        });
    }

    private void BrowseTed() =>
        Background(ref this.tedWork, async ct =>
        {
            var playlists = await this.Ted.PlaylistsAsync(ct);
            this.window.Library.Ted.SetPlaylists(playlists);
            this.log.Information($"[ted] {playlists.Count} playlists");
        }, ex => this.log.Warning($"[ted] playlists: {ex.Message}"));

    private void OpenTed(TedTalks.Playlist? playlist, string search)
    {
        var listing = search.Length > 0 ? "search:" + search : playlist is { } p ? "playlist:" + p.Id : "newest";
        Background(ref this.tedWork, async ct =>
        {
            var items = search.Length > 0 ? await this.Ted.SearchAsync(search, ct)
                : playlist is { } pl ? await this.Ted.PlaylistAsync(pl, ct)
                : await this.Ted.NewestAsync(ct);
            this.window.Library.Ted.SetItems(listing, items, items.Count == 0 ? "TED has nothing there." : string.Empty);
        }, ex =>
        {
            this.log.Warning($"[ted] {ex.Message}");
            this.window.Library.Ted.SetItems(listing, [], "TED did not answer: " + Ui.Ellipsis(ex.Message, 80));
        });
    }

    private void BrowseDailymotion() =>
        Background(ref this.dailymotionWork, async ct =>
        {
            var channels = await this.Daily.ChannelsAsync(ct);
            this.window.Library.Dailymotion.SetChannels(channels);
            this.log.Information($"[dailymotion] {channels.Count} channels");
        }, ex => this.log.Warning($"[dailymotion] channels: {ex.Message}"));

    private void OpenDailymotion(string channel, string search)
    {
        var listing = search.Length > 0 ? "search:" + search : "channel:" + channel;
        Background(ref this.dailymotionWork, async ct =>
        {
            var items = await this.Daily.VideosAsync(channel, search, ct);
            this.window.Library.Dailymotion.SetItems(listing, items, items.Count == 0 ? "Dailymotion has nothing there." : string.Empty);
        }, ex =>
        {
            this.log.Warning($"[dailymotion] {ex.Message}");
            this.window.Library.Dailymotion.SetItems(listing, [], "Dailymotion did not answer: " + Ui.Ellipsis(ex.Message, 80));
        });
    }

    /// <summary>Runs one piece of shelf work at a time per shelf, the previous one cancelled, failures handed back.</summary>
    private static void Background(ref CancellationTokenSource? slot, Func<CancellationToken, Task> work, Action<Exception> failed)
    {
        slot?.Cancel();
        slot = new CancellationTokenSource();
        var token = slot.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await work(token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    failed(ex);
            }
        });
    }
}
