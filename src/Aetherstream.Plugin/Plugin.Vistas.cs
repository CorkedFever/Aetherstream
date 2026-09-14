using Aetherstream.Plugin.Video;

using FFXIVClientStructs.FFXIV.Client.Game.UI;

using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin;

/// <summary>
/// What the painting show is made from: the Sightseeing Log's vistas, with their pictures, the
/// hours they can be seen, and whether this character has found them.
/// </summary>
public sealed partial class Plugin
{
    private List<Vista>? vistas;

    private IReadOnlyList<Vista> Vistas()
    {
        if (this.vistas is not null)
            return this.vistas;

        var list = new List<Vista>();
        try
        {
            var index = 0;
            foreach (var row in this.dataManager.GetExcelSheet<Adventure>())
            {
                var name = row.Name.ToString();
                if (name.Length == 0 || row.IconDiscovered == 0)
                {
                    index++;
                    continue;
                }

                list.Add(new Vista(
                    index++,
                    name,
                    row.PlaceName.ValueNullable?.Name.ToString() ?? string.Empty,
                    row.Impression.ToString(),
                    row.Description.ToString(),
                    row.MinTime,
                    row.MaxTime,
                    row.Emote.ValueNullable?.Name.ToString() ?? string.Empty,
                    (uint)row.IconDiscovered));
            }

            this.log.Information($"[painting] {list.Count} vistas in the log");
        }
        catch (Exception ex)
        {
            this.log.Warning($"[painting] the sightseeing log could not be read: {ex.Message}");
        }

        this.vistas = list;
        return list;
    }

    /// <summary>Whether this character has found the vista at <paramref name="index"/> in the log. Main thread.</summary>
    private unsafe bool VistaFound(int index)
    {
        try
        {
            var player = PlayerState.Instance();
            return player != null && player->IsAdventureComplete((uint)index);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
