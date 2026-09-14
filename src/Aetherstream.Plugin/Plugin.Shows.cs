using System.IO.Compression;
using System.Text.Json;

using Aetherstream.Plugin.Video;

using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin;

/// <summary>
/// What the wildlife show is made from: the hunting log's creatures with where they live, and
/// the outdoor zones with their regions. Read once from the game's tables.
/// </summary>
public sealed partial class Plugin
{
    private List<Creature>? creatures;
    private List<DeitySprite>? twelve;

    private IReadOnlyList<Creature> Creatures()
    {
        if (this.creatures is not null)
            return this.creatures;

        var list = new List<Creature>();
        try
        {
            var regions = this.ZoneRegions();
            foreach (var target in this.dataManager.GetExcelSheet<MonsterNoteTarget>())
            {
                var name = target.BNpcName.ValueNullable?.Singular.ToString() ?? string.Empty;
                if (name.Length == 0 || target.Icon == 0)
                    continue;

                string zone = string.Empty;
                foreach (var place in target.PlaceNameZone)
                {
                    zone = place.ValueNullable?.Name.ToString() ?? string.Empty;
                    if (zone.Length > 0)
                        break;
                }

                if (zone.Length == 0)
                    continue;

                var region = regions.GetValueOrDefault(zone, string.Empty);
                list.Add(new Creature(Capitalise(name), zone, region, (uint)target.Icon, (int)(target.RowId / 10 % 10) + 1));
            }

            this.log.Information($"[wildlife] {list.Count} creatures in the hunting log");
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "Could not read the hunting log; the wildlife show is off.");
        }

        this.creatures = list;
        return list;
    }

    /// <summary>The Twelve's sprites, from data/twelve.json.gz beside the fish; none if the file is missing.</summary>
    private IReadOnlyList<DeitySprite> Twelve()
    {
        if (this.twelve is not null)
            return this.twelve;

        var list = new List<DeitySprite>();
        try
        {
            var path = Path.Combine(Path.GetDirectoryName(this.fishDataPath)!, "twelve.json.gz");
            if (File.Exists(path))
            {
                using var file = File.OpenRead(path);
                using var gz = new GZipStream(file, CompressionMode.Decompress);
                using var doc = JsonDocument.Parse(gz);
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    var w = e.GetProperty("w").GetInt32();
                    var h = e.GetProperty("h").GetInt32();
                    var px = e.GetProperty("p").EnumerateArray().Select(v => (uint)v.GetInt64()).ToArray();
                    if (px.Length == w * h)
                        list.Add(new DeitySprite(e.GetProperty("d").GetInt32(), e.GetProperty("n").GetString() ?? string.Empty, w, h, px));
                }
            }

            this.log.Information($"[stories] {list.Count} of the Twelve drawn from their renders");
        }
        catch (Exception ex)
        {
            this.log.Warning($"[stories] the Twelve could not be read: {ex.Message}");
        }

        this.twelve = list;
        return list;
    }

    /// <summary>Outdoor zones by name, with the region each belongs to.</summary>
    private Dictionary<string, string> ZoneRegions()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in this.dataManager.GetExcelSheet<TerritoryType>())
        {
            if (t.WeatherRate.RowId == 0 || t.PlaceName.RowId == 0)
                continue;

            var zone = t.PlaceName.ValueNullable?.Name.ToString() ?? string.Empty;
            var region = t.PlaceNameRegion.ValueNullable?.Name.ToString() ?? string.Empty;
            if (zone.Length == 0 || region.Length == 0 || region == "???" || zone == "???")
                continue;

            // The outdoors only: the towns and the field zones, not instances or houses.
            if (t.TerritoryIntendedUse.RowId is not (0 or 1 or 13 or 14))
                continue;

            map.TryAdd(zone, region);
        }

        return map;
    }

    private static string Capitalise(string name) =>
        name.Length == 0 ? name : string.Concat(name.Split(' ').Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w).Select(w => w + " ")).TrimEnd();
}
