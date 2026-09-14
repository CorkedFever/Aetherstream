using System.Numerics;

using Aetherstream.Plugin.Video;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// The drawn channels, as a dial: pick one and it goes up on the set. Also home to the market
/// watch list, since that is the one channel that needs telling what to show.
/// </summary>
internal sealed class ChannelsTab(UiContext ui)
{
    /// <summary>Set by the plugin: finds a marketable item by name, or null.</summary>
    public Func<string, (uint Id, string Name)?>? FindItem;

    /// <summary>Set by the plugin: the watch list changed, fetch again.</summary>
    public Action? WatchListChanged;

    private string itemInput = string.Empty;
    private string itemStatus = string.Empty;

    public void Draw()
    {
        Ui.Section("Channels");
        Ui.Hint("Channels drawn by the set itself, from data rather than a stream. They show wherever the picture does.");

        var session = ui.Session;
        var columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / 250f));
        var tile = new Vector2((ImGui.GetContentRegionAvail().X - ((columns - 1) * 8f)) / columns, 64f);

        var i = 0;
        foreach (var (name, blurb, channel) in ui.Channels)
        {
            if (i++ % columns != 0)
                ImGui.SameLine(0f, 8f);

            var up = ReferenceEquals(session.Channel, channel);
            var available = channel.Available;

            using var colours = ImRaii.PushColor(ImGuiCol.Button, up ? Theme.GlassLit : Theme.Glass)
                .Push(ImGuiCol.ButtonHovered, Theme.GlassLit)
                .Push(ImGuiCol.Border, up ? Theme.Accent : Theme.GlassEdge);
            using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
            using var disabled = ImRaii.Disabled(!available);

            var origin = ImGui.GetCursorScreenPos();
            if (ImGui.Button($"##ch{name}", tile))
                session.Channel = up ? null : channel;

            var drawList = ImGui.GetWindowDrawList();
            using (Theme.PushDisplay())
                drawList.AddText(origin + new Vector2(12f, 10f), Theme.U32(up ? Theme.Accent : Theme.Text), name.ToUpperInvariant());

            drawList.AddText(origin + new Vector2(12f, 36f), Theme.U32(Theme.TextDim), blurb);

            if (up)
                drawList.AddCircleFilled(origin + new Vector2(tile.X - 14f, 14f), 4f, Theme.U32(Theme.Good), 12);
        }

        if (session.Channel is not null)
        {
            ImGui.Dummy(new Vector2(0f, 4f));
            if (ImGui.Button("Back to the picture"))
                session.Channel = null;
        }

        this.DrawMarketWatch();
    }

    private void DrawMarketWatch()
    {
        Ui.Section("Market watch");
        Ui.Hint("What the market channel lists. Prices come from Universalis for the world you are on.");

        var list = ui.Config.MarketWatch;
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            using var id = ImRaii.PushId(i);

            if (ImGui.SmallButton("×"))
            {
                list.RemoveAt(i);
                ui.SaveConfig();
                this.WatchListChanged?.Invoke();
                i--;
                continue;
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(item.Name);
        }

        if (list.Count == 0)
            ImGui.TextColored(Theme.TextFaint, "nothing watched yet");

        ImGui.SetNextItemWidth(260);
        var entered = ImGui.InputTextWithHint("##marketitem", "item name, e.g. Fire Crystal", ref this.itemInput, 64, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Add") || entered) && this.itemInput.Trim().Length > 0)
        {
            var found = this.FindItem?.Invoke(this.itemInput.Trim());
            if (found is { } item)
            {
                if (list.Any(w => w.Id == item.Id))
                {
                    this.itemStatus = $"{item.Name} is already on the list.";
                }
                else if (list.Count >= 40)
                {
                    this.itemStatus = "Forty is plenty for one channel.";
                }
                else
                {
                    list.Add(new MarketItem { Id = item.Id, Name = item.Name });
                    ui.SaveConfig();
                    this.WatchListChanged?.Invoke();
                    this.itemStatus = $"Added {item.Name}.";
                    this.itemInput = string.Empty;
                }
            }
            else
            {
                this.itemStatus = "No marketable item by that name.";
            }
        }

        if (this.itemStatus.Length > 0)
            ImGui.TextColored(Theme.TextDim, this.itemStatus);

        Ui.Tip("Exact names work best; a name that starts the same way is taken when there is only one.");
    }
}
