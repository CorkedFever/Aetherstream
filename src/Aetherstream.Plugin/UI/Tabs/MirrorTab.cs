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

    /// <summary>Set by the plugin: the channel itself, for the view controls.</summary>
    internal MirrorChannel? Channel;

    private string resizeNote = string.Empty;

    /// <summary>How the window is shown: the whole of it shrunk, or a piece at its own pixels, and a way to make the window fit.</summary>
    private void DrawView(nint current)
    {
        if (this.Channel is not { } channel || current == 0)
            return;

        var (width, height) = channel.Size;
        var larger = width > Canvas.Width || height > Canvas.Height;
        ImGui.Spacing();
        ImGui.TextColored(Ui.Faint, width > 0 ? $"The window is {width}x{height}; the picture is {Canvas.Width}x{Canvas.Height}." : "Waiting for the first frame…");

        var actual = channel.ActualPixels;
        if (ImGui.RadioButton("Whole window, shrunk to fit", !actual))
            channel.ActualPixels = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("A piece of it, at actual pixels", actual))
            channel.ActualPixels = true;
        Ui.Tip("A window much bigger than the picture loses its text when shrunk. At actual pixels a picture-sized piece of it is shown sharp, and the sliders choose which piece.");

        if (actual && larger)
        {
            var panX = channel.PanX;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.SliderFloat("Left / right##mirrorpan", ref panX, 0f, 1f, "%.2f"))
                channel.PanX = panX;
            var panY = channel.PanY;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.SliderFloat("Up / down##mirrorpan", ref panY, 0f, 1f, "%.2f"))
                channel.PanY = panY;
        }

        if (larger && ImGui.Button($"Size the window to {Canvas.Width}x{Canvas.Height}"))
        {
            this.resizeNote = WindowCapture.ResizeClient(current, Canvas.Width, Canvas.Height)
                ? "Resized. It mirrors pixel for pixel now."
                : "That window would not resize. A fullscreen game decides its own size.";
        }

        if (larger)
            Ui.Tip("Makes the window the same size as the picture, so the whole of it mirrors sharp. Best for a browser or a windowed game; drag it back afterwards.");
        if (this.resizeNote.Length > 0)
            ImGui.TextColored(Ui.Faint, this.resizeNote);
    }

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

        this.DrawView(current);

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
