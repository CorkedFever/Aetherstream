using System.Runtime.InteropServices;
using Aetherstream.Core;
using NAudio.Wave;

namespace Aetherstream.PoC;

/// <summary>
/// Plays a <see cref="StereoRingBuffer"/> through NAudio at the device's own mix format.
/// <para>
/// The device format is adopted verbatim rather than requested, because shared-mode WASAPI does
/// not convert for you: asking for stereo on an 8-channel endpoint lays each stereo pair across
/// an eight-slot frame and plays the result four times too fast — audible as pure garble. So the
/// stream is written into front L/R and the remaining channels are left silent.
/// </para>
/// <para>
/// This implements IWaveProvider, not ISampleProvider, for the same reason Memoria's audio path
/// does: ISampleProvider rejects the WAVEFORMATEXTENSIBLE that multi-channel endpoints report.
/// </para>
/// </summary>
public sealed class RingSampleProvider(StereoRingBuffer ring, WaveFormat deviceFormat) : IWaveProvider
{
    private float[] scratch = [];

    public WaveFormat WaveFormat { get; } = deviceFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        var channels = this.WaveFormat.Channels;
        var frames = count / sizeof(float) / channels;
        if (frames <= 0)
            return 0;

        var needed = frames * 2;
        if (this.scratch.Length < needed)
            this.scratch = new float[needed];

        ring.Read(this.scratch.AsSpan(0, needed));

        var bytes = frames * channels * sizeof(float);
        var destination = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, bytes));
        destination.Clear();

        if (channels == 1)
        {
            // A mono endpoint gets the downmix rather than only the left channel.
            for (var f = 0; f < frames; f++)
                destination[f] = (this.scratch[f * 2] + this.scratch[(f * 2) + 1]) * 0.5f;
        }
        else
        {
            for (var f = 0; f < frames; f++)
            {
                destination[f * channels] = this.scratch[f * 2];
                destination[(f * channels) + 1] = this.scratch[(f * 2) + 1];
            }
        }

        // Always hand back the full request: a short read ends the stream as far as NAudio cares,
        // and the ring already covers a producer shortfall by holding its last frame.
        return bytes;
    }
}
