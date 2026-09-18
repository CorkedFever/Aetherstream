using Aetherstream.Plugin.Video;

using Dalamud.Game.Chat;
using Dalamud.Game.Text;

namespace Aetherstream.Plugin;

/// <summary>
/// The maintenance banner: what the game pushes to chat, and what the Lodestone has scheduled
/// within the hour, painted along the bottom of whatever is on.
/// </summary>
public sealed partial class Plugin
{
    private MaintenanceNotice? pushedNotice;
    private DateTime testNoticeUntil = DateTime.MinValue;

    /// <summary>
    /// The game's own push: "From Sep. 14 at 12:00 a.m. to 1:00 a.m. (PDT), emergency
    /// maintenance will take place on Gilgamesh World." It arrives as a notice, in purple, to
    /// everyone at once. This is the trigger worth having: no polling, no scrape, the moment it lands.
    /// </summary>
    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (message.LogKind is not (XivChatType.Notice or XivChatType.Urgent or XivChatType.SystemMessage))
            return;

        var text = message.Message.TextValue;
        if (text.Length < 20 || !text.Contains("maintenance", StringComparison.OrdinalIgnoreCase))
            return;

        var notice = MaintenanceBanner.FromChat(text, DateTime.UtcNow);
        if (notice is null)
            return;

        this.pushedNotice = notice;
        this.log.Information($"[alert] maintenance push: {notice.Worlds} {(notice.Emergency ? "emergency " : string.Empty)}{notice.Start:HH:mm}-{notice.End:HH:mm} UTC");
    }

    /// <summary>
    /// What the banner should show right now, or null. A push wins while its window is ahead or
    /// under way; otherwise the Lodestone's soonest maintenance once it is within the notice
    /// period. A push with no readable times shows for an hour after it arrived.
    /// </summary>
    private MaintenanceNotice? CurrentNotice()
    {
        var now = DateTime.UtcNow;

        if (now < this.testNoticeUntil)
        {
            var start = now.AddMinutes(47);
            return new MaintenanceNotice("Gilgamesh", true, start, start.AddHours(1), "test", now);
        }

        if (!this.config.MaintenanceBanner)
            return null;

        if (this.pushedNotice is { } pushed)
        {
            var until = pushed.End ?? pushed.Start?.AddHours(1) ?? pushed.Received.AddHours(1);
            if (now < until)
                return pushed;

            this.pushedNotice = null;
        }

        var ahead = TimeSpan.FromMinutes(this.config.MaintenanceBannerMinutes);
        MaintenanceNotice? soonest = null;
        foreach (var item in this.NewsSnapshot()?.Items ?? [])
        {
            if (item.Kind != "maintenance" || item.Start is not { } s || item.End is not { } e)
                continue;

            // Only the game itself: the Lodestone also schedules the Mog Station and the app.
            if (!item.Title.Contains("World", StringComparison.OrdinalIgnoreCase))
                continue;

            if (e <= now || s - now > ahead)
                continue;

            if (soonest is null || s < soonest.Start)
                soonest = MaintenanceBanner.FromNews(item);
        }

        return soonest;
    }

    /// <summary>Shows a made-up emergency notice for twenty seconds, so the banner can be seen without an outage.</summary>
    private void TestMaintenanceBanner() => this.testNoticeUntil = DateTime.UtcNow.AddSeconds(20);

    /// <summary>The overlay the session paints: the banner, whenever there is a notice to show.</summary>
    private sealed class MaintenanceOverlay(MaintenanceBanner banner, Func<MaintenanceNotice?> notice) : IFrameOverlay
    {
        private MaintenanceNotice? current;
        private long askedAtMs = -1;

        public bool Active
        {
            get
            {
                // The notice is looked up once a second, not once a frame: it walks the news list.
                var now = Environment.TickCount64;
                if (now - this.askedAtMs > 1000)
                {
                    this.askedAtMs = now;
                    this.current = notice();
                }

                return this.current is not null;
            }
        }

        public void Paint(uint[] target, int width, int height, DateTime now, double seconds)
        {
            if (this.current is { } n)
                banner.Paint(target, width, height, n, now, seconds);
        }
    }
}
