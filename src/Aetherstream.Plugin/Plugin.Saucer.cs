using Aetherstream.Plugin.Video;
using Aetherstream.Plugin.Weather;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin;

/// <summary>
/// The Gold Saucer show's data: your MGP, the Jumbo Cactpot draw, the next GATE, the Fashion
/// Report window, and a Triple Triad card you do not have yet, with its numbers and where it
/// comes from. The cards are read once from the tables; the rest is the clock and the character.
/// </summary>
public sealed partial class Plugin
{
    private const uint MgpItemId = 29;

    private List<TriadCard>? triadCards;
    private SaucerSnapshot? saucerSnapshot;
    private long saucerAtMs = -1;

    private SaucerSnapshot? SaucerSnapshot()
    {
        var ticks = Environment.TickCount64;
        if (this.saucerSnapshot is not null && ticks - this.saucerAtMs < 1000)
            return this.saucerSnapshot;

        var utc = DateTime.UtcNow;
        var region = this.CurrentRegion();
        var cactpot = ResetTimers.NextJumboCactpot(utc, region);

        // GATEs open every twenty minutes, on the hour, twenty past and twenty to.
        var minute = utc.Minute % 20;
        var nextGate = utc.AddMinutes(20 - minute).AddSeconds(-utc.Second);

        // Fashion Report: judging opens Friday 08:00 UTC and closes at the weekly reset.
        var friday = utc.Date.AddDays(((int)DayOfWeek.Friday - (int)utc.DayOfWeek + 7) % 7).AddHours(8);
        var weekly = ResetTimers.NextWeekly(utc);
        var judgingOpen = utc >= friday.AddDays(friday > utc ? -7 : 0) && utc < weekly && weekly - utc <= TimeSpan.FromDays(4);
        var fashion = judgingOpen ? weekly : (friday > utc ? friday : friday.AddDays(7));

        var cards = this.TriadCards();
        var missing = cards.Where(c => !this.HasCard(c.Id)).ToList();

        this.saucerSnapshot = new SaucerSnapshot(this.Mgp(), cactpot, nextGate, judgingOpen, fashion, cards.Count, cards.Count - missing.Count, missing);
        this.saucerAtMs = ticks;
        return this.saucerSnapshot;
    }

    private unsafe long Mgp()
    {
        try
        {
            var inv = InventoryManager.Instance();
            return inv == null ? 0 : inv->GetInventoryItemCount(MgpItemId);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private unsafe bool HasCard(uint cardId)
    {
        try
        {
            var ui = UIState.Instance();
            return ui != null && ui->IsTripleTriadCardUnlocked((ushort)cardId);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Every card in the game with its numbers, stars, type and how it is got, read once.</summary>
    private IReadOnlyList<TriadCard> TriadCards()
    {
        if (this.triadCards is not null)
            return this.triadCards;

        var list = new List<TriadCard>();
        try
        {
            var residents = this.dataManager.GetExcelSheet<TripleTriadCardResident>();
            var acquisitions = this.dataManager.GetExcelSheet<TripleTriadCardObtain>();
            foreach (var card in this.dataManager.GetExcelSheet<TripleTriadCard>())
            {
                var name = card.Name.ToString();
                if (name.Length == 0 || residents.GetRowOrDefault(card.RowId) is not { } r)
                    continue;

                var how = acquisitions?.GetRowOrDefault(r.AcquisitionType.RowId)?.Text.ValueNullable?.Text.ToString() ?? string.Empty;
                list.Add(new TriadCard(
                    card.RowId,
                    name,
                    card.Description.ToString(),
                    r.Top, r.Bottom, r.Left, r.Right,
                    (int)r.TripleTriadCardRarity.RowId,
                    r.TripleTriadCardType.ValueNullable?.Name.ToString() ?? string.Empty,
                    how,
                    r.SaleValue));
            }

            this.log.Information($"[saucer] {list.Count} triple triad cards");
        }
        catch (Exception ex)
        {
            this.log.Warning($"[saucer] the card tables could not be read: {ex.Message}");
        }

        this.triadCards = list;
        return list;
    }
}
