using Aetherstream.Plugin.Video;
using Aetherstream.Plugin.Weather;

namespace Aetherstream.Plugin;

/// <summary>
/// The gathering board's listing: every timed node's state this second, up ones first, then the
/// soonest. Rebuilt once a second; the countdowns are what change, and once a second is what a
/// countdown needs.
/// </summary>
public sealed partial class Plugin
{
    private GatheringTimers? gathering;
    private GatheringSnapshot? gatheringSnapshot;
    private long gatheringSnapshotAtMs = -1;

    private GatheringSnapshot? GatheringSnapshot()
    {
        var ticks = Environment.TickCount64;
        if (this.gatheringSnapshot is not null && ticks - this.gatheringSnapshotAtMs < 1000)
            return this.gatheringSnapshot;

        this.gathering ??= new GatheringTimers(this.dataManager, this.log);
        if (!this.gathering.Available)
            return null;

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var statuses = this.gathering.Now(unix);

        // Up now, soonest to close, first; then the rest by how soon they open. Capped, since a
        // board that lists every node in the game is a list, not a board.
        var ordered = statuses
            .OrderByDescending(s => s.Up)
            .ThenBy(s => s.RealSecondsLeft)
            .Take(30)
            .Select(s => new GatheringRow(
                s.Node.Kind,
                s.Node.Type,
                s.Node.Level,
                s.Node.Place,
                s.Node.Zone,
                string.Join(", ", s.Node.Items.Take(3)),
                s.Up,
                s.RealSecondsLeft,
                s.WindowStart,
                s.WindowEnd))
            .ToList();

        this.gatheringSnapshot = new GatheringSnapshot(ordered, statuses.Count(s => s.Up), statuses.Count);
        this.gatheringSnapshotAtMs = ticks;
        return this.gatheringSnapshot;
    }
}
