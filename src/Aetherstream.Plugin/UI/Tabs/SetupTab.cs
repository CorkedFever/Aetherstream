using System.Numerics;

using Aetherstream.Playback;

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
            ImGui.TextColored(Theme.TextDim, "yt-dlp");
            ImGui.SameLine();
            Theme.Displayed(Theme.Accent, this.VersionOf(found));
            ImGui.SameLine();
            ImGui.TextColored(Theme.TextDim, Ui.Ellipsis(found, 52));
            Ui.Tip($"{found}\n\nIf this version is months old, YouTube will refuse it. Update with \"yt-dlp -U\" or reinstall with winget.");
        }
        else
        {
            ImGui.TextColored(Theme.Warn, "yt-dlp not found — YouTube, Kick and most sites will not play.");
        }

        // Whether YouTube can be handled at all is the second question, and it has nothing to do
        // with yt-dlp's own presence — so it gets its own line rather than being folded in.
        var runtime = YtDlpResolver.LocateJsRuntime();
        Ui.Dot(runtime is not null ? Theme.Good : Theme.Warn, runtime is not null ? "found" : "not found");
        ImGui.SameLine();
        if (runtime is not null)
        {
            ImGui.TextColored(Theme.TextDim, "JavaScript runtime for YouTube");
            ImGui.SameLine();
            ImGui.TextColored(Theme.Text, Path.GetFileNameWithoutExtension(runtime));
            Ui.Tip(runtime);
        }
        else
        {
            ImGui.TextColored(Theme.Warn, "no JavaScript runtime — YouTube will half-work at best.");
            Ui.Tip("yt-dlp solves YouTube's challenges with Deno. \"winget install DenoLand.Deno\", then restart the game.");
        }

        // A picker, not a text box. Nobody should be typing a path into a game.
        if (ImGui.Button(found is null ? "Find yt-dlp.exe…" : "Use a different yt-dlp.exe…"))
        {
            ui.FileDialogs.OpenFileDialog(
                "Where is yt-dlp.exe?",
                "yt-dlp{yt-dlp.exe},Programs{.exe},All files{.*}",
                (accepted, paths) =>
                {
                    if (accepted && paths.Count > 0)
                    {
                        ui.Config.YtDlpPath = paths[0];
                        ui.SaveConfig();
                    }
                },
                selectionCountMax: 1,
                startPath: StartFolder(),
                isModal: false);
        }

        Ui.Tip("Opens a file picker. Point it at the yt-dlp.exe you downloaded, wherever you put it.");

        if (ui.Config.YtDlpPath.Length > 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("Forget it"))
            {
                ui.Config.YtDlpPath = string.Empty;
                ui.SaveConfig();
            }

            Ui.Tip("Go back to looking in the usual places (a winget install, or the plugin's folder).");
        }

        Ui.Hint(
            "Easiest: in PowerShell run \"winget install yt-dlp\" and \"winget install DenoLand.Deno\", " +
            "then restart the game. Deno is what yt-dlp uses to handle YouTube; without it YouTube " +
            "half-works at best.");

        this.DrawSignIn();
    }

    /// <summary>
    /// The way past YouTube's "confirm you're not a bot" wall, which is the remedy that error
    /// itself names: read the signed-in session from a browser. Off by default, because most
    /// people never see the wall and reading a browser's cookies is not something to do unasked.
    /// </summary>
    private void DrawSignIn()
    {
        ImGui.Spacing();
        ImGui.TextColored(Theme.TextDim, "If YouTube says \"confirm you're not a bot\": sign in using");

        var current = ui.Config.YtDlpCookiesBrowser;
        var label = current.Length == 0 ? "nothing (default)" : Capitalise(current);

        ImGui.SetNextItemWidth(160);
        using (var combo = ImRaii.Combo("##cookiesbrowser", label))
        {
            if (combo)
            {
                if (ImGui.Selectable("nothing (default)", current.Length == 0))
                {
                    ui.Config.YtDlpCookiesBrowser = string.Empty;
                    ui.SaveConfig();
                }

                foreach (var browser in YtDlpResolver.Browsers)
                {
                    if (ImGui.Selectable(Capitalise(browser), browser == current))
                    {
                        ui.Config.YtDlpCookiesBrowser = browser;
                        ui.SaveConfig();
                    }
                }
            }
        }

        Ui.Tip(
            "yt-dlp reads the YouTube sign-in from that browser and uses it, so YouTube sees a " +
            "signed-in person rather than a bot. Nothing leaves this machine except to YouTube.\n\n" +
            "Firefox works reliably. Brave, Chrome and Edge encrypt their cookies in a way yt-dlp " +
            "can often only read while that browser is fully closed — close it first, then try.");

        if (current.Length > 0 && current is not "firefox")
        {
            ImGui.SameLine();
            ImGui.TextColored(Theme.Warn, "close the browser first");
        }
    }

    private static string Capitalise(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    // -- version readout -----------------------------------------------------------------------------

    private string? versionForPath;
    private string versionText = "…";

    /// <summary>
    /// The version of the yt-dlp at <paramref name="path"/>, probed once per path off the render
    /// thread. "…" while it is being asked.
    /// </summary>
    private string VersionOf(string path)
    {
        if (path != this.versionForPath)
        {
            this.versionForPath = path;
            this.versionText = "…";

            _ = Task.Run(async () =>
            {
                var version = await YtDlpResolver.VersionAsync(path, CancellationToken.None);

                // Only if the path has not moved on while we were asking.
                if (path == this.versionForPath)
                    this.versionText = version;
            });
        }

        return this.versionText;
    }

    /// <summary>
    /// Where the picker opens: beside the file already chosen, otherwise the Desktop — which is
    /// where a hand-downloaded exe usually is.
    /// </summary>
    private string StartFolder()
    {
        var chosen = Path.GetDirectoryName(ui.Config.YtDlpPath);
        return chosen is { Length: > 0 } && Directory.Exists(chosen)
            ? chosen
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
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
