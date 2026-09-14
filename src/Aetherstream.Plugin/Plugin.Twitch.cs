using Aetherstream.Playback;
using Aetherstream.Plugin.UI;

namespace Aetherstream.Plugin;

/// <summary>
/// The Library's Twitch shelf: listings through Twitch's GraphQL, and for the followed list, the
/// session borrowed from the browser set for YouTube, kept in memory for half an hour.
/// </summary>
public sealed partial class Plugin
{
    private TwitchBrowser? twitch;
    private CancellationTokenSource? twitchBrowse;
    private string? twitchToken;
    private string? twitchLogin;
    private long twitchTokenAtMs = -1;

    private TwitchBrowser Twitch => this.twitch ??= new TwitchBrowser(this.http);

    private void BrowseTwitch(string key)
    {
        this.twitchBrowse?.Cancel();
        this.twitchBrowse = new CancellationTokenSource();
        var token = this.twitchBrowse.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var games = new List<TwitchBrowser.Game>();
                List<TwitchBrowser.Stream> streams;
                var message = string.Empty;
                if (key == "followed")
                {
                    var (login, session) = await this.TwitchSessionAsync(token);
                    if (login is null || session is null)
                    {
                        this.window.Library.Twitch.SetStreams(key, [], [], "No Twitch session in that browser. Sign in to Twitch there, then try again.");
                        return;
                    }

                    streams = await this.Twitch.FollowedAsync(login, session, token);
                    message = streams.Count == 0 ? $"Nobody you follow is live right now, {login}." : string.Empty;
                }
                else if (key.StartsWith("game:", StringComparison.Ordinal))
                {
                    streams = await this.Twitch.ByGameAsync(key[5..], token);
                    message = streams.Count == 0 ? "Nobody is streaming that right now." : string.Empty;
                }
                else if (key.StartsWith("search:", StringComparison.Ordinal))
                {
                    var found = await this.Twitch.SearchAsync(key[7..], token);
                    streams = found.Channels;
                    games = found.Games;
                    message = streams.Count == 0 && games.Count == 0 ? "Nothing on Twitch by that name." : string.Empty;
                }
                else
                {
                    streams = await this.Twitch.TopAsync(token);
                }

                if (token.IsCancellationRequested)
                    return;

                this.window.Library.Twitch.SetStreams(key, streams, games, message);
                this.log.Information($"[twitch] {streams.Count} streams for {key}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.log.Warning($"[twitch] {key}: {ex.Message}");
                this.window.Library.Twitch.SetStreams(key, [], [], "Twitch did not answer: " + Ui.Ellipsis(ex.Message, 80));
            }
        });
    }

    /// <summary>The browser session's login and token, fetched through yt-dlp's cookie reading and kept for half an hour.</summary>
    private async Task<(string? Login, string? Token)> TwitchSessionAsync(CancellationToken ct)
    {
        var now = Environment.TickCount64;
        if (this.twitchToken is not null && now - this.twitchTokenAtMs < 30 * 60_000)
            return (this.twitchLogin, this.twitchToken);

        var ytDlp = YtDlpResolver.Locate(this.config.YtDlpPath, this.ToolDirectories());
        var value = await BrowserCookies.ValueAsync(ytDlp, this.config.YtDlpCookiesBrowser, this.config.YtDlpCookiesFile, "twitch.tv", "auth-token", "https://www.twitch.tv/twitch", ct);
        if (value is null)
            return (null, null);

        var login = await this.Twitch.WhoAmIAsync(value, ct);
        if (login is null)
            return (null, null);

        this.twitchToken = value;
        this.twitchLogin = login;
        this.twitchTokenAtMs = now;
        this.log.Information($"[twitch] session for {login} from the browser");
        return (login, value);
    }
}
