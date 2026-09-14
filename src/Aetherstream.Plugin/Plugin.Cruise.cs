using Aetherstream.Plugin.Video;
using Aetherstream.Plugin.Weather;

namespace Aetherstream.Plugin;

/// <summary>
/// The ocean cruise show's data: the next boats on both routes, and what the sky is doing on
/// the coasts they leave from. The schedule is arithmetic; the sea conditions are the clock.
/// </summary>
public sealed partial class Plugin
{
    private static readonly string[] CoastZones = ["Limsa Lominsa Lower Decks", "Eastern La Noscea", "Western La Noscea", "Kugane", "The Ruby Sea", "Kholusia", "The Tempest"];

    private CruiseSnapshot? cruiseSnapshot;
    private long cruiseAtMs = -1;

    private CruiseSnapshot? CruiseSnapshot()
    {
        var ticks = Environment.TickCount64;
        if (this.cruiseSnapshot is not null && ticks - this.cruiseAtMs < 1000)
            return this.cruiseSnapshot;

        var utc = DateTime.UtcNow;
        var voyages = new List<ResetTimers.Voyage>(6);
        var at = utc;
        for (var i = 0; i < 6; i++)
        {
            var v = ResetTimers.NextVoyage(at);
            voyages.Add(v);
            at = v.DepartsUtc.AddMinutes(20);
        }

        var coasts = new List<(string Zone, string Weather)>();
        if (this.weather.Available)
        {
            var start = EorzeaWeather.PeriodStart(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            foreach (var zone in this.weather.Around)
            {
                if (Array.IndexOf(CoastZones, zone.Name) >= 0)
                    coasts.Add((zone.Name, this.weather.NameOf(this.weather.WeatherAt(zone, start))));
            }
        }

        this.cruiseSnapshot = new CruiseSnapshot(voyages, coasts);
        this.cruiseAtMs = ticks;
        return this.cruiseSnapshot;
    }
}
