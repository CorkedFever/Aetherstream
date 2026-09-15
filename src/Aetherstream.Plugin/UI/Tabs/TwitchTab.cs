using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Twitch, browsed: who you follow that is live, the most watched, a game, or a search, as wide
/// tiles with the stream's preview. Pick one and it plays through the Twitch resolver as ever.
/// </summary>
internal sealed class TwitchTab(UiContext ui)
{
    private List<TwitchBrowser.Stream> streams = [];
    private List<TwitchBrowser.Game> games = [];
    private string status = string.Empty;
    private string query = string.Empty;

    private bool pendingSearch;

    /// <summary>A search handed in from the home screen's Search: run as soon as the app is drawn.</summary>
    public void SearchFor(string text)
    {
        this.query = text;
        this.pendingSearch = true;
    }
    private string mode = "top";
    private string game = string.Empty;
    private bool autoBrowsed;

    /// <summary>Set by the plugin: a listing, in the background: "followed", "top", "game:<name>", or "search:<query>".</summary>
    internal Action<string>? Browse;

    public void SetStreams(string forMode, List<TwitchBrowser.Stream> value, List<TwitchBrowser.Game> foundGames, string message)
    {
        if (forMode != this.Key)
            return;
        this.streams = value;
        this.games = foundGames;
        this.status = message;
    }

    private string Key => this.mode switch { "game" => "game:" + this.game, "search" => "search:" + this.query.Trim(), _ => this.mode };

    public void Draw()
    {
        if (!this.autoBrowsed)
        {
            this.autoBrowsed = true;
            this.Go("top");
        }

        Ui.Hint("Who is live on Twitch. Your follows need a signed-in browser's cookies, set under Setup, Sources, the same as YouTube.");

        var hasCookies = ui.Config.YtDlpCookiesBrowser.Length > 0 || ui.Config.YtDlpCookiesFile.Length > 0;
        this.Chip("Followed", "followed", enabled: hasCookies, tip: hasCookies ? null : "Needs a signed-in browser's cookies: Setup, Sources.");
        ImGui.SameLine();
        this.Chip("Top", "top");
        ImGui.SameLine();
        this.Chip("FFXIV", "game", "FINAL FANTASY XIV Online");
        ImGui.SameLine();
        this.Chip("Just Chatting", "game", "Just Chatting");
        ImGui.SameLine();
        this.Chip("DJs", "game", "DJs");
        ImGui.SameLine();
        this.Chip("Music", "game", "Music");

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##twquery", "Search channels and games", ref this.query, 80, ImGuiInputTextFlags.EnterReturnsTrue);
        if (this.pendingSearch)
        {
            this.pendingSearch = false;
            go = true;
        }
        ImGui.SameLine();
        if ((ImGui.Button("Search##tw") || go) && this.query.Trim().Length > 0)
            this.Go("search");

        if (this.games.Count > 0)
        {
            ImGui.TextColored(Theme.TextDim, "Games");
            foreach (var g in this.games.Take(6))
            {
                using (ImRaii.PushColor(ImGuiCol.Button, Theme.Glass).Push(ImGuiCol.Border, Theme.GlassEdge))
                using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f))
                {
                    if (ImGui.Button($"{g.DisplayName}##g{g.Name}"))
                        this.Go("game", g.Name);
                }

                Ui.Tip($"{g.Viewers:N0} watching");
                ImGui.SameLine();
            }

            ImGui.NewLine();
        }

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void Chip(string label, string newMode, string? newGame = null, bool enabled = true, string? tip = null)
    {
        var selected = this.mode == newMode && (newGame is null || this.game == newGame);
        using var colours = ImRaii.PushColor(ImGuiCol.Button, selected ? Theme.GlassLit : Theme.Glass)
            .Push(ImGuiCol.Border, selected ? Theme.Accent : Theme.GlassEdge)
            .Push(ImGuiCol.Text, selected ? Theme.Accent : Theme.Text);
        using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
        using var disabled = ImRaii.Disabled(!enabled);
        if (ImGui.Button($"{label}##tw{label}"))
            this.Go(newMode, newGame);
        if (tip is not null)
            Ui.Tip(tip);
    }

    private void Go(string newMode, string? newGame = null)
    {
        this.mode = newMode;
        if (newGame is not null)
            this.game = newGame;
        this.streams = [];
        this.games = [];
        this.status = "Asking Twitch…";
        this.Browse?.Invoke(this.Key);
    }

    private void DrawGrid()
    {
        var shown = this.streams;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##twgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        // Previews change every few minutes; a bucket on the URL keeps the cache honest.
        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 300;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;

        foreach (var s in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var live = s.Viewers >= 0;
            var subtitle = live ? string.Join("  ·  ", new[] { $"{s.Viewers:N0} watching", s.Game }.Where(x => x.Length > 0)) : "offline";
            var art = s.Preview.Length > 0 ? $"{s.Preview}{(s.Preview.Contains('?') ? '&' : '?')}t={bucket}" : s.Avatar;
            if (PosterCard.Draw(ui, $"##tw{s.Login}", () => art.Length > 0 ? ui.Art.GetUrl(art) : null, s.DisplayName, subtitle, container: false, wide: true))
                ui.PlayAndRemember($"https://www.twitch.tv/{s.Login}", $"{s.DisplayName} on Twitch", s.Avatar);

            if (s.Title.Length > 0)
                Ui.Tip(s.Title);
            column++;
        }
    }
}
