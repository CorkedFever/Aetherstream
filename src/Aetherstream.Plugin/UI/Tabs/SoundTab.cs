using Dalamud.Bindings.ImGui;

namespace Aetherstream.Plugin.UI.Tabs;

internal sealed class SoundTab(UiContext ui)
{
    public void Draw()
    {
        Ui.Section("Sound");

        var enabled = ui.Config.AudioEnabled;
        if (ImGui.Checkbox("Play sound", ref enabled))
        {
            ui.Config.AudioEnabled = enabled;
            ui.SaveConfig();
        }

        Ui.Tip("Takes effect the next time playback starts.");

        var volume = ui.Config.Volume;
        if (ImGui.SliderFloat("Volume", ref volume, 0f, 1f, "%.2f"))
        {
            ui.Config.Volume = volume;
            ui.SaveConfig();
        }

        var falloff = ui.Config.AudioFalloffYalms;
        if (ImGui.SliderFloat("Fades out over", ref falloff, 0f, 60f, "%.0f yalms"))
        {
            ui.Config.AudioFalloffYalms = falloff;
            ui.SaveConfig();
        }

        Ui.Tip(
            "Sound quietens as you walk away from the screen, so it behaves like something in the " +
            "room. Set it to zero to keep the level constant wherever you are.");

        Ui.Section("Sync");

        var offset = ui.Config.AudioOffsetMs;

        ImGui.TextColored(
            offset == 0 ? Ui.Faint : Ui.Accent,
            offset == 0 ? "sound and picture unshifted"
            : offset > 0 ? $"sound held back {offset} ms"
            : $"sound brought forward {-offset} ms");

        if (ImGui.SliderInt("##audiooffset", ref offset, -1500, 1500, "%d ms"))
        {
            ui.Config.AudioOffsetMs = offset;
            ui.SaveConfig();
        }

        Ui.Tip(
            "Positive holds the sound back — use it when the sound runs ahead of the picture. " +
            "Negative brings it forward. Nothing is discarded; libvlc shifts the sound at the " +
            "source. Takes effect on the next Play.");

        if (offset != 0 && ImGui.SmallButton("Reset to zero"))
        {
            ui.Config.AudioOffsetMs = 0;
            ui.SaveConfig();
        }

        Ui.Hint(
            "This is one setting for every source, and the right value is not the same for all of " +
            "them: live streams generally need none, while a Plex transcode has needed around " +
            "+1000 ms. Retune it when you switch between the two.");
    }
}
