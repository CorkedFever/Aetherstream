using Aetherstream.Playback;

namespace Aetherstream.Plugin;

/// <summary>
/// The Internet Archive behind the library's Archive shelf: searches in the background, and
/// picks a film's best file before handing it to the player as a direct URL.
/// </summary>
public sealed partial class Plugin
{
    private ArchiveLibrary? archive;
    private CancellationTokenSource? archiveSearch;

    private ArchiveLibrary Archive => this.archive ??= new ArchiveLibrary(this.http);

    private void SearchArchive(string collection, string query)
    {
        this.archiveSearch?.Cancel();
        this.archiveSearch = new CancellationTokenSource();
        var token = this.archiveSearch.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var films = await this.Archive.SearchAsync(collection, query, token);
                if (token.IsCancellationRequested)
                    return;

                var message = films.Count == 0
                    ? (query.Length > 0 ? $"Nothing in the archive for \"{query}\"." : "The archive has nothing on that shelf.")
                    : string.Empty;
                this.window.Library.Archive.SetFilms(films, message);
                this.log.Information($"[archive] {films.Count} films for '{query}' in {collection}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.log.Warning($"[archive] search failed: {ex.Message}");
                this.window.Library.Archive.SetStatus("The archive did not answer. It does that at busy times; try again in a moment.");
            }
        });
    }

    private void OpenArchiveFilm(ArchiveLibrary.Film film)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var pick = await this.Archive.PickAsync(film.Identifier, CancellationToken.None);
                this.window.Library.Archive.SetOpening(string.Empty);
                if (pick is not { } chosen)
                {
                    this.window.Library.Archive.SetStatus($"\"{film.Title}\" has no MP4 in the archive, only a format that does not stream well.");
                    return;
                }

                var label = film.Year.Length > 0 ? $"{film.Title} ({film.Year})" : film.Title;
                this.log.Information($"[archive] '{label}': {chosen.FileName} {chosen.Width}x{chosen.Height} {chosen.Bytes / 1e6:0} MB");
                this.window.Library.Archive.SetStatus(string.Empty);
                this.PlayArchive(chosen.Url, label, film.Poster);
            }
            catch (Exception ex)
            {
                this.log.Warning($"[archive] could not open '{film.Title}': {ex.Message}");
                this.window.Library.Archive.SetOpening(string.Empty);
                this.window.Library.Archive.SetStatus("The archive did not answer for that one. Try again in a moment.");
            }
        });
    }

    /// <summary>Plays a film's file and records it, with its poster, so it comes back through Recents.</summary>
    private void PlayArchive(string url, string label, string poster)
    {
        this.config.Source = url;
        this.config.Remember(url, label, poster);
        this.RememberTitle(url, label);
        this.SaveConfig();
        this.PlayAsync(url);
    }
}
