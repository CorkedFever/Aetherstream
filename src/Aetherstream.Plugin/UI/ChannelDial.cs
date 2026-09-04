using Aetherstream.Playback;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// Channel numbers, channel up/down, last channel, and which channels are known to be dead.
/// <para>
/// A pinned channel's number is its position in the pin list — the order you pinned them in — so
/// numbers never shuffle when the playlist is re-downloaded and never depend on a list that is
/// different for everyone. Unpinning renumbers whatever came after, exactly as it would on a real
/// set when a preset is cleared.
/// </para>
/// </summary>
internal sealed class ChannelDial(UiContext ui)
{
    /// <summary>How long a channel stays marked offline after it dies.</summary>
    private static readonly TimeSpan OfflineFor = TimeSpan.FromMinutes(30);

    private Dictionary<string, M3uPlaylist.Channel> byUrl = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> offline = new(StringComparer.OrdinalIgnoreCase);
    private string current = string.Empty;
    private string previous = string.Empty;

    public bool HasChannels => this.byUrl.Count > 0;

    public void SetChannels(List<M3uPlaylist.Channel> channels)
    {
        var map = new Dictionary<string, M3uPlaylist.Channel>(channels.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var channel in channels)
            map.TryAdd(channel.Url, channel);

        this.byUrl = map;
    }

    /// <summary>
    /// Keeps the last-channel memory current. Called once a frame: the source can change from
    /// anywhere — a tile, a party code, the command line — and this only cares that it changed.
    /// </summary>
    public void Track()
    {
        var source = ui.Config.Source;

        // A channel that is visibly playing again has come back, whatever it did earlier.
        if (this.offline.Count > 0 && ui.Session.Uploader is { HasFrame: true } && ui.Session.StalledAtMs <= 0)
            this.offline.Remove(source);

        if (string.Equals(source, this.current, StringComparison.OrdinalIgnoreCase))
            return;

        // Only channels count. Swapping "back" to a Plex film from a channel is not what anyone
        // means by last channel.
        if (this.current.Length > 0 && this.byUrl.ContainsKey(this.current))
            this.previous = this.current;

        this.current = source;
    }

    public M3uPlaylist.Channel? Find(string url) =>
        this.byUrl.TryGetValue(url, out var channel) ? channel : null;

    /// <summary>1-based, or 0 when the URL is not pinned.</summary>
    public int NumberOf(string url)
    {
        var pins = ui.Config.LiveTvFavourites;
        for (var i = 0; i < pins.Count; i++)
        {
            if (string.Equals(pins[i], url, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return 0;
    }

    public M3uPlaylist.Channel? ByNumber(int number)
    {
        var pins = ui.Config.LiveTvFavourites;
        return number >= 1 && number <= pins.Count ? this.Find(pins[number - 1]) : null;
    }

    /// <summary>Pinned channels that are actually in the loaded list, in number order.</summary>
    public IEnumerable<(int Number, M3uPlaylist.Channel Channel)> Pinned()
    {
        var pins = ui.Config.LiveTvFavourites;
        for (var i = 0; i < pins.Count; i++)
        {
            if (this.Find(pins[i]) is { } channel)
                yield return (i + 1, channel);
        }
    }

    public bool CanStep => this.HasChannels && ui.Config.LiveTvFavourites.Count > 0;

    /// <summary>Channel up or down through the pins, wrapping at either end.</summary>
    public void Step(int direction)
    {
        var pinned = this.Pinned().ToList();
        if (pinned.Count == 0)
            return;

        var at = pinned.FindIndex(p => string.Equals(p.Channel.Url, ui.Config.Source, StringComparison.OrdinalIgnoreCase));

        // Not on a pinned channel: up goes to the first, down to the last.
        var next = at < 0
            ? (direction > 0 ? 0 : pinned.Count - 1)
            : ((at + direction) % pinned.Count + pinned.Count) % pinned.Count;

        this.Play(pinned[next].Channel);
    }

    public bool HasLast => this.previous.Length > 0 && this.byUrl.ContainsKey(this.previous);

    public void Last()
    {
        if (this.Find(this.previous) is { } channel)
            this.Play(channel);
    }

    public void Play(M3uPlaylist.Channel channel)
    {
        ui.Config.Source = channel.Url;
        ui.Config.Remember(channel.Url, channel.Name);
        ui.SaveConfig();

        // Straight to the decoder with the channel's own headers, not back through the resolver as
        // a bare string — hundreds of channels in the public list refuse to serve without them.
        ui.PlayResolved(channel.ToStream());
    }

    public bool IsPinned(string url) => this.NumberOf(url) > 0;

    public void TogglePin(string url)
    {
        var pins = ui.Config.LiveTvFavourites;
        var at = pins.FindIndex(p => string.Equals(p, url, StringComparison.OrdinalIgnoreCase));

        if (at >= 0)
            pins.RemoveAt(at);
        else
            pins.Add(url);

        ui.SaveConfig();
    }

    // -- offline -----------------------------------------------------------------------------------

    /// <summary>
    /// Notes that a channel died. Kept in memory only and forgotten after a while: a public host
    /// having a bad hour should not be buried for good, but nobody should have to click it four
    /// times in a row to learn it is down right now.
    /// </summary>
    public void MarkOffline(string url)
    {
        if (url.Length > 0)
            this.offline[url] = DateTime.UtcNow + OfflineFor;
    }

    public void MarkOnline(string url) => this.offline.Remove(url);

    public bool IsOffline(string url) =>
        this.offline.TryGetValue(url, out var until) && until > DateTime.UtcNow;
}
