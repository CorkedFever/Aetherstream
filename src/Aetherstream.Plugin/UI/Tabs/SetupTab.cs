using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Things set once and then forgotten, plus the diagnostics. Deliberately the last tab: nothing here
/// is needed to watch anything.
/// </summary>
internal sealed class SetupTab(UiContext ui)
{
    private bool showToken;

    public void Draw()
    {
        this.DrawTools();
        this.DrawPlex();
        this.DrawDecoding();
        this.DrawDiagnostics();
    }

    /// <summary>
    /// yt-dlp: where it is, and where to say it is. First on the tab because it is the one thing a
    /// new install is most likely to be missing, and "I downloaded it, where do I put it" should
    /// have an answer that is not a numbered folder.
    /// </summary>
    private void DrawTools()
    {
        Ui.Section("YouTube and other sites");

        var found = ui.LocateYtDlp();

        Ui.Dot(found is not null ? Theme.Good : Theme.Warn, found is not null ? "found" : "not found");
        ImGui.SameLine();

        if (found is not null)
        {
            ImGui.TextColored(Theme.TextDim, "yt-dlp found:");
            ImGui.SameLine();
            ImGui.TextColored(Theme.Text, Ui.Ellipsis(found, 60));
            Ui.Tip(found);
        }
        else
        {
            ImGui.TextColored(Theme.Warn, "yt-dlp not found — YouTube, Kick and most sites will not play.");
        }

        var path = ui.Config.YtDlpPath;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##ytdlppath", @"where you put yt-dlp.exe, e.g. C:\Users\you\Desktop\yt-dlp", ref path, 512))
        {
            ui.Config.YtDlpPath = path.Trim();
            ui.SaveConfig();
        }

        Ui.Tip(
            "Paste the folder yt-dlp.exe is in, or the file itself. Leave it empty if you " +
            "installed with winget — that is found automatically.");

        Ui.Hint(
            "Easiest: in PowerShell run \"winget install yt-dlp\" and \"winget install DenoLand.Deno\", " +
            "then restart the game. Deno is what yt-dlp uses to handle YouTube; without it YouTube " +
            "half-works at best.");
    }

    private void DrawPlex()
    {
        Ui.Section("Plex server");

        Ui.Hint("Signing in on the Library tab fills these in. Type them yourself only if that fails.");

        var server = ui.Config.PlexServer;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##plexserver", "http://192.168.1.20:32400", ref server, 256))
        {
            ui.Config.PlexServer = server.Trim();
            ui.SaveConfig();
        }

        var token = ui.Config.PlexToken;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint(
            "##plextoken",
            "X-Plex-Token",
            ref token,
            128,
            this.showToken ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password))
        {
            ui.Config.PlexToken = token.Trim();
            ui.SaveConfig();
        }

        ImGui.Checkbox("Show token", ref this.showToken);

        ImGui.SameLine();
        Ui.Dot(
            ui.Config.PlexServer.Length > 0 && ui.Config.PlexToken.Length > 0 ? Ui.Good : Ui.Faint,
            ui.Config.PlexServer.Length > 0 && ui.Config.PlexToken.Length > 0
                ? "configured"
                : "not configured");

        // Direct play sends the original file. Over a WAN link that is a whole remux crossing the
        // internet in real time, which is exactly what a bitrate ceiling is for.
        var options = new[] { 0, 20000, 12000, 8000, 4000, 2000 };
        var labels = new[] { "Original file (LAN only)", "20 Mbps", "12 Mbps", "8 Mbps", "4 Mbps", "2 Mbps" };

        var index = Array.IndexOf(options, ui.Config.PlexMaxKilobits);
        if (index < 0)
            index = 0;

        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##plexquality", ref index, labels, labels.Length))
        {
            ui.Config.PlexMaxKilobits = options[index];
            ui.SaveConfig();
        }

        Ui.Tip(
            "From a remote server, pick a bitrate your connection can carry — Plex transcodes down " +
            "to it. \"Original file\" sends the untouched file and is only sensible on a LAN.\n\n" +
            "Transcoding is also what puts the sound out of step with the picture, so direct play " +
            "is worth trying first if your line can take it.");
    }

    private void DrawDecoding()
    {
        Ui.Section("Decoding");

        var hardware = ui.Config.UseHardwareDecode;
        if (ImGui.Checkbox("Hardware decoding", ref hardware))
        {
            ui.Config.UseHardwareDecode = hardware;
            ui.SaveConfig();
        }

        Ui.Tip(
            "Off by default, and worth leaving off. It spins up a second D3D11 video device inside " +
            "the game, which has been a source of instability here. Software decode of one 720p " +
            "stream costs a few percent of a modern CPU.\n\nTakes effect the next time playback starts.");
    }

    private void DrawDiagnostics()
    {
        Ui.Section("Diagnostics");

        if (ImGui.Button("Inspect what I'm targeting"))
            ui.Inspector.Inspect(ui.FindAnchor());

        ImGui.SameLine();
        if (ImGui.Button("Copy report"))
            ImGui.SetClipboardText(ui.Inspector.Report);

        Ui.Hint("Read-only: it reports what an object is made of and changes nothing.");

        using var child = ImRaii.Child("##report", new Vector2(-1, -1), true);
        if (!child)
            return;

        ImGui.TextUnformatted(
            ui.Inspector.Report.Length > 0
                ? ui.Inspector.Report
                : "Target something and press Inspect.");
    }
}
