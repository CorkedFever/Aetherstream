using Aetherstream.Playback;
using Aetherstream.Plugin.UI;

namespace Aetherstream.Plugin;

/// <summary>The Library's Red Bull TV and PBS shelves: sessions, listings and playback, off the render thread.</summary>
public sealed partial class Plugin
{
    private RedBullTv? redBull;
    private RedBullTv.Session? redBullSession;
    private CancellationTokenSource? redBullWork;
    private PbsShows? pbs;
    private CancellationTokenSource? pbsWork;

    private RedBullTv RedBull => this.redBull ??= new RedBullTv(this.http);

    private PbsShows Pbs => this.pbs ??= new PbsShows(this.http);

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

    private void RedBullWork(Func<CancellationToken, Task> work)
    {
        this.redBullWork?.Cancel();
        this.redBullWork = new CancellationTokenSource();
        var token = this.redBullWork.Token;
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
                this.log.Warning($"[redbull] {ex.Message}");
                this.redBullSession = null;
                this.window.Library.RedBull.SetStatus("Red Bull TV did not answer: " + Ui.Ellipsis(ex.Message, 80));
            }
        });
    }

    private void OpenPbs(string slug)
    {
        this.pbsWork?.Cancel();
        this.pbsWork = new CancellationTokenSource();
        var token = this.pbsWork.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var episodes = await this.Pbs.EpisodesAsync(slug, token);
                this.window.Library.Pbs.SetEpisodes(slug, episodes, episodes.Count == 0 ? "PBS's page listed no episodes." : string.Empty);
                this.log.Information($"[pbs] {episodes.Count} episodes for {slug}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.log.Warning($"[pbs] {slug}: {ex.Message}");
                this.window.Library.Pbs.SetEpisodes(slug, [], "PBS did not answer: " + Ui.Ellipsis(ex.Message, 80));
            }
        });
    }
}
