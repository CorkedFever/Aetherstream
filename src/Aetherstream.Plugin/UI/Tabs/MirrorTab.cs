using System.Numerics;

using Aetherstream.Plugin.Video;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Mirror: pick a window on this PC and it goes up on the set, the way sharing a window in a
/// call works. A game running beside this one, a video in any player, a second screen. The
/// list is what is open right now, refreshed on request rather than every frame.
/// </summary>
internal sealed class MirrorTab(UiContext ui)
{
    private List<WindowCapture.WindowInfo> windows = [];
    private bool listed;

    /// <summary>Set by the plugin: mirrors a window on the set.</summary>
    internal Action<nint, string>? Show;

    /// <summary>Set by the plugin: takes the mirror down.</summary>
    internal Action? Stop;

    /// <summary>Set by the plugin: the mirror's status line, and which window it has.</summary>
    internal Func<(string Status, nint Window, bool Up)>? State;

    public void Draw()
    {
        if (!WindowCapture.Supported)
        {
            Ui.Hint("Mirroring a window needs Windows 10 version 1903 or later.");
            return;
        }

        if (!this.listed)
        {
            this.listed = true;
            this.windows = WindowCapture.ListWindows();
        }

        Ui.Hint("Put another window on the set: a game running beside this one, a video in any player, a second screen. Its sound stays where it is for now.");

        var (status, current, up) = this.State?.Invoke() ?? ("Nothing mirrored.", 0, false);
        ImGui.TextColored(current != 0 ? Theme.Accent : Ui.Faint, status);
        if (current != 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton(up ? "Stop mirroring" : "Put it back up"))
            {
                if (up)
                    this.Stop?.Invoke();
                else if (this.windows.FirstOrDefault(w => w.Handle == current) is { Handle: not 0 } again)
                    this.Show?.Invoke(again.Handle, again.Title);
            }
        }

        ImGui.Spacing();
        if (ImGui.Button("Refresh the list"))
            this.windows = WindowCapture.ListWindows();
        ImGui.SameLine();
        ImGui.TextColored(Ui.Faint, $"{this.windows.Count} windows open");

        Ui.Hint("A window playing protected video, a browser on Netflix say, mirrors as black. That is Windows, not the set.");

        using var child = ImRaii.Child("##mirrorlist", new Vector2(-1f, -1f), false);
        if (!child)
            return;

        foreach (var w in this.windows)
        {
            var selected = w.Handle == current;
            using var colours = ImRaii.PushColor(ImGuiCol.Header, Theme.GlassLit, selected);
            var label = w.Process.Length > 0 ? $"{w.Title}##{w.Handle}" : $"{w.Title}##{w.Handle}";
            if (ImGui.Selectable(label, selected))
                this.Show?.Invoke(w.Handle, w.Title);
            if (w.Process.Length > 0)
            {
                ImGui.SameLine();
                ImGui.TextColored(Ui.Faint, w.Process);
            }
        }
    }
}
