using Dalamud.Plugin.Services;

using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin.Weather;

/// <summary>
/// The timed gathering nodes — unspoiled, legendary, ephemeral — and when each is up.
/// <para>
/// Every node's windows are in the game's own tables as Eorzean clock times, so which ones are
/// up and which come next is arithmetic on the clock, the same as the weather. Read once at
/// startup from the data files; the current patch's nodes are always the ones listed.
/// </para>
/// </summary>
internal sealed class GatheringTimers
{
    /// <summary>A window in Eorzean minutes of the day: start, and how long.</summary>
    public readonly record struct Window(int StartMinute, int Minutes);

    public sealed record Node(
        uint BaseId,
        string Kind,
        string Type,
        int Level,
        string Place,
        string Zone,
        IReadOnlyList<string> Items,
        IReadOnlyList<Window> Windows);

    /// <summary>A node with where it is in its cycle right now.</summary>
    public readonly record struct Status(Node Node, bool Up, int RealSecondsLeft, int WindowStart, int WindowEnd);

    private readonly List<Node> nodes = [];

    public bool Available => this.nodes.Count > 0;

    public int Count => this.nodes.Count;

    public GatheringTimers(IDataManager data, IPluginLog log)
    {
        try
        {
            var points = data.GetExcelSheet<GatheringPoint>();
            var transients = data.GetExcelSheet<GatheringPointTransient>();
            var gatheringItems = data.GetExcelSheet<GatheringItem>();
            var items = data.GetExcelSheet<Item>();
            var types = data.GetExcelSheet<GatheringType>();

            var seen = new HashSet<uint>();
            foreach (var point in points)
            {
                if (!transients.TryGetRow(point.RowId, out var transient))
                    continue;

                var timed = transient.GatheringRarePopTimeTable.RowId != 0;
                var ephemeral = transient.EphemeralStartTime != 65535 && transient.EphemeralEndTime != 65535
                    && !(transient.EphemeralStartTime == 0 && transient.EphemeralEndTime == 0);
                if (!timed && !ephemeral)
                    continue;

                var baseId = point.GatheringPointBase.RowId;
                if (baseId == 0 || !seen.Add(baseId))
                    continue;

                var gpBase = point.GatheringPointBase.Value;
                if (point.TerritoryType.RowId == 0 || point.PlaceName.RowId == 0)
                    continue;

                // The windows: up to three timed ones, or one ephemeral span (which may wrap midnight).
                var windows = new List<Window>();
                if (timed)
                {
                    var table = transient.GatheringRarePopTimeTable.Value;
                    for (var i = 0; i < table.StartTime.Count && i < table.Duration.Count; i++)
                    {
                        var start = table.StartTime[i];
                        var duration = table.Duration[i];
                        if (start == 65535 || duration == 0)
                            continue;

                        windows.Add(new Window(Clock(start), Clock(duration)));
                    }
                }
                else
                {
                    var start = Clock(transient.EphemeralStartTime);
                    var end = Clock(transient.EphemeralEndTime);
                    var minutes = end > start ? end - start : (1440 - start) + end;
                    windows.Add(new Window(start, minutes));
                }

                if (windows.Count == 0)
                    continue;

                // What it yields, by name, hidden entries skipped; the first few is all a board can show.
                var names = new List<string>();
                foreach (var entry in gpBase.Item)
                {
                    if (entry.RowId == 0 || !gatheringItems.TryGetRow(entry.RowId, out var gi))
                        continue;

                    if (gi.IsHidden || gi.Item.RowId == 0 || !items.TryGetRow(gi.Item.RowId, out var item))
                        continue;

                    var name = item.Name.ToString();
                    if (name.Length > 0 && !names.Contains(name))
                        names.Add(name);
                }

                if (names.Count == 0)
                    continue;

                var typeName = types.TryGetRow(gpBase.GatheringType.RowId, out var type) ? type.Name.ToString() : "?";
                var zone = point.TerritoryType.Value.PlaceName.Value.Name.ToString();

                this.nodes.Add(new Node(
                    baseId,
                    ephemeral ? "Ephemeral" : gpBase.GatheringLevel >= 70 ? "Legendary" : "Unspoiled",
                    typeName,
                    gpBase.GatheringLevel,
                    point.PlaceName.Value.Name.ToString(),
                    zone,
                    names,
                    windows));
            }

            log.Information($"[gathering] {this.nodes.Count} timed nodes");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not read the gathering tables; the gathering channel is off.");
        }
    }

    /// <summary>The tables store clock times as HHMM; durations use the same digits (160 is an hour and sixty minutes).</summary>
    private static int Clock(int hhmm) => ((hhmm / 100) * 60) + (hhmm % 100);

    /// <summary>
    /// Every node with where it stands right now: up, with real seconds until it closes, or
    /// down, with real seconds until it opens. A minute of Eorzean time is 175/60 real seconds.
    /// </summary>
    public List<Status> Now(long unix)
    {
        var etMinuteOfDay = (int)(unix * 24 / 70 % 1440);
        var result = new List<Status>(this.nodes.Count);

        foreach (var node in this.nodes)
        {
            var bestUntil = int.MaxValue;
            Status? best = null;

            foreach (var w in node.Windows)
            {
                var end = (w.StartMinute + w.Minutes) % 1440;
                var sinceStart = ((etMinuteOfDay - w.StartMinute) % 1440 + 1440) % 1440;

                if (sinceStart < w.Minutes)
                {
                    // Up now. Seconds left is the remainder of the window, in real time.
                    var leftEt = w.Minutes - sinceStart;
                    best = new Status(node, true, RealSeconds(leftEt), w.StartMinute, end);
                    bestUntil = -1;
                    break;
                }

                var untilEt = ((w.StartMinute - etMinuteOfDay) % 1440 + 1440) % 1440;
                if (untilEt < bestUntil)
                {
                    bestUntil = untilEt;
                    best = new Status(node, false, RealSeconds(untilEt), w.StartMinute, end);
                }
            }

            if (best is { } status)
                result.Add(status);
        }

        return result;
    }

    private static int RealSeconds(int etMinutes) => (int)Math.Round(etMinutes * 175.0 / 60.0);
}
