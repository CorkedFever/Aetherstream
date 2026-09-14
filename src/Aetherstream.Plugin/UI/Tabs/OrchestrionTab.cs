using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>An orchestrion roll: the game's name for it, its words, where its music is, and its shelf in the orchestrion.</summary>
internal sealed record OrchestrionRoll(uint Id, string Name, string Description, string Path, string Category);

/// <summary>
/// The orchestrion rolls as a playlist: tick the ones to hear, and they play under the drawn
/// channels in place of the music, shuffled. Only the rolls the character has by default, since
/// that is the collection; the rest are a checkbox away, because it is your set.
/// </summary>
internal sealed class OrchestrionTab(UiContext ui)
{
    private string filter = string.Empty;
    private string category = string.Empty;

    /// <summary>Set by the plugin: every roll, read once.</summary>
    internal Func<IReadOnlyList<OrchestrionRoll>>? Rolls;

    /// <summary>Set by the plugin: whether the character has a roll.</summary>
    internal Func<uint, bool>? Has;

    /// <summary>Set by the plugin: the ticked rolls changed, so they are the music now.</summary>
    internal Action? Changed;

    public void Draw()
    {
        var rolls = this.Rolls?.Invoke() ?? [];
        Ui.Hint("The game's own music, from the orchestrion. Tick rolls to make them the music under the drawn channels; they are pulled out of the game's files once and kept.");

        var mine = ui.Config.OrchestrionOnlyMine;
        if (ImGui.Checkbox("Only rolls I have", ref mine))
        {
            ui.Config.OrchestrionOnlyMine = mine;
            ui.SaveConfig();
        }

        ImGui.SameLine();
        var chosen = ui.Config.ChannelMusicRolls;
        ImGui.TextColored(Theme.TextDim, $"{chosen.Count} ticked");
        ImGui.SameLine();
        if (ImGui.SmallButton("Tick all shown"))
        {
            foreach (var r in this.Shown(rolls))
            {
                if (!chosen.Contains(r.Id))
                    chosen.Add(r.Id);
            }

            ui.SaveConfig();
            this.Changed?.Invoke();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Clear"))
        {
            chosen.Clear();
            ui.SaveConfig();
            this.Changed?.Invoke();
        }

        this.DrawCategories(rolls);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##rollfilter", "Filter by name", ref this.filter, 80);

        using var child = ImRaii.Child("##rolls", new Vector2(-1, -1), false);
        if (!child)
            return;

        var playing = ui.Session.MusicNowPlaying;
        foreach (var r in this.Shown(rolls))
        {
            var on = chosen.Contains(r.Id);
            if (ImGui.Checkbox($"##roll{r.Id}", ref on))
            {
                if (on)
                    chosen.Add(r.Id);
                else
                    chosen.Remove(r.Id);
                ui.SaveConfig();
                this.Changed?.Invoke();
            }

            ImGui.SameLine();
            var isPlaying = on && r.Name == playing;
            ImGui.TextColored(isPlaying ? Theme.Accent : Theme.Text, r.Name);
            if (r.Description.Length > 0)
                Ui.Tip(r.Description);
            ImGui.SameLine();
            ImGui.TextColored(Theme.TextFaint, r.Category);
        }
    }

    private void DrawCategories(IReadOnlyList<OrchestrionRoll> rolls)
    {
        var names = rolls.Select(r => r.Category).Where(c => c.Length > 0 && c != "All").Distinct().ToList();
        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var first = true;
        foreach (var name in new[] { "Everything" }.Concat(names))
        {
            var key = name == "Everything" ? string.Empty : name;
            var width = ImGui.CalcTextSize(name).X + (ImGui.GetStyle().FramePadding.X * 2);
            if (!first && ImGui.GetItemRectMax().X + spacing + width < rightEdge)
                ImGui.SameLine();
            first = false;

            var selected = key == this.category;
            using var colours = ImRaii.PushColor(ImGuiCol.Button, selected ? Theme.GlassLit : Theme.Glass)
                .Push(ImGuiCol.Border, selected ? Theme.Accent : Theme.GlassEdge)
                .Push(ImGuiCol.Text, selected ? Theme.Accent : Theme.Text);
            using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
            if (ImGui.Button($"{name}##cat{name}"))
                this.category = key;
        }
    }

    private IEnumerable<OrchestrionRoll> Shown(IReadOnlyList<OrchestrionRoll> rolls)
    {
        foreach (var r in rolls)
        {
            if (ui.Config.OrchestrionOnlyMine && this.Has?.Invoke(r.Id) != true)
                continue;
            if (this.category.Length > 0 && r.Category != this.category)
                continue;
            if (this.filter.Length > 0 && !r.Name.Contains(this.filter, StringComparison.OrdinalIgnoreCase))
                continue;
            yield return r;
        }
    }
}
