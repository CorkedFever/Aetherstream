using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>Dailymotion: what is trending, its channels as chips, a search, and wide tiles. Free, no account; plays through yt-dlp.</summary>
internal sealed class DailymotionTab(UiContext ui)
{
    private List<Dailymotion.Channel> channels = [];
    private List<Dailymotion.Item> items = [];
    private string status = string.Empty;
    private string query = string.Empty;

    private bool pendingSearch;

    /// <summary>A search handed in from the home screen's Search: run as soon as the app is drawn.</summary>
    public void SearchFor(string text)
    {
        this.query = text;
        this.pendingSearch = true;
    }
    private string listing = string.Empty;
    private bool autoBrowsed;

    internal Action? Browse;

    internal Action<string, string>? Open;

    public void SetChannels(List<Dailymotion.Channel> value) => this.channels = value;

    public void SetItems(string forListing, List<Dailymotion.Item> value, string message)
    {
        if (forListing != this.listing)
            return;
        this.items = value;
        this.status = message;
    }

    public void SetStatus(string value) => this.status = value;

    public void Draw()
    {
        if (ui.LocateYtDlp() is null)
        {
            Ui.Hint("Dailymotion plays through yt-dlp. Setup, Sources says where to get it.");
            return;
        }

        if (!this.autoBrowsed)
        {
            this.autoBrowsed = true;
            this.Browse?.Invoke();
            this.Go(string.Empty, string.Empty);
        }

        Ui.Hint("Dailymotion: what is trending in English, by channel, and a search. Free, no account; plays through yt-dlp.");

        var labels = new List<string> { "Trending" };
        labels.AddRange(this.channels.Select(c => c.Name));
        var selected = this.listing.StartsWith("search:", StringComparison.Ordinal) ? -1
            : this.listing.Length == 0 || this.listing == "channel:" ? 0
            : this.channels.FindIndex(c => "channel:" + c.Id == this.listing) + 1;
        var picked = Ui.Chips("dm", labels, selected);
        if (picked >= 0 && picked != selected)
        {
            this.query = string.Empty;
            this.Go(picked == 0 ? string.Empty : this.channels[picked - 1].Id, string.Empty);
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##dmquery", "Search Dailymotion", ref this.query, 80, ImGuiInputTextFlags.EnterReturnsTrue);
        if (this.pendingSearch)
        {
            this.pendingSearch = false;
            go = true;
        }
        ImGui.SameLine();
        if ((ImGui.Button("Search##dm") || go) && this.query.Trim().Length > 0)
            this.Go(string.Empty, this.query.Trim());

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void Go(string channel, string search)
    {
        this.listing = search.Length > 0 ? "search:" + search : "channel:" + channel;
        this.items = [];
        this.status = search.Length > 0 ? $"Searching Dailymotion for \"{search}\"…" : "Asking Dailymotion…";
        this.Open?.Invoke(channel, search);
    }

    private void DrawGrid()
    {
        var shown = this.items;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##dmgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var column = 0;

        foreach (var item in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var subtitle = string.Join("  ·  ", new[] { item.Owner, item.Seconds > 0 ? $"{(int)Math.Round(item.Seconds / 60)} min" : string.Empty }.Where(x => x.Length > 0));
            var thumb = item.Thumb;
            if (PosterCard.Draw(ui, $"##dm{item.Id}", () => thumb.Length > 0 ? ui.Art.GetUrl(thumb) : null, item.Title, subtitle, container: false, wide: true))
                ui.PlayAndRemember(item.Url, item.Title, thumb);
            column++;
        }
    }
}
