// Modeled on Memoria's AudioRingBuffer (src/RomEmulator.Core/AudioRingBuffer.cs), widened to
// interleaved stereo. Indices count frames (L+R pairs), never raw floats, so a full ring can
// never split a frame and swap the channels.

namespace Aetherstream.Core;

/// <summary>
/// Single-producer, single-consumer ring of interleaved stereo float frames. The decoder's audio
/// callback writes, the audio device thread reads, and neither ever takes a lock — an audio
/// callback that blocks produces a dropout, so the buffer is built so it cannot.
/// </summary>
public sealed class StereoRingBuffer(int capacityFrames)
{
    private readonly float[] buffer = new float[capacityFrames * 2];

    private int writeFrame;
    private int readFrame;
    private float lastLeft;
    private float lastRight;

    private int CapacityFrames => this.buffer.Length / 2;

    /// <summary>Gets the number of frames waiting to be played.</summary>
    public int Count
    {
        get
        {
            var available = Volatile.Read(ref this.writeFrame) - Volatile.Read(ref this.readFrame);
            return available < 0 ? available + this.CapacityFrames : available;
        }
    }

    /// <summary>Gets how full the buffer is, 0 to 1.</summary>
    public float Fill => this.Count / (float)(this.CapacityFrames - 1);

    /// <summary>Number of frames dropped because the consumer fell behind. Diagnostics only.</summary>
    public long Overruns { get; private set; }

    /// <summary>Number of frames invented because the producer fell behind. Diagnostics only.</summary>
    public long Underruns { get; private set; }

    /// <summary>
    /// Appends interleaved stereo samples. Producer thread only. Drops whole frames once the ring
    /// is full — dropping the newest audio is better than overwriting what the device is about to
    /// play, and it self-corrects within a callback or two.
    /// </summary>
    public void Write(ReadOnlySpan<float> interleaved)
    {
        var capacity = this.CapacityFrames;
        var write = this.writeFrame;

        for (var i = 0; i + 1 < interleaved.Length; i += 2)
        {
            var next = write + 1 == capacity ? 0 : write + 1;
            if (next == Volatile.Read(ref this.readFrame))
            {
                this.Overruns += (interleaved.Length - i) / 2;
                break;
            }

            this.buffer[write * 2] = interleaved[i];
            this.buffer[(write * 2) + 1] = interleaved[i + 1];
            Volatile.Write(ref this.writeFrame, next);
            write = next;
        }
    }

    /// <summary>
    /// Fills <paramref name="interleaved"/> with stereo frames. Consumer thread only. If the
    /// producer has not kept up, the shortfall holds the last frame rather than snapping to
    /// silence — a discontinuity to zero is an audible click, a held level usually is not.
    /// </summary>
    public void Read(Span<float> interleaved)
    {
        var capacity = this.CapacityFrames;
        var read = this.readFrame;
        var write = Volatile.Read(ref this.writeFrame);
        var i = 0;

        for (; i + 1 < interleaved.Length && read != write; i += 2)
        {
            this.lastLeft = this.buffer[read * 2];
            this.lastRight = this.buffer[(read * 2) + 1];
            interleaved[i] = this.lastLeft;
            interleaved[i + 1] = this.lastRight;
            read = read + 1 == capacity ? 0 : read + 1;
        }

        Volatile.Write(ref this.readFrame, read);

        if (i + 1 >= interleaved.Length)
            return;

        this.Underruns += (interleaved.Length - i) / 2;
        for (; i + 1 < interleaved.Length; i += 2)
        {
            interleaved[i] = this.lastLeft;
            interleaved[i + 1] = this.lastRight;
        }
    }

    public void Clear()
    {
        Volatile.Write(ref this.readFrame, 0);
        Volatile.Write(ref this.writeFrame, 0);
    }
}
