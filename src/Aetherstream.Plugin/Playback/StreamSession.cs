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

    /// <summary>
    /// Reports a failure that happened before anything started — a source that could not be
    /// resolved. Without this, that failure only reached the log, and someone pasting a YouTube
    /// link on a machine without yt-dlp saw "no signal" and nothing else. Safe from any thread:
    /// the screen only reads it.
    /// </summary>
    public void Fail(string message)
    {
        this.Error = message;
        this.Status = null;
    }

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

    public IReadOnlyList<(int Id, string Name)> Subtitles => this.source?.Subtitles() ?? [];

    public int CurrentSubtitle => this.source?.CurrentSubtitle ?? -1;

    /// <summary>A deliberate choice, which also stops the language preference from overriding it.</summary>
    public void SetSubtitle(int id)
    {
        this.source?.SetSubtitle(id);
        this.subtitlePreferenceApplied = true;
    }

    private bool subtitlePreferenceApplied;

    public IReadOnlyList<(int Id, string Name)> AudioTracks => this.source?.AudioTracks() ?? [];

    public int CurrentAudioTrack => this.source?.CurrentAudioTrack ?? -1;

    /// <summary>A deliberate choice, which also stops the language preference from overriding it.</summary>
    public void SetAudioTrack(int id)
    {
        this.source?.SetAudioTrack(id);
        this.audioPreferenceApplied = true;
    }

    private bool audioPreferenceApplied;

    /// <summary>
    /// Applies the preferred audio language once the tracks are listed. Same shape as the
    /// subtitle preference; there is no "off" here, because silence is what mute is for.
    /// </summary>
    private void ApplyAudioPreference()
    {
        if (this.audioPreferenceApplied || this.source is null)
            return;

        var preference = config.AudioLanguage.Trim();
        if (preference.Length == 0)
        {
            this.audioPreferenceApplied = true;
            return;
        }

        var tracks = this.source.AudioTracks();
        if (tracks.Count == 0 && this.sinceStart.ElapsedMilliseconds < 5000)
            return;

        this.audioPreferenceApplied = true;

        foreach (var (id, name) in tracks)
        {
            if (name.Contains(preference, StringComparison.OrdinalIgnoreCase))
            {
                if (id != this.source.CurrentAudioTrack)
                {
                    this.source.SetAudioTrack(id);
                    log.Information($"[audio] '{name}' for preference '{preference}'");
                }

                return;
            }
        }
    }

    /// <summary>
    /// Applies the preferred subtitle language once the tracks are known. libvlc lists them a
    /// moment after the media opens, so this waits for them — but not forever: five seconds in,
    /// a stream with no listed tracks simply has none.
    /// </summary>
    private void ApplySubtitlePreference()
    {
        if (this.subtitlePreferenceApplied || this.source is null)
            return;

        var preference = config.SubtitleLanguage.Trim();
        if (preference.Length == 0)
        {
            // No preference: libvlc's own choice stands (a track flagged default plays, else none).
            this.subtitlePreferenceApplied = true;
            return;
        }

        var tracks = this.source.Subtitles();
        if (tracks.Count == 0 && this.sinceStart.ElapsedMilliseconds < 5000)
            return;

        this.subtitlePreferenceApplied = true;

        if (preference.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            this.source.SetSubtitle(-1);
            return;
        }

        foreach (var (id, name) in tracks)
        {
            if (name.Contains(preference, StringComparison.OrdinalIgnoreCase))
            {
                this.source.SetSubtitle(id);
                log.Information($"[subtitles] '{name}' for preference '{preference}'");
                return;
            }
        }
    }

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

        // Raised on libvlc's thread; acted on here, because teardown is render-thread work.
        if (this.endedPending)
        {
            this.endedPending = false;
            this.Ended = true;
            this.endedUnconsumed = true;
            this.TearDown();
            this.Status = null;
            log.Information("[playback] reached the end");
        }

        if (this.failedPending)
        {
            this.failedPending = false;
            this.Error ??= "The decoder gave up on this stream — libvlc's reason is in the log.";
            this.TearDown();
        }

        if (this.pendingStart is { } request)
        {
            this.pendingStart = null;
            var resumeAt = this.pendingResumeMs;
            this.pendingResumeMs = 0;
            this.Start(request, resumeAt);
        }

        if (this.Channel is { Available: true } channel)
        {
            this.ShowChannel(channel);
            return;
        }

        this.music?.Stop();

        if (this.source is null)
        {
            this.ShowIdleCard();
            return;
        }

        if (this.uploader is null)
            return;

        if (!this.PullVideo())
            return;

        if (config.RetroMode)
            this.Retro(this.frame);

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

    /// <summary>
    /// Pulls the newest decoded picture into the frame buffer, with the housekeeping that goes
    /// with a fresh frame. False when the decoder has not moved on since last time.
    /// <para>
    /// RenderFrame repeats the last picture when nothing new has been presented, so this is
    /// cheap but not free; the frame is only uploaded when the decoder has actually moved on.
    /// </para>
    /// </summary>
    private bool PullVideo()
    {
        if (this.source is null)
            return false;

        var presented = this.source.Stats.FramesPresented;
        if (presented == this.lastPresented)
            return false;

        this.lastPresented = presented;

        // A resume can only land after the first frame proves the media is open and seekable.
        if (this.resumeTargetMs > 0 && this.source.TrySeek(this.resumeTargetMs))
        {
            log.Information($"[resume] picked up at {this.resumeTargetMs}ms");
            this.ResumedAtMs = this.resumeTargetMs;
            this.ResumedTicks = Environment.TickCount64;
            this.resumeTargetMs = 0;
        }

        this.ApplySubtitlePreference();
        this.ApplyAudioPreference();

        this.source.RenderFrame(this.frame);
        this.ReportSync();
        return true;
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
    private volatile bool endedPending;
    private volatile bool failedPending;
    private bool endedUnconsumed;

    /// <summary>The last thing played ran to its end, as opposed to stopping or failing.</summary>
    public bool Ended { get; private set; }

    /// <summary>Reports a reached end, once — for whoever wants to play the next thing.</summary>
    public bool ConsumeEnded()
    {
        if (!this.endedUnconsumed)
            return false;

        this.endedUnconsumed = false;
        return true;
    }

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

    private StereoRingBuffer? ring;
    private int delayFrames;

    /// <summary>
    /// Moves the sound to the output the config now names, without stopping the picture.
    /// <para>
    /// The decoder was configured for the old device's sample rate. When the new one runs at the
    /// same rate — nearly always 48 kHz on both — the output is simply reopened on it and the ring
    /// carries on. When it differs, the only correct answer is to restart the decoder for the new
    /// rate, which is done in place, resuming at the current position.
    /// </para>
    /// </summary>
    public void ReopenAudio()
    {
        this.music?.Reopen(config.AudioDeviceId);

        if (this.source is not { } playing || this.ring is null || this.current is null)
            return;

        int rate;
        try
        {
            rate = AudioOutput.MixRateOf(config.AudioDeviceId);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not open the chosen audio device; keeping the current one.");
            return;
        }

        if (rate != playing.SampleRate)
        {
            log.Information($"[audio] device runs at {rate} Hz, stream is {playing.SampleRate} Hz — restarting in place");
            this.RequestStart(this.current, Math.Max(0, playing.PositionMs));
            return;
        }

        try
        {
            var replacement = new AudioOutput(this.ring, this.delayFrames, config.AudioDeviceId);
            this.audio?.Dispose();
            this.audio = replacement;
            log.Information("[audio] output moved to the chosen device");
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not move the sound; keeping the current device.");
        }
    }

    /// <summary>The card shown when nothing plays. Set by the plugin; null when the artwork is missing.</summary>
    public TestCard? IdleCard { get; set; }

    /// <summary>Whether the uploader currently holds the test card rather than video.</summary>
    public bool IdleShowing { get; private set; }

    private (int Minute, bool Retro, bool Opaque, bool Fit) idleStamp = (-1, false, false, false);

    /// <summary>Where the last resume landed, or -1; and when, for the OSD and the start-over button.</summary>
    public long ResumedAtMs { get; private set; } = -1;

    public long ResumedTicks { get; private set; }

    /// <summary>
    /// Paints the test card while nothing is playing, and only re-paints when the clock or the
    /// look changes — once a minute, not once a frame.
    /// </summary>
    private void ShowIdleCard()
    {
        if (!config.IdleCard || this.IdleCard is not { Available: true } card)
        {
            if (this.IdleShowing)
                this.TearDown();

            return;
        }

        var now = DateTime.Now;
        var stamp = ((now.Hour * 60) + now.Minute, config.RetroMode, config.PaintOnSurface, config.HasFit);

        if (this.uploader is null)
        {
            this.uploader = this.CreateUploader();
            this.IdleShowing = true;
            this.idleStamp = (-1, false, false, false);
        }

        if (stamp == this.idleStamp)
            return;

        this.idleStamp = stamp;
        card.Render(this.frame, now);

        if (config.RetroMode)
            this.Retro(this.frame);

        if (config.PaintOnSurface)
            this.MakeOpaque(this.frame);

        try
        {
            this.uploader.Upload(config.HasFit ? this.Fit(this.frame) : this.frame);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not show the test card.");
            this.TearDown();
        }
    }

    // -- drawn channels ------------------------------------------------------------------------

    /// <summary>
    /// The drawn channel on screen — the guide, the weather — or null for the picture itself.
    /// It composes over whatever else is on, or over nothing; the sound carries on underneath.
    /// </summary>
    public IFrameChannel? Channel { get; set; }

    /// <summary>What to play under a channel that wants music. Set by the plugin; asked once a frame.</summary>
    public Func<IReadOnlyList<string>>? MusicTracks { get; set; }

    private Jukebox? music;

    public bool MusicPlaying => this.music is { Playing: true };

    /// <summary>The track under the channel, or empty. For the screen's corner.</summary>
    public string MusicNowPlaying => this.music?.NowPlaying ?? string.Empty;

    private uint[]? composed;
    private readonly System.Diagnostics.Stopwatch channelClock = new();
    private long channelPaintedMs = -1;

    /// <summary>
    /// Paints a drawn channel, over the live picture when it wants one. Repainted about thirty
    /// times a second so anything that scrolls moves, and whenever the decoder presents a new
    /// picture for the corner.
    /// </summary>
    private void ShowChannel(IFrameChannel channel)
    {
        if (!this.channelClock.IsRunning)
            this.channelClock.Restart();

        // Music first, every frame, so a finished track moves on even while the paint is throttled.
        if (channel.WantsMusic && config.ChannelMusic && config.AudioEnabled && this.MusicTracks is { } tracks)
        {
            this.music ??= new Jukebox(vlc, log);
            this.music.Ensure(tracks(), config.AudioDeviceId);
            this.music.Update();
        }
        else
        {
            this.music?.Stop();
        }

        if (this.uploader is null)
        {
            this.uploader = this.CreateUploader();
            this.channelPaintedMs = -1;
        }

        // The test card path must repaint from scratch once the channel comes down.
        this.idleStamp = (-1, false, false, false);
        this.IdleShowing = this.source is null;

        var moved = this.PullVideo();
        var now = this.channelClock.ElapsedMilliseconds;
        if (!moved && this.channelPaintedMs >= 0 && now - this.channelPaintedMs < 33)
            return;

        this.channelPaintedMs = now;

        uint[]? picture = null;
        if (channel.WantsPicture)
        {
            if (this.source is not null)
            {
                picture = this.frame;
            }
            else if (this.IdleCard is { Available: true } card)
            {
                card.Render(this.frame, DateTime.Now);
                picture = this.frame;
            }
        }

        this.composed ??= new uint[Width * Height];
        channel.Render(this.composed, picture, DateTime.Now, now / 1000.0);

        if (config.RetroMode)
            this.Retro(this.composed);

        if (config.PaintOnSurface)
            this.MakeOpaque(this.composed);

        try
        {
            this.uploader.Upload(config.HasFit ? this.Fit(this.composed) : this.composed);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not show the channel.");
            this.Channel = null;
            this.TearDown();
        }
    }

    private ushort[]? retroFactor;

    /// <summary>
    /// Scanlines and a vignette, from one precomputed per-pixel factor in 8.8 fixed point. About
    /// a million multiplies per frame, which is a couple of milliseconds — fine for an opt-in look.
    /// </summary>
    private void Retro(uint[] pixels)
    {
        var factor = this.retroFactor ??= BuildRetroFactor();

        for (var i = 0; i < pixels.Length; i++)
        {
            var f = (uint)factor[i];
            if (f == 256)
                continue;

            var p = pixels[i];
            var r = ((p & 0xFFu) * f) >> 8;
            var g = (((p >> 8) & 0xFFu) * f) >> 8;
            var b = (((p >> 16) & 0xFFu) * f) >> 8;
            pixels[i] = (p & 0xFF000000u) | (b << 16) | (g << 8) | r;
        }
    }

    private static ushort[] BuildRetroFactor()
    {
        var table = new ushort[Width * Height];

        for (var y = 0; y < Height; y++)
        {
            // Every other line dimmed: the scanline. The tube's own look, at the cost of a little light.
            var line = (y & 1) == 1 ? 0.74f : 1f;
            var ny = ((y / (Height - 1f)) * 2f) - 1f;

            for (var x = 0; x < Width; x++)
            {
                var nx = ((x / (Width - 1f)) * 2f) - 1f;

                // A soft vignette from about 70% of the way out, never below 45% in the corners.
                var radius = MathF.Sqrt((nx * nx * 0.85f) + (ny * ny));
                var vignette = radius < 0.7f ? 1f : Math.Max(0.45f, 1f - ((radius - 0.7f) / 0.55f * 0.55f));

                table[(y * Width) + x] = (ushort)Math.Round(line * vignette * 256f);
            }
        }

        return table;
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
    public void ApplyVolume(float distanceYalms, float pan = 0f)
    {
        if (this.audio is null)
        {
            // No picture sound, but music may still be on — the guide over nothing, say.
            if (this.music is { Playing: true })
            {
                var alone = 1f;
                if (config.AudioFalloffYalms > 0.01f)
                {
                    var ta = Math.Clamp(distanceYalms / config.AudioFalloffYalms, 0f, 1f);
                    alone = (1f - ta) * (1f - ta);
                }

                this.music.ApplyVolume(this.Muted ? 0f : config.Volume * config.ChannelMusicVolume * alone, pan);
            }

            return;
        }

        var falloff = 1f;
        if (config.AudioFalloffYalms > 0.01f)
        {
            var t = Math.Clamp(distanceYalms / config.AudioFalloffYalms, 0f, 1f);
            // Squared falloff reads as more natural than linear over a room-sized distance.
            falloff = (1f - t) * (1f - t);
        }

        this.DistanceGain = falloff;
        var gain = this.Muted ? 0f : config.Volume * falloff;
        var musicOn = this.music is { Playing: true };

        // Under the guide or the forecast the film goes silent; the music takes its place at the
        // same spot in the room, a little quieter.
        this.audio.Volume = musicOn ? 0f : gain;
        this.audio.Pan = pan;
        this.music?.ApplyVolume(gain * config.ChannelMusicVolume, pan);
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.TearDown();
        this.music?.Dispose();

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
                    sampleRate = AudioOutput.MixRateOf(config.AudioDeviceId);

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
                    output = new AudioOutput(ring, delayFrames, config.AudioDeviceId);
                    output.Volume = config.Volume;

                    // Kept so the output can be reopened on another device mid-stream.
                    this.ring = ring;
                    this.delayFrames = delayFrames;
                }

                this.uploader = this.CreateUploader();
                // Negative (bring sound forward) is the only case libvlc can serve; positive is ours.
                created.Play(
                    stream,
                    config.UseHardwareDecode,
                    Math.Min(0, config.AudioOffsetMs),
                    config.NetworkCachingMs);

                created.PlaybackEnded += (_, _) => this.endedPending = true;
                created.PlaybackFailed += (_, _) => this.failedPending = true;

                this.source = created;
                this.current = stream;
                this.audio = output;
                this.Ended = false;
                this.subtitlePreferenceApplied = false;
                this.audioPreferenceApplied = false;
                this.ResumedAtMs = -1;
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
        this.IdleShowing = false;

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
