using Aetherstream.Core;
using Aetherstream.Playback;
using Aetherstream.Plugin.Audio;
using Aetherstream.Plugin.Video;

using Dalamud.Plugin.Services;

using LibVLCSharp.Shared;

namespace Aetherstream.Plugin.Playback;

/// <summary>
/// One playing stream and everything the screen needs to show it: the decoder, the frame it last
/// produced, the texture that frame lives in, and the audio device draining it.
/// <para>
/// Starting and stopping happen off the render thread, but the texture may only be created or
/// destroyed on it. So a request to play is recorded and applied at the top of a frame — the same
/// deferred-reconfiguration rule Memoria's display uses, and for the same reason: disposing a
/// texture the current draw list still references crashes the game.
/// </para>
/// </summary>
internal sealed class StreamSession(
    LibVLC vlc,
    ITextureProvider textures,
    IPluginLog log,
    Configuration config) : IDisposable
{
    private readonly uint[] frame = new uint[Width * Height];

    /// <summary>
    /// Frames to keep a retired texture alive before freeing it.
    /// <para>
    /// Taking our texture back out of the game's material is not enough on its own: draw calls
    /// already submitted still reference it, and freeing it under them faults inside the display
    /// driver rather than in any of our code. Holding it for a few frames lets that work drain.
    /// </para>
    /// </summary>
    private const int RetireFrames = 5;

    private readonly List<(IFrameUploader Uploader, int FramesLeft)> retiring = [];

    private VlcStreamSource? source;
    private IFrameUploader? uploader;
    private AudioOutput? audio;
    private ResolvedStream? pendingStart;
    private bool stopRequested;
    private bool disposed;

    public const int Width = 1280;
    public const int Height = 720;

    public bool IsPlaying => this.source is not null;

    public string? Status { get; private set; }

    public string? Error { get; private set; }

    public IFrameUploader? Uploader => this.uploader;

    public long FramesPresented => this.source?.Stats.FramesPresented ?? 0;

    /// <summary>Where playback has reached, or -1 when unknown. Live sources report -1 or 0.</summary>
    public long PositionMs => this.source?.PositionMs ?? -1;

    /// <summary>Length in milliseconds; 0 means live, or not yet known.</summary>
    public long DurationMs => this.source?.DurationMs ?? 0;

    public bool IsSeekable => this.source?.IsSeekable ?? false;

    public bool IsPaused => this.source?.IsPaused ?? false;

    public bool TrySeek(long positionMs) => this.source?.TrySeek(positionMs) ?? false;

    /// <summary>Nudges playback along by a number of seconds, clamped to the end.</summary>
    public bool Skip(int seconds)
    {
        if (this.source is not { } current)
            return false;

        var position = current.PositionMs;
        if (position < 0)
            return false;

        var duration = current.DurationMs;
        var target = position + (seconds * 1000L);
        if (duration > 0)
            target = Math.Min(target, duration - 2000);

        return current.TrySeek(Math.Max(0, target));
    }

    public bool TrySetPaused(bool paused) => this.source?.TrySetPaused(paused) ?? false;

    /// <summary>Queues a stream to start. Safe from any thread.</summary>
    public void RequestStart(ResolvedStream stream, long resumeAtMs = 0)
    {
        this.pendingStart = stream;
        this.pendingResumeMs = resumeAtMs;
        this.stopRequested = false;
    }

    private long pendingResumeMs;

    /// <summary>Queues a stop. Safe from any thread.</summary>
    public void RequestStop()
    {
        this.pendingStart = null;
        this.stopRequested = true;
    }

    /// <summary>
    /// Applies queued start/stop requests and pulls the newest frame onto the GPU.
    /// Render thread only; call at the top of the frame, before anything draws.
    /// </summary>
    public void Update()
    {
        if (this.disposed)
            return;

        this.DrainRetired();

        if (this.stopRequested)
        {
            this.stopRequested = false;
            this.TearDown();
            this.Status = null;
        }

        if (this.pendingStart is { } request)
        {
            this.pendingStart = null;
            var resumeAt = this.pendingResumeMs;
            this.pendingResumeMs = 0;
            this.Start(request, resumeAt);
        }

        if (this.source is null || this.uploader is null)
            return;

        // RenderFrame repeats the last picture when nothing new has been presented, so this is
        // cheap but not free; only upload when the decoder has actually moved on.
        var presented = this.source.Stats.FramesPresented;
        if (presented == this.lastPresented)
            return;

        this.lastPresented = presented;

        // A resume can only land after the first frame proves the media is open and seekable.
        if (this.resumeTargetMs > 0 && this.source.TrySeek(this.resumeTargetMs))
        {
            log.Information($"[resume] picked up at {this.resumeTargetMs}ms");
            this.resumeTargetMs = 0;
        }

        this.source.RenderFrame(this.frame);
        this.ReportSync();

        if (config.PaintOnSurface)
            this.MakeOpaque(this.frame);

        try
        {
            this.uploader.Upload(config.HasFit ? this.Fit(this.frame) : this.frame);
        }
        catch (Exception ex)
        {
            this.Error = $"Frame upload failed: {ex.Message}";
            log.Error(ex, "Frame upload failed.");
            this.TearDown();
        }
    }

    private long resumeTargetMs;
    private long lastPresented = -1;
    private uint[]? fitted;
    private readonly System.Diagnostics.Stopwatch sinceStart = new();
    private long lastReportMs;

    /// <summary>
    /// How long playback may make no progress before it is treated as dead.
    /// <para>
    /// A stalled network stream does not announce itself: libvlc's video output keeps redisplaying
    /// its last frame, so the frame counter still climbs and nothing looks wrong from inside. The
    /// only honest progress signal is whether the decoder is still being *fed* — delivered audio,
    /// and playback position. When both stop moving, playback has stopped, whatever the frame
    /// counter says.
    /// </para>
    /// <para>
    /// Long enough that ordinary rebuffering on a poor connection is not mistaken for death, short
    /// enough that a channel which has genuinely stopped is not left frozen on screen for most of a
    /// minute before anything is done about it.
    /// </para>
    /// </summary>
    private const long StallAfterMs = 8000;

    private long lastProgressAtMs;
    private long lastDeliveredMs = -1;
    private long lastPositionMs = -1;
    private ResolvedStream? current;

    /// <summary>Where playback had reached when it stalled, so a retry can resume there.</summary>
    public long StalledAtMs { get; private set; } = -1;

    /// <summary>What is playing, so the caller can restart it without re-resolving.</summary>
    public ResolvedStream? Current => this.current;

    /// <summary>
    /// Logs how far the audio decoder has run ahead of real time.
    /// <para>
    /// The decisive number for an A/V offset this large is not any buffer we own — those are tens
    /// of milliseconds — but whether libvlc is handing us audio faster than the clock. Delivered
    /// sound minus elapsed time IS the lead, and it needs no guesswork about where a delay hides.
    /// </para>
    /// </summary>
    private void ReportSync()
    {
        if (this.source is null)
            return;

        if (!this.sinceStart.IsRunning)
            this.sinceStart.Restart();

        var elapsed = this.sinceStart.ElapsedMilliseconds;
        if (elapsed - this.lastReportMs < 3000)
            return;

        this.lastReportMs = elapsed;

        this.CheckProgress(elapsed);

        var deliveredMs = this.source.AudioDeliveredMs;
        var ringMs = this.source.Audio is { } ring && this.source.SampleRate > 0
            ? ring.Count * 1000L / this.source.SampleRate
            : 0;

        log.Information(
            $"[sync] elapsed {elapsed}ms | audio delivered {deliveredMs}ms " +
            $"(lead {deliveredMs - elapsed:+#;-#;0}ms) | waiting in ring {ringMs}ms " +
            $"| video last at {this.source.LastVideoAtMs}ms, audio last at {this.source.LastAudioAtMs}ms " +
            $"| pts {this.source.LastAudioPts / 1000}ms | frames {this.source.Stats.FramesPresented}");
    }

    /// <summary>
    /// Forces every pixel opaque, and optionally brightens.
    /// <para>
    /// Video has no alpha channel, so whatever libvlc leaves in that byte is not something to rely
    /// on — and a surface shader that honours it will blend the picture into whatever is behind,
    /// which reads as the wall showing through. Only done when painting on a surface: the overlay
    /// panel uses alpha deliberately, for its own opacity setting.
    /// </para>
    /// </summary>
    private void MakeOpaque(uint[] pixels)
    {
        var gain = config.SurfaceBrightness;

        if (gain <= 1.001f)
        {
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] |= 0xFF000000;

            return;
        }

        // An effect that blends additively shows dark pixels as transparent, so brightening is the
        // only lever that makes such a surface read as solid.
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            var r = Math.Min(255, (int)((pixel & 0xFF) * gain));
            var g = Math.Min(255, (int)(((pixel >> 8) & 0xFF) * gain));
            var b = Math.Min(255, (int)(((pixel >> 16) & 0xFF) * gain));
            pixels[i] = 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
        }
    }

    /// <summary>
    /// Places the picture inside the texture at the configured scale and offset, leaving the rest
    /// black. Nearest-neighbour, on the render thread, once per decoded frame — a 720p resample is
    /// cheap next to the decode that produced it.
    /// </summary>
    private uint[] Fit(uint[] source)
    {
        this.fitted ??= new uint[Width * Height];
        Array.Clear(this.fitted);

        var drawWidth = Math.Clamp((int)(Width * config.FitScaleX), 1, Width);
        var drawHeight = Math.Clamp((int)(Height * config.FitScaleY), 1, Height);
        var left = (int)(((Width - drawWidth) * 0.5f) + (config.FitOffsetX * Width));
        var top = (int)(((Height - drawHeight) * 0.5f) + (config.FitOffsetY * Height));

        for (var y = 0; y < drawHeight; y++)
        {
            var destinationY = top + y;
            if (destinationY < 0 || destinationY >= Height)
                continue;

            var sourceY = y * Height / drawHeight;
            var sourceRow = sourceY * Width;
            var destinationRow = destinationY * Width;

            for (var x = 0; x < drawWidth; x++)
            {
                var destinationX = left + x;
                if (destinationX < 0 || destinationX >= Width)
                    continue;

                this.fitted[destinationRow + destinationX] = source[sourceRow + (x * Width / drawWidth)];
            }
        }

        return this.fitted;
    }

    /// <summary>
    /// Notices when playback has stopped making progress and says so, rather than leaving a frozen
    /// picture that looks like it is still playing.
    /// </summary>
    private void CheckProgress(long elapsed)
    {
        if (this.source is not { } playing)
            return;

        var delivered = playing.AudioDeliveredMs;
        var position = playing.PositionMs;

        // Delivered audio is the authority whenever there is an audio track, because it is the only
        // one of these that moves solely when the decoder is fed. Position keeps advancing on a
        // starved live stream — libvlc's clock runs on regardless — so accepting *either* signal, as
        // this did, meant a stream that had plainly stopped never registered as stalled at all: a
        // real one ran thirty-three seconds past its last sample without a word.
        var moved = playing.SampleRate > 0
            ? delivered != this.lastDeliveredMs
            : position != this.lastPositionMs;

        if (moved)
        {
            this.lastDeliveredMs = delivered;
            this.lastPositionMs = position;
            this.lastProgressAtMs = elapsed;
            return;
        }

        if (elapsed - this.lastProgressAtMs < StallAfterMs)
            return;

        this.StalledAtMs = position;
        this.Error = position > 0
            ? $"Stream stopped at {TimeSpan.FromMilliseconds(position):hh\\:mm\\:ss}. "
                + "Press Restart to pick up where it left off."
            : "Stream stopped. Press Restart.";

        log.Warning($"[stall] no progress for {elapsed - this.lastProgressAtMs}ms at position {position}ms");
        this.lastProgressAtMs = elapsed;
        this.stallPending = true;
    }

    private bool stallPending;

    /// <summary>
    /// Reports a newly detected stall, once.
    /// <para>
    /// One-shot because the detector re-arms and will say so again every twelve seconds for as long
    /// as nothing arrives, and whoever acts on this — by restarting through the relay — must do it
    /// once rather than once per report.
    /// </para>
    /// </summary>
    public bool ConsumeStall()
    {
        if (!this.stallPending)
            return false;

        this.stallPending = false;
        return true;
    }

    /// <summary>
    /// Silenced for now, without touching the saved volume. Applied every frame by
    /// <see cref="ApplyVolume"/>, so it is a runtime state rather than a setting.
    /// </summary>
    public bool Muted { get; set; }

    /// <summary>
    /// How much the distance falloff is currently taking off, 1 for none. Exposed so the screen
    /// can say "far from screen" instead of leaving a quiet stream looking like a broken one.
    /// </summary>
    public float DistanceGain { get; private set; } = 1f;

    /// <summary>Applies the configured volume, optionally attenuated by distance to the screen.</summary>
    public void ApplyVolume(float distanceYalms)
    {
        if (this.audio is null)
            return;

        var falloff = 1f;
        if (config.AudioFalloffYalms > 0.01f)
        {
            var t = Math.Clamp(distanceYalms / config.AudioFalloffYalms, 0f, 1f);
            // Squared falloff reads as more natural than linear over a room-sized distance.
            falloff = (1f - t) * (1f - t);
        }

        this.DistanceGain = falloff;
        this.audio.Volume = this.Muted ? 0f : config.Volume * falloff;
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.TearDown();

        // Deliberately NOT disposed. On unload there are no further frames in which queued GPU work
        // could drain, so freeing these textures here races the driver — and losing that race is an
        // access violation inside nvwgf2umx on a driver thread, which kills the game rather than
        // the plugin. Leaking a few megabytes until the process exits is the cheaper mistake.
        this.retiring.Clear();
    }

    private void Start(ResolvedStream stream, long resumeAtMs = 0)
    {
        this.TearDown();
        this.Error = null;

        try
        {
            var wantsAudio = config.AudioEnabled;
            var sampleRate = 0;

            AudioOutput? output = null;
            VlcStreamSource? created = null;

            try
            {
                // The device decides the rate; the decoder is configured to match it, never the
                // other way round.
                if (wantsAudio)
                {
                    using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                    using var endpoint = enumerator.GetDefaultAudioEndpoint(
                        NAudio.CoreAudioApi.DataFlow.Render,
                        NAudio.CoreAudioApi.Role.Multimedia);
                    sampleRate = endpoint.AudioClient.MixFormat.SampleRate;
                }

                created = new VlcStreamSource(
                    vlc,
                    sampleRate: sampleRate,
                    width: Width,
                    height: Height,
                    callbackAudio: wantsAudio,
                    muteOutput: !wantsAudio);

                if (wantsAudio && created.Audio is { } ring)
                {
                    // A positive offset holds the sound back, which we do ourselves by buffering.
                    var delayFrames = Math.Max(0, config.AudioOffsetMs) * sampleRate / 1000;
                    output = new AudioOutput(ring, delayFrames);
                    output.Volume = config.Volume;
                }

                this.uploader = this.CreateUploader();
                // Negative (bring sound forward) is the only case libvlc can serve; positive is ours.
                created.Play(
                    stream,
                    config.UseHardwareDecode,
                    Math.Min(0, config.AudioOffsetMs),
                    config.NetworkCachingMs);

                this.source = created;
                this.current = stream;
                this.audio = output;
                this.StalledAtMs = -1;
                this.lastProgressAtMs = 0;
                this.lastDeliveredMs = -1;
                this.lastPositionMs = -1;
                this.stallPending = false;

                // Seek only once libvlc has the media open; before that a seek is discarded.
                this.resumeTargetMs = resumeAtMs;
                this.lastPresented = -1;
                this.Status = stream.DisplayName;
                created = null;
                output = null;
            }
            finally
            {
                output?.Dispose();
                created?.Dispose();
            }
        }
        catch (Exception ex)
        {
            this.Error = ex.Message;
            log.Error(ex, "Could not start playback.");
            this.TearDown();
        }
    }

    private IFrameUploader CreateUploader()
    {
        if (config.UseDynamicTexture)
        {
            try
            {
                return new DynamicTextureUploader(textures, Width, Height);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Dynamic texture path unavailable; falling back to per-frame uploads.");
            }
        }

        return new RawTextureUploader(textures, Width, Height);
    }

    /// <summary>
    /// Raised immediately before the texture is released, while it is still valid.
    /// <para>
    /// Anything that handed our texture to the game — the surface binding does exactly that — must
    /// take it back first. Releasing a texture the game is still rendering from does not fault in
    /// our code; it faults inside the display driver on its next draw, which is a crash to desktop
    /// with none of our frames on the stack.
    /// </para>
    /// </summary>
    public event Action? UploaderReleasing;

    /// <summary>
    /// Order matters: whoever borrowed the texture gives it back, then the decoder stops, then the
    /// buffers and textures it feeds are released.
    /// </summary>
    private void TearDown()
    {
        if (this.uploader is not null)
            this.UploaderReleasing?.Invoke();

        this.source?.Dispose();
        this.source = null;

        this.audio?.Dispose();
        this.audio = null;

        // Retired rather than disposed: see RetireFrames. During Dispose these are never freed at
        // all — there is no later frame in which the driver's queued work could drain.
        if (this.uploader is not null)
            this.retiring.Add((this.uploader, RetireFrames));

        this.uploader = null;

        this.lastPresented = -1;
        this.sinceStart.Reset();
        this.lastReportMs = 0;
    }

    /// <summary>Frees textures whose retirement period has elapsed. Render thread only.</summary>
    private void DrainRetired()
    {
        for (var i = this.retiring.Count - 1; i >= 0; i--)
        {
            var (retired, framesLeft) = this.retiring[i];
            if (framesLeft > 0)
            {
                this.retiring[i] = (retired, framesLeft - 1);
                continue;
            }

            retired.Dispose();
            this.retiring.RemoveAt(i);
        }
    }
}
