using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The folded window: an actual remote control, held upright.
/// <para>
/// A little display at the top that shows what you are dialling, a power key, a number pad
/// that tunes pinned channels by number, channel and volume rockers, transport, and keys for
/// the guide and the weather. The number pad is the point — pressing 7 to get channel 7 is
/// the whole reason anyone reaches for a remote — and the two-second wait after the last digit
/// is how every real set has done it.
/// </para>
/// </summary>
internal sealed class RemoteWidget(UiContext ui, ChannelDial dial)
{
    public const float Width = 176f;

    private static readonly Vector2 Key = new(52f, 34f);
    private const float Gap = 8f;

    /// <summary>How long after the last digit the number is tuned.</summary>
    private const long DialSettleMs = 1500;

    private string dialled = string.Empty;
    private long lastDigitTicks;
    private string flash = string.Empty;
    private long flashUntilTicks;

    /// <summary>Set by the window: the MENU key unfolds it.</summary>
    public Action? Unfold;

    public void Draw()
    {
        var session = ui.Session;
        var now = Environment.TickCount64;

        this.SettleDial(now);

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(0f, 0f));
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(Gap, Gap));
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 8f);

        this.DrawDisplay(session, now);

        // -- power and menu -----------------------------------------------------------------
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
        if (this.IconKey(FontAwesomeIcon.Bars, "Menu — open the full window", "##menu"))
            this.Unfold?.Invoke();

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
        if (playing)
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
            (text, colour) = (ReferenceEquals(session.Channel, ui.Weather) ? "WEATHER" : "GUIDE", Theme.Accent);
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

        // The live thumbnail on the right of the strip, when there is one.
        if (session.Uploader is { HasFrame: true } uploader)
        {
            var thumb = new Vector2((size.Y - 6f) * 16f / 9f, size.Y - 6f);
            var at = origin + new Vector2(size.X - thumb.X - 3f, 3f);
            drawList.AddImage(uploader.Handle, at, at + thumb);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
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
}
