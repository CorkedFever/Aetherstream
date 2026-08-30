using System.Collections.Concurrent;

using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// Poster art for the library, fetched once and kept.
/// <para>
/// Plex will resize server-side, so the thumbnails arrive at the size they are drawn rather than as
/// full-resolution posters — a few kilobytes each instead of a few hundred.
/// </para>
/// </summary>
internal sealed class PlexArt(ITextureProvider textures, HttpClient http, IPluginLog log) : IDisposable
{
    /// <summary>Requested from the server at twice the drawn size, so it stays sharp at any UI scale.</summary>
    public const int PosterWidth = 240;

    public const int PosterHeight = 360;

    /// <summary>
    /// Above this, the oldest are retired. A poster is roughly 340 KB on the GPU, so this caps the
    /// cache near 80 MB — enough for several screens of a large library.
    /// </summary>
    private const int MaxEntries = 240;

    /// <summary>
    /// Frames to wait before releasing a retired texture. A draw list built this frame is submitted
    /// after Draw returns, so anything released immediately can still be referenced by the GPU.
    /// Same reasoning as the video path's texture retirement, and the same fix.
    /// </summary>
    private const int RetireFrames = 5;

    /// <summary>
    /// How many thumbnails may be in flight at once. A library page is hundreds of tiles, and
    /// without a limit opening one starts that many simultaneous requests on the shared HttpClient
    /// — which times them out en masse and stalls whatever else is using it, including resolving
    /// the stream you are trying to play.
    /// </summary>
    private const int MaxConcurrentFetches = 6;

    /// <summary>
    /// Ticks before a failed fetch may be retried. Without this a failure is permanent for the
    /// session, and the dead entries accumulate against the cache budget forever.
    /// </summary>
    private const int RetryAfterTicks = 900;

    private sealed class Entry
    {
        public IDalamudTextureWrap? Wrap;

        public long FailedAt;

        public long LastUsed;
    }

    private readonly ConcurrentDictionary<string, Entry> cache = new();
    private readonly List<(IDalamudTextureWrap Wrap, int Frames)> retiring = [];
    private readonly SemaphoreSlim fetching = new(MaxConcurrentFetches, MaxConcurrentFetches);
    private long tick = 1;
    private volatile bool disposed;

    /// <summary>Call once per frame, before anything is drawn.</summary>
    public void Update()
    {
        this.tick++;

        for (var i = this.retiring.Count - 1; i >= 0; i--)
        {
            var (wrap, frames) = this.retiring[i];
            if (frames > 0)
            {
                this.retiring[i] = (wrap, frames - 1);
                continue;
            }

            wrap.Dispose();
            this.retiring.RemoveAt(i);
        }

        // Drop failures once they are stale, so a blip against the server is retried rather than
        // remembered for the session — and so they stop occupying the cache.
        foreach (var dead in this.cache
            .Where(kv => kv.Value.Wrap is null
                && kv.Value.FailedAt > 0
                && this.tick - kv.Value.FailedAt > RetryAfterTicks)
            .Select(kv => kv.Key)
            .ToList())
        {
            this.cache.TryRemove(dead, out _);
        }

        // Only textures count against the budget. Counting pending and failed entries too — while
        // choosing eviction candidates from textures alone — meant a few hundred failures could ask
        // for more evictions than there were textures, throwing out every poster on screen and
        // refetching them next frame, forever.
        var loaded = this.cache.Count(kv => kv.Value.Wrap is not null);
        if (loaded <= MaxEntries)
            return;

        // Retire least-recently-drawn first. Browsing is a walk through the library, so the posters
        // that fall out are the ones already scrolled past.
        foreach (var key in this.cache
            .Where(kv => kv.Value.Wrap is not null)
            .OrderBy(kv => kv.Value.LastUsed)
            .Take(loaded - MaxEntries)
            .Select(kv => kv.Key)
            .ToList())
        {
            if (this.cache.TryRemove(key, out var entry) && entry.Wrap is { } wrap)
                this.retiring.Add((wrap, RetireFrames));
        }
    }

    /// <summary>
    /// The texture for a thumbnail path, or null while it is still arriving. Starts the fetch on
    /// first ask, so drawing a poster is all a caller has to do.
    /// </summary>
    public IDalamudTextureWrap? Get(string server, string token, string thumb)
    {
        if (this.disposed || thumb.Length == 0 || server.Length == 0 || token.Length == 0)
            return null;

        if (this.cache.TryGetValue(thumb, out var existing))
        {
            existing.LastUsed = this.tick;
            return existing.Wrap;
        }

        var entry = new Entry { LastUsed = this.tick };
        if (!this.cache.TryAdd(thumb, entry))
            return null;

        _ = Task.Run(() => this.FetchAsync(server, token, thumb, entry));
        return null;
    }

    private async Task FetchAsync(string server, string token, string thumb, Entry entry)
    {
        await this.fetching.WaitAsync();

        try
        {
            // The queue may have been long; if everything went away while waiting, do not start.
            if (this.disposed)
                return;

            // The transcode endpoint takes the thumbnail's own path as a parameter and hands back a
            // JPEG at whatever size is asked for.
            var url = $"{server.TrimEnd('/')}/photo/:/transcode" +
                $"?width={PosterWidth}&height={PosterHeight}&minSize=1&upscale=1" +
                $"&url={Uri.EscapeDataString(thumb)}" +
                $"&X-Plex-Token={Uri.EscapeDataString(token)}";

            var bytes = await http.GetByteArrayAsync(url);
            if (this.disposed)
                return;

            var wrap = await textures.CreateFromImageAsync(bytes, $"aetherstream-poster:{thumb}");

            entry.Wrap = wrap;

            // Publish first, then re-check. Testing before the assignment leaves a window where
            // Dispose walks the cache, sees no texture yet, clears the entry, and the texture that
            // lands a moment later is owned by nobody — leaked past unload.
            if (this.disposed && Interlocked.Exchange(ref entry.Wrap, null) is { } orphan)
                orphan.Dispose();
        }
        catch (Exception ex)
        {
            // A missing poster is not worth a visible error — the card falls back to its title.
            entry.FailedAt = this.tick;
            log.Debug($"[art] {thumb}: {ex.Message}");
        }
        finally
        {
            this.fetching.Release();
        }
    }

    public void Dispose()
    {
        this.disposed = true;

        // Safe here in a way it is not mid-frame: Dispose runs during plugin teardown, after the
        // last Draw, so nothing holds these any more. Claimed atomically because a fetch may be
        // publishing into the same field right now — whoever takes it disposes it, exactly once.
        foreach (var entry in this.cache.Values)
        {
            if (Interlocked.Exchange(ref entry.Wrap, null) is { } wrap)
                wrap.Dispose();
        }

        foreach (var (wrap, _) in this.retiring)
            wrap.Dispose();

        this.cache.Clear();
        this.retiring.Clear();
        this.fetching.Dispose();
    }
}
