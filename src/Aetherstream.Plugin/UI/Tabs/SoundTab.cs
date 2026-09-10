using Aetherstream.Plugin.Audio;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

internal sealed class SoundTab(UiContext ui)
{
    private List<(string Id, string Name)> devices = [];
    private bool devicesListed;

    /// <summary>
    /// Which output the sound plays through. Exists because Windows cannot redirect it for us:
    /// the stream is opened on a specific endpoint, so the per-app routing in Windows' own sound
    /// settings never applies, and a headset plugged in mid-stream is not picked up either.
    /// </summary>
    private void DrawDevicePicker()
    {
        var chosen = ui.Config.AudioDeviceId;
        var label = chosen.Length == 0
            ? "System default"
            : this.devices.FirstOrDefault(d => d.Id == chosen).Name ?? "(chosen device not present)";

        ImGui.SetNextItemWidth(-1);
        using (var combo = ImRaii.Combo("##audiodevice", label))
        {
            if (combo)
            {
                // Enumerated when the list opens, not every frame — asking Windows for its
                // endpoints is a COM round trip, and nothing about it changes while you look.
                if (!this.devicesListed)
                {
                    this.devices = AudioOutput.Devices();
                    this.devicesListed = true;
                }

                if (ImGui.Selectable("System default", chosen.Length == 0))
                    this.Choose(string.Empty);

                foreach (var (id, name) in this.devices)
                {
                    if (ImGui.Selectable(name, id == chosen))
                        this.Choose(id);
                }
            }
            else
            {
                this.devicesListed = false;
            }
        }

        Ui.Tip(
            "Where the sound comes out. Switches straight away, mid-stream. \"System default\" " +
            "follows whatever Windows had as the default when playback started.");
    }

    private void Choose(string id)
    {
        if (id == ui.Config.AudioDeviceId)
            return;

        ui.Config.AudioDeviceId = id;
        ui.SaveConfig();
        ui.Session.ReopenAudio();
    }

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

        this.DrawDevicePicker();

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
