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

    /// <summary>Set by the plugin: finds a marketable item by name, or null.</summary>
    public Func<string, (uint Id, string Name)?>? FindItem;

    /// <summary>Set by the plugin: the watch list changed, fetch again.</summary>
    public Action? WatchListChanged;

    /// <summary>Set by the plugin: checks every lineup channel's link in the background.</summary>
    public Action? CheckChannels;

    /// <summary>Set by the plugin: puts a made-up emergency notice on the banner for a few seconds.</summary>
    public Action? TestBanner;

    /// <summary>Set by the plugin: the last check's verdict.</summary>
    public Func<string>? HealthStatus;

    /// <summary>Set by the window: what the playlist offers to pick a lineup from.</summary>
    public Func<(IReadOnlyList<string> Countries, IReadOnlyList<string> Groups)>? LineupChoices;

    private string itemInput = string.Empty;
    private string itemStatus = string.Empty;

    public void Draw()
    {
        this.DrawChannelSettings();
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
            Ui.Tip("yt-dlp solves YouTube's challenges with Deno. \"winget install --id DenoLand.Deno --exact\", then restart the game.");
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
            "Easiest: in PowerShell run \"winget install --id yt-dlp.yt-dlp --exact\" and \"winget install --id DenoLand.Deno --exact\", " +
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

        // The route that works when the browser one does not — which for Brave, Chrome and Edge
        // is most of the time ("failed to decrypt with DPAPI").
        var file = ui.Config.YtDlpCookiesFile;
        var haveFile = file.Length > 0 && File.Exists(file);

        ImGui.TextColored(Theme.TextDim, "or use a cookies file exported from your browser");

        if (ImGui.Button(haveFile ? "Use a different cookies file…" : "Pick a cookies file…"))
        {
            ui.FileDialogs.OpenFileDialog(
                "Pick the exported cookies file",
                "Cookies{.txt},All files{.*}",
                (accepted, paths) =>
                {
                    if (accepted && paths.Count > 0)
                    {
                        ui.Config.YtDlpCookiesFile = paths[0];
                        ui.SaveConfig();
                    }
                },
                selectionCountMax: 1,
                startPath: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
                isModal: false);
        }

        Ui.Tip(
            "How to make one: install the \"Get cookies.txt LOCALLY\" extension in your browser, " +
            "open youtube.com while signed in, click the extension, Export. That saves a cookies.txt " +
            "— pick it here. It works for every browser, Brave included.\n\n" +
            "That file is your YouTube sign-in. Keep it to yourself, and pick it again after you " +
            "sign out or the cookies expire.");

        if (file.Length > 0)
        {
            ImGui.SameLine();
            Ui.Dot(haveFile ? Theme.Good : Theme.Bad, haveFile ? "using it" : "file is missing");
            ImGui.SameLine();
            ImGui.TextColored(haveFile ? Theme.TextDim : Theme.Bad, Ui.Ellipsis(Path.GetFileName(file), 30));
            Ui.Tip(file);

            ImGui.SameLine();
            if (ImGui.Button("Forget it##cookies"))
            {
                ui.Config.YtDlpCookiesFile = string.Empty;
                ui.SaveConfig();
            }
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

        var buffer = ui.Config.NetworkCachingMs / 1000;
        ImGui.SetNextItemWidth(220);
        if (ImGui.SliderInt("Buffer before playing", ref buffer, 2, 20, "%d s"))
        {
            ui.Config.NetworkCachingMs = buffer * 1000;
            ui.SaveConfig();
        }

        Ui.Tip(
            "How much libvlc holds before it starts, the way a Roku sits on a spinner first. More " +
            "rides out a slow host; less starts faster. The audio ring grows with it. Takes effect " +
            "on the next Play.");

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

    // -- the drawn channels' settings --------------------------------------------------------------

    /// <summary>The guide's lineup, the market watch list, and the venues filter, together.</summary>
    private void DrawChannelSettings()
    {
        Ui.Section("Guide lineup");
        Ui.Hint("What the guide channel lists after your pinned channels, numbered on from them. Channel up and down step through it.");

        var (countries, groups) = this.LineupChoices?.Invoke() ?? ([], []);

        ImGui.SetNextItemWidth(160);
        var countryLabel = ui.Config.GuideCountry.Length > 0 ? ui.Config.GuideCountry : "my region";
        using (var combo = ImRaii.Combo("##guidecountry", countryLabel))
        {
            if (combo)
            {
                if (ImGui.Selectable("my region", ui.Config.GuideCountry.Length == 0))
                {
                    ui.Config.GuideCountry = string.Empty;
                    ui.SaveConfig();
                }

                foreach (var country in countries)
                {
                    if (ImGui.Selectable(country, country == ui.Config.GuideCountry))
                    {
                        ui.Config.GuideCountry = country;
                        ui.SaveConfig();
                    }
                }
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        var groupLabel = ui.Config.GuideGroup.Length > 0 ? ui.Config.GuideGroup : "every group";
        using (var combo = ImRaii.Combo("##guidegroup", groupLabel))
        {
            if (combo)
            {
                if (ImGui.Selectable("every group", ui.Config.GuideGroup.Length == 0))
                {
                    ui.Config.GuideGroup = string.Empty;
                    ui.SaveConfig();
                }

                foreach (var group in groups)
                {
                    if (ImGui.Selectable(group, group == ui.Config.GuideGroup))
                    {
                        ui.Config.GuideGroup = group;
                        ui.SaveConfig();
                    }
                }
            }
        }

        Ui.Tip("Countries are the playlist's two-letter codes. \"My region\" follows the character: US, UK, JP or AU. Up to 120 channels are listed.");

        if (ImGui.Button("Check the lineup's links"))
            this.CheckChannels?.Invoke();

        Ui.Tip("Asks each channel for its headers and drops the ones that do not answer for a day. Runs on its own once per lineup; a channel that plays black for fifteen seconds is dropped the same way.");

        if (this.HealthStatus?.Invoke() is { Length: > 0 } verdict)
        {
            ImGui.SameLine();
            ImGui.TextColored(Theme.TextDim, verdict);
        }

        var dead = ui.Config.LiveTvDead.Count;
        if (dead > 0)
        {
            ImGui.TextColored(Theme.TextFaint, $"{dead} channel{(dead == 1 ? string.Empty : "s")} on the dead list");
            ImGui.SameLine();
            if (ImGui.SmallButton("Forgive them"))
            {
                ui.Config.LiveTvDead.Clear();
                ui.SaveConfig();
            }
        }

        Ui.Section("Market watch");
        Ui.Hint("What the market channel lists. Prices come from Universalis for the world you are on.");

        var list = ui.Config.MarketWatch;
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            using var id = ImRaii.PushId(i);

            if (ImGui.SmallButton("×"))
            {
                list.RemoveAt(i);
                ui.SaveConfig();
                this.WatchListChanged?.Invoke();
                i--;
                continue;
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(item.Name);
        }

        if (list.Count == 0)
            ImGui.TextColored(Theme.TextFaint, "nothing watched yet");

        ImGui.SetNextItemWidth(260);
        var entered = ImGui.InputTextWithHint("##marketitem", "item name, e.g. Fire Crystal", ref this.itemInput, 64, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Add") || entered) && this.itemInput.Trim().Length > 0)
        {
            var found = this.FindItem?.Invoke(this.itemInput.Trim());
            if (found is { } item)
            {
                if (list.Any(w => w.Id == item.Id))
                {
                    this.itemStatus = $"{item.Name} is already on the list.";
                }
                else if (list.Count >= 40)
                {
                    this.itemStatus = "Forty is plenty for one channel.";
                }
                else
                {
                    list.Add(new MarketItem { Id = item.Id, Name = item.Name });
                    ui.SaveConfig();
                    this.WatchListChanged?.Invoke();
                    this.itemStatus = $"Added {item.Name}.";
                    this.itemInput = string.Empty;
                }
            }
            else
            {
                this.itemStatus = "No marketable item by that name.";
            }
        }

        if (this.itemStatus.Length > 0)
            ImGui.TextColored(Theme.TextDim, this.itemStatus);

        Ui.Tip("Exact names work best; a name that starts the same way is taken when there is only one.");

        Ui.Section("Venues");
        var sfw = ui.Config.VenuesSfwOnly;
        if (ImGui.Checkbox("Only venues marked safe for work", ref sfw))
        {
            ui.Config.VenuesSfwOnly = sfw;
            ui.SaveConfig();
        }

        Ui.Tip("Listings come from ffxivvenues.com for your region. Venues mark themselves; the site's own word is taken for it. With this off, adult venues are listed in purple.");

        Ui.Section("Maintenance banner");
        var banner = ui.Config.MaintenanceBanner;
        if (ImGui.Checkbox("Show a banner when maintenance is coming", ref banner))
        {
            ui.Config.MaintenanceBanner = banner;
            ui.SaveConfig();
        }

        using (ImRaii.Disabled(!banner))
        {
            var minutes = ui.Config.MaintenanceBannerMinutes;
            ImGui.SetNextItemWidth(220);
            if (ImGui.SliderInt("##bannerlead", ref minutes, 15, 240, "%d min ahead"))
            {
                ui.Config.MaintenanceBannerMinutes = minutes;
                ui.SaveConfig();
            }

            ImGui.SameLine();
            if (ImGui.Button("Test") && this.TestBanner is { } test)
                test();
        }

        Ui.Tip(
            "A band along the bottom of the picture. The game's own push - the purple notice everyone gets " +
            "for an emergency - goes up the moment it lands, with the window in your time and a countdown. " +
            "Scheduled maintenance from the Lodestone goes up this far ahead. Test shows one for twenty seconds.");
    }
}
