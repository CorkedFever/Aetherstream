using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The remote control, held upright. Always the left of the window; folded, it is the window.
/// <para>
/// A little display at the top that shows what you are dialling, power, mute and Home, a number
/// pad that tunes pinned channels by number, channel and volume rockers with the guide and the
/// weather between them, transport, subtitles and audio and the gear, and a line at the bottom
/// saying what is on and how far in. The number pad is the point — pressing 7 to get channel 7
/// is the whole reason anyone reaches for a remote — and the two-second wait after the last
/// digit is how every real set has done it.
/// </para>
/// </summary>
internal sealed class RemoteWidget(UiContext ui, ChannelDial dial, Screen screen)
{
    public const float Width = 176f;

    /// <summary>The body around the keys, and its width: what the folded window and the left column measure.</summary>
    private const float Pad = 8f;

    public const float BodyWidth = Width + (Pad * 2f);

    private static readonly Vector2 Key = new(52f, 34f);
    private const float Gap = 8f;

    /// <summary>How long after the last digit the number is tuned.</summary>
    private const long DialSettleMs = 1500;

    private string dialled = string.Empty;
    private long lastDigitTicks;
    private string flash = string.Empty;
    private long flashUntilTicks;

    private string audioLanguageBuffer = string.Empty;
    private bool audioLanguageLoaded;
    private string languageBuffer = string.Empty;
    private bool languageLoaded;

    /// <summary>Set by the window: the Home key shows the apps, unfolding if need be.</summary>
    public Action? Home;

    /// <summary>Set by the window: the gear opens Setup.</summary>
    public Action? OpenSetup;

    public void Draw()
    {
        var session = ui.Session;
        var now = Environment.TickCount64;

        this.SettleDial(now);

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(0f, 0f));
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(Gap, Gap));
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 8f);

        // The body is drawn under the keys once their height is known: two draw channels, the
        // keys on top, the slab filled in afterwards on the one beneath.
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(start + new Vector2(Pad, Pad));
        ImGui.BeginGroup();

        this.DrawDisplay(session, now);

        // -- power, mute, home ----------------------------------------------------------------
        var on = session.IsPlaying || session.Channel is not null;
        using (ImRaii.PushColor(ImGuiCol.Text, on ? Theme.Bad : Theme.TextDim))
        {
            if (this.IconKey(FontAwesomeIcon.PowerOff, on ? "Off — stops the picture and puts channels away" : "On — plays the last source", "##power", on || ui.Config.Source.Length > 0))
            {
                if (on)
                {
                    session.Channel = null;
                    session.RequestStop();
                }
                else
                {
                    ui.Play(ui.Config.Source);
                }
            }
        }

        ImGui.SameLine();
        this.IconKey(FontAwesomeIcon.VolumeMute, "Mute", "##mute", ui.Config.AudioEnabled, lit: session.Muted && ui.Config.AudioEnabled, onPress: () => session.Muted = !session.Muted);

        ImGui.SameLine();
        if (this.IconKey(FontAwesomeIcon.Home, "Home — the apps", "##home"))
            this.Home?.Invoke();

        // -- number pad ---------------------------------------------------------------------
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                if (col > 0)
                    ImGui.SameLine();

                var digit = (row * 3) + col + 1;
                this.DigitKey(digit, now);
            }
        }

        this.IconKey(FontAwesomeIcon.History, "Last channel", "##last", dial.HasLast, onPress: dial.Last);
        ImGui.SameLine();
        this.DigitKey(0, now);
        ImGui.SameLine();
        this.IconKey(FontAwesomeIcon.Backspace, "Clear what you dialled", "##clear", this.dialled.Length > 0, onPress: () => this.dialled = string.Empty);

        // -- rockers ------------------------------------------------------------------------
        this.Label("CH", 0);
        this.Label("VOL", 2);
        ImGui.Dummy(new Vector2(0f, 10f));

        var canStep = dial.CanStep;
        this.IconKey(FontAwesomeIcon.CaretUp, "Channel up", "##chup", canStep, onPress: () => dial.Step(+1));
        ImGui.SameLine();
        this.ChannelKey(session, ui.Guide, FontAwesomeIcon.ThList, "Guide", "##guide");
        ImGui.SameLine();
        this.IconKey(FontAwesomeIcon.Plus, "Louder", "##volup", ui.Config.AudioEnabled, onPress: () => this.Nudge(+0.05f));

        this.IconKey(FontAwesomeIcon.CaretDown, "Channel down", "##chdown", canStep, onPress: () => dial.Step(-1));
        ImGui.SameLine();
        this.ChannelKey(session, ui.Weather, FontAwesomeIcon.CloudSun, "Weather", "##weather");
        ImGui.SameLine();
        this.IconKey(FontAwesomeIcon.Minus, "Quieter", "##voldown", ui.Config.AudioEnabled, onPress: () => this.Nudge(-0.05f));

        // -- transport ----------------------------------------------------------------------
        ImGui.Dummy(new Vector2(0f, 2f));
        var playing = session.IsPlaying;
        var seekable = playing && session.DurationMs > 0;
        var small = new Vector2((Width - (3f * Gap)) / 4f, 30f);

        this.IconKey(FontAwesomeIcon.Backward, "Back 30 seconds", "##back", seekable, size: small, onPress: () => session.Skip(-30));
        ImGui.SameLine();
        if (session.StalledAtMs > 0)
        {
            // The way out of a stall goes where play sits, since that is what anyone reaches for.
            this.IconKey(FontAwesomeIcon.Redo, "Resume where it stopped", "##resume", size: small, lit: true, onPress: () => screen.ResumeStalled?.Invoke());
        }
        else if (playing)
        {
            var paused = session.IsPaused;
            this.IconKey(paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause, paused ? "Resume" : "Pause", "##pause", size: small, lit: true, onPress: () => session.TrySetPaused(!paused));
        }
        else
        {
            this.IconKey(FontAwesomeIcon.Play, "Play the current source", "##play", ui.Config.Source.Length > 0, size: small, onPress: () => ui.Play(ui.Config.Source));
        }

        ImGui.SameLine();
        this.IconKey(FontAwesomeIcon.Forward, "Forward 30 seconds", "##fwd", seekable, size: small, onPress: () => session.Skip(30));
        ImGui.SameLine();
        this.IconKey(FontAwesomeIcon.Stop, "Stop", "##stop", playing, size: small, onPress: session.RequestStop);

        // -- subtitles, audio, setup --------------------------------------------------------
        var third = new Vector2((Width - (2f * Gap)) / 3f, 30f);
        var subtitleOn = playing && session.CurrentSubtitle >= 0;
        if (this.IconKey(FontAwesomeIcon.ClosedCaptioning, "Subtitles", "##cc", playing, size: third, lit: subtitleOn))
            ImGui.OpenPopup("##ccmenu");
        this.DrawSubtitleMenu(session);

        ImGui.SameLine();
        var audioChoice = playing && session.AudioTracks.Count > 1;
        if (this.IconKey(FontAwesomeIcon.Language, audioChoice ? "Audio track — this stream has several" : "Audio track", "##audiotrack", playing, size: third, lit: audioChoice))
            ImGui.OpenPopup("##audiomenu");
        this.DrawAudioMenu(session);

        ImGui.SameLine();
        if (this.IconKey(FontAwesomeIcon.Cog, "Setup", "##setup", size: third))
            this.OpenSetup?.Invoke();

        // -- what is on ---------------------------------------------------------------------
        this.DrawInfo(session);

        ImGui.EndGroup();
        var end = new Vector2(start.X + BodyWidth, ImGui.GetItemRectMax().Y + Pad);
        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(start, end, Theme.U32(Theme.Glass), 16f);
        drawList.AddRect(start, end, Theme.U32(Theme.GlassEdge), 16f);
        drawList.ChannelsMerge();
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(end - start);
    }

    // -- the display -----------------------------------------------------------------------------

    /// <summary>
    /// The strip at the top: digits as you dial them, then what is on. Drawn like a segment
    /// display on the body of the remote rather than as text on a window.
    /// </summary>
    private void DrawDisplay(Playback.StreamSession session, long now)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var size = new Vector2(Width, 30f);

        drawList.AddRectFilled(origin, origin + size, Theme.U32(Theme.Tube), 6f);
        drawList.AddRect(origin, origin + size, Theme.U32(Theme.GlassEdge), 6f);

        string text;
        Vector4 colour;

        if (this.dialled.Length > 0)
        {
            text = $"CH {this.dialled}_";
            colour = Theme.Accent;
        }
        else if (now < this.flashUntilTicks)
        {
            text = this.flash;
            colour = Theme.Warn;
        }
        else if (session.Error is not null)
        {
            (text, colour) = ("NO PICTURE", Theme.Bad);
        }
        else if (session.StalledAtMs > 0)
        {
            (text, colour) = ("SIGNAL LOST", Theme.Warn);
        }
        else if (session.IsPlaying)
        {
            var number = dial.NumberOf(ui.Config.Source);
            text = session.IsPaused ? "PAUSED"
                : number > 0 ? $"CH {number}"
                : session.DurationMs <= 0 ? "LIVE"
                : Ui.Clock(session.PositionMs);
            colour = Theme.Accent;
        }
        else if (session.Channel is not null)
        {
            (text, colour) = (ReferenceEquals(session.Channel, ui.Weather) ? "WEATHER" : ReferenceEquals(session.Channel, ui.Guide) ? "GUIDE" : "CHANNEL", Theme.Accent);
        }
        else
        {
            (text, colour) = ("NO SIGNAL", Theme.TextFaint);
        }

        // The little LED on the left, as on the set's own bar.
        var led = session.Error is not null ? Theme.Bad
            : session.StalledAtMs > 0 ? Theme.Warn
            : session.IsPlaying || session.Channel is not null ? Theme.Good
            : Theme.Edge;
        drawList.AddCircleFilled(origin + new Vector2(10f, size.Y / 2f), 3f, Theme.U32(led), 12);

        using (Theme.PushDisplay())
        {
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorScreenPos(origin + new Vector2(22f, (size.Y - textSize.Y) / 2f));
            ImGui.TextColored(colour, text);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    /// <summary>What is on and how far in, at the foot of the remote, so the folded window still says.</summary>
    private void DrawInfo(Playback.StreamSession session)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        drawList.AddLine(origin, origin + new Vector2(Width, 0f), Theme.U32(Theme.Edge), 1f);
        ImGui.Dummy(new Vector2(0f, 2f));

        string title;
        if (session.IsPlaying)
            title = screen.Title();
        else if (session.Channel is not null)
            title = ui.Channels.FirstOrDefault(c => ReferenceEquals(c.Channel, session.Channel)).Name ?? "Drawn channel";
        else
            title = ui.Config.Source.Length > 0 ? screen.Title() : "Nothing on";

        var faint = !session.IsPlaying && session.Channel is null;
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Width);
        ImGui.TextColored(faint ? Theme.TextFaint : Theme.TextDim, Ui.Ellipsis(title, 44));
        ImGui.PopTextWrapPos();

        if (session.IsPlaying && session.DurationMs > 0)
        {
            var at = ImGui.GetCursorScreenPos();
            var fraction = Math.Clamp((float)session.PositionMs / session.DurationMs, 0f, 1f);
            drawList.AddRectFilled(at, at + new Vector2(Width, 3f), Theme.U32(Theme.GlassEdge));
            drawList.AddRectFilled(at, at + new Vector2(Width * fraction, 3f), Theme.U32(Theme.Accent));
            ImGui.Dummy(new Vector2(Width, 3f));
        }
    }

    // -- dialling ------------------------------------------------------------------------------------

    private void DigitKey(int digit, long now)
    {
        using var font = Theme.PushDisplay();
        if (ImGui.Button($"{digit}##d{digit}", Key))
        {
            this.dialled = (this.dialled + digit).TrimStart('0');
            if (this.dialled.Length == 0)
                this.dialled = "0";

            this.lastDigitTicks = now;

            // Three digits is as long as a pin list gets; no sense waiting for a fourth.
            if (this.dialled.Length >= 3)
                this.TuneDialled(now);
        }
    }

    private void SettleDial(long now)
    {
        if (this.dialled.Length > 0 && now - this.lastDigitTicks >= DialSettleMs)
            this.TuneDialled(now);
    }

    private void TuneDialled(long now)
    {
        var number = int.TryParse(this.dialled, out var n) ? n : 0;
        this.dialled = string.Empty;

        if (dial.ByNumber(number) is { } channel)
        {
            dial.Play(channel);
            return;
        }

        this.flash = number == 0 ? "NO CH 0" : $"NO CH {number}";
        this.flashUntilTicks = now + 1500;
    }

    private void Nudge(float delta)
    {
        ui.Config.Volume = Math.Clamp(ui.Config.Volume + delta, 0f, 1f);
        ui.SaveConfig();
        this.flash = $"VOL {(int)Math.Round(ui.Config.Volume * 100)}";
        this.flashUntilTicks = Environment.TickCount64 + 1200;
    }

    // -- keys ----------------------------------------------------------------------------------------

    private bool IconKey(FontAwesomeIcon icon, string tooltip, string id, bool enabled = true, Vector2? size = null, bool lit = false, Action? onPress = null)
    {
        using var disabled = ImRaii.Disabled(!enabled);
        using var colours = ImRaii.PushColor(ImGuiCol.Button, Theme.GlassLit, lit).Push(ImGuiCol.Border, Theme.Accent, lit);
        using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, lit ? 1f : 0f);

        bool pressed;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            pressed = ImGui.Button(icon.ToIconString() + id, size ?? Key);

        Ui.Tip(tooltip);

        if (pressed && enabled)
            onPress?.Invoke();

        return pressed && enabled;
    }

    private void ChannelKey(Playback.StreamSession session, Video.IFrameChannel? channel, FontAwesomeIcon icon, string name, string id)
    {
        var up = channel is not null && ReferenceEquals(session.Channel, channel);
        this.IconKey(icon, up ? $"Put the {name.ToLowerInvariant()} away" : $"{name} channel", id, channel is { Available: true }, lit: up, onPress: () => session.Channel = up ? null : channel);
    }

    /// <summary>A small caption over a column of keys: CH over the channel rocker, VOL over the volume one.</summary>
    private void Label(string text, int column)
    {
        var origin = ImGui.GetCursorScreenPos();
        var x = column * (Key.X + Gap) + ((Key.X - ImGui.CalcTextSize(text).X) / 2f);
        ImGui.GetWindowDrawList().AddText(origin + new Vector2(x, 0f), Theme.U32(Theme.TextFaint), text);
    }

    // -- menus ---------------------------------------------------------------------------------------

    private void DrawAudioMenu(Playback.StreamSession session)
    {
        using var popup = ImRaii.Popup("##audiomenu");
        if (!popup)
            return;

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(6f, 4f));
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

    /// <summary>
    /// The tracks the stream offers, and the standing preference. Both here rather than on a
    /// settings tab, because the moment anyone wants subtitles is the moment they are watching.
    /// </summary>
    private void DrawSubtitleMenu(Playback.StreamSession session)
    {
        using var popup = ImRaii.Popup("##ccmenu");
        if (!popup)
            return;

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(6f, 4f));
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
