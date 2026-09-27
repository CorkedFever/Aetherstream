using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Party groups. Make one, or join one with a six-character code.
/// <para>
/// The code never changes and carries nothing — the service resolves it, and checks you are a member
/// before telling you anything at all. Send it once and it keeps working every week.
/// </para>
/// </summary>
internal sealed class ShareTab(UiContext ui)
{
    private string hostBuffer = string.Empty;
    private string newPartyName = string.Empty;
    private string joinBuffer = string.Empty;
    private string startAt = string.Empty;

    /// <summary>Set by an empty Join, so the code box takes the keyboard on the next frame.</summary>
    private bool focusJoin;

    /// <summary>Set by the plugin — every one of these is a network call or a process spawn.</summary>
    internal Action<string, string>? StartBroadcast;

    internal Action? StopBroadcast;

    internal Func<BroadcastSession>? Session;

    internal Action<string, string>? SignInAsHost;

    internal Action<string>? CreateParty;

    internal Action<string>? DeleteParty;

    internal Action<string, string>? FollowParty;

    internal Action<string>? LeaveParty;

    internal Action? RefreshParties;

    private List<PartyDirectory.Group> groups = [];
    private string status = string.Empty;

    public void SetParties(List<PartyDirectory.Group> value) => this.groups = value;

    /// <summary>The parties as last heard from the service. For the guide channel.</summary>
    public IReadOnlyList<PartyDirectory.Group> Parties => this.groups;

    public void SetStatus(string value) => this.status = value;

    private bool Connected => ui.Config.PartyApiHost.Length > 0;

    public void Draw()
    {
        var session = this.Session?.Invoke();
        var running = session?.IsRunning ?? false;

        // Join and Create sign in on their own, to the Aetherstream server unless another is set,
        // so the parties come first even before the service has answered.
        this.DrawGroups(running);

        ImGui.Spacing();
        this.DrawBroadcast(session, running);

        ImGui.Spacing();
        if (BroadcastSession.HasFfmpeg)
            Ui.Hint("Watching needs nothing. Hosting uses ffmpeg, which is in place.");
        else
            ImGui.TextColored(Theme.Warn, "Hosting needs ffmpeg: press Get ffmpeg under Setup, Sources. Watching needs nothing.");

        // The party server's address is not offered here any more: everyone uses the Aetherstream
        // one, and the box only confused the tab people were already struggling with. Everything
        // behind it still works, and a config that already points elsewhere keeps it. To offer
        // it again, call DrawServer from here or from Setup.
        if (ShowServerBox)
            this.DrawServer();
    }

    /// <summary>Off: see the note in <see cref="Draw"/>. Kept so a custom server can be offered again.</summary>
    private const bool ShowServerBox = false;

    // -- groups ----------------------------------------------------------------------------------

    private void DrawGroups(bool running)
    {
        Ui.Section("Parties");

        // Both buttons are always live. Greyed out until their box was filled, they read as broken:
        // people clicked them first and nothing happened. An empty Join now says what it wants and
        // puts the cursor there; an empty Create just makes a party with a plain name.
        if (this.focusJoin)
        {
            ImGui.SetKeyboardFocusHere();
            this.focusJoin = false;
        }

        ImGui.SetNextItemWidth(-160);
        var joined = ImGui.InputTextWithHint(
            "##join", "join with a code, e.g. 0ZY-6HH", ref this.joinBuffer, 32,
            ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();
        if (ImGui.Button("Join") || joined)
        {
            if (this.joinBuffer.Trim().Length == 0)
            {
                this.status = "Paste the code someone sent you in the box first.";
                this.focusJoin = true;
            }
            else
            {
                this.status = "Joining…";
                this.FollowParty?.Invoke(this.joinBuffer.Trim(), string.Empty);
                this.joinBuffer = string.Empty;
            }
        }

        Ui.Tip("Paste the six characters someone sent you. You only ever do this once per party.");

        ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.Sync, "Refresh", "##refresh"))
            this.RefreshParties?.Invoke();

        ImGui.SetNextItemWidth(-160);
        var named = ImGui.InputTextWithHint(
            "##newparty", "or make one, e.g. Movie Night", ref this.newPartyName, 60,
            ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();
        if (ImGui.Button("Create") || named)
        {
            var name = this.newPartyName.Trim();
            this.status = "Making the party…";
            this.CreateParty?.Invoke(name.Length > 0 ? name : "Watch party");
            this.newPartyName = string.Empty;
        }

        Ui.Tip("You own what you make, and only you can broadcast to it. Leave the name empty for \"Watch party\".");

        if (this.status.Length > 0)
            ImGui.TextColored(Theme.Accent, this.status);

        if (this.groups.Count == 0)
        {
            Ui.Hint("No parties yet. Make one, or join with a code someone sent you.");
            return;
        }

        ImGui.Spacing();

        foreach (var group in this.groups)
        {
            using var id = ImRaii.PushId(group.Code);

            // Only your own groups can be broadcast to, so only they get a selector.
            if (group.Owner)
            {
                var selected = ui.Config.PartyCodeInUse == group.Code;
                if (ImGui.RadioButton("##use", selected) && !running)
                {
                    ui.Config.PartyCodeInUse = group.Code;
                    ui.SaveConfig();
                }

                Ui.Tip(running ? "Stop broadcasting before switching." : "Broadcast to this one.");
            }
            else
            {
                ImGui.Dummy(new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight()));
            }

            ImGui.SameLine();
            Ui.Dot(group.Live ? Ui.Good : Ui.Faint, group.Live ? "streaming now" : "nobody streaming");

            ImGui.SameLine();
            ImGui.TextColored(
                group.Live ? Ui.Accent : Ui.Faint,
                group.Name.Length > 0 ? group.Name : "(unnamed)");

            // The code in the display face: six characters of Crockford base32 read like a channel
            // number, which is what a party code is to the person typing it in.
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Theme.Displayed(group.Live ? Theme.Accent : Theme.TextDim, PartyDirectory.Pretty(group.Code));

            if (group.Live && group.Title.Length > 0)
            {
                ImGui.SameLine();
                ImGui.TextColored(Ui.Faint, $"— {Ui.Ellipsis(group.Title, 24)}");
            }

            ImGui.SameLine();
            Ui.RightAlign(180);

            using (ImRaii.Disabled(!group.Live))
            {
                if (ImGui.Button("Watch"))
                {
                    // Offered before playing, so the prompt is already there when the picture is.
                    ui.OfferScreen(group.Screen);
                    ui.PlayAndRemember(group.WatchUrl, group.Name.Length > 0 ? group.Name : "Party");
                }
            }

            Ui.Tip(group.Live ? "Play it on your own screen." : "Nobody is streaming to this yet.");

            ImGui.SameLine();
            if (ImGui.Button("Copy code"))
                ImGui.SetClipboardText(PartyDirectory.Pretty(group.Code));

            Ui.Tip("Send this to the room. They paste it into Join and that is the whole of it.");

            ImGui.SameLine();
            if (group.Owner)
            {
                using (ImRaii.Disabled(running && ui.Config.PartyCodeInUse == group.Code))
                {
                    if (Ui.IconButton(FontAwesomeIcon.Trash, "Delete this party for everyone", "##del"))
                        this.DeleteParty?.Invoke(group.Code);
                }
            }
            else if (Ui.IconButton(FontAwesomeIcon.Times, "Leave this party", "##leave"))
            {
                this.LeaveParty?.Invoke(group.Code);
            }
        }
    }

    // -- connecting ------------------------------------------------------------------------------

    /// <summary>The party server's address, for someone running their own. Not shown at present.</summary>
    private void DrawServer()
    {
        Ui.Section("Party server");

        Ui.Hint(
            "Only for running your own party server; everyone else can leave this alone. Join and " +
            "Create sign in by themselves. Your identity is generated here and never typed: there " +
            "is no account and no password.");

        ImGui.SetNextItemWidth(-110);
        var submitted = ImGui.InputTextWithHint(
            "##apihost",
            this.Connected ? ui.Config.PartyApiHost : Configuration.DefaultPartyApiHost,
            ref this.hostBuffer,
            200,
            ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();
        if (ImGui.Button("Connect", new Vector2(-1, 0)) || submitted)
        {
            this.SignInAsHost?.Invoke(string.Empty, this.hostBuffer.Trim());
            this.hostBuffer = string.Empty;
        }

        Ui.Dot(this.Connected ? Ui.Good : Ui.Faint, this.Connected ? "server" : "default server");
        ImGui.SameLine();
        ImGui.TextColored(Ui.Faint, this.Connected ? ui.Config.PartyApiHost : Configuration.DefaultPartyApiHost);
    }

    // -- broadcasting ----------------------------------------------------------------------------

    private void DrawBroadcast(BroadcastSession? session, bool running)
    {
        Ui.Section("Broadcast");

        if (!this.groups.Any(g => g.Owner))
        {
            Ui.Hint("Make a party of your own to broadcast. You can watch anyone else's without one.");
            return;
        }

        if (ui.Config.PartyCodeInUse.Length == 0)
        {
            Ui.Hint("Pick which of your parties to broadcast to, above.");
            return;
        }

        if (running)
        {
            Ui.Dot(Theme.Bad, "broadcasting");
            ImGui.SameLine();
            Theme.Displayed(Theme.Bad, "● ON AIR");
            ImGui.SameLine();
            ImGui.TextColored(Theme.TextDim, session?.IsCopying == true ? "copying" : "re-encoding");

            ImGui.SameLine();
            Ui.RightAlignedText(Ui.Clock((long)(session?.Elapsed.TotalMilliseconds ?? 0)), Theme.TextDim);
        }
        else
        {
            Ui.Dot(Theme.TextFaint, "not broadcasting");
            ImGui.SameLine();
            Theme.Displayed(Theme.TextFaint, "OFF AIR");
        }

        var input = ui.Config.PartyInput;
        using (ImRaii.Disabled(running))
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##partyinput", "path to a file, or a URL", ref input, 1024))
            {
                ui.Config.PartyInput = input;
                ui.SaveConfig();
            }
        }

        using (ImRaii.Disabled(running || ui.Session.Current is null))
        {
            if (ImGui.Button("Use what I'm watching") && ui.Session.Current is { } current)
            {
                // A local file plays as a file URI; ffmpeg wants the path back.
                var playing = current.Origin ?? current.PlaylistUrl;
                if (playing.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    playing = new Uri(playing).LocalPath;
                ui.Config.PartyInput = playing;
                ui.SaveConfig();
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        using (ImRaii.Disabled(running))
            ImGui.InputTextWithHint("##startat", "00:20:00", ref this.startAt, 16);

        Ui.Tip("Optional start point. Viewers cannot scrub a live stream, so this is the only way to skip.");

        ImGui.Spacing();

        if (running)
        {
            using var colour = ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.55f, 0.18f, 0.18f, 1f));
            if (ImGui.Button("Stop broadcasting", new Vector2(180, ImGui.GetFrameHeight() + 6)))
                this.StopBroadcast?.Invoke();
        }
        else
        {
            using var colour = ImRaii.PushColor(ImGuiCol.Button, Ui.AccentDim with { W = 0.35f });
            using (ImRaii.Disabled(ui.Config.PartyInput.Trim().Length == 0))
            {
                if (ImGui.Button("Start broadcasting", new Vector2(180, ImGui.GetFrameHeight() + 6)))
                    this.StartBroadcast?.Invoke(ui.Config.PartyInput.Trim(), this.startAt.Trim());
            }
        }

        if (session?.Error is { } error)
        {
            ImGui.TextColored(Ui.Bad, Ui.Ellipsis(error, 70));
            Ui.Tip(error);
        }
        else if (running && session?.Status is { Length: > 0 } s)
        {
            ImGui.TextColored(Ui.Faint, s);
        }

        ImGui.Spacing();
        var shareScreen = ui.Config.PartyShareScreen;
        if (ImGui.Checkbox("Include my screen setup", ref shareScreen))
        {
            ui.Config.PartyShareScreen = shareScreen;
            ui.SaveConfig();
        }

        Ui.Tip(
            "Sends which furnishing and texture you are painting on, so the room can land the " +
            "picture on the same object in one click. Where your screen stands never travels — " +
            "those are coordinates in your house.");
    }
}
