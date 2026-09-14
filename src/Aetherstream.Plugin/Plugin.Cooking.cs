using Aetherstream.Plugin.Video;

using Lumina.Data.Files;
using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin;

/// <summary>
/// The cooking show's larder: every Culinarian recipe the game has, read once from the tables,
/// and the item icons the show plates them on, read from the game's textures as they are needed.
/// </summary>
public sealed partial class Plugin
{
    private const uint Culinarian = 7;

    private List<Dish>? dishes;
    private readonly Dictionary<uint, IconPixels?> iconCache = [];

    private IReadOnlyList<Dish> Dishes()
    {
        if (this.dishes is not null)
            return this.dishes;

        var list = new List<Dish>();
        try
        {
            var items = this.dataManager.GetExcelSheet<Item>();
            foreach (var r in this.dataManager.GetExcelSheet<Recipe>())
            {
                if (r.CraftType.RowId != Culinarian || r.ItemResult.RowId == 0)
                    continue;

                if (!items.TryGetRow(r.ItemResult.RowId, out var result))
                    continue;

                var name = result.Name.ToString();
                if (name.Length == 0)
                    continue;

                var ingredients = new List<(string, int, uint)>();
                for (var i = 0; i < r.Ingredient.Count && i < r.AmountIngredient.Count; i++)
                {
                    var amount = r.AmountIngredient[i];
                    if (amount == 0 || r.Ingredient[i].RowId == 0 || !items.TryGetRow(r.Ingredient[i].RowId, out var item))
                        continue;

                    // Crystals are the craft, not the cooking. Every recipe has them; the show skips them.
                    var ingredientName = item.Name.ToString();
                    if (ingredientName.EndsWith(" Shard", StringComparison.Ordinal)
                        || ingredientName.EndsWith(" Crystal", StringComparison.Ordinal)
                        || ingredientName.EndsWith(" Cluster", StringComparison.Ordinal))
                        continue;

                    ingredients.Add((ingredientName, amount, item.Icon));
                }

                if (ingredients.Count == 0)
                    continue;

                list.Add(new Dish(
                    name,
                    result.Description.ToString(),
                    r.RecipeLevelTable.ValueNullable?.ClassJobLevel ?? 0,
                    Math.Max(1, (int)r.AmountResult),
                    result.Icon,
                    r.SecretRecipeBook.RowId != 0,
                    ingredients));
            }

            this.log.Information($"[kitchen] {list.Count} culinarian recipes");
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "Could not read the recipes; the kitchen is closed.");
        }

        this.dishes = list;
        return list;
    }

    /// <summary>
    /// An item icon at the high-resolution size, as frame pixels. Read once and kept; a miss is
    /// kept too, so a missing texture is not asked for every frame.
    /// </summary>
    private IconPixels? Icon(uint iconId)
    {
        if (this.iconCache.TryGetValue(iconId, out var cached))
            return cached;

        IconPixels? pixels = null;
        try
        {
            var folder = iconId / 1000 * 1000;
            var tex = this.dataManager.GetFile<TexFile>($"ui/icon/{folder:D6}/{iconId:D6}_hr1.tex")
                ?? this.dataManager.GetFile<TexFile>($"ui/icon/{folder:D6}/{iconId:D6}.tex");
            if (tex is not null)
            {
                // Lumina hands the image back as B8G8R8A8; the frame is R8G8B8A8 in memory.
                var w = tex.Header.Width;
                var h = tex.Header.Height;
                var data = tex.ImageData;
                var px = new uint[w * h];
                for (var i = 0; i < px.Length && (i * 4) + 3 < data.Length; i++)
                {
                    var b = data[i * 4];
                    var g = data[(i * 4) + 1];
                    var r = data[(i * 4) + 2];
                    var a = data[(i * 4) + 3];
                    px[i] = ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
                }

                pixels = new IconPixels(px, w, h);
            }
        }
        catch (Exception ex)
        {
            this.log.Debug($"[kitchen] icon {iconId}: {ex.Message}");
        }

        this.iconCache[iconId] = pixels;
        return pixels;
    }
}
