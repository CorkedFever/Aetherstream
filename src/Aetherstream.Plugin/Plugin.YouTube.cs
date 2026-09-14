using Aetherstream.Playback;
using Aetherstream.Plugin.UI;

namespace Aetherstream.Plugin;

/// <summary>The Library's YouTube shelf: listings through yt-dlp, off the render thread.</summary>
public sealed partial class Plugin
{
    private CancellationTokenSource? youTubeBrowse;

    private void BrowseYouTube(string target)
    {
        var executable = YtDlpResolver.Locate(this.config.YtDlpPath, this.ToolDirectories());
        if (executable is null)
        {
            this.window.Library.YouTube.SetVideos(target, [], "yt-dlp is not installed.");
            return;
        }

        this.youTubeBrowse?.Cancel();
        this.youTubeBrowse = new CancellationTokenSource();
        var token = this.youTubeBrowse.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var browser = new YtDlpBrowser(executable, this.config.YtDlpCookiesBrowser, this.config.YtDlpCookiesFile);
                var videos = await browser.ListAsync(target, token);
                if (token.IsCancellationRequested)
                    return;

                var message = videos.Count == 0
                    ? (YtDlpBrowser.NeedsCookies(target) ? "Nothing came back. Are the cookies from a browser that is signed in to YouTube?" : "Nothing found.")
                    : string.Empty;
                this.window.Library.YouTube.SetVideos(target, videos, message);
                this.log.Information($"[youtube] {videos.Count} videos for {target}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                this.log.Warning($"[youtube] {target}: {ex.Message}");
                this.window.Library.YouTube.SetVideos(target, [], Ui.Ellipsis(ex.Message, 120));
            }
        });
    }
}
