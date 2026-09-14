using System.Text.Json;

using Aetherstream.Plugin.Video;

namespace Aetherstream.Plugin;

/// <summary>
/// Open plots for the housing channel, from PaissaDB. The game never lists open plots; PaissaDB
/// is fed by everyone running PaissaHouse who opens a ward, and answers with what they saw and
/// when. The world is whichever one the character is on. Fetched every five minutes while the
/// channel is up.
/// </summary>
public sealed partial class Plugin
{
    private static readonly TimeSpan HousingFreshFor = TimeSpan.FromMinutes(5);

    private HousingSnapshot? housingSnapshot;
    private long housingRequestedAtMs = -1;
    private bool housingFetching;
    private string housingKey = string.Empty;

    private HousingSnapshot? HousingSnapshot()
    {
        var world = this.CurrentWorldName();
        var worldId = this.CurrentWorldId();
        var now = Environment.TickCount64;

        if (world.Length == 0 || worldId == 0)
            return new HousingSnapshot(string.Empty, [], DateTime.MinValue, "log in to a character to see a world");

        var stale = now - this.housingRequestedAtMs > HousingFreshFor.TotalMilliseconds;
        if (!this.housingFetching && (world != this.housingKey || stale))
        {
            this.housingKey = world;
            this.housingRequestedAtMs = now;
            this.FetchHousing(world, worldId);
        }

        return this.housingSnapshot ?? new HousingSnapshot(world, [], DateTime.MinValue, "asking PaissaDB");
    }

    private void FetchHousing(string world, uint worldId)
    {
        this.housingFetching = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var text = await this.http.GetStringAsync($"https://paissadb.zhu.codes/worlds/{worldId}");
                var plots = ParseHousing(text);
                this.housingSnapshot = new HousingSnapshot(world, plots, DateTime.UtcNow, string.Empty);
                this.log.Information($"[housing] {plots.Count} open plots on {world}");
            }
            catch (Exception ex)
            {
                this.log.Warning($"[housing] fetch failed: {ex.Message}");
                this.housingSnapshot ??= new HousingSnapshot(world, [], DateTime.MinValue, "could not reach PaissaDB");
            }
            finally
            {
                this.housingFetching = false;
            }
        });
    }

    /// <summary>
    /// The plots out of PaissaDB's world document: every district's open plots, wards and plots
    /// counted from one the way the game shows them. Ordered by district then ward then plot.
    /// </summary>
    private static List<OpenPlot> ParseHousing(string json)
    {
        var plots = new List<OpenPlot>();
        using var doc = JsonDocument.Parse(json);
        foreach (var district in doc.RootElement.GetProperty("districts").EnumerateArray())
        {
            var name = district.GetProperty("name").GetString() ?? string.Empty;
            foreach (var p in district.GetProperty("open_plots").EnumerateArray())
            {
                plots.Add(new OpenPlot(
                    name,
                    p.GetProperty("ward_number").GetInt32() + 1,
                    p.GetProperty("plot_number").GetInt32() + 1,
                    p.GetProperty("size").GetInt32(),
                    p.GetProperty("price").GetInt64(),
                    p.TryGetProperty("purchase_system", out var ps) ? ps.GetInt32() : 0,
                    p.TryGetProperty("lotto_phase", out var lp) && lp.ValueKind == JsonValueKind.Number ? lp.GetInt32() : 0,
                    p.TryGetProperty("lotto_entries", out var le) && le.ValueKind == JsonValueKind.Number ? le.GetInt32() : -1,
                    p.TryGetProperty("lotto_phase_until", out var lu) && lu.ValueKind == JsonValueKind.Number ? DateTimeOffset.FromUnixTimeSeconds((long)lu.GetDouble()).UtcDateTime : DateTime.MinValue,
                    DateTimeOffset.FromUnixTimeSeconds((long)p.GetProperty("last_updated_time").GetDouble()).UtcDateTime));
            }
        }

        return plots.OrderBy(p => p.District).ThenBy(p => p.Ward).ThenBy(p => p.Plot).ToList();
    }

    private uint CurrentWorldId()
    {
        try
        {
            return this.objects.LocalPlayer?.CurrentWorld.RowId ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
