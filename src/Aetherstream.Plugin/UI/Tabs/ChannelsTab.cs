using System.Numerics;

using Aetherstream.Plugin.Video;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// The drawn channels, as a dial: pick one and it goes up on the set.
/// </summary>
internal sealed class ChannelsTab(UiContext ui)
{
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
            ChannelLogos.Draw(drawList, name, origin + new Vector2(10f, 10f), 44f, up);

            using (Theme.PushDisplay())
                drawList.AddText(origin + new Vector2(64f, 10f), Theme.U32(up ? Theme.Accent : Theme.Text), name.ToUpperInvariant());

            // The blurb is cut to the tile, with an ellipsis, rather than run under the next one.
            var blurbWidth = tile.X - 64f - 24f;
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), origin + new Vector2(64f, 36f), Theme.U32(Theme.TextDim), blurb, blurbWidth);

            if (up)
                drawList.AddCircleFilled(origin + new Vector2(tile.X - 14f, 14f), 4f, Theme.U32(Theme.Good), 12);
        }

        if (session.Channel is not null)
        {
            ImGui.Dummy(new Vector2(0f, 4f));
            if (ImGui.Button("Back to the picture"))
                session.Channel = null;
        }

        Ui.Hint("The guide's lineup, the market watch list and the venues filter are in Setup.");
    }

}
