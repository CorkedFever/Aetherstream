using System.Numerics;
using System.Text;

using Aetherstream.Playback;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>Adult Swim: the marathon streams as wide tiles, what each is showing under its name, the next few in the tooltip. Nothing to install.</summary>
internal sealed class AdultSwimTab(UiContext ui)
{
    private List<AdultSwim.Stream> streams = [];
    private Dictionary<string, List<AdultSwim.Showing>> schedules = [];
    private string status = string.Empty;
    private DateTime askedUtc;

    internal Action? Browse;

    /// <summary>For the home tile: how many streams, once that is known.</summary>
    public string Summary => this.streams.Count > 0 ? $"{this.streams.Count} marathon streams" : "marathon streams";

    public void SetStreams(List<AdultSwim.Stream> value, Dictionary<string, List<AdultSwim.Showing>> showings, string message)
    {
        // Replaced, never edited: Draw may be walking the old ones on the render thread.
        this.schedules = showings;
        this.streams = value;
        this.status = message;
    }

    public void SetStatus(string value) => this.status = value;

    public void Draw()
    {
        // Asked on first sight, and again every quarter hour so "now" stays true.
        if (DateTime.UtcNow - this.askedUtc > TimeSpan.FromMinutes(15))
        {
            this.askedUtc = DateTime.UtcNow;
            if (this.streams.Count == 0)
                this.status = "Asking Adult Swim…";
            this.Browse?.Invoke();
        }

        Ui.Hint("Adult Swim's marathon streams: a show on a loop, each, free with the site's own breaks. What's on now is under each name.");

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void DrawGrid()
    {
        var shown = this.streams;
        var showings = this.schedules;
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##asgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(true) + spacing)));
        var now = DateTime.UtcNow;

        for (var i = 0; i < shown.Count; i++)
        {
            var stream = shown[i];
            if (i > 0 && i % perRow != 0)
                ImGui.SameLine();

            var schedule = showings.TryGetValue(stream.Id, out var list) ? list : [];
            var onNow = schedule.FirstOrDefault(s => s.StartUtc <= now && now < s.EndUtc);
            var subtitle = onNow.Name.Length > 0 ? "Now  ·  " + onNow.Name : "a loop of the show";
            var image = stream.Image;
            if (PosterCard.Draw(ui, $"##as{stream.Id}", () => image.Length > 0 ? ui.Art.GetUrl(image) : null, stream.Title, subtitle, container: false, wide: true))
                ui.PlayAndRemember(stream.Url, "Adult Swim · " + stream.Title, image);

            var tip = new StringBuilder(stream.Description);
            foreach (var next in schedule.Where(s => s.StartUtc > now).Take(3))
            {
                if (tip.Length > 0)
                    tip.Append('\n');
                tip.Append(next.StartUtc.ToLocalTime().ToString("t")).Append("  ").Append(next.Name);
            }

            if (tip.Length > 0)
                Ui.Tip(tip.ToString());
        }
    }
}
