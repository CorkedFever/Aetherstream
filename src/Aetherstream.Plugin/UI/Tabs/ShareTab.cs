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

    public void SetStatus(string value) => this.status = value;

    private bool Connected => ui.Config.PartyApiHost.Length > 0;

    public void Draw()
    {
        var session = this.Session?.Invoke();
        var running = session?.IsRunning ?? false;

        if (!this.Connected)
        {
            this.DrawConnect();
            return;
        }

        this.DrawGroups(running);

        ImGui.Spacing();
        this.DrawBroadcast(session, running);

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Connection"))
            this.DrawConnect();

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);
    }

    // -- groups ----------------------------------------------------------------------------------

    private void DrawGroups(bool running)
    {
        Ui.Section("Parties");

        ImGui.SetNextItemWidth(-160);
        var joined = ImGui.InputTextWithHint(
            "##join", "join with a code, e.g. 0ZY-6HH", ref this.joinBuffer, 32,
            ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();
        using (ImRaii.Disabled(this.joinBuffer.Trim().Length == 0))
        {
            if (ImGui.Button("Join") || (joined && this.joinBuffer.Trim().Length > 0))
            {
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
        using (ImRaii.Disabled(this.newPartyName.Trim().Length == 0))
        {
            if (ImGui.Button("Create") || (named && this.newPartyName.Trim().Length > 0))
            {
                this.CreateParty?.Invoke(this.newPartyName.Trim());
                this.newPartyName = string.Empty;
            }
        }

        Ui.Tip("You own what you make, and only you can broadcast to it.");

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

    private void DrawConnect()
    {
        Ui.Section("Party server");

        Ui.Hint(
            "The address of the Aetherstream party server. Your identity is generated here and " +
            "never typed — there is no account and no password.");

        ImGui.SetNextItemWidth(-110);
        var submitted = ImGui.InputTextWithHint(
            "##apihost",
            this.Connected ? ui.Config.PartyApiHost : "party.example.com",
            ref this.hostBuffer,
            200,
            ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();
        if (ImGui.Button("Connect", new Vector2(-1, 0)) || submitted)
        {
            this.SignInAsHost?.Invoke(string.Empty, this.hostBuffer.Trim());
            this.hostBuffer = string.Empty;
        }

        Ui.Dot(this.Connected ? Ui.Good : Ui.Faint, this.Connected ? "connected" : "not connected");
        ImGui.SameLine();
        ImGui.TextColored(Ui.Faint, this.Connected ? ui.Config.PartyApiHost : "no server yet");

        Ui.Hint("ffmpeg must be on PATH to broadcast. Watching needs nothing.");
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
                ui.Config.PartyInput = current.PlaylistUrl;
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
