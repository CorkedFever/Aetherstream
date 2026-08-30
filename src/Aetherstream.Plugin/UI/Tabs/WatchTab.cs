using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Where something gets started: one box to paste into, and everything played before.
/// </summary>
internal sealed class WatchTab(UiContext ui)
{
    private string input = string.Empty;
    private bool initialised;

    public void Draw()
    {
        if (!this.initialised)
        {
            this.input = ui.Config.Source;
            this.initialised = true;
        }

        Ui.Section("Play something");

        // Enter plays, which is how anyone treats a single text box with a button beside it.
        ImGui.SetNextItemWidth(-(ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X));
        var submitted = ImGui.InputTextWithHint(
            "##source",
            "party code, twitch channel, youtube link, or any stream URL",
            ref this.input,
            1024,
            ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();
        var pressed = Ui.IconButton(
            FontAwesomeIcon.Play,
            "Play it",
            "##playsource",
            this.input.Trim().Length > 0);

        if ((submitted || pressed) && this.input.Trim().Length > 0)
        {
            var source = this.input.Trim();
            ui.PlayAndRemember(source, Ui.Pretty(source));
        }

        Ui.Hint(
            "Sent a party code? Paste it here and press Play — that is the whole of it.\n\n" +
            "Also takes a Twitch channel name on its own, a YouTube link, a direct .m3u8 or .mp4, " +
            "or \"plex: some film\" to search your library. The Library tab is easier for Plex.");

        this.DrawOfferedScreen();
        this.DrawRecents();
    }

    /// <summary>
    /// A party code can carry the host's screen. It is offered rather than applied, because taking
    /// it replaces whatever surface this install was already painting.
    /// </summary>
    private void DrawOfferedScreen()
    {
        if (ui.OfferedScreen is not { } screen)
            return;

        ImGui.Spacing();
        using var frame = ImRaii.Child("##screenoffer", new Vector2(-1, ImGui.GetTextLineHeightWithSpacing() * 4.4f), true);
        if (!frame)
            return;

        ImGui.TextColored(Ui.Accent, "This party comes with a screen");
        ImGui.TextWrapped(
            $"The host is showing it on {screen.DisplayName}. Stand by your own one and take their " +
            "setup, and the picture lands on it.");

        if (ImGui.Button("Use that screen"))
            ui.AcceptOfferedScreen();

        Ui.Tip(
            "Sets the surface, mask, brightness and fit from the host. It does not move your screen " +
            "— where the furnishing stands stays yours.");

        ImGui.SameLine();
        if (ImGui.Button("No thanks"))
            ui.DeclineOfferedScreen();
    }

    private void DrawRecents()
    {
        if (ui.Config.Recents.Count == 0)
            return;

        Ui.Section("Watched before");

        using var child = ImRaii.Child("##recents", new Vector2(-1, -1), false);
        if (!child)
            return;

        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.Width + ImGui.GetStyle().ItemSpacing.X)));
        var column = 0;

        // Copied before iterating: replaying an entry reorders the list, and mutating it mid-loop
        // would throw.
        foreach (var recent in ui.Config.Recents.ToList())
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var when = DateTimeOffset.FromUnixTimeSeconds(recent.PlayedAtUnix);
            if (PosterCard.Draw(
                ui,
                $"##recent{recent.Source}",
                recent.Thumb,
                recent.Label,
                Ago(when),
                container: false))
            {
                this.input = recent.Source;
                ui.PlayAndRemember(recent.Source, recent.Label, recent.Thumb);
            }

            // Right-click clears one entry. A history you cannot prune stops being useful the first
            // time something embarrassing lands in it.
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ui.Config.Recents.RemoveAll(r => r.Source == recent.Source);
                ui.SaveConfig();
            }

            column++;
        }
    }

    private static string Ago(DateTimeOffset when)
    {
        var span = DateTimeOffset.UtcNow - when;

        return span.TotalMinutes < 2 ? "just now"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min ago"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours} h ago"
            : span.TotalDays < 30 ? $"{(int)span.TotalDays} d ago"
            : when.ToLocalTime().ToString("d MMM");
    }
}
