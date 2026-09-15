using Aetherstream.Playback;
using Aetherstream.Plugin.UI;

namespace Aetherstream.Plugin;

/// <summary>The Library's Jellyfin or Emby shelf: a sign-in kept as a token, listings off the render thread, and playback of the file straight from the server.</summary>
public sealed partial class Plugin
{
    private CancellationTokenSource? mediaServerWork;

    private MediaServer? MediaServerClient =>
        this.config.MediaServerUrl.Length > 0 && this.config.MediaServerToken.Length > 0
            ? new MediaServer(this.http, this.config.MediaServerUrl, this.config.MediaServerToken, this.config.MediaServerUserId, this.config.ClientId)
            : null;

    private void SignInMediaServer(string address, string user, string password) =>
        Background(ref this.mediaServerWork, async ct =>
        {
            var server = MediaServer.Normalise(address);
            var identity = await MediaServer.IdentifyAsync(this.http, server, ct);
            var signedIn = await MediaServer.SignInAsync(this.http, server, user, password, this.config.ClientId, ct);
            this.config.MediaServerUrl = server;
            this.config.MediaServerToken = signedIn.Token;
            this.config.MediaServerUserId = signedIn.UserId;
            this.config.MediaServerUserName = signedIn.UserName;
            this.config.MediaServerName = identity.Name.Length > 0 ? identity.Name : identity.Kind;
            this.config.MediaServerKind = identity.Kind;
            this.SaveConfig();
            this.window.Library.MediaServer.Reset();
            this.log.Information($"[mediaserver] signed in to {identity.Kind} '{identity.Name}' {identity.Version} as {signedIn.UserName}");
        }, ex =>
        {
            this.log.Warning($"[mediaserver] sign-in: {ex.Message}");
            this.window.Library.MediaServer.SetStatus("Could not sign in: " + Ui.Ellipsis(ex.Message, 100));
        });

    /// <summary>Forgets the token. The server keeps its record of the session until it is revoked there.</summary>
    private void SignOutMediaServer()
    {
        this.config.MediaServerToken = string.Empty;
        this.config.MediaServerUserId = string.Empty;
        this.config.MediaServerUserName = string.Empty;
        this.SaveConfig();
        this.window.Library.MediaServer.Reset();
    }

    private void BrowseMediaServer() =>
        Background(ref this.mediaServerWork, async ct =>
        {
            if (this.MediaServerClient is not { } client)
                return;
            var views = await client.ViewsAsync(ct);
            this.window.Library.MediaServer.SetViews(views);
            this.log.Information($"[mediaserver] {views.Count} libraries");
        }, ex => this.MediaServerFailed(ex));

    private void OpenMediaServer(string viewOrContinue, string search)
    {
        var listing = search.Length > 0 ? "search:" + search : viewOrContinue;
        Background(ref this.mediaServerWork, async ct =>
        {
            if (this.MediaServerClient is not { } client)
                return;
            var items = viewOrContinue == "continue" && search.Length == 0
                ? await client.ContinueAsync(ct)
                : await client.ItemsAsync(viewOrContinue == "continue" ? string.Empty : viewOrContinue, search, ct);
            this.window.Library.MediaServer.SetItems(listing, items, items.Count == 0 ? (viewOrContinue == "continue" && search.Length == 0 ? "Nothing part-way through." : "Nothing there.") : string.Empty);
        }, ex => this.MediaServerFailed(ex));
    }

    private void OpenMediaServerSeries(MediaServer.Item series) =>
        Background(ref this.mediaServerWork, async ct =>
        {
            if (this.MediaServerClient is not { } client)
                return;
            var episodes = await client.EpisodesAsync(series.Id, ct);
            this.window.Library.MediaServer.SetEpisodes(series, episodes, episodes.Count == 0 ? "The server lists no episodes for it." : string.Empty);
        }, ex => this.MediaServerFailed(ex));

    private (string Stream, string Image) MediaServerAddresses(MediaServer.Item item) =>
        this.MediaServerClient is { } client ? (client.StreamUrl(item), client.ImageUrl(item)) : (string.Empty, string.Empty);

    private void MediaServerFailed(Exception ex)
    {
        this.log.Warning($"[mediaserver] {ex.Message}");
        this.window.Library.MediaServer.SetStatus($"{this.config.MediaServerKind} did not answer: " + Ui.Ellipsis(ex.Message, 100));
    }
}
