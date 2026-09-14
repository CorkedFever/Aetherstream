using System.Text.Json;

using Aetherstream.Plugin.Video;

using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin;

/// <summary>
/// Prices for the market channel, from Universalis. The watch list is item ids the user picked
/// by name; the world is whichever one the character is on. Fetched every ten minutes while
/// the channel is up, which is polite to a volunteer-run service and as fresh as the board is.
/// </summary>
public sealed partial class Plugin
{
    private static readonly TimeSpan MarketFreshFor = TimeSpan.FromMinutes(10);

    private MarketSnapshot? marketSnapshot;
    private long marketRequestedAtMs = -1;
    private bool marketFetching;
    private string marketKey = string.Empty;
    private readonly Dictionary<uint, long> marketLastMin = [];

    private Dictionary<string, (uint Id, string Name)>? marketableItems;

    private MarketSnapshot? MarketSnapshot()
    {
        var world = this.CurrentWorldName();
        var ids = this.config.MarketWatch.Select(w => w.Id).ToList();
        var key = $"{world}|{string.Join(',', ids)}";
        var now = Environment.TickCount64;

        if (world.Length == 0)
            return new MarketSnapshot(string.Empty, [], DateTime.MinValue, "log in to a character to see a world");

        if (ids.Count == 0)
            return new MarketSnapshot(world, [], DateTime.MinValue, "add items in the Channels tab");

        var stale = now - this.marketRequestedAtMs > MarketFreshFor.TotalMilliseconds;
        if (!this.marketFetching && (key != this.marketKey || stale))
        {
            this.marketKey = key;
            this.marketRequestedAtMs = now;
            this.FetchMarket(world, ids);
        }

        return this.marketSnapshot ?? new MarketSnapshot(world, [], DateTime.MinValue, "asking Universalis");
    }

    private void FetchMarket(string world, List<uint> ids)
    {
        this.marketFetching = true;
        _ = Task.Run(async () =>
        {
            try
            {
                // Two ids at least: with one, Universalis answers with the item itself rather than a map.
                var request = ids.Count == 1 ? [ids[0], ids[0]] : ids;
                var url = $"https://universalis.app/api/v2/{Uri.EscapeDataString(world)}/{string.Join(',', request.Distinct())}" +
                    "?listings=0&entries=20&fields=items.itemID,items.minPrice,items.averagePrice,items.regularSaleVelocity,items.recentHistory.pricePerUnit";

                var text = await this.http.GetStringAsync(url);
                var rows = this.ParseMarket(text, ids);

                this.marketSnapshot = new MarketSnapshot(world, rows, DateTime.UtcNow, rows.Count == 0 ? "no prices came back" : string.Empty);
                this.log.Information($"[market] {rows.Count} items on {world}");
            }
            catch (Exception ex)
            {
                this.log.Warning($"[market] fetch failed: {ex.Message}");
                this.marketSnapshot ??= new MarketSnapshot(world, [], DateTime.MinValue, "could not reach Universalis");
            }
            finally
            {
                this.marketFetching = false;
            }
        });
    }

    private List<MarketRow> ParseMarket(string json, List<uint> order)
    {
        var byId = new Dictionary<uint, MarketRow>();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in items.EnumerateObject())
                this.ReadMarketItem(property.Value, byId);
        }
        else
        {
            this.ReadMarketItem(root, byId);
        }

        var rows = new List<MarketRow>();
        foreach (var id in order)
        {
            if (byId.TryGetValue(id, out var row))
                rows.Add(row);
        }

        return rows;
    }

    private void ReadMarketItem(JsonElement item, Dictionary<uint, MarketRow> into)
    {
        if (!item.TryGetProperty("itemID", out var idElement) || idElement.ValueKind != JsonValueKind.Number)
            return;

        var id = idElement.GetUInt32();
        var name = this.config.MarketWatch.FirstOrDefault(w => w.Id == id)?.Name ?? $"Item {id}";
        var min = Number(item, "minPrice");
        var avg = Number(item, "averagePrice");
        var velocity = item.TryGetProperty("regularSaleVelocity", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

        // Recent sales, newest first from the service; oldest first for a chart.
        var history = new List<long>();
        if (item.TryGetProperty("recentHistory", out var recent) && recent.ValueKind == JsonValueKind.Array)
        {
            foreach (var sale in recent.EnumerateArray())
                history.Add(Number(sale, "pricePerUnit"));
        }

        history.Reverse();

        var trend = this.marketLastMin.TryGetValue(id, out var last) && last > 0 && min > 0
            ? Math.Sign(min - last)
            : 0;
        this.marketLastMin[id] = min;

        into[id] = new MarketRow(id, name, min, avg, velocity, trend, history);
    }

    private static long Number(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? (long)Math.Round(value.GetDouble())
            : 0;

    private string CurrentWorldName()
    {
        try
        {
            return this.objects.LocalPlayer?.CurrentWorld.Value.Name.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void WatchListChanged() => this.marketKey = string.Empty;

    /// <summary>
    /// Finds a marketable item by name: an exact match first, then the only item that starts
    /// that way. The table is read once from the game data, forty thousand rows or so.
    /// </summary>
    private (uint Id, string Name)? FindMarketItem(string query)
    {
        this.marketableItems ??= this.ReadMarketableItems();

        if (this.marketableItems.TryGetValue(query, out var exact))
            return exact;

        var starts = this.marketableItems
            .Where(kv => kv.Key.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Value)
            .Take(2)
            .ToList();

        return starts.Count == 1 ? starts[0] : null;
    }

    private List<AdItem>? adItems;

    /// <summary>Marketable items with an icon and a category, for the adverts. Read once.</summary>
    private IReadOnlyList<AdItem> AdItems()
    {
        if (this.adItems is not null)
            return this.adItems;

        var list = new List<AdItem>();
        try
        {
            foreach (var item in this.dataManager.GetExcelSheet<Item>())
            {
                if (item.ItemSearchCategory.RowId == 0 || item.Icon == 0)
                    continue;

                var name = item.Name.ToString();
                if (name.Length == 0)
                    continue;

                list.Add(new AdItem(item.RowId, name, item.ItemUICategory.ValueNullable?.Name.ToString() ?? "thing"));
            }
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "[adverts] could not read the item table.");
        }

        this.adItems = list;
        return list;
    }

    private Dictionary<string, (uint Id, string Name)> ReadMarketableItems()
    {
        var map = new Dictionary<string, (uint, string)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var item in this.dataManager.GetExcelSheet<Item>())
            {
                if (item.ItemSearchCategory.RowId == 0)
                    continue;

                var name = item.Name.ToString();
                if (name.Length > 0)
                    map.TryAdd(name, (item.RowId, name));
            }
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "[market] could not read the item table.");
        }

        return map;
    }
}
