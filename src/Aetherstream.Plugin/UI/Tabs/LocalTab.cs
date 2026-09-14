using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherstream.Plugin.UI.Tabs;

/// <summary>A video file on this machine, as a tile: its name, where it is, and a poster if one sits beside it.</summary>
internal sealed record LocalVideo(string Path, string Name, string Folder, long Bytes, DateTime Modified, string Poster);

/// <summary>
/// Films and videos on this machine. The Videos folder by default, and any folders added here;
/// scanned for the usual formats, shown as tiles, with a poster when an image of the same name
/// or a folder poster sits next to the file. Click to play.
/// </summary>
internal sealed class LocalTab(UiContext ui)
{
    private List<LocalVideo> videos = [];
    private string status = string.Empty;
    private string filter = string.Empty;
    private bool autoScanned;
    private bool scanning;

    /// <summary>Set by the plugin: the scan walks folders and cannot run in Draw.</summary>
    internal Action<IReadOnlyList<string>>? Scan;

    public void SetVideos(List<LocalVideo> value, string message)
    {
        this.videos = value;
        this.status = message;
        this.scanning = false;
    }

    private List<string> Folders
    {
        get
        {
            var list = ui.Config.LocalVideoFolders;
            if (list.Count == 0)
            {
                var videosFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                if (videosFolder.Length > 0)
                    list.Add(videosFolder);
            }

            return list;
        }
    }

    public void Draw()
    {
        if (!this.autoScanned)
        {
            this.autoScanned = true;
            this.Rescan();
        }

        Ui.Hint("Videos on this machine. Your Videos folder to start with; add any folder. A picture with the same name as a file, or a poster.jpg in its folder, becomes its tile.");
        this.DrawFolders();

        ImGui.Spacing();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##localfilter", "Filter by name", ref this.filter, 80);

        if (this.status.Length > 0)
            ImGui.TextColored(Ui.Faint, this.status);

        this.DrawGrid();
    }

    private void DrawFolders()
    {
        var folders = this.Folders;
        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var first = true;
        string? remove = null;

        foreach (var folder in folders)
        {
            var label = Path.GetFileName(folder.TrimEnd('\\', '/'));
            if (label.Length == 0)
                label = folder;
            var width = ImGui.CalcTextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2);
            if (!first && ImGui.GetItemRectMax().X + spacing + width < rightEdge)
                ImGui.SameLine();
            first = false;

            using var colours = ImRaii.PushColor(ImGuiCol.Button, Theme.Glass).Push(ImGuiCol.Border, Theme.GlassEdge);
            using var border = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1f);
            ImGui.Button($"{label}##folder{folder}");
            Ui.Tip($"{folder}\nRight-click to remove this folder from the list.");
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                remove = folder;
        }

        if (!first)
            ImGui.SameLine();
        if (Ui.IconButton(FontAwesomeIcon.FolderPlus, "Add a folder", "##addfolder"))
        {
            ui.FileDialogs.OpenFolderDialog(
                "Which folder has the videos?",
                (accepted, path) =>
                {
                    if (accepted && path.Length > 0 && !ui.Config.LocalVideoFolders.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        var list = this.Folders;
                        list.Add(path);
                        ui.Config.LocalVideoFolders = list;
                        ui.SaveConfig();
                        this.Rescan();
                    }
                },
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                true);
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(this.scanning))
        {
            if (Ui.IconButton(FontAwesomeIcon.Sync, "Look again", "##rescan"))
                this.Rescan();
        }

        if (remove is not null)
        {
            var list = this.Folders;
            list.RemoveAll(f => string.Equals(f, remove, StringComparison.OrdinalIgnoreCase));
            ui.Config.LocalVideoFolders = list;
            ui.SaveConfig();
            this.Rescan();
        }
    }

    private void Rescan()
    {
        this.scanning = true;
        this.status = "Looking…";
        this.Scan?.Invoke(this.Folders.ToList());
    }

    private void DrawGrid()
    {
        var shown = this.videos;
        if (this.filter.Length > 0)
            shown = shown.Where(v => v.Name.Contains(this.filter, StringComparison.OrdinalIgnoreCase)).ToList();
        if (shown.Count == 0)
            return;

        using var child = ImRaii.Child("##localgrid", new Vector2(-1, -1), false);
        if (!child)
            return;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (PosterCard.WidthOf(false) + spacing)));
        var column = 0;

        foreach (var video in shown)
        {
            if (column > 0 && column % perRow != 0)
                ImGui.SameLine();

            var poster = video.Poster;
            var subtitle = video.Bytes >= 1L << 30 ? $"{video.Bytes / (double)(1L << 30):0.0} GB" : $"{video.Bytes / (double)(1L << 20):0} MB";
            if (PosterCard.Draw(ui, $"##local{video.Path}", () => poster.Length > 0 ? ui.Art.GetFile(poster) : null, video.Name, subtitle, container: false, progress: this.Progress(video)))
                ui.PlayAndRemember(video.Path, video.Name, poster);

            Ui.Tip($"{video.Path}\n{video.Modified:d MMM yyyy}");
            column++;
        }
    }

    private float Progress(LocalVideo video) =>
        ui.Config.ResumePositions.TryGetValue(video.Path, out var ms) && ms > 0 ? 0.5f : 0f;
}
