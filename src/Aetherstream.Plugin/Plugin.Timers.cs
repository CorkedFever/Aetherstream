using Aetherstream.Plugin.Video;
using Aetherstream.Plugin.Weather;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;


namespace Aetherstream.Plugin;

/// <summary>
/// The timers board's listing: the fixed resets and the boat from the clock, the retainers and
/// today's roulettes from the client. Rebuilt once a second.
/// </summary>
public sealed partial class Plugin
{
    private TimersSnapshot? timersSnapshot;
    private long timersSnapshotAtMs = -1;
    private List<(uint Id, string Name)>? rouletteList;

    private TimersSnapshot? TimersSnapshot()
    {
        var ticks = Environment.TickCount64;
        if (this.timersSnapshot is not null && ticks - this.timersSnapshotAtMs < 1000)
            return this.timersSnapshot;

        var utc = DateTime.UtcNow;
        var rows = new List<TimerRow>
        {
            new("Daily reset", "Tribal quests, daily hunts, mini cactpot", ResetTimers.NextDaily(utc), "reset"),
            new("Grand Company reset", "Supply and provisioning missions", ResetTimers.NextGrandCompany(utc), "reset"),
            new("Weekly reset", "Tomestones, raids, deliveries, Wondrous Tails, challenge log", ResetTimers.NextWeekly(utc), "reset"),
        };

        var voyage = ResetTimers.NextVoyage(utc);
        rows.Add(new TimerRow(
            "Ocean fishing",
            $"Indigo: {voyage.IndigoDestination} at {voyage.IndigoTime.ToLowerInvariant()}  /  Ruby: {voyage.RubyDestination} at {voyage.RubyTime.ToLowerInvariant()}",
            voyage.DepartsUtc,
            "boat"));

        var region = this.CurrentRegion();
        if (ResetTimers.NextJumboCactpot(utc, region) is { } draw)
            rows.Add(new TimerRow("Jumbo Cactpot draw", region switch { 1 => "Japan", 2 => "North America", 3 => "Europe", 4 => "Oceania", _ => "your region" }, draw, "cactpot"));

        var retainerNote = this.ReadRetainers(rows, utc);
        var roulettes = this.ReadRoulettes();

        this.timersSnapshot = new TimersSnapshot(rows, roulettes, retainerNote);
        this.timersSnapshotAtMs = ticks;
        return this.timersSnapshot;
    }

    private uint CurrentRegion()
    {
        try
        {
            return this.objects.LocalPlayer?.CurrentWorld.Value.DataCenter.Value.Region.RowId ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Each retainer out on a venture becomes a row; the note summarises the rest. The list is
    /// only populated once the retainer bell has been used this session, which is the game's
    /// doing, not ours.
    /// </summary>
    private unsafe string ReadRetainers(List<TimerRow> rows, DateTime utc)
    {
        if (this.objects.LocalPlayer is null)
            return "not logged in";

        try
        {
            var manager = RetainerManager.Instance();
            if (manager == null || !manager->IsReady)
                return "visit a summoning bell once and they appear here";

            var idle = 0;
            var away = 0;
            var count = manager->GetRetainerCount();
            for (var i = 0; i < count; i++)
            {
                var retainer = manager->GetRetainerBySortedIndex((uint)i);
                if (retainer == null || !retainer->Available)
                    continue;

                var name = retainer->NameString;
                if (retainer->VentureId == 0)
                {
                    idle++;
                    continue;
                }

                var back = DateTimeOffset.FromUnixTimeSeconds(retainer->VentureComplete).UtcDateTime;
                rows.Add(new TimerRow(name, back <= utc ? "back, at the bell" : "on a venture", back, "retainer"));
                away++;
            }

            return away == 0 && idle == 0 ? "no retainers"
                : $"{away} out on ventures, {idle} idle";
        }
        catch (Exception ex)
        {
            this.log.Debug($"[timers] retainers unreadable: {ex.Message}");
            return "could not read the retainer list";
        }
    }

    /// <summary>The duty finder's roulettes and whether each has been run since the daily reset.</summary>
    private unsafe List<RouletteRow> ReadRoulettes()
    {
        var result = new List<RouletteRow>();
        if (this.objects.LocalPlayer is null)
            return result;

        try
        {
            this.rouletteList ??= this.dataManager.GetExcelSheet<Lumina.Excel.Sheets.ContentRoulette>()
                .Where(r => r.IsInDutyFinder && !r.IsPvP && r.Name.ToString().Length > 0)
                .OrderBy(r => r.SortKey)
                .Select(r => (r.RowId, r.Name.ToString()))
                .ToList();

            var content = FFXIVClientStructs.FFXIV.Client.Game.UI.InstanceContent.Instance();
            if (content == null)
                return result;

            foreach (var (id, name) in this.rouletteList)
                result.Add(new RouletteRow(name, content->IsRouletteComplete((byte)id)));
        }
        catch (Exception ex)
        {
            this.log.Debug($"[timers] roulettes unreadable: {ex.Message}");
        }

        return result;
    }
}
