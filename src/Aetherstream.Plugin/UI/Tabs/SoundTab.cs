using Aetherstream.Plugin.Audio;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

internal sealed class SoundTab(UiContext ui)
{
    /// <summary>Set by the plugin: fetches the Plex audio playlists into <see cref="SetPlexPlaylists"/>.</summary>
    public Action? LoadPlexPlaylists;

    /// <summary>Set by the plugin: the music source changed, so its track list should be rebuilt.</summary>
    public Action? MusicChanged;

    private List<Aetherstream.Playback.PlexLibrary.Playlist> plexPlaylists = [];
    private string plexStatus = string.Empty;

    public void SetPlexPlaylists(List<Aetherstream.Playback.PlexLibrary.Playlist> value, string status)
    {
        this.plexPlaylists = value;
        this.plexStatus = status;
    }

    /// <summary>
    /// What plays under the guide and the weather. Bundled tracks by default, so it works out of
    /// the box; your own folder or a Plex playlist when you would rather it were your music.
    /// </summary>
    private void DrawMusic()
    {
        Ui.Section("Channel music");

        var on = ui.Config.ChannelMusic;
        if (ImGui.Checkbox("Play music on the guide and weather channels", ref on))
        {
            ui.Config.ChannelMusic = on;
            ui.SaveConfig();
        }

        Ui.Tip("The smooth jazz under the forecast. The weather covers the picture, so its sound gives way to the music; the guide keeps the picture in the corner, so a film playing keeps its sound and the music only fills in when nothing is on.");

        if (!on)
            return;

        var level = ui.Config.ChannelMusicVolume;
        if (ImGui.SliderFloat("Music level", ref level, 0f, 1f, "%.2f"))
        {
            ui.Config.ChannelMusicVolume = level;
            ui.SaveConfig();
        }

        Ui.Tip("Relative to the main volume, so turning the set down turns this down with it.");

        var source = ui.Config.ChannelMusicSource;
        var label = source switch { "folder" => "A folder of my own", "plex" => "A Plex playlist", _ => "The bundled tracks" };

        ImGui.SetNextItemWidth(260);
        using (var combo = ImRaii.Combo("##musicsource", label))
        {
            if (combo)
            {
                foreach (var (key, name) in (ReadOnlySpan<(string, string)>)[("bundled", "The bundled tracks"), ("folder", "A folder of my own"), ("plex", "A Plex playlist")])
                {
                    if (ImGui.Selectable(name, key == source) && key != source)
                    {
                        ui.Config.ChannelMusicSource = key;
                        ui.SaveConfig();
                        this.MusicChanged?.Invoke();
                        if (key == "plex")
                            this.LoadPlexPlaylists?.Invoke();
                    }
                }
            }
        }

        switch (source)
        {
            case "folder":
                this.DrawMusicFolder();
                break;
            case "plex":
                this.DrawMusicPlex();
                break;
            default:
                Ui.Hint("A few royalty-free lounge tracks that ship with the plugin. Credits are in the music folder.");
                break;
        }

        if (ui.Session.MusicNowPlaying is { Length: > 0 } track)
            ImGui.TextColored(Theme.TextDim, $"Now playing: {track}");
    }

    private void DrawMusicFolder()
    {
        var folder = ui.Config.ChannelMusicFolder;
        ImGui.TextColored(folder.Length > 0 ? Theme.Text : Theme.TextFaint, folder.Length > 0 ? folder : "no folder chosen");

        if (ImGui.Button(folder.Length > 0 ? "Choose a different folder…" : "Choose a folder…"))
        {
            ui.FileDialogs.OpenFolderDialog(
                "Which folder has the music?",
                (accepted, path) =>
                {
                    if (accepted && path.Length > 0)
                    {
                        ui.Config.ChannelMusicFolder = path;
                        ui.SaveConfig();
                        this.MusicChanged?.Invoke();
                    }
                },
                folder.Length > 0 ? folder : Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                true);
        }

        Ui.Tip("Searched for mp3, flac, ogg, m4a, wav and opus files, subfolders included, and shuffled.");
    }

    private void DrawMusicPlex()
    {
        var chosen = ui.Config.ChannelMusicPlexPlaylist;
        var label = chosen.Length > 0 ? ui.Config.ChannelMusicPlexPlaylistName : "pick a playlist";

        ImGui.SetNextItemWidth(260);
        using (var combo = ImRaii.Combo("##musicplex", label))
        {
            if (combo)
            {
                if (this.plexPlaylists.Count == 0)
                    ImGui.TextColored(Theme.TextFaint, this.plexStatus.Length > 0 ? this.plexStatus : "no audio playlists found");

                foreach (var playlist in this.plexPlaylists)
                {
                    if (ImGui.Selectable($"{playlist.Title} ({playlist.Count})##pl{playlist.RatingKey}", playlist.RatingKey == chosen))
                    {
                        ui.Config.ChannelMusicPlexPlaylist = playlist.RatingKey;
                        ui.Config.ChannelMusicPlexPlaylistName = playlist.Title;
                        ui.SaveConfig();
                        this.MusicChanged?.Invoke();
                    }
                }
            }
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Refresh"))
            this.LoadPlexPlaylists?.Invoke();

        Ui.Tip("Audio playlists on the Plex server you are signed in to. Tracks stream from it directly.");
    }

    private List<(string Id, string Name)> devices = [];
    private bool devicesListed;
    private bool everListed;

    /// <summary>
    /// Which output the sound plays through. Exists because Windows cannot redirect it for us:
    /// the stream is opened on a specific endpoint, so the per-app routing in Windows' own sound
    /// settings never applies, and a headset plugged in mid-stream is not picked up either.
    /// </summary>
    private void DrawDevicePicker()
    {
        var chosen = ui.Config.AudioDeviceId;

        // A chosen device needs its name before the list is ever opened, or the closed picker
        // would call it missing. Once, not per frame — enumeration is a COM round trip.
        if (chosen.Length > 0 && !this.everListed)
        {
            this.devices = AudioOutput.Devices();
            this.everListed = true;
        }

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

        var spatial = ui.Config.SpatialSound;
        if (ImGui.Checkbox("Sound comes from where the screen is", ref spatial))
        {
            ui.Config.SpatialSound = spatial;
            ui.SaveConfig();
        }

        Ui.Tip(
            "A set on your left sounds like it is on your left. It follows the camera, not your " +
            "character, and it only ever turns the far side down — nothing gets louder.");

        this.DrawMusic();

        Ui.Section("Sync");

        var autoSync = ui.Config.AutoSync;
        if (ImGui.Checkbox("Auto-sync", ref autoSync))
        {
            ui.Config.AutoSync = autoSync;
            ui.SaveConfig();
        }

        Ui.Tip(
            "libvlc hands the sound over up to two seconds before it means it to be heard. Auto-sync " +
            "measures that at the start of each play and holds the sound back by exactly that much, " +
            "so it lands with the picture. The slider below is applied on top. Takes effect on the next Play.");

        // The slider edits the current source's own offset, which is remembered by source; with
        // nothing playing it edits the default that anything unlisted starts from.
        var source = ui.Config.Source;
        var perSource = source.Length > 0;
        var offset = perSource ? ui.Config.OffsetFor(source) : ui.Config.AudioOffsetMs;
        var remembered = perSource && ui.Config.AudioOffsets.ContainsKey(source);

        ImGui.TextColored(
            offset == 0 ? Ui.Faint : Ui.Accent,
            (offset == 0 ? "sound and picture unshifted"
            : offset > 0 ? $"sound held back {offset} ms"
            : $"sound brought forward {-offset} ms") + (perSource ? remembered ? "  (this source)" : "  (default)" : string.Empty));

        if (ImGui.SliderInt("##audiooffset", ref offset, -1500, 1500, "%d ms"))
        {
            if (perSource)
                ui.Config.AudioOffsets[source] = offset;
            else
                ui.Config.AudioOffsetMs = offset;
            ui.SaveConfig();
        }

        Ui.Tip(
            "Positive holds the sound back — use it when the sound runs ahead of the picture. " +
            "Negative brings it forward. Nothing is discarded; libvlc shifts the sound at the " +
            "source. Takes effect on the next Play, and is remembered for this source.");

        if (perSource && remembered && ImGui.SmallButton("Forget for this source"))
        {
            ui.Config.AudioOffsets.Remove(source);
            ui.SaveConfig();
        }

        if (perSource && remembered)
            ImGui.SameLine();

        if (ImGui.SmallButton("Make this the default"))
        {
            ui.Config.AudioOffsetMs = offset;
            ui.SaveConfig();
        }

        Ui.Hint(
            "Each source keeps its own value once you touch the slider while it plays, so a channel " +
            "that needs +400 stays at +400 and the next one starts from the default. Live channels " +
            "usually need none; a Plex transcode has needed around +1000 ms. The log's [sync] line, " +
            "every three seconds, says whether the plugin is in step: a steady lead is the stream's " +
            "own offset, a drifting one is ours.");
    }
}
