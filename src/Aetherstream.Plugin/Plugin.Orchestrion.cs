using Aetherstream.Plugin.Audio;
using Aetherstream.Plugin.UI.Tabs;

using FFXIVClientStructs.FFXIV.Client.Game.UI;

using Lumina.Excel.Sheets;

namespace Aetherstream.Plugin;

/// <summary>
/// The orchestrion rolls as a music source: the game's own music, read from its files, the
/// Ogg pulled out of each roll's SCD and kept beside the config so it is done once. Which rolls
/// the character owns comes from the player state.
/// </summary>
public sealed partial class Plugin
{
    private List<OrchestrionRoll>? rolls;
    private readonly Dictionary<uint, string> rollFiles = [];
    private readonly HashSet<uint> rollExtracting = [];

    private string RollsFolder => Path.Combine(this.pluginInterface.GetPluginConfigDirectory(), "rolls");

    private IReadOnlyList<OrchestrionRoll> Rolls()
    {
        if (this.rolls is not null)
            return this.rolls;

        var list = new List<OrchestrionRoll>();
        try
        {
            var paths = this.dataManager.GetExcelSheet<OrchestrionPath>();
            var uiparams = this.dataManager.GetExcelSheet<OrchestrionUiparam>();
            var categories = this.dataManager.GetExcelSheet<OrchestrionCategory>();
            foreach (var roll in this.dataManager.GetExcelSheet<Orchestrion>())
            {
                var name = roll.Name.ToString();
                var path = paths.GetRowOrDefault(roll.RowId)?.File.ToString() ?? string.Empty;
                if (name.Length == 0 || path.Length == 0)
                    continue;

                var category = uiparams?.GetRowOrDefault(roll.RowId)?.OrchestrionCategory.RowId ?? 0;
                var categoryName = categories?.GetRowOrDefault(category)?.Name.ToString() ?? string.Empty;
                list.Add(new OrchestrionRoll(roll.RowId, name, roll.Description.ToString(), path, categoryName));
            }

            this.log.Information($"[rolls] {list.Count} orchestrion rolls");
        }
        catch (Exception ex)
        {
            this.log.Warning($"[rolls] the orchestrion tables could not be read: {ex.Message}");
        }

        this.rolls = list;
        return list;
    }

    /// <summary>Whether the character has the roll. Main thread.</summary>
    private unsafe bool HasRoll(uint id)
    {
        try
        {
            var player = PlayerState.Instance();
            return player != null && player->IsOrchestrionRollUnlocked(id);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The extracted file for a roll, or null while it is being pulled out. The first ask starts
    /// the work in the background; the file is kept, so the next session has it at once.
    /// </summary>
    private string? RollFile(OrchestrionRoll roll)
    {
        if (this.rollFiles.TryGetValue(roll.Id, out var have))
            return have;

        var file = Path.Combine(this.RollsFolder, $"{roll.Id}.ogg");
        if (File.Exists(file))
        {
            this.rollFiles[roll.Id] = file;
            return file;
        }

        if (!this.rollExtracting.Add(roll.Id))
            return null;

        _ = Task.Run(() =>
        {
            try
            {
                var raw = this.dataManager.GetFile(roll.Path)?.Data;
                var ogg = raw is null ? null : ScdOgg.Extract(raw);
                if (ogg is null)
                {
                    this.log.Warning($"[rolls] '{roll.Name}' has no Ogg stream to play");
                    return;
                }

                Directory.CreateDirectory(this.RollsFolder);
                File.WriteAllBytes(file, ogg);
                this.rollFiles[roll.Id] = file;
                this.musicKey = string.Empty;
                this.log.Information($"[rolls] extracted '{roll.Name}' ({ogg.Length / 1024} KB)");
            }
            catch (Exception ex)
            {
                this.log.Warning($"[rolls] could not extract '{roll.Name}': {ex.Message}");
            }
        });
        return null;
    }

    /// <summary>The chosen rolls' files, those already extracted; the rest follow as they land.</summary>
    private List<string> RollTracks()
    {
        var chosen = this.config.ChannelMusicRolls;
        var list = new List<string>();
        foreach (var roll in this.Rolls())
        {
            if (!chosen.Contains(roll.Id))
                continue;
            if (this.RollFile(roll) is { } file)
                list.Add(file);
        }

        return list;
    }

    private string? RollTitle(string track)
    {
        var stem = Path.GetFileNameWithoutExtension(track);
        return uint.TryParse(stem, out var id) && track.StartsWith(this.RollsFolder, StringComparison.OrdinalIgnoreCase)
            ? this.Rolls().FirstOrDefault(r => r.Id == id)?.Name
            : null;
    }

    private void WireOrchestrion()
    {
        this.window.Sound.Rolls.Rolls = this.Rolls;
        this.window.Sound.Rolls.Has = this.HasRoll;
        this.window.Sound.Rolls.Changed = () =>
        {
            this.config.ChannelMusicSource = "rolls";
            this.config.ChannelMusic = true;
            this.SaveConfig();
            this.MusicChanged();
        };
    }
}
