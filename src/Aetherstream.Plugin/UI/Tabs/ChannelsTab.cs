using System.Numerics;

using Aetherstream.Plugin.Video;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// The drawn channels, as a dial: pick one and it goes up on the set. Three groups on a strip, the
/// way the inputs are: the ones that tell you something, the shows, and the ones that are just
/// on. The strip remembers where you were; a channel that is up lights its group.
/// </summary>
internal sealed class ChannelsTab(UiContext ui)
{
    private static readonly (string Key, string Label, string Hint)[] Groups =
    [
        ("Info", "Info", "What is on, what is up, what it costs, what is open: drawn from the game and the services it talks to."),
        ("Shows", "Shows", "Programmes with a host, on a clock, so everyone watching sees the same episode."),
        ("Music", "Music", "The set's own jukebox, on screen. No breaks."),
        ("Ambience", "Ambience", "Nothing to read. Something to leave on."),
    ];

    private int group;

    public void Draw()
    {
        Ui.Section("Channels");
        Ui.Hint("Channels drawn by the set itself, from data rather than a stream. They show wherever the picture does.");

        var session = ui.Session;
        this.DrawStrip(session);

        var key = Groups[this.group].Key;
        Ui.Hint(Groups[this.group].Hint);
        ImGui.Dummy(new Vector2(0f, 4f));

        var columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / 250f));
        var tile = new Vector2((ImGui.GetContentRegionAvail().X - ((columns - 1) * 8f)) / columns, 92f);

        var i = 0;
        foreach (var (name, blurb, g, channel) in ui.Channels)
        {
            if (g != key)
                continue;

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

            // The blurb wraps to the tile; two lines fit, and the tile is tall enough for them.
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

    /// <summary>The group strip, drawn like the input strip: the display face, the chosen one underlined, a dot on the group whose channel is up.</summary>
    private void DrawStrip(Playback.StreamSession session)
    {
        var drawList = ImGui.GetWindowDrawList();
        var upGroup = ui.Channels.FirstOrDefault(c => ReferenceEquals(session.Channel, c.Channel)).Group;

        using (Theme.PushDisplay())
        {
            for (var i = 0; i < Groups.Length; i++)
            {
                if (i > 0)
                    ImGui.SameLine(0f, 14f);

                var label = Groups[i].Label.ToUpperInvariant();
                var size = ImGui.CalcTextSize(label);
                var active = i == this.group;

                if (ImGui.InvisibleButton($"##group{i}", size + new Vector2(6f, 6f)))
                    this.group = i;

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var hovered = ImGui.IsItemHovered();
                drawList.AddText(min + new Vector2(3f, 3f), Theme.U32(active ? Theme.Accent : hovered ? Theme.Text : Theme.TextDim), label);
                if (active)
                    drawList.AddRectFilled(new Vector2(min.X, max.Y - 1f), new Vector2(max.X, max.Y + 1f), Theme.U32(Theme.Accent));
                if (Groups[i].Key == upGroup)
                    drawList.AddCircleFilled(new Vector2(max.X + 5f, min.Y + 6f), 3f, Theme.U32(Theme.Good), 10);
            }
        }

        var y = ImGui.GetItemRectMax().Y + 5f;
        var left = ImGui.GetCursorScreenPos().X;
        drawList.AddLine(new Vector2(left, y), new Vector2(left + ImGui.GetContentRegionAvail().X, y), Theme.U32(Theme.Edge), 1f);
        ImGui.Dummy(new Vector2(0f, 8f));
    }
}
