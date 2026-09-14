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
        var tasks = this.ReadTasks(rows, utc);
        var estate = this.ReadEstate(rows, utc);

        this.timersSnapshot = new TimersSnapshot(rows, roulettes, retainerNote, tasks, estate);
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

    /// <summary>
    /// The allowances and journals the game's own Timers window lists, read from the same places
    /// it reads them. Anything that cannot be read is left off rather than guessed.
    /// </summary>
    private unsafe List<TaskRow> ReadTasks(List<TimerRow> rows, DateTime utc)
    {
        var tasks = new List<TaskRow>();
        if (this.objects.LocalPlayer is null)
            return tasks;

        try
        {
            var quests = QuestManager.Instance();
            if (quests != null)
            {
                var leves = quests->NumLeveAllowances;
                var nextLeves = QuestManager.GetNextLeveAllowancesDateTime();
                var nextLocal = nextLeves.Kind == DateTimeKind.Utc ? nextLeves.ToLocalTime() : nextLeves;
                tasks.Add(new TaskRow("Leve allowances", $"{leves} in hand, +3 at {nextLocal:h:mm tt}", leves >= 100));

                var tribe = quests->GetBeastTribeAllowance();
                tasks.Add(new TaskRow("Allied society quests", $"{tribe} of 12 left today", tribe == 0));
            }

            var supply = SatisfactionSupplyManager.Instance();
            if (supply != null)
            {
                var left = supply->GetRemainingAllowances();
                tasks.Add(new TaskRow("Custom deliveries", $"{left} of 12 left this week", left == 0));
            }

            var player = PlayerState.Instance();
            if (player != null)
            {
                if (player->HasWeeklyBingoJournal)
                {
                    var stickers = player->WeeklyBingoNumPlacedStickers;
                    var expired = player->IsWeeklyBingoExpired();
                    var expires = player->WeeklyBingoExpireDateTime;
                    var expiresLocal = expires.Kind == DateTimeKind.Utc ? expires.ToLocalTime() : expires;
                    tasks.Add(new TaskRow(
                        "Wondrous Tails",
                        expired ? "journal expired, get a new one" : $"{stickers} of 9 stickers, due {expiresLocal:ddd h:mm tt}",
                        stickers >= 9 || expired));
                }
                else
                {
                    tasks.Add(new TaskRow("Wondrous Tails", "no journal this week", false));
                }

                var mission = player->SquadronMissionCompletionTimestamp;
                if (mission > 0)
                {
                    var back = DateTimeOffset.FromUnixTimeSeconds(mission).UtcDateTime;
                    var done = back <= utc;
                    tasks.Add(new TaskRow("Squadron mission", done ? "complete, report to the barracks" : $"back {back.ToLocalTime():h:mm tt}", done));
                    if (!done)
                        rows.Add(new TimerRow("Squadron mission", "out in the field", back, "squadron"));
                }
                else
                {
                    tasks.Add(new TaskRow("Squadron mission", "none under way", false));
                }
            }

            // Fashion Report is a fixed window: judging opens Friday 08:00 UTC and closes at the weekly reset.
            var daysToFriday = ((int)DayOfWeek.Friday - (int)utc.DayOfWeek + 7) % 7;
            var friday = new DateTime(utc.Year, utc.Month, utc.Day, 8, 0, 0, DateTimeKind.Utc).AddDays(daysToFriday);
            if (friday > utc.AddDays(4))
                friday = friday.AddDays(-7);
            var judgingOpen = utc >= friday && utc < ResetTimers.NextWeekly(utc);
            tasks.Add(new TaskRow("Fashion Report", judgingOpen ? $"judging open until {ResetTimers.NextWeekly(utc).ToLocalTime():ddd h:mm tt}" : $"judging opens {(friday > utc ? friday : friday.AddDays(7)).ToLocalTime():ddd h:mm tt}", false));
        }
        catch (Exception ex)
        {
            this.log.Debug($"[timers] tasks unreadable: {ex.Message}");
        }

        return tasks;
    }

    private readonly List<(string Name, string Kind, DateTime Back)> vessels = [];
    private bool vesselsSeen;

    /// <summary>
    /// Submersibles and airships out on voyages. The workshop's data is only in memory while
    /// you stand in the workshop, so what was seen there is kept and keeps counting down.
    /// </summary>
    private unsafe List<TaskRow> ReadEstate(List<TimerRow> rows, DateTime utc)
    {
        var estate = new List<TaskRow>();
        if (this.objects.LocalPlayer is null)
            return estate;

        try
        {
            var housing = HousingManager.Instance();
            var workshop = housing != null ? housing->WorkshopTerritory : null;
            if (workshop != null && workshop->IsLoaded())
            {
                this.vessels.Clear();
                this.vesselsSeen = true;

                foreach (ref var sub in workshop->Submersible.Data)
                {
                    var name = sub.NameString;
                    if (name.Length == 0 || sub.ReturnTime == 0)
                        continue;

                    this.vessels.Add((name, "Submersible", DateTimeOffset.FromUnixTimeSeconds(sub.ReturnTime).UtcDateTime));
                }

                foreach (ref var ship in workshop->Airship.Data)
                {
                    var name = ship.NameString;
                    if (name.Length == 0 || ship.ReturnTime == 0)
                        continue;

                    this.vessels.Add((name, "Airship", DateTimeOffset.FromUnixTimeSeconds(ship.ReturnTime).UtcDateTime));
                }
            }
        }
        catch (Exception ex)
        {
            this.log.Debug($"[timers] workshop unreadable: {ex.Message}");
        }

        if (!this.vesselsSeen)
            return estate;

        if (this.vessels.Count == 0)
        {
            estate.Add(new TaskRow("Workshop", "no vessels out", false));
            return estate;
        }

        foreach (var (name, kind, back) in this.vessels)
        {
            var done = back <= utc;
            estate.Add(new TaskRow($"{kind} {name}", done ? "returned, collect at the workshop" : $"back {back.ToLocalTime():ddd h:mm tt}", done));
            if (!done)
                rows.Add(new TimerRow(name, $"{kind.ToLowerInvariant()} on a voyage", back, "vessel"));
        }

        return estate;
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
                .Where(r => r.IsInDutyFinder && !r.IsPvP && !r.IsGoldSaucer && r.Name.ToString().Length > 0)
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
