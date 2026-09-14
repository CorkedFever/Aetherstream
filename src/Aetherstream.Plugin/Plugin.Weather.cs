using Aetherstream.Plugin.Video;
using Aetherstream.Plugin.Weather;

using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin;

/// <summary>
/// The weather channel's view of the world: where you are, what the sky is doing, and what it
/// is doing everywhere else. Rebuilt once a second; the maths is cheap but the zone walk is not
/// free, and nothing on it changes faster than that.
/// </summary>
public sealed partial class Plugin
{
    private WeatherSnapshot? weatherSnapshot;
    private long weatherSnapshotAtMs = -1;
    private EorzeaWeather.Zone? lastWeatherZone;

    private WeatherSnapshot? WeatherSnapshot()
    {
        var ticks = Environment.TickCount64;
        if (this.weatherSnapshot is not null && ticks - this.weatherSnapshotAtMs < 1000)
            return this.weatherSnapshot;

        if (!this.weather.Available)
            return null;

        // Where you are. Indoors has no sky, so the district you are in stands in — and failing
        // that, the last place that had one, so walking into a house does not blank the channel.
        var territory = this.clientState.TerritoryType;
        string? placeZone = null;
        if (territory != 0 && this.territoryZoneNames.TryGetValue(territory, out var known))
            placeZone = known;

        var zone = this.weather.ZoneFor(territory, placeZone) ?? this.lastWeatherZone;
        if (zone is not { } here)
        {
            // Not in the world yet — the title screen, say. New Gridania is as good a default as any.
            here = this.weather.ZoneFor(132, null) ?? this.weather.Around[0];
        }

        this.lastWeatherZone = here;

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var start = EorzeaWeather.PeriodStart(unix);

        var nowId = this.weather.WeatherAt(here, start);
        var now = new WeatherPeriod(start, nowId, this.weather.NameOf(nowId));

        var next = new List<WeatherPeriod>(5);
        for (var i = 1; i <= 5; i++)
        {
            var at = start + (i * EorzeaWeather.PeriodSeconds);
            var id = this.weather.WeatherAt(here, at);
            next.Add(new WeatherPeriod(at, id, this.weather.NameOf(id)));
        }

        var around = new List<WeatherElsewhere>(this.weather.Around.Count);
        foreach (var other in this.weather.Around)
        {
            if (other.TerritoryId == here.TerritoryId)
                continue;

            var id = this.weather.WeatherAt(other, start);
            around.Add(new WeatherElsewhere(other.Name, id, this.weather.NameOf(id)));
        }

        this.weatherSnapshot = new WeatherSnapshot(here.Name, here.Region, now, next, around);
        this.weatherSnapshotAtMs = ticks;
        return this.weatherSnapshot;
    }

    /// <summary>
    /// Territory id to the name of the zone it sits in, for the territories that have no weather
    /// of their own. Read once, lazily: it is only needed indoors.
    /// </summary>
    private Dictionary<uint, string> territoryZoneNames => this.territoryZoneNamesCache ??= this.ReadTerritoryZoneNames();

    private Dictionary<uint, string>? territoryZoneNamesCache;

    private Dictionary<uint, string> ReadTerritoryZoneNames()
    {
        var map = new Dictionary<uint, string>();
        try
        {
            foreach (var t in this.dataManager.GetExcelSheet<TerritoryType>())
            {
                if (t.WeatherRate.RowId == 0 && t.PlaceNameZone.RowId != 0)
                    map[t.RowId] = t.PlaceNameZone.Value.Name.ToString();
            }
        }
        catch (Exception ex)
        {
            this.log.Debug($"[weather] zone names unavailable: {ex.Message}");
        }

        return map;
    }
}
