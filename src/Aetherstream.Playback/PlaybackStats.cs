namespace Aetherstream.Playback;

/// <summary>
/// Counters shared across the decoder's callback threads and whoever renders the overlay.
/// Everything goes through Interlocked/Volatile — callbacks must never take a lock.
/// </summary>
public sealed class PlaybackStats
{
    private long framesPresented;
    private long framesDropped;

    /// <summary>Frames libvlc declared due for display (its presentation clock ticking).</summary>
    public long FramesPresented => Volatile.Read(ref this.framesPresented);

    /// <summary>Presented frames the consumer never picked up before they were overwritten.</summary>
    public long FramesDropped => Volatile.Read(ref this.framesDropped);

    private long audioFramesDelivered;

    /// <summary>
    /// Audio frames libvlc has handed us. Divided by elapsed seconds this must equal the sample
    /// rate we asked for — if it comes out a whole multiple, the requested format was not honoured
    /// and the stream is being interpreted at the wrong rate or channel count.
    /// </summary>
    public long AudioFramesDelivered => Volatile.Read(ref this.audioFramesDelivered);

    internal void CountPresented() => Interlocked.Increment(ref this.framesPresented);

    internal void CountDropped() => Interlocked.Increment(ref this.framesDropped);

    internal void CountAudio(int frames) => Interlocked.Add(ref this.audioFramesDelivered, frames);
}
