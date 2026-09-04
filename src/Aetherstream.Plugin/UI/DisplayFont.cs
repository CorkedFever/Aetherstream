using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// Loads VT323 — the VCR-and-teletext face — at two sizes, and hands them out for the on-screen
/// display, the source strip and headings.
/// <para>
/// Deliberately never used for body text. A face drawn to look like a 1980s terminal has no
/// hinting at small sizes and no lowercase worth the name, which is fine for <c>NO SIGNAL</c> and
/// miserable for a Windows path or an exception. The first long error message would undo the whole
/// effect, so anything that might be long stays in the default face.
/// </para>
/// </summary>
internal sealed class DisplayFont : IDisposable
{
    private const string FileName = "VT323-Regular.ttf";

    private readonly IFontHandle? small;
    private readonly IFontHandle? large;

    public DisplayFont(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        try
        {
            var directory = pluginInterface.AssemblyLocation.Directory?.FullName;
            var path = Path.Combine(directory ?? ".", "Fonts", FileName);

            if (!File.Exists(path))
            {
                log.Warning("{File} not found next to the plugin; using the default font.", FileName);
                return;
            }

            // VT323 is drawn on a 20-pixel grid, so it only looks right at that size or a multiple
            // of it. Anything in between blurs the pixel edges that are its entire character.
            this.small = Load(pluginInterface, path, 20f);
            this.large = Load(pluginInterface, path, 40f);
        }
        catch (Exception ex)
        {
            // A font that fails to load is cosmetic, not a reason to fail the plugin.
            log.Warning(ex, "Could not load the display font; using the default font.");
        }
    }

    public bool Available => this.small is not null;

    /// <summary>Headings, the source strip, OSD captions. A no-op scope when the font is missing.</summary>
    public IDisposable Push() => this.small?.Push() ?? NullScope.Instance;

    /// <summary>The big words on the screen itself — NO SIGNAL, a party code.</summary>
    public IDisposable PushLarge() => this.large?.Push() ?? NullScope.Instance;

    public void Dispose()
    {
        this.small?.Dispose();
        this.large?.Dispose();
    }

    private static IFontHandle Load(IDalamudPluginInterface pluginInterface, string path, float sizePx) =>
        pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
            e => e.OnPreBuild(tk => tk.AddFontFromFile(path, new SafeFontConfig { SizePx = sizePx })));

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
