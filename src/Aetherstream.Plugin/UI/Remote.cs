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

        // Offered for a while after a resume: the one time "from the beginning" is what someone
        // might have meant instead.
        if (playing && session.ResumedAtMs > 0 && Environment.TickCount64 - session.ResumedTicks < 20_000)
        {
            ImGui.SameLine();
            if (Ui.IconButton(FontAwesomeIcon.StepBackward, "Start over from the beginning", "##startover"))
                session.TrySeek(0);
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

        // The guide channel. Lit while it is up; works with or without something playing.
        ImGui.SameLine();
        var guideUp = session.GuideActive;
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.GlassLit, guideUp).Push(ImGuiCol.Border, Theme.Accent, guideUp))
        {
            if (Ui.IconButton(FontAwesomeIcon.ThList, guideUp ? "Put the guide away" : "Guide channel", "##guide", session.Guide is { Available: true }))
                session.GuideActive = !guideUp;
        }

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

        // -- subtitles --------------------------------------------------------------------------

        ImGui.SameLine();
        var subtitleOn = playing && session.CurrentSubtitle >= 0;
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.GlassLit, subtitleOn).Push(ImGuiCol.Border, Theme.Accent, subtitleOn))
        {
            if (Ui.IconButton(FontAwesomeIcon.ClosedCaptioning, "Subtitles", "##cc", playing))
                ImGui.OpenPopup("##ccmenu");
        }

        this.DrawSubtitleMenu(session);

        // Audio tracks: dubs and commentaries. Lit when there is actually a choice to make.
        ImGui.SameLine();
        var audioChoice = playing && session.AudioTracks.Count > 1;
        using (ImRaii.PushColor(ImGuiCol.Button, Theme.GlassLit, audioChoice).Push(ImGuiCol.Border, Theme.Accent, audioChoice))
        {
            if (Ui.IconButton(FontAwesomeIcon.Language, audioChoice ? "Audio track — this stream has several" : "Audio track", "##audiotrack", playing))
                ImGui.OpenPopup("##audiomenu");
        }

        this.DrawAudioMenu(session);

        // -- readout ----------------------------------------------------------------------------

        if (playing && session.FramesPresented > 0)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();

            var tag = session.Current is { Relayed: true } ? "relay · " : string.Empty;
            Ui.RightAlignedText($"{tag}{session.FramesPresented:N0} frames", Theme.TextFaint);
        }
    }

    private string audioLanguageBuffer = string.Empty;
    private bool audioLanguageLoaded;

    private void DrawAudioMenu(Playback.StreamSession session)
    {
        using var popup = ImRaii.Popup("##audiomenu");
        if (!popup)
            return;

        var tracks = session.AudioTracks;
        var current = session.CurrentAudioTrack;

        Theme.Displayed(Theme.Accent, "AUDIO");

        if (tracks.Count == 0)
            ImGui.TextColored(Theme.TextFaint, "no tracks listed yet");

        foreach (var (id, name) in tracks)
        {
            if (ImGui.Selectable($"{name}##aud{id}", id == current))
                session.SetAudioTrack(id);
        }

        ImGui.Separator();
        ImGui.TextColored(Theme.TextDim, "Always prefer");

        if (!this.audioLanguageLoaded)
        {
            this.audioLanguageBuffer = ui.Config.AudioLanguage;
            this.audioLanguageLoaded = true;
        }

        ImGui.SetNextItemWidth(160);
        if (ImGui.InputTextWithHint("##audlang", "Japanese, eng…", ref this.audioLanguageBuffer, 32, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ui.Config.AudioLanguage = this.audioLanguageBuffer.Trim();
            ui.SaveConfig();
        }

        Ui.Tip(
            "Picked automatically when a stream starts, by matching the track's name — the original " +
            "Japanese over a dub, say. Leave it empty to let the decoder choose. Press Enter to save.");
    }

    private string languageBuffer = string.Empty;
    private bool languageLoaded;

    /// <summary>
    /// The tracks the stream offers, and the standing preference. Both here rather than on a
    /// settings tab, because the moment anyone wants subtitles is the moment they are watching.
    /// </summary>
    private void DrawSubtitleMenu(Playback.StreamSession session)
    {
        using var popup = ImRaii.Popup("##ccmenu");
        if (!popup)
            return;

        var tracks = session.Subtitles;
        var current = session.CurrentSubtitle;

        Theme.Displayed(Theme.Accent, "SUBTITLES");

        if (ImGui.Selectable("Off", current < 0))
            session.SetSubtitle(-1);

        if (tracks.Count == 0)
            ImGui.TextColored(Theme.TextFaint, "this stream offers none");

        foreach (var (id, name) in tracks)
        {
            if (ImGui.Selectable($"{name}##sub{id}", id == current))
                session.SetSubtitle(id);
        }

        ImGui.Separator();
        ImGui.TextColored(Theme.TextDim, "Always prefer");

        if (!this.languageLoaded)
        {
            this.languageBuffer = ui.Config.SubtitleLanguage;
            this.languageLoaded = true;
        }

        ImGui.SetNextItemWidth(160);
        if (ImGui.InputTextWithHint("##sublang", "English, jpn, off…", ref this.languageBuffer, 32, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ui.Config.SubtitleLanguage = this.languageBuffer.Trim();
            ui.SaveConfig();
        }

        Ui.Tip(
            "Picked automatically when a stream starts, by matching the track's name. Leave it " +
            "empty to let the decoder choose, or type \"off\" to always start without them. Press Enter to save.");
    }
}
