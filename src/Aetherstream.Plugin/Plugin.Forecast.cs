using Aetherstream.Plugin.Video;
using Aetherstream.Plugin.Weather;

namespace Aetherstream.Plugin;

/// <summary>
/// The forecast show's data: every outdoor zone by region, what the sky is doing now and for
/// the next three periods, and the rare weather coming anywhere. All computed from the clock,
/// once a second at most.
/// </summary>
public sealed partial class Plugin
{
    private ForecastSnapshot? forecastSnapshot;
    private long forecastAtMs = -1;

    private ForecastSnapshot? ForecastSnapshot()
    {
        var ticks = Environment.TickCount64;
        if (this.forecastSnapshot is not null && ticks - this.forecastAtMs < 1000)
            return this.forecastSnapshot;

        if (!this.weather.Available)
            return null;

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var start = EorzeaWeather.PeriodStart(unix);
        var regions = new List<ForecastRegion>();
        string? region = null;
        var zones = new List<ForecastZone>();
        foreach (var zone in this.weather.Around)
        {
            if (region is not null && zone.Region != region)
            {
                regions.Add(new ForecastRegion(region, zones));
                zones = [];
            }

            region = zone.Region;
            var periods = new string[4];
            for (var i = 0; i < 4; i++)
                periods[i] = this.weather.NameOf(this.weather.WeatherAt(zone, start + (i * EorzeaWeather.PeriodSeconds)));
            zones.Add(new ForecastZone(zone.Name, periods, this.weather.ChanceOf(zone, this.weather.WeatherAt(zone, start))));
        }

        if (region is not null)
            regions.Add(new ForecastRegion(region, zones));

        var here = this.lastWeatherZone?.Name ?? string.Empty;
        this.forecastSnapshot = new ForecastSnapshot(regions, this.RareWeatherAhead(start), here, start);
        this.forecastAtMs = ticks;
        return this.forecastSnapshot;
    }
}
