using Aetherstream.Plugin.Video;
using Aetherstream.Plugin.Weather;

using FFXIVClientStructs.FFXIV.Client.Game;

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

        var alerts = this.RareWeatherAhead(start);
        var live = this.LiveWeatherAlert(territory, here, nowId);

        this.weatherSnapshot = new WeatherSnapshot(here.Name, here.Region, now, next, around, alerts, live);
        this.weatherSnapshotAtMs = ticks;
        return this.weatherSnapshot;
    }

    /// <summary>
    /// Weather that is unusual for its zone — a one-in-ten chance or rarer — in the next six
    /// periods, anywhere outdoors. Fishers and gatherers wait on exactly these.
    /// </summary>
    private List<string> RareWeatherAhead(long start)
    {
        var found = new List<(long At, string Text)>();
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var zone in this.weather.Around)
        {
            for (var i = 0; i < 6; i++)
            {
                var at = start + (i * EorzeaWeather.PeriodSeconds);
                var id = this.weather.WeatherAt(zone, at);
                var chance = this.weather.ChanceOf(zone, id);
                if (chance == 0 || chance > 10)
                    continue;

                var minutes = Math.Max(0, (at - unix) / 60);
                var when = i == 0 ? "now" : minutes < 60 ? $"in {minutes} min" : $"in {minutes / 60}h {minutes % 60:00}m";
                found.Add((at, $"{this.weather.NameOf(id)} in {zone.Name} {when} ({chance}%)"));
            }
        }

        return found.OrderBy(f => f.At).Select(f => f.Text).Take(8).ToList();
    }

    private long unusualSinceMs = -1;

    /// <summary>
    /// What the sky is actually doing where you stand, against what the tables say it should.
    /// A difference means the game forced it — a FATE boss that brings its own weather, like
    /// Odin's Tension over the Shroud. Named when known; otherwise reported once it has lasted
    /// long enough not to be the fade between two periods.
    /// </summary>
    private unsafe string LiveWeatherAlert(uint territory, EorzeaWeather.Zone here, uint expected)
    {
        if (territory == 0 || !this.weather.HasOwnWeather(territory) || this.objects.LocalPlayer is null)
        {
            this.unusualSinceMs = -1;
            return string.Empty;
        }

        uint live;
        try
        {
            var manager = WeatherManager.Instance();
            if (manager == null)
                return string.Empty;

            live = manager->GetCurrentWeather();
        }
        catch
        {
            return string.Empty;
        }

        if (live == 0 || live == expected)
        {
            this.unusualSinceMs = -1;
            return string.Empty;
        }

        var name = this.weather.NameOf(live);
        var known = live switch
        {
            20 => "Tension over the Shroud: Odin is abroad (Steel Reign)",
            53 or 55 => "Royal Levin in the Forelands: Coeurlregina (Long Live the Coeurl)",
            87 => "Quicklevin in the Lochs: Ixion (A Horse Outside)",
            54 => "Hyperelectricity in Azys Lla: Proto Ultima (Prey Online)",
            _ => null,
        };

        if (known is not null)
            return known;

        // Unknown difference: give the fade between periods ninety seconds before calling it odd.
        var now = Environment.TickCount64;
        if (this.unusualSinceMs < 0)
            this.unusualSinceMs = now;

        return now - this.unusualSinceMs > 90_000 ? $"Unusual weather in {here.Name}: {name}" : string.Empty;
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
