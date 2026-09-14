using Aetherstream.Plugin.Video;
using Aetherstream.Plugin.Weather;

using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Aetherstream.Plugin;

/// <summary>
/// The fishing board's listing: every fish with a window, its state this second, up ones first,
/// then the soonest; the fish already in your log left out unless asked for. Plus the next boats.
/// </summary>
public sealed partial class Plugin
{
    private FishingLog? fishingLog;
    private FishingSnapshot? fishingSnapshot;
    private long fishingSnapshotAtMs = -1;

    private FishingSnapshot? FishingSnapshot()
    {
        var ticks = Environment.TickCount64;
        if (this.fishingSnapshot is not null && ticks - this.fishingSnapshotAtMs < 1000)
            return this.fishingSnapshot;

        this.fishingLog ??= new FishingLog(this.fishDataPath, this.dataManager, this.weather, this.log);
        if (!this.fishingLog.Available)
            return null;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        const long Horizon = 36 * 3600;
        var here = this.clientState.TerritoryType;
        var showCaught = this.config.FishingShowCaught;

        var rows = new List<FishingRow>();
        var caughtCount = 0;
        foreach (var f in this.fishingLog.All)
        {
            var caught = this.IsCaught(f.FishParameterId);
            if (caught)
                caughtCount++;

            if (caught && !showCaught)
                continue;

            var windows = this.fishingLog.Windows(f, now - EorzeaWeather.PeriodSeconds, now + Horizon);
            FishingLog.Window? current = null;
            FishingLog.Window? next = null;
            foreach (var w in windows)
            {
                if (w.From <= now && now < w.To)
                    current = w;
                else if (w.From > now && next is null)
                    next = w;
            }

            if (current is null && next is null)
                continue;

            var up = current is { } c;
            var window = current ?? next!.Value;
            var left = (int)(up ? window.To - now : window.From - now);

            var conditions = string.Join(" ", new[]
            {
                f.PreviousNames.Length > 0 ? $"AFTER {string.Join("/", f.PreviousNames)}" : string.Empty,
                f.WeatherNames.Length > 0 ? string.Join("/", f.WeatherNames) : string.Empty,
                f.StartHour == 0 && f.EndHour == 24 ? string.Empty : $"{Clock(f.StartHour)}-{Clock(f.EndHour)} ET",
            }.Where(s => s.Length > 0));

            var bait = f.Bait.Length > 0 ? string.Join(" > ", f.Bait) : string.Empty;
            if (f.Predators.Count > 0)
                bait = (bait.Length > 0 ? bait + "  " : string.Empty) + "INTUITION " + string.Join(", ", f.Predators.Select(p => $"{p.Value}x {p.Key}"));

            rows.Add(new FishingRow(
                f.Name,
                f.Spot,
                f.Zone,
                conditions,
                bait,
                up,
                left,
                (int)(window.From * 24 / 70 % 1440),
                (int)(window.To * 24 / 70 % 1440),
                f.Folklore,
                caught,
                f.TerritoryId == here));
        }

        // Up now, soonest to close, first; then the rest by how soon they open. The ones at the
        // spot you are standing at sort ahead of the rest in each group.
        var ordered = rows
            .OrderByDescending(r => r.Up)
            .ThenByDescending(r => r.Here)
            .ThenBy(r => r.SecondsLeft)
            .Take(40)
            .ToList();

        var voyages = new List<ResetTimers.Voyage>();
        var at = DateTime.UtcNow;
        for (var i = 0; i < 4; i++)
        {
            var v = ResetTimers.NextVoyage(at);
            voyages.Add(v);
            at = v.DepartsUtc.AddMinutes(20);
        }

        this.fishingSnapshot = new FishingSnapshot(ordered, rows.Count(r => r.Up), this.fishingLog.All.Count, caughtCount, voyages);
        this.fishingSnapshotAtMs = ticks;
        return this.fishingSnapshot;
    }

    private static string Clock(double hour) => $"{(int)hour:00}:{(int)Math.Round((hour % 1) * 60):00}";

    private unsafe bool IsCaught(uint fishParameterId)
    {
        if (fishParameterId == 0)
            return false;

        try
        {
            var player = PlayerState.Instance();
            return player != null && player->IsFishCaught(fishParameterId);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
