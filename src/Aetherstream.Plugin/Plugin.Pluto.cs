using Aetherstream.Playback;
using Aetherstream.Plugin.UI;

namespace Aetherstream.Plugin;

/// <summary>The Library's Pluto shelf: a session kept for a few hours, listings in the background, and playback through the direct path.</summary>
public sealed partial class Plugin
{
    private PlutoLibrary? pluto;
    private PlutoLibrary.Session? plutoSession;
    private CancellationTokenSource? plutoWork;

    private PlutoLibrary Pluto => this.pluto ??= new PlutoLibrary(this.http);

    private async Task<PlutoLibrary.Session> PlutoSessionAsync(CancellationToken ct)
    {
        if (this.plutoSession is { } s && DateTime.UtcNow - s.MadeUtc < TimeSpan.FromHours(3))
            return s;

        var made = await this.Pluto.BootAsync(ct);
        this.plutoSession = made;
        this.log.Information("[pluto] new session");
        return made;
    }

    private void BrowsePluto() => this.PlutoWork(async ct =>
    {
        var s = await this.PlutoSessionAsync(ct);
        var categories = await this.Pluto.CategoriesAsync(s, ct);
        this.window.Library.Pluto.SetCategories(categories, categories.Count == 0 ? "Pluto has nothing on demand for your country." : string.Empty);
        this.log.Information($"[pluto] {categories.Count} shelves");
    });

    private void SearchPluto(string query) => this.PlutoWork(async ct =>
    {
        var s = await this.PlutoSessionAsync(ct);
        var items = await this.Pluto.SearchAsync(s, query, ct);
        this.window.Library.Pluto.SetSearch(items, items.Count == 0 ? $"Nothing on Pluto for \"{query}\"." : string.Empty);
    });

    private void OpenPlutoSeries(PlutoLibrary.Item series) => this.PlutoWork(async ct =>
    {
        var s = await this.PlutoSessionAsync(ct);
        var seasons = await this.Pluto.SeasonsAsync(s, series.Id, ct);
        this.window.Library.Pluto.SetSeasons(series, seasons, seasons.Count == 0 ? "Pluto lists no episodes for it." : string.Empty);
    });

    /// <summary>Plays a film or an episode, the rest of its list queued after it.</summary>
    private void PlayPluto(PlutoLibrary.Item item, IReadOnlyList<PlutoLibrary.Item> rest) => this.PlutoWork(async ct =>
    {
        var s = await this.PlutoSessionAsync(ct);
        if (item.HlsPath.Length == 0)
        {
            this.window.Library.Pluto.SetStatus($"\"{item.Name}\" has no stream Pluto will hand out.");
            return;
        }

        var url = PlutoLibrary.StreamUrl(s, item);
        var label = item.Type == "episode" || item.Number > 0 ? $"{item.Name} (S{item.Season}E{item.Number})" : item.Name;
        this.uiContext.NextUp.Clear();
        foreach (var later in rest)
        {
            if (later.HlsPath.Length > 0)
                this.uiContext.NextUp.Add((PlutoLibrary.StreamUrl(s, later), $"{later.Name} (S{later.Season}E{later.Number})", later.Wide.Length > 0 ? later.Wide : later.Poster, (long)(later.Seconds * 1000)));
        }

        this.config.Source = url;
        this.config.Remember(url, label, item.Poster.Length > 0 ? item.Poster : item.Wide);
        this.RememberTitle(url, label);
        this.SaveConfig();
        this.PlayAsync(url);
        this.log.Information($"[pluto] playing '{label}'");
    });

    private void PlutoWork(Func<CancellationToken, Task> work)
    {
        this.plutoWork?.Cancel();
        this.plutoWork = new CancellationTokenSource();
        var token = this.plutoWork.Token;
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
                this.log.Warning($"[pluto] {ex.Message}");
                this.plutoSession = null;
                this.window.Library.Pluto.SetStatus("Pluto did not answer: " + Ui.Ellipsis(ex.Message, 80));
            }
        });
    }
}
