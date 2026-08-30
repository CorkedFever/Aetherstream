using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// The Plex library, browsed by looking at it. Typing a title into the source box works, but nobody
/// chooses what to watch by spelling it correctly first time.
/// </summary>
internal sealed class LibraryTab(UiContext ui)
{
    private PlexAccount.Pin? pin;
    private List<PlexAccount.Server> servers = [];
    private List<PlexLibrary.Section> sections = [];
    private List<PlexLibrary.Item> items = [];
    private string status = string.Empty;
    private string section = string.Empty;
    private string sectionTitle = string.Empty;
    private string filter = string.Empty;
    private bool autoBrowsed;

    /// <summary>
    /// How deep into a show we are: empty at the library, one entry inside a show, two inside a
    /// season. Held here rather than as a single label so "back" can go up one level instead of
    /// always returning to the top, which is what made a multi-season show unusable.
    /// </summary>
    private List<PlexLibrary.Item> trail = [];

    /// <summary>Set by the plugin. Every one of these is a network round trip and cannot run in Draw.</summary>
    internal Action? BeginSignIn;

    internal Action<PlexAccount.Pin>? CompleteSignIn;

    internal Action? Browse;

    internal Action<string, string>? OpenSection;

    internal Action<PlexLibrary.Item>? OpenItem;

    /// <summary>Flattens a multi-season show past its seasons, on request rather than by default.</summary>
    internal Action<PlexLibrary.Item>? OpenAllEpisodes;

    public void SetPin(PlexAccount.Pin? value) => this.pin = value;

    public void SetStatus(string value) => this.status = value;

    public void SetServers(List<PlexAccount.Server> value, string message)
    {
        this.servers = value;
        this.status = message;
        this.pin = null;
    }

    public void SetSections(List<PlexLibrary.Section> value, string message)
    {
        this.sections = value;
        this.items = [];
        this.trail = [];
        this.status = message;
    }

    public void SetItems(List<PlexLibrary.Item> value) => this.items = value;

    private bool Configured => ui.Config.PlexServer.Length > 0 && ui.Config.PlexToken.Length > 0;

    public void Draw()
    {
        if (!this.Configured)
        {
            this.DrawSignIn();
            return;
        }

        // Opening the tab on a configured server should show the library, not a button that goes and
        // fetches it. Guarded so it fires once rather than on every frame — this is a round trip to
        // a server that may be on the other side of the world.
        if (!this.autoBrowsed && this.sections.Count == 0)
        {
            this.autoBrowsed = true;
            this.Browse?.Invoke();
        }

        this.DrawSections();

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    /// <summary>
    /// The same flow a TV app uses: a code is entered on plex.tv and the account reports its own
    /// servers, so no address is transcribed and no password is handled here.
    /// </summary>
    private void DrawSignIn()
    {
        Ui.Section("Connect to Plex");

        if (this.pin is { } waiting)
        {
            ImGui.TextUnformatted("Enter this code at");
            ImGui.SameLine();
            ImGui.TextColored(Ui.Accent, "plex.tv/link");

            // The code is the whole point of this screen, so it is given room rather than being set
            // in the same text as the sentence above it.
            using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(18, 10)))
            using (ImRaii.PushColor(ImGuiCol.Button, Ui.AccentDim with { W = 0.22f })
                .Push(ImGuiCol.ButtonHovered, Ui.AccentDim with { W = 0.22f })
                .Push(ImGuiCol.ButtonActive, Ui.AccentDim with { W = 0.22f })
                .Push(ImGuiCol.Text, Ui.Accent))
            {
                if (ImGui.Button(waiting.Code))
                    ImGui.SetClipboardText(waiting.Code);
            }

            Ui.Tip("Click to copy.");

            ImGui.SameLine();
            if (Ui.IconButton(FontAwesomeIcon.Copy, "Copy the code", "##copycode"))
                ImGui.SetClipboardText(waiting.Code);

            ImGui.Spacing();
            if (ImGui.Button("I've entered it"))
                this.CompleteSignIn?.Invoke(waiting);

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                this.pin = null;
        }
        else
        {
            Ui.Hint(
                "Signing in lists your servers and lets you browse the library here. Your token is " +
                "stored locally and only ever sent to your own server.");

            ImGui.Spacing();
            if (ImGui.Button("Sign in with Plex"))
                this.BeginSignIn?.Invoke();
        }

        if (this.status.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextWrapped(this.status);
        }

        if (this.servers.Count > 0)
            this.DrawServerPicker();
    }

    private void DrawServerPicker()
    {
        Ui.Section("Which server");

        foreach (var found in this.servers)
        {
            var selected = ui.Config.PlexServer == found.Uri;
            if (ImGui.RadioButton($"{found.Name}##{found.Uri}", selected))
            {
                ui.Config.PlexServer = found.Uri;
                ui.SaveConfig();
                this.Browse?.Invoke();
            }

            ImGui.SameLine();
            ImGui.TextColored(Ui.Faint, found.IsLocal ? "local" : "remote");
            Ui.Tip(found.Uri);
        }
    }

    /// <summary>Libraries as a row of chips, with the search box beside them.</summary>
    private void DrawSections()
    {
        if (this.sections.Count == 0)
        {
            if (ImGui.Button("Load my libraries"))
                this.Browse?.Invoke();

            ImGui.SameLine();
            if (Ui.IconButton(FontAwesomeIcon.SignOutAlt, "Sign out and connect a different account", "##plexreset"))
            {
                ui.Config.PlexToken = string.Empty;
                this.sections = [];
                this.items = [];
                this.autoBrowsed = false;
                ui.SaveConfig();
            }

            return;
        }

        // Wrapped by hand: a row of chips that runs off the edge is worse than one that folds, and
        // ImGui has no flow layout to do it for us. Both bounds are in screen coordinates, and the
        // decision is made against the *previous* item's right edge — after a button the cursor has
        // already moved to the next line, so it cannot answer this question.
        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var first = true;

        foreach (var found in this.sections)
        {
            var width = ImGui.CalcTextSize(found.Title).X + (ImGui.GetStyle().FramePadding.X * 2);

            if (!first && ImGui.GetItemRectMax().X + spacing + width < rightEdge)
                ImGui.SameLine();

            first = false;

            var active = this.section == found.Key;
            using var colour = ImRaii.PushColor(ImGuiCol.Button, Ui.AccentDim with { W = 0.35f }, active);

            if (ImGui.Button($"{found.Title}##{found.Key}"))
            {
                this.section = found.Key;
                this.sectionTitle = found.Title;
                this.filter = string.Empty;
                this.trail = [];
                this.OpenSection?.Invoke(found.Key, string.Empty);
            }
        }

        this.DrawTrail();
    }

    /// <summary>Where we are, and the way back up one level.</summary>
    private void DrawTrail()
    {
        if (this.trail.Count == 0)
        {
            ImGui.SetNextItemWidth(220);
            if (ImGui.InputTextWithHint(
                "##plexsearch",
                "search titles",
                ref this.filter,
                128,
                ImGuiInputTextFlags.EnterReturnsTrue)
                && this.section.Length > 0)
            {
                this.OpenSection?.Invoke(this.section, this.filter.Trim());
            }

            Ui.Tip("Press Enter to search the selected library.");
            return;
        }

        // Going up replaces the trail, so nothing below may keep reading the old one — draw the rest
        // next frame, against whatever the trail has become.
        if (Ui.IconButton(FontAwesomeIcon.ArrowLeft, "Up one level", "##plexback"))
        {
            this.GoUp();
            return;
        }

        // One snapshot for the whole method. The trail is replaced rather than edited in place, so
        // holding the reference keeps this consistent even if a background listing lands mid-draw.
        var steps = this.trail;
        if (steps.Count == 0)
            return;

        ImGui.SameLine();
        ImGui.TextColored(Ui.Faint, this.sectionTitle);

        foreach (var step in steps)
        {
            ImGui.SameLine(0, 4);
            ImGui.TextColored(Ui.Faint, "›");
            ImGui.SameLine(0, 4);
            ImGui.TextColored(Ui.Accent, Ui.Ellipsis(step.Title, 28));
        }

        // Offered only where it means something: a show with more than one season. Flattening a
        // single-season show is what it already shows.
        if (steps is [{ IsShow: true, ChildCount: > 1 } show])
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"All {show.LeafCount} episodes"))
                this.OpenAllEpisodes?.Invoke(show);
        }
    }

    private void GoUp()
    {
        var steps = this.trail;
        if (steps.Count > 0)
            this.trail = steps.GetRange(0, steps.Count - 1);

        if (this.trail.Count == 0)
            this.OpenSection?.Invoke(this.section, this.filter.Trim());
        else
            this.OpenItem?.Invoke(this.trail[^1]);
    }

    private void DrawGrid()
    {
        // Snapshotted once: a background listing replaces this field, and reading it twice can
        // straddle that swap — which is how counting one list and indexing another goes out of range.
        var shown = this.items;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##plexgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        // Episodes are stills, so the whole grid switches shape when it is showing them.
        var wide = shown[0].IsEpisode;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(wide) + spacing)));
        var column = 0;

        foreach (var item in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var (title, subtitle) = Label(item);

            if (PosterCard.Draw(
                ui,
                $"##item{item.RatingKey}",
                item.Thumb,
                title,
                subtitle,
                item.IsContainer,
                wide))
            {
                if (item.IsContainer)
                {
                    this.trail = [.. this.trail, item];
                    this.OpenItem?.Invoke(item);
                }
                else
                {
                    // The history gets the full name, so an episode does not land in it as
                    // "Episode 1" with nothing to say which show it belongs to.
                    ui.PlayAndRemember(
                        PlexResolver.SourceFor(item.RatingKey),
                        FullName(item),
                        item.Thumb);
                }
            }

            column++;
        }

        if (shown.Count >= PlexLibrary.MaxItems)
        {
            ImGui.Spacing();
            Ui.Hint($"Only the first {PlexLibrary.MaxItems} are shown. Use the search box to narrow it down.");
        }
    }

    /// <summary>What goes on the tile. An episode leads with its number, since that is how it is found.</summary>
    private static (string Title, string Subtitle) Label(PlexLibrary.Item item)
    {
        if (item.IsEpisode)
        {
            var code = item.EpisodeCode;
            return (
                code.Length > 0 ? $"{code}  {item.Title}" : item.Title,
                item.DurationMs > 0 ? Ui.Clock(item.DurationMs) : string.Empty);
        }

        if (item.IsSeason)
            return (item.Title, item.LeafCount > 0 ? $"{item.LeafCount} episodes" : string.Empty);

        if (item.IsShow)
        {
            var seasons = item.ChildCount == 1 ? "1 season" : $"{item.ChildCount} seasons";
            return (item.Title, item.ChildCount > 0 ? seasons : item.Year);
        }

        return (item.Title, item.Year.Length > 0 ? item.Year : Ui.Clock(item.DurationMs));
    }

    /// <summary>The name used for playback and history — never just the episode's own title.</summary>
    private static string FullName(PlexLibrary.Item item)
    {
        if (!item.IsEpisode)
            return item.Title;

        var show = item.ShowTitle.Length > 0 ? item.ShowTitle : string.Empty;
        var code = item.EpisodeCode;

        return (show.Length > 0, code.Length > 0) switch
        {
            (true, true) => $"{show} · {code} · {item.Title}",
            (true, false) => $"{show} · {item.Title}",
            (false, true) => $"{code} · {item.Title}",
            _ => item.Title,
        };
    }
}
