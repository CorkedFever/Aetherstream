using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The transport, pinned above the tabs.
/// <para>
/// It sits outside the tab bar deliberately: pausing what is playing should never mean navigating
/// away from what you were doing, and the state of the picture is the one thing worth seeing from
/// every panel.
/// </para>
/// </summary>
internal sealed class NowPlayingBar(UiContext ui)
{
    /// <summary>Set by the plugin — restarting a stalled stream needs the already-resolved source.</summary>
    internal Action? ResumeStalled;

    /// <summary>
    /// Held while a scrub is in progress. The slider has to show the dragged position rather than
    /// the playing one, or the thumb springs back to the decoder's position on every frame of the
    /// drag and the bar becomes impossible to aim.
    /// </summary>
    private int scrubbing = -1;

    public void Draw()
    {
        var session = ui.Session;
        var playing = session.IsPlaying;
        var stalled = session.StalledAtMs > 0;
        var failed = session.Error is not null;

        using var child = ImRaii.Child(
            "##nowplaying",
            new Vector2(-1, this.Height()),
            true);

        if (!child)
            return;

        // --- Title row ---------------------------------------------------------------------------

        var (dot, state) =
            failed ? (Ui.Bad, "failed")
            : stalled ? (Ui.Warn, "stalled — the stream stopped sending")
            : !playing ? (Ui.Faint, "stopped")
            : session.IsPaused ? (Ui.Warn, "paused")
            : (Ui.Good, "playing");

        Ui.Dot(dot, state);
        ImGui.SameLine();

        var title = ui.Config.Recents.FirstOrDefault()?.Label is { Length: > 0 } remembered
            && string.Equals(ui.Config.Recents[0].Source, ui.Config.Source, StringComparison.OrdinalIgnoreCase)
                ? remembered
                : Ui.Pretty(ui.Config.Source);

        ImGui.TextColored(playing ? Ui.Accent : Ui.Faint, Ui.Ellipsis(title, 46));
        Ui.Tip(ui.Config.Source.Length > 0 ? ui.Config.Source : "Nothing has been played yet.");

        var duration = session.DurationMs;
        var position = session.PositionMs;
        var live = playing && duration <= 0;

        if (live)
        {
            ImGui.SameLine();
            Ui.RightAlignedText("LIVE", Ui.Bad);
            Ui.Tip("A live stream has no end to scrub towards, so there is nothing to seek through.");
        }
        else if (playing && duration > 0)
        {
            ImGui.SameLine();
            var shown = this.scrubbing >= 0 ? this.scrubbing * 1000L : position;
            Ui.RightAlignedText($"{Ui.Clock(shown)} / {Ui.Clock(duration)}", Ui.Faint);
        }

        // --- Scrub bar ---------------------------------------------------------------------------

        if (playing && duration > 0 && position >= 0)
            this.DrawScrub(position, duration);

        // --- Buttons -----------------------------------------------------------------------------

        this.DrawButtons(playing, duration > 0);

        // --- Status line -------------------------------------------------------------------------

        if (session.Error is { } error)
        {
            ImGui.TextColored(Ui.Bad, Ui.Ellipsis(error.ReplaceLineEndings(" "), 60));
            Ui.Tip(error);
        }
        else if (stalled)
        {
            ImGui.TextColored(Ui.Warn, $"Stopped receiving at {Ui.Clock(session.StalledAtMs)}.");
        }
        else if (session.Status is { } status)
        {
            ImGui.TextColored(Ui.Faint, Ui.Ellipsis(status, 60));
        }
        else
        {
            // Keeps the bar a fixed height whatever the state, so the tabs below never jump.
            ImGui.NewLine();
        }
    }

    /// <summary>
    /// The seek is applied on release, not while dragging: seeking on every frame of a drag would
    /// restart the decoder dozens of times over one gesture.
    /// </summary>
    private void DrawScrub(long position, long duration)
    {
        var seconds = this.scrubbing >= 0 ? this.scrubbing : (int)(position / 1000);
        var total = Math.Max(1, (int)(duration / 1000));

        using var frame = ImRaii.PushColor(ImGuiCol.FrameBg, Ui.AccentDim with { W = 0.18f })
            .Push(ImGuiCol.SliderGrab, Ui.Accent)
            .Push(ImGuiCol.SliderGrabActive, Ui.Accent);

        ImGui.SetNextItemWidth(-1);
        using (ImRaii.Disabled(!ui.Session.IsSeekable))
        {
            if (ImGui.SliderInt("##scrub", ref seconds, 0, total, string.Empty))
                this.scrubbing = seconds;
        }

        if (!ui.Session.IsSeekable)
        {
            Ui.Tip("This source cannot be seeked.");
            return;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            ui.Session.TrySeek(seconds * 1000L);
            this.scrubbing = -1;
        }
        else if (this.scrubbing >= 0 && !ImGui.IsItemActive())
        {
            // The drag ended somewhere the "after edit" test did not catch — release the hold rather
            // than freezing the bar on a stale position.
            this.scrubbing = -1;
        }
    }

    private void DrawButtons(bool playing, bool seekable)
    {
        var session = ui.Session;

        if (Ui.IconButton(FontAwesomeIcon.Backward, "Back 30 seconds", "##back30", playing && seekable))
            session.Skip(-30);

        ImGui.SameLine();
        if (playing)
        {
            var paused = session.IsPaused;
            if (Ui.IconButton(
                paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause,
                paused ? "Resume" : "Pause",
                "##pause"))
            {
                session.TrySetPaused(!paused);
            }
        }
        else if (Ui.IconButton(FontAwesomeIcon.Play, "Play the current source", "##play",
            ui.Config.Source.Length > 0))
        {
            ui.Play(ui.Config.Source);
        }

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.Forward, "Forward 30 seconds", "##fwd30", playing && seekable))
            session.Skip(30);

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.FastForward, "Forward 5 minutes", "##fwd5m", playing && seekable))
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
                this.ResumeStalled?.Invoke();
        }

        if (playing && session.FramesPresented > 0)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Ui.RightAlignedText($"{session.FramesPresented:N0} frames", Ui.Faint);
        }
    }

    /// <summary>
    /// A fixed height, so the tab contents below do not move as playback state changes. Four rows:
    /// title, scrub, buttons, status.
    /// </summary>
    private float Height()
    {
        var line = ImGui.GetTextLineHeightWithSpacing();
        return (line * 2) + (ImGui.GetFrameHeight() * 2) + (ImGui.GetStyle().ItemSpacing.Y * 3)
            + (ImGui.GetStyle().WindowPadding.Y * 2);
    }
}
