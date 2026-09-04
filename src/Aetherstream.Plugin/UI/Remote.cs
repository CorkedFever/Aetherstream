using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The remote: transport on the left, channel buttons in the middle, sound on the right.
/// <para>
/// The channel buttons are the point. Before, flipping between two favourite channels meant
/// opening a grid of tiles and finding them again; a remote has channel up, channel down and
/// "last channel" precisely because those are the three things people do most.
/// </para>
/// </summary>
internal sealed class Remote(UiContext ui, ChannelDial dial, Screen screen)
{
    public void Draw()
    {
        var session = ui.Session;
        var playing = session.IsPlaying;
        var seekable = playing && session.DurationMs > 0;

        // Bigger targets than the default: this row gets pressed more than anything else in the
        // window, and a remote with tiny buttons is a remote you keep missing.
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(9f, 6f));

        if (Ui.IconButton(FontAwesomeIcon.Backward, "Back 30 seconds", "##back30", seekable))
            session.Skip(-30);

        ImGui.SameLine();
        if (playing)
        {
            var paused = session.IsPaused;
            using var lit = ImRaii.PushColor(ImGuiCol.Button, Theme.GlassLit).Push(ImGuiCol.Border, Theme.Accent);
            if (Ui.IconButton(paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause, paused ? "Resume" : "Pause", "##pause"))
                session.TrySetPaused(!paused);
        }
        else if (Ui.IconButton(FontAwesomeIcon.Play, "Play the current source", "##play", ui.Config.Source.Length > 0))
        {
            ui.Play(ui.Config.Source);
        }

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.Forward, "Forward 30 seconds", "##fwd30", seekable))
            session.Skip(30);

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.FastForward, "Forward 5 minutes", "##fwd5m", seekable))
            session.Skip(300);

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.Stop, "Stop", "##stop", playing))
            session.RequestStop();

        // Only offered when it is the way out of a stall, and it returns to where the picture froze
        // rather than to the beginning.
        if (session.StalledAtMs > 0)
        {
            ImGui.SameLine();
            if (Ui.IconButton(FontAwesomeIcon.Redo, "Resume where it stopped", "##resume"))
                screen.ResumeStalled?.Invoke();
        }

        // -- channels ---------------------------------------------------------------------------

        ImGui.SameLine(0f, 18f);

        var canStep = dial.CanStep;
        if (Ui.IconButton(FontAwesomeIcon.ChevronUp, "Next pinned channel", "##chup", canStep))
            dial.Step(+1);

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.ChevronDown, "Previous pinned channel", "##chdown", canStep))
            dial.Step(-1);

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.History, "Last channel", "##last", dial.HasLast))
            dial.Last();

        if (dial.NumberOf(ui.Config.Source) is > 0 and var number)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Theme.Displayed(Theme.Accent, $"CH {number}");
        }

        // -- sound ------------------------------------------------------------------------------

        ImGui.SameLine(0f, 18f);

        var muted = session.Muted || !ui.Config.AudioEnabled;
        if (Ui.IconButton(
            muted ? FontAwesomeIcon.VolumeMute : FontAwesomeIcon.VolumeUp,
            !ui.Config.AudioEnabled ? "Sound is off — turn it on in Sound" : muted ? "Unmute" : "Mute",
            "##mute",
            ui.Config.AudioEnabled))
        {
            session.Muted = !session.Muted;
        }

        // -- readout ----------------------------------------------------------------------------

        if (playing && session.FramesPresented > 0)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();

            var tag = session.Current is { Relayed: true } ? "relay · " : string.Empty;
            Ui.RightAlignedText($"{tag}{session.FramesPresented:N0} frames", Theme.TextFaint);
        }
    }
}
