using Dalamud.Plugin.Services;

using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin.Weather;

/// <summary>
/// Eorzea's weather, computed.
/// <para>
/// The game does not roll weather; it derives it from the clock. Every eight Eorzean hours
/// (1400 real seconds) a number from 0 to 99 is drawn from the time, and each zone has a table
/// of weathers with cumulative odds that the number is looked up in. That is the whole thing,
/// and it is why every forecast site agrees to the minute — they all run this. Reading the
/// tables out of the game's own data means the zones and their odds are always the current
/// patch's.
/// </para>
/// </summary>
internal sealed class EorzeaWeather
{
    /// <summary>Real seconds per weather period.</summary>
    public const int PeriodSeconds = 1400;

    public readonly record struct Zone(uint TerritoryId, string Name, string Region, uint RateId);

    private readonly Dictionary<uint, string> weatherNames = [];
    private readonly Dictionary<uint, (uint WeatherId, int Cumulative)[]> rates = [];
    private readonly Dictionary<uint, Zone> zonesByTerritory = [];
    private readonly Dictionary<string, Zone> zonesByPlace = new(StringComparer.Ordinal);
    private readonly List<Zone> around = [];

    public bool Available { get; }

    public EorzeaWeather(IDataManager data, IPluginLog log)
    {
        try
        {
            var weathers = data.GetExcelSheet<Lumina.Excel.Sheets.Weather>();
            var rateSheet = data.GetExcelSheet<WeatherRate>();
            var territories = data.GetExcelSheet<TerritoryType>();

            foreach (var w in weathers)
            {
                var name = w.Name.ToString();
                if (name.Length > 0)
                    this.weatherNames[w.RowId] = name;
            }

            foreach (var r in rateSheet)
            {
                var table = new List<(uint, int)>();
                var acc = 0;
                for (var i = 0; i < r.Rate.Count && i < r.Weather.Count; i++)
                {
                    if (r.Rate[i] == 0)
                        continue;

                    acc += r.Rate[i];
                    table.Add((r.Weather[i].RowId, acc));
                }

                if (table.Count > 0)
                    this.rates[r.RowId] = [.. table];
            }

            // Every territory with a weather table gets a forecast. The "around Eorzea" crawl
            // takes the ones you can stand outdoors in: towns, fields and housing districts,
            // one per name, in the order the game lists them.
            foreach (var t in territories)
            {
                if (t.PlaceName.RowId == 0 || t.WeatherRate.RowId == 0 || !this.rates.ContainsKey(t.WeatherRate.RowId))
                    continue;

                var zone = new Zone(
                    t.RowId,
                    t.PlaceName.Value.Name.ToString(),
                    t.PlaceNameRegion.Value.Name.ToString(),
                    t.WeatherRate.RowId);

                this.zonesByTerritory[t.RowId] = zone;

                var outdoors = t.TerritoryIntendedUse.RowId is 0 or 1 or 13 && t.IsInUse && !t.IsPvpZone;
                if (outdoors && t.Bg.ToString().Length > 0 && this.zonesByPlace.TryAdd(zone.Name, zone))
                    this.around.Add(zone);
            }

            this.Available = this.around.Count > 0;
            log.Information($"[weather] {this.zonesByTerritory.Count} zones with weather, {this.around.Count} outdoors");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not read the weather tables; the weather channel is off.");
        }
    }

    /// <summary>
    /// The zone to forecast for a territory. Indoors — a house, an apartment — has no weather
    /// table of its own, so the district it is in stands in for it, matched by name.
    /// </summary>
    public Zone? ZoneFor(uint territoryId, string? placeNameZone)
    {
        if (this.zonesByTerritory.TryGetValue(territoryId, out var zone))
            return zone;

        if (placeNameZone is { Length: > 0 } && this.zonesByPlace.TryGetValue(placeNameZone, out zone))
            return zone;

        return null;
    }

    public IReadOnlyList<Zone> Around => this.around;

    public string NameOf(uint weatherId) =>
        this.weatherNames.TryGetValue(weatherId, out var name) ? name : "Unknown";

    /// <summary>The start of the period a real unix time falls in.</summary>
    public static long PeriodStart(long unix) => unix - (unix % PeriodSeconds);

    /// <summary>What the sky is doing in a zone at a real unix time.</summary>
    public uint WeatherAt(Zone zone, long unix)
    {
        if (!this.rates.TryGetValue(zone.RateId, out var table))
            return 0;

        var target = Target(PeriodStart(unix));
        foreach (var (weatherId, cumulative) in table)
        {
            if (target < cumulative)
                return weatherId;
        }

        return table[^1].WeatherId;
    }

    /// <summary>
    /// The game's draw for a period: the same number for everyone on every server, which is
    /// what makes a shared forecast possible at all.
    /// </summary>
    private static int Target(long periodStart)
    {
        var bell = periodStart / 175;
        var increment = (uint)((bell + 8 - (bell % 8)) % 24);
        var totalDays = (uint)(periodStart / 4200);
        var calcBase = (totalDays * 100) + increment;
        var step1 = (calcBase << 11) ^ calcBase;
        var step2 = (step1 >> 8) ^ step1;
        return (int)(step2 % 100);
    }
}
