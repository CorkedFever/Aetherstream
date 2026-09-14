using Aetherstream.Plugin.Video;

namespace Aetherstream.Plugin;

/// <summary>
/// What the guide channel lists: the plugin's knowledge of pinned channels, live parties and
/// recent things, gathered into one snapshot a second.
/// </summary>
public sealed partial class Plugin
{
    private GuideSnapshot? guideSnapshot;
    private long guideSnapshotAtMs = -1;

    /// <summary>
    /// Builds the listing. Cheap, but not free — it walks the pins and the recents — so it is
    /// rebuilt once a second rather than thirty times, which is also as often as anything on it
    /// can change.
    /// </summary>
    private GuideSnapshot GuideSnapshot()
    {
        var now = Environment.TickCount64;
        if (this.guideSnapshot is { } cached && now - this.guideSnapshotAtMs < 1000)
            return cached;

        var rows = new List<GuideRow>();
        var source = this.config.Source;
        var dial = this.window.Dial;
        var utc = DateTime.UtcNow;

        // What is on right now, first, wherever it came from: its remaining run, then whatever is
        // queued after it, laid end to end from each one's length. A schedule computed, not looked up.
        // A live channel you are on but have not pinned still gets a row, at the top, with its listings.
        var currentChannel = dial.Find(source);
        if (currentChannel is { } tuned && this.session.IsPlaying && dial.NumberOf(source) == 0)
        {
            var group = tuned.Group.Length > 0 ? tuned.Group : "Live";
            rows.Add(new GuideRow("NOW", tuned.Name, group, Current: true, Offline: dial.IsOffline(tuned.Url), Slots: this.ListingsFor(tuned, utc)));
        }

        if (this.session.Current is { } playing && this.session.IsPlaying && currentChannel is null)
        {
            var slots = new List<GuideSlot>();
            var duration = this.session.DurationMs;
            var position = this.session.PositionMs;
            var cursor = utc;

            if (duration > 0 && position >= 0)
            {
                var ends = utc.AddMilliseconds(duration - position);
                slots.Add(new GuideSlot(utc.AddMilliseconds(-position), ends, playing.DisplayName, true));
                cursor = ends;

                foreach (var (_, label, _, length) in this.uiContext.NextUp)
                {
                    var span = length > 0 ? TimeSpan.FromMilliseconds(length) : TimeSpan.FromMinutes(30);
                    slots.Add(new GuideSlot(cursor, cursor + span, label, false));
                    cursor += span;
                    if (cursor > utc.AddHours(2))
                        break;
                }
            }
            else
            {
                slots.Add(new GuideSlot(utc.AddHours(-1), utc.AddHours(3), playing.DisplayName, true));
            }

            rows.Add(new GuideRow("NOW", playing.DisplayName, "playing", Current: true, Slots: slots));
        }

        // Pinned channels, by their number on the dial.
        foreach (var (number, channel) in dial.Pinned())
        {
            var detail = channel.Group.Length > 0
                ? channel.Country.Length > 0 ? $"{channel.Group} · {channel.Country}" : channel.Group
                : channel.Country.Length > 0 ? channel.Country : "Live";

            rows.Add(new GuideRow(
                number.ToString(),
                channel.Name,
                detail,
                Current: string.Equals(channel.Url, source, StringComparison.OrdinalIgnoreCase),
                Offline: dial.IsOffline(channel.Url),
                Slots: this.ListingsFor(channel, utc)));
        }

        // The rest of the lineup: whatever the Live TV tab is filtered to, unpinned, unnumbered.
        // Listings only for the first few dozen, since every one is a guide lookup a second.
        var listed = 0;
        foreach (var channel in this.window.LiveTv.Lineup(80))
        {
            if (dial.NumberOf(channel.Url) > 0 || string.Equals(channel.Url, source, StringComparison.OrdinalIgnoreCase))
                continue;

            var detail = channel.Group.Length > 0
                ? channel.Country.Length > 0 ? $"{channel.Group} / {channel.Country}" : channel.Group
                : channel.Country.Length > 0 ? channel.Country : "Live";

            rows.Add(new GuideRow(
                "--",
                channel.Name,
                detail,
                Offline: dial.IsOffline(channel.Url),
                Slots: listed++ < 40 ? this.ListingsFor(channel, utc) : null));
        }

        // Parties that are on the air right now.
        foreach (var group in this.window.Share.Parties)
        {
            if (!group.Live)
                continue;

            var name = group.Name.Length > 0 ? group.Name : Aetherstream.Playback.PartyDirectory.Pretty(group.Code);
            var title = group.Title.Length > 0 ? group.Title : "live";
            var watching = group.Members == 1 ? "1 watching" : $"{group.Members} watching";

            rows.Add(new GuideRow(
                "LIVE",
                name,
                $"{title} · {watching}",
                Current: group.WatchUrl.Length > 0 && string.Equals(group.WatchUrl, source, StringComparison.OrdinalIgnoreCase),
                Live: true));
        }

        // Recent things that are not channels: films, videos, anything on demand.
        var recents = 0;
        foreach (var recent in this.config.Recents)
        {
            if (dial.Find(recent.Source) is not null || recents >= 6)
                continue;

            var label = recent.Label.Length > 0 ? recent.Label : UI.Ui.Pretty(recent.Source);
            var detail = this.config.ResumePositions.TryGetValue(recent.Source, out var resumeMs) && resumeMs > 0
                ? $"On demand, resume at {UI.Ui.Clock(resumeMs)}"
                : "On demand";
            rows.Add(new GuideRow(
                "VOD",
                label,
                detail,
                Current: string.Equals(recent.Source, source, StringComparison.OrdinalIgnoreCase)));
            recents++;
        }

        if (rows.Count == 0)
            rows.Add(new GuideRow("--", "Nothing listed", "Pin channels in Live TV and they appear here"));

        // The channel's own name when it is one; a relayed channel's stream name is a hostname.
        var nowPlaying = currentChannel?.Name
            ?? (this.session.Current?.DisplayName is { Length: > 0 } onNow ? onNow : "Nothing on");
        if (currentChannel is { } named && this.OnNow(named) is { } programme)
            nowPlaying = $"{named.Name}: {programme}";

        var pinned = this.config.LiveTvFavourites.Count;
        var live = this.window.Share.Parties.Count(g => g.Live);
        var ticker =
            $"Aetherstream guide  ·  {pinned} channel{(pinned == 1 ? "" : "s")} pinned  ·  " +
            $"{live} part{(live == 1 ? "y" : "ies")} live  ·  " +
            "channel up and down on the remote still change channel  ·  " +
            "the Live TV tab's filters pick the lineup";

        this.guideSnapshot = new GuideSnapshot(rows, nowPlaying, ticker);
        this.guideSnapshotAtMs = now;
        return this.guideSnapshot;
    }
}
