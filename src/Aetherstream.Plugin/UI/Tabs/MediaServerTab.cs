using System.Numerics;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>
/// Your Jellyfin or Emby server: a sign-in when there is none, then its libraries to pick from,
/// what you were in the middle of, a search, posters, and a series opened into its episodes with
/// the rest queued after the one you press.
/// </summary>
internal sealed class MediaServerTab(UiContext ui)
{
    private List<MediaServer.View> views = [];
    private List<MediaServer.Item> items = [];
    private List<MediaServer.Item> episodes = [];
    private MediaServer.Item? series;
    private string status = string.Empty;
    private string query = string.Empty;
    private string listing = string.Empty;
    private string address = string.Empty;
    private string user = string.Empty;
    private string password = string.Empty;
    private bool signingIn;
    private bool autoBrowsed;

    internal Action<string, string, string>? SignIn;

    internal Action? Browse;

    /// <summary>Opens a listing: a view id, "continue", or a search.</summary>
    internal Action<string, string>? Open;

    internal Action<MediaServer.Item>? OpenSeries;

    /// <summary>The stream and art addresses for an item, from the signed-in client.</summary>
    internal Func<MediaServer.Item, (string Stream, string Image)>? Addresses;

    public void SetViews(List<MediaServer.View> value) => this.views = value;

    public void SetItems(string forListing, List<MediaServer.Item> value, string message)
    {
        if (forListing != this.listing)
            return;
        this.items = value;
        this.status = message;
    }

    public void SetEpisodes(MediaServer.Item forSeries, List<MediaServer.Item> value, string message)
    {
        if (this.series is not { } open || open.Id != forSeries.Id)
            return;
        this.episodes = value;
        this.status = message;
    }

    public void SetStatus(string value)
    {
        this.status = value;
        this.signingIn = false;
    }

    /// <summary>After a sign-in, or a sign-out, the shelf starts over.</summary>
    public void Reset()
    {
        this.views = [];
        this.items = [];
        this.episodes = [];
        this.series = null;
        this.listing = string.Empty;
        this.status = string.Empty;
        this.password = string.Empty;
        this.signingIn = false;
        this.autoBrowsed = false;
    }

    public void Draw()
    {
        if (ui.Config.MediaServerToken.Length == 0)
        {
            this.DrawSignIn();
            return;
        }

        if (!this.autoBrowsed)
        {
            this.autoBrowsed = true;
            this.Browse?.Invoke();
            this.Go("continue", string.Empty);
        }

        if (this.series is { } open)
        {
            this.DrawSeries(open);
            return;
        }

        Ui.Hint($"{ui.Config.MediaServerName} ({ui.Config.MediaServerKind}), signed in as {ui.Config.MediaServerUserName}. Sign out under Setup, Sources.");

        var labels = new List<string> { "Continue watching" };
        labels.AddRange(this.views.Select(v => v.Name));
        var selected = this.listing.StartsWith("search:", StringComparison.Ordinal) ? -1
            : this.listing == "continue" ? 0
            : this.views.FindIndex(v => v.Id == this.listing) + 1;
        var picked = Ui.Chips("ms", labels, selected);
        if (picked >= 0 && picked != selected)
        {
            this.query = string.Empty;
            this.Go(picked == 0 ? "continue" : this.views[picked - 1].Id, string.Empty);
        }

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 90f);
        var go = ImGui.InputTextWithHint("##msquery", "Search films, series and episodes", ref this.query, 80, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGui.Button("Search##ms") || go) && this.query.Trim().Length > 0)
            this.Go(string.Empty, this.query.Trim());

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid(this.items, "##msgrid");
    }

    private void DrawSignIn()
    {
        Ui.Hint("Your own Jellyfin or Emby server. The password goes to your server once, for a token; it is not kept.");

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##msaddress", "http://192.168.1.20:8096", ref this.address, 256);
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##msuser", "Username", ref this.user, 64);
        ImGui.SetNextItemWidth(-1);
        var go = ImGui.InputTextWithHint("##mspassword", "Password", ref this.password, 128, ImGuiInputTextFlags.Password | ImGuiInputTextFlags.EnterReturnsTrue);

        using (ImRaii.Disabled(this.signingIn || this.address.Trim().Length == 0 || this.user.Trim().Length == 0))
        {
            if ((ImGui.Button("Sign in##ms") || go) && !this.signingIn && this.address.Trim().Length > 0 && this.user.Trim().Length > 0)
            {
                this.signingIn = true;
                this.status = "Asking the server…";
                this.SignIn?.Invoke(this.address.Trim(), this.user.Trim(), this.password);
                this.password = string.Empty;
            }
        }

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);
    }

    private void Go(string viewOrContinue, string search)
    {
        this.listing = search.Length > 0 ? "search:" + search : viewOrContinue;
        this.items = [];
        this.status = search.Length > 0 ? $"Searching for \"{search}\"…" : "Asking the server…";
        this.Open?.Invoke(viewOrContinue, search);
    }

    private void DrawSeries(MediaServer.Item open)
    {
        if (ImGui.Button("< Back##ms"))
        {
            this.series = null;
            this.episodes = [];
            this.status = string.Empty;
            return;
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(open.Name);
        if (open.Overview.Length > 0)
            Ui.Hint(Ui.Ellipsis(open.Overview, 300));

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid(this.episodes, "##msepisodes");
    }

    private void DrawGrid(List<MediaServer.Item> shown, string id)
    {
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child(id, new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var wideRow = shown[0].IsEpisode;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(wideRow) + spacing)));
        var column = 0;

        for (var i = 0; i < shown.Count; i++)
        {
            var item = shown[i];
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var (stream, image) = this.Addresses?.Invoke(item) ?? (string.Empty, string.Empty);
            var subtitle = item.IsEpisode ? $"S{item.Season}E{item.Episode}" + (item.Seconds > 0 ? $"  ·  {(int)Math.Round(item.Seconds / 60)} min" : string.Empty)
                : item.IsSeries ? "series"
                : string.Join("  ·  ", new[] { item.Year > 0 ? item.Year.ToString() : string.Empty, item.Seconds > 0 ? $"{(int)Math.Round(item.Seconds / 60)} min" : string.Empty }.Where(x => x.Length > 0));
            var progress = item.Seconds > 0 && item.ResumeSeconds > 0 ? (float)Math.Clamp(item.ResumeSeconds / item.Seconds, 0, 1) : 0f;

            if (PosterCard.Draw(ui, $"{id}{item.Id}", () => image.Length > 0 ? ui.Art.GetUrl(image) : null, item.IsEpisode ? item.Name : item.Name, subtitle, container: item.IsSeries, wide: item.IsEpisode, progress: progress))
            {
                if (item.IsSeries)
                {
                    this.series = item;
                    this.episodes = [];
                    this.status = "Listing the episodes…";
                    this.OpenSeries?.Invoke(item);
                }
                else if (stream.Length > 0)
                {
                    ui.PlayAndRemember(stream, item.Label, image);

                    // The rest of the list follows: the next episodes of a series, or whatever is
                    // after it on the shelf.
                    for (var j = i + 1; j < shown.Count; j++)
                    {
                        var later = shown[j];
                        var (laterStream, laterImage) = this.Addresses?.Invoke(later) ?? (string.Empty, string.Empty);
                        if (!later.IsSeries && laterStream.Length > 0)
                            ui.NextUp.Add((laterStream, later.Label, laterImage, (long)(later.Seconds * 1000)));
                    }
                }
            }

            if (item.Overview.Length > 0)
                Ui.Tip(Ui.Ellipsis(item.Overview, 300));
            column++;
        }
    }
}
