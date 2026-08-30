using System.Runtime.InteropServices;
using Aetherstream.Core;
using LibVLCSharp.Shared;

namespace Aetherstream.Playback;

/// <summary>
/// Decodes a stream through libvlc and exposes it as an <see cref="IFrameSource"/>.
/// <para>
/// libvlc is asked (via SetVideoFormat) to scale and convert every frame to RGBA at a fixed
/// canvas size before the lock callback fires, so ABR variant switches never change our
/// dimensions — which is what keeps the dimensions-read-once contract honest. Chroma "RGBA" is
/// byte order R,G,B,A in memory, i.e. exactly the red-in-low-bits uint the pipeline expects on
/// little-endian. ("RV32" would be BGRA; do not "fix" this to RV32.)
/// </para>
/// <para>
/// A/V sync: libvlc keeps its own audio-mastered clock and fires the display callback at each
/// frame's presentation time. Publishing in that callback IS the sync; consumers just poll for
/// the latest published frame at whatever cadence they like.
/// </para>
/// <para>
/// The frame handoff mirrors FramePipeline's triple buffer, but over native allocations —
/// libvlc writes into these from its own threads, and GC-managed memory has no business there.
/// </para>
/// </summary>
public sealed unsafe class VlcStreamSource : IFrameSource, IDisposable
{
    private const int BufferAlignment = 32;

    /// <summary>Multiple libvlc rounds a picture's line count up to when allocating.</summary>
    private const int LineAlignment = 32;

    private readonly LibVLC vlc;
    private readonly MediaPlayer player;
    private readonly void*[] slots = new void*[3];
    private readonly int frameBytes;

    // Same invariant as FramePipeline: three indices, always distinct, traded by exchange.
    private int writeIndex;
    private int readyIndex = 1;
    private int readIndex = 2;
    private long sequence;
    private long lastConsumedSnapshot;
    private long lastConsumed;
    private bool everConsumed;

    /// <summary>
    /// Wall clock for latency measurement. Started on the first presented frame so both callbacks
    /// measure against the same origin.
    /// </summary>
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

    private Media? media;

    /// <summary>
    /// Serialises transport control against teardown. Every member below takes it, and so does
    /// Dispose — so a seek can never land on a player that is being stopped underneath it.
    /// </summary>
    private readonly object control = new();

    /// <summary>Set when teardown begins, purely to make Dispose idempotent and non-reentrant.</summary>
    private volatile bool tearingDown;

    /// <summary>Set only once the buffers are actually gone. Guards every read of them.</summary>
    private volatile bool disposed;

    // The marshalled callbacks must be kept alive for as long as libvlc might call them; locals
    // would be collected out from under the native side.
    private readonly MediaPlayer.LibVLCVideoLockCb lockCb;
    private readonly MediaPlayer.LibVLCVideoDisplayCb displayCb;
    private readonly MediaPlayer.LibVLCAudioPlayCb? audioPlayCb;
    private readonly MediaPlayer.LibVLCAudioFlushCb? audioFlushCb;
    // Only ever touched on the audio callback thread.
    private float[] audioScratch = [];

    /// <param name="sampleRate">
    /// Required, and deliberately without a default: it must come from the audio endpoint that
    /// will actually play this. Assuming a rate here is how you get a stream decoded at one rate
    /// and played at another, which sounds like garbage rather than failing cleanly.
    /// Pass 0 when <paramref name="callbackAudio"/> is false and libvlc owns the output.
    /// </param>
    public VlcStreamSource(
        LibVLC vlc,
        int sampleRate,
        int width = 1280,
        int height = 720,
        bool callbackAudio = true,
        bool muteOutput = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 16);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 16);

        this.Width = width;
        this.Height = height;
        this.frameBytes = width * height * sizeof(uint);

        // libvlc does NOT confine itself to width*height. Its picture allocator rounds the line
        // count up (32 is the usual multiple), and picture_CopyPixels then copies that many rows
        // into whatever buffer we handed over — so an exactly-sized buffer is overrun by the
        // difference. At 720 lines that is 16 rows, about 82 KB, which is enough to crash the host
        // process outright when the following page happens not to be ours. Over-allocating to the
        // aligned height plus a couple of spare rows is the cheap, permanent fix.
        var pitch = width * sizeof(uint);
        var alignedHeight = (height + LineAlignment - 1) / LineAlignment * LineAlignment;
        var slotBytes = (nuint)(pitch * (alignedHeight + 2));

        for (var i = 0; i < this.slots.Length; i++)
        {
            this.slots[i] = NativeMemory.AlignedAlloc(slotBytes, BufferAlignment);
            NativeMemory.Clear(this.slots[i], slotBytes);
        }

        this.vlc = vlc;
        this.player = new MediaPlayer(vlc);

        this.lockCb = this.OnLock;
        this.displayCb = this.OnDisplay;
        this.player.SetVideoFormat("RGBA", (uint)width, (uint)height, (uint)(width * sizeof(uint)));
        this.player.SetVideoCallbacks(this.lockCb, null, this.displayCb);

        if (callbackAudio)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);

            // Decode straight to the device's own rate so nothing has to resample downstream.
            this.SampleRate = sampleRate;
            // Four seconds, not one. libvlc hands audio to a callback ahead of when it should be
            // played, expecting the output to hold it — so the standing queue here IS the sync
            // correction, and it has to fit. A one-second ring sat at ~90% full and overran at the
            // peaks, discarding audio while still being too small to align anything.
            this.Audio = new StereoRingBuffer(sampleRate * 4);
            this.audioPlayCb = this.OnAudioPlay;
            this.audioFlushCb = this.OnAudioFlush;
            // Callbacks first, then the format: libvlc only honours a fixed format when callbacks
            // are already registered. Getting this backwards leaves it decoding in whatever native
            // format the stream carries — commonly 5.1, or 16-bit — while we read the buffer as
            // stereo float. That does not fail; it plays full-scale noise.
            // S16N, not FL32: this libvlc build silently ignores a float32 request and keeps
            // decoding in the stream's own format, which we then read as float — measured peaks
            // of 3.4e38 and NaNs, i.e. full-scale noise. 16-bit native is honoured, so the
            // conversion to float happens here instead, where it is one multiply.
            this.player.SetAudioCallbacks(this.audioPlayCb, null, null, this.audioFlushCb, null);
            this.player.SetAudioFormat("S16N", (uint)sampleRate, 2);
        }

        if (muteOutput)
        {
            this.player.Mute = true;
            this.player.Volume = 0;
        }
    }

    public int SampleRate { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Poll-rate hint for the driving clock. Presentation timing is libvlc's.</summary>
    public double FrameRate => 60.0;

    /// <summary>Null when libvlc plays audio through its own output instead of callbacks.</summary>
    public StereoRingBuffer? Audio { get; }

    public PlaybackStats Stats { get; } = new();

    /// <summary>Fires on a libvlc thread when the stream errors or ends; do not tear down from it.</summary>
    public event EventHandler? PlaybackEnded;

    /// <param name="audioDesyncMs">
    /// Shifts audio against video inside libvlc. Negative delivers audio earlier, which is what
    /// corrects our own pipeline: the picture is presented on libvlc's clock, while the sound still
    /// has to cross a ring buffer and the sound card's own buffer before anyone hears it. Shifting
    /// the source is lossless — unlike discarding samples downstream, which just adds gaps.
    /// </param>
    /// <summary>Where playback has reached, in milliseconds, or -1 when that is unknown.</summary>
    public long PositionMs
    {
        get
        {
            lock (this.control)
                return this.disposed || this.tearingDown ? -1 : this.player.Time;
        }
    }

    /// <summary>Total length in milliseconds; 0 for a live stream, which has no end.</summary>
    public long DurationMs
    {
        get
        {
            lock (this.control)
                return this.disposed || this.tearingDown ? 0 : Math.Max(0, this.player.Length);
        }
    }

    /// <summary>Whether this source can be moved through at all — false for live.</summary>
    public bool IsSeekable
    {
        get
        {
            lock (this.control)
                return !this.disposed && !this.tearingDown && this.player.IsSeekable;
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (this.control)
                return !this.disposed && !this.tearingDown && this.player.State == VLCState.Paused;
        }
    }

    /// <summary>
    /// Moves playback. Returns false rather than throwing when the source is going away — losing a
    /// seek costs nothing, and the alternative is an exception on whichever thread asked.
    /// </summary>
    public bool TrySeek(long positionMs)
    {
        lock (this.control)
        {
            if (this.disposed || this.tearingDown || !this.player.IsSeekable)
                return false;

            this.player.Time = Math.Max(0, positionMs);
            return true;
        }
    }

    public bool TrySetPaused(bool paused)
    {
        lock (this.control)
        {
            if (this.disposed || this.tearingDown || !this.player.CanPause)
                return false;

            this.player.SetPause(paused);
            return true;
        }
    }

    public void Play(
        ResolvedStream stream,
        bool hardwareDecode = true,
        int audioDesyncMs = 0,
        int networkCachingMs = 1500)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        // How much libvlc holds before presenting. 1.5s is fine for a well-fed CDN and thin for a
        // live relay over a home connection, where one late segment is a visible stall. Latency
        // costs nothing here: everyone is watching the same broadcast a few seconds apart anyway.
        var options = new List<string>
        {
            $":network-caching={Math.Clamp(networkCachingMs, 300, 20000)}",
        };
        if (audioDesyncMs != 0)
            options.Add($":audio-desync={audioDesyncMs}");
        if (hardwareDecode)
            options.Add(":avcodec-hw=d3d11va");

        // When the service only publishes video and audio separately, libvlc pulls the second
        // stream alongside the first and synchronises them by timestamp.
        if (stream.AudioUrl is { } audioUrl)
            options.Add($":input-slave={audioUrl}");

        // Formatted from an int, so there is nothing here for a hostile source string to inject.
        if (stream.AudioTrackIndex is { } audioTrack and >= 0)
            options.Add($":audio-track={audioTrack.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        if (stream.HttpHeaders is not null)
        {
            // libvlc 3 has no generic header option — it exposes exactly these two. Anything else
            // yt-dlp negotiated is dropped, which is fine in practice: sites that gate on headers
            // gate on the user agent and referer.
            foreach (var (name, value) in stream.HttpHeaders)
            {
                if (name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
                    options.Add($":http-user-agent={value}");
                else if (name.Equals("Referer", StringComparison.OrdinalIgnoreCase))
                    options.Add($":http-referrer={value}");
            }
        }

        var previous = this.media;
        this.media = new Media(this.vlc, stream.PlaylistUrl, FromType.FromLocation, options.ToArray());
        this.player.EncounteredError += this.OnPlayerStopped;
        this.player.EndReached += this.OnPlayerStopped;
        this.player.Play(this.media);
        previous?.Dispose();
    }

    /// <summary>
    /// Copies the newest presented frame; repeats the last one when nothing new has been
    /// presented (live video legitimately repeats frames). Zero-alloc, never blocks.
    /// </summary>
    public void RenderFrame(Span<uint> rgba)
    {
        if (this.disposed)
        {
            // The buffers are gone. Hand back black rather than reading freed memory.
            rgba.Clear();
            return;
        }

        var published = Volatile.Read(ref this.sequence);
        if (published != this.lastConsumed)
        {
            this.lastConsumed = published;
            Volatile.Write(ref this.lastConsumedSnapshot, published);
            this.readIndex = Interlocked.Exchange(ref this.readyIndex, this.readIndex);
            this.everConsumed = true;
        }

        if (!this.everConsumed)
        {
            rgba.Fill(Rgba.Pack(0, 0, 0));
            return;
        }

        new ReadOnlySpan<uint>(this.slots[this.readIndex], this.Width * this.Height).CopyTo(rgba);
    }

    public void Dispose()
    {
        if (this.disposed || this.tearingDown)
            return;

        // Order is load-bearing, and the disposed flag is deliberately set LAST.
        //
        // Stop() blocks until libvlc's vout/aout threads quiesce, so once it returns no callback can
        // be in flight and the buffers are safe to release. Flagging "disposed" before that point
        // only creates a window where callbacks are still running but the object claims to be gone —
        // which is precisely how a null buffer reached libvlc and took the process down.
        //
        // Never call this from a libvlc callback thread; Stop() would deadlock waiting on itself.
        lock (this.control)
            this.tearingDown = true;

        this.player.EncounteredError -= this.OnPlayerStopped;
        this.player.EndReached -= this.OnPlayerStopped;
        this.player.Stop();
        this.player.Dispose();
        this.media?.Dispose();

        this.disposed = true;

        for (var i = 0; i < this.slots.Length; i++)
        {
            NativeMemory.AlignedFree(this.slots[i]);
            this.slots[i] = null;
        }
    }

    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        // libvlc hands us a void*[] of plane pointers to fill; RGBA has a single plane.
        //
        // This must ALWAYS return a real, writable buffer. Returning null to signal "we are shutting
        // down" does not decline the frame — libvlc takes the pointer at face value and memcpys a
        // frame into address zero, killing the host process. Teardown is handled by stopping the
        // player before the buffers go away, not by refusing to supply one here.
        *(void**)planes = this.slots[this.writeIndex];
        return IntPtr.Zero;
    }

    private void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        if (this.disposed)
            return;

        this.LastVideoAtMs = this.clock.ElapsedMilliseconds;

        // This frame is due on screen *now* — publish it. Exchange is a full fence, so the
        // decoder's writes are visible to whoever acquires the slot.
        this.writeIndex = Interlocked.Exchange(ref this.readyIndex, this.writeIndex);

        var published = Interlocked.Increment(ref this.sequence);
        this.Stats.CountPresented();
        if (published - Volatile.Read(ref this.lastConsumedSnapshot) > 1)
            this.Stats.CountDropped();
    }

    /// <summary>
    /// Peak absolute sample value seen. The cheapest proof that the requested format was honoured:
    /// genuine FL32 audio sits inside [-1, 1], so a peak far above 1 (or a NaN) means the buffer is
    /// really some other format being read as float — the difference between quiet and painful.
    /// </summary>
    public float AudioPeak { get; private set; }

    /// <summary>Samples that were NaN or infinite — nonzero means the format is being misread.</summary>
    public long AudioBadSamples { get; private set; }

    /// <summary>Milliseconds since the clock started, when libvlc last declared a frame due.</summary>
    public long LastVideoAtMs { get; private set; } = -1;

    /// <summary>Milliseconds since the clock started, when libvlc last handed us audio.</summary>
    public long LastAudioAtMs { get; private set; } = -1;

    /// <summary>The presentation timestamp libvlc attached to that audio, in microseconds.</summary>
    public long LastAudioPts { get; private set; } = -1;

    /// <summary>How much audio libvlc has handed us in total, expressed as milliseconds of sound.</summary>
    public long AudioDeliveredMs =>
        this.SampleRate > 0 ? this.Stats.AudioFramesDelivered * 1000 / this.SampleRate : 0;

    private void OnAudioPlay(IntPtr data, IntPtr samples, uint count, long pts)
    {
        if (this.disposed || this.Audio is null)
            return;

        // count is frames; FL32 stereo means two floats per frame, interleaved.
        this.Stats.CountAudio((int)count);

        // count is frames; S16N stereo means two shorts per frame, interleaved.
        var pcm = new ReadOnlySpan<short>((void*)samples, (int)count * 2);
        var scratch = this.audioScratch;
        if (scratch.Length < pcm.Length)
            scratch = this.audioScratch = new float[pcm.Length];

        const float Scale = 1f / 32768f;
        for (var i = 0; i < pcm.Length; i++)
            scratch[i] = pcm[i] * Scale;

        var block = scratch.AsSpan(0, pcm.Length);
        this.InspectFormat(block);
        this.Audio.Write(block);
    }

    private void InspectFormat(ReadOnlySpan<float> block)
    {
        // Sampled, not exhaustive: this runs on the audio callback thread and must stay cheap.
        var peak = this.AudioPeak;
        var bad = 0L;

        for (var i = 0; i < block.Length; i += 64)
        {
            var v = block[i];
            if (float.IsNaN(v) || float.IsInfinity(v))
                bad++;
            else
                peak = Math.Max(peak, Math.Abs(v));
        }

        this.AudioPeak = peak;
        this.AudioBadSamples += bad;
    }

    private void OnAudioFlush(IntPtr data, long pts) => this.Audio?.Clear();

    private void OnPlayerStopped(object? sender, EventArgs e) =>
        this.PlaybackEnded?.Invoke(this, EventArgs.Empty);
}
