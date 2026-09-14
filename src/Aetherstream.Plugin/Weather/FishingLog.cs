using System.IO.Compression;
using System.Text.Json;

using Dalamud.Plugin.Services;

using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin.Weather;

/// <summary>
/// The fish worth waiting for: the bundled conditions (weather, preceding weather, Eorzean
/// hours, bait) joined to the game's own fishing spots and the forecast. The conditions are
/// community knowledge, from the Carbuncle Plushy fish tracker; the spots, the zones and the
/// weather are the client's.
/// </summary>
internal sealed class FishingLog
{
    /// <summary>A fish and where it bites, with everything resolved against the game's tables.</summary>
    public sealed record Fish(
        string Name,
        uint FishParameterId,
        string Spot,
        string Zone,
        EorzeaWeather.Zone WeatherZone,
        uint TerritoryId,
        int StartHour,
        int EndHour,
        uint[] Weathers,
        string[] WeatherNames,
        uint[] Previous,
        string[] PreviousNames,
        string[] Bait,
        IReadOnlyDictionary<string, int> Predators,
        bool Folklore,
        float Patch,
        string Tug);

    /// <summary>A window in real unix seconds, [From, To).</summary>
    public readonly record struct Window(long From, long To);

    public bool Available { get; }

    public IReadOnlyList<Fish> All => this.fish;

    public string Status { get; } = string.Empty;

    private readonly List<Fish> fish = [];
    private readonly EorzeaWeather weather;

    public FishingLog(string dataPath, IDataManager data, EorzeaWeather weather, IPluginLog log)
    {
        this.weather = weather;
        if (!weather.Available)
        {
            this.Status = "the weather tables are not available";
            return;
        }

        try
        {
            using var file = File.OpenRead(dataPath);
            using var gz = new GZipStream(file, CompressionMode.Decompress);
            var entries = JsonSerializer.Deserialize<List<Entry>>(gz) ?? [];

            // The game's spots by name, with the zone each stands in; the weather by name; and
            // the log slot each fish occupies, by item name.
            var spots = new Dictionary<string, (uint Territory, string Zone, EorzeaWeather.Zone Weather)>(StringComparer.OrdinalIgnoreCase);
            var territories = data.GetExcelSheet<TerritoryType>();
            foreach (var spot in data.GetExcelSheet<FishingSpot>())
            {
                var name = spot.PlaceName.ValueNullable?.Name.ToString() ?? string.Empty;
                if (name.Length == 0 || spot.TerritoryType.RowId == 0)
                    continue;

                if (!territories.TryGetRow(spot.TerritoryType.RowId, out var territory))
                    continue;

                var zoneName = territory.PlaceName.ValueNullable?.Name.ToString() ?? string.Empty;
                var wz = weather.ZoneFor(territory.RowId, territory.PlaceNameZone.ValueNullable?.Name.ToString());
                if (wz is not { } weatherZone)
                    continue;

                spots.TryAdd(name, (territory.RowId, zoneName, weatherZone));
            }

            var weatherIds = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in data.GetExcelSheet<Lumina.Excel.Sheets.Weather>())
            {
                var name = w.Name.ToString();
                if (name.Length > 0)
                    weatherIds.TryAdd(name, w.RowId);
            }

            var logSlots = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            var items = data.GetExcelSheet<Item>();
            foreach (var p in data.GetExcelSheet<FishParameter>())
            {
                if (!p.IsInLog || p.Item.RowId == 0 || !items.TryGetRow(p.Item.RowId, out var item))
                    continue;

                var name = item.Name.ToString();
                if (name.Length > 0)
                    logSlots.TryAdd(name, p.RowId);
            }

            var unmatched = 0;
            foreach (var e in entries)
            {
                if (!spots.TryGetValue(e.Location, out var spot))
                {
                    unmatched++;
                    continue;
                }

                uint[] Ids(string[] names) => names.Select(n => weatherIds.GetValueOrDefault(n, 0u)).Where(id => id != 0).ToArray();

                this.fish.Add(new Fish(
                    e.Name,
                    logSlots.GetValueOrDefault(e.Name, 0u),
                    e.Location,
                    spot.Zone,
                    spot.Weather,
                    spot.Territory,
                    e.Start,
                    e.End,
                    Ids(e.Weathers),
                    e.Weathers,
                    Ids(e.Previous),
                    e.Previous,
                    e.Bait,
                    e.Predators,
                    e.Folklore,
                    e.Patch,
                    e.Tug));
            }

            this.Available = this.fish.Count > 0;
            log.Information($"[fishing] {this.fish.Count} fish with windows, {unmatched} at spots the game does not list");
        }
        catch (Exception ex)
        {
            this.Status = "the fish data could not be read";
            log.Warning(ex, "Could not read the fish data; the fishing channel is off.");
        }
    }

    /// <summary>
    /// When a fish is up between two real times: each weather period that satisfies the weather
    /// and what came before it, cut to the fish's Eorzean hours. Adjacent windows are joined.
    /// </summary>
    public List<Window> Windows(Fish f, long fromUnix, long toUnix)
    {
        var windows = new List<Window>();
        const int Period = EorzeaWeather.PeriodSeconds;
        const int Hour = 175;

        for (var start = EorzeaWeather.PeriodStart(fromUnix); start < toUnix; start += Period)
        {
            if (f.Weathers.Length > 0 && Array.IndexOf(f.Weathers, this.weather.WeatherAt(f.WeatherZone, start)) < 0)
                continue;

            if (f.Previous.Length > 0 && Array.IndexOf(f.Previous, this.weather.WeatherAt(f.WeatherZone, start - Period)) < 0)
                continue;

            // The period covers eight Eorzean hours from a multiple of eight. The fish's hours
            // may wrap midnight, so both the plain and the wrapped range are tried.
            var periodHour = (int)(start / Hour % 24);
            foreach (var (h0, h1) in Ranges(f.StartHour, f.EndHour))
            {
                var a = Math.Max(h0, periodHour);
                var b = Math.Min(h1, periodHour + 8);
                if (a >= b)
                    continue;

                var from = start + ((a - periodHour) * Hour);
                var to = start + ((b - periodHour) * Hour);
                if (windows.Count > 0 && windows[^1].To == from)
                    windows[^1] = windows[^1] with { To = to };
                else
                    windows.Add(new Window(from, to));
            }
        }

        return windows;
    }

    private static IEnumerable<(int, int)> Ranges(int start, int end)
    {
        if (start == end || (start == 0 && end == 24))
        {
            yield return (0, 24);
        }
        else if (start < end)
        {
            yield return (start, end);
        }
        else
        {
            yield return (start, 24);
            yield return (0, end);
        }
    }

    private sealed class Entry
    {
        [System.Text.Json.Serialization.JsonPropertyName("n")] public string Name { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("l")] public string Location { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("s")] public int Start { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("e")] public int End { get; set; } = 24;
        [System.Text.Json.Serialization.JsonPropertyName("w")] public string[] Weathers { get; set; } = [];
        [System.Text.Json.Serialization.JsonPropertyName("p")] public string[] Previous { get; set; } = [];
        [System.Text.Json.Serialization.JsonPropertyName("b")] public string[] Bait { get; set; } = [];
        [System.Text.Json.Serialization.JsonPropertyName("m")] public Dictionary<string, int> Predators { get; set; } = [];
        [System.Text.Json.Serialization.JsonPropertyName("f")] public bool Folklore { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("v")] public float Patch { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("t")] public string Tug { get; set; } = string.Empty;
    }
}
