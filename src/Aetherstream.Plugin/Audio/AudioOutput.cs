using System.Runtime.InteropServices;

using Aetherstream.Core;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Aetherstream.Plugin.Audio;

/// <summary>
/// Plays a <see cref="StereoRingBuffer"/> through WASAPI.
/// <para>
/// The device's own mix format is adopted verbatim rather than requested. Shared-mode WASAPI does
/// no conversion: asking for stereo on an eight-channel endpoint lays each stereo pair across an
/// eight-slot frame and plays it four times too fast, which is not a glitch but full-scale noise.
/// The stream is written into front L/R and the other channels left silent.
/// </para>
/// </summary>
internal sealed class AudioOutput : IDisposable
{
    private readonly WasapiOut? device;
    private readonly RingProvider? provider;
    private bool disposed;

    /// <param name="delayFrames">
    /// How much audio to accumulate before the device starts reading. Whatever is held becomes a
    /// standing delay, because producer and consumer then run at the same rate — so this is a
    /// direct, deterministic way to hold the sound back behind the picture. Unlike asking libvlc
    /// to shift it, this is entirely ours and can be reasoned about.
    /// </param>
    public AudioOutput(StereoRingBuffer ring, int delayFrames = 0)
    {
        // The endpoint is queried and released here rather than held: this runs on the plugin load
        // thread, and holding a COM object across threads invites apartment trouble.
        using var enumerator = new MMDeviceEnumerator();
        using var endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        this.MixFormat = endpoint.AudioClient.MixFormat;
        this.provider = new RingProvider(ring, this.MixFormat, delayFrames);
        // 60ms, not less. The device buffer is latency, but it is also the only cushion against a
        // late callback: too small and the ring underruns, which is heard as crackle rather than as
        // tighter sync. Trading audible artefacts for 30ms is a bad trade.
        this.device = new WasapiOut(endpoint, AudioClientShareMode.Shared, useEventSync: true, latency: 60);
        this.device.Init(this.provider);
        this.device.Play();
    }

    public WaveFormat MixFormat { get; }

    /// <summary>Linear gain applied as samples are handed to the device.</summary>
    public float Volume
    {
        get => this.provider?.Volume ?? 0f;
        set
        {
            if (this.provider is not null)
                this.provider.Volume = Math.Clamp(value, 0f, 1f);
        }
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.device?.Stop();
        this.device?.Dispose();
    }

    /// <summary>
    /// IWaveProvider rather than ISampleProvider on purpose: ISampleProvider rejects the
    /// WAVEFORMATEXTENSIBLE that multi-channel endpoints report.
    /// </summary>
    private sealed class RingProvider(StereoRingBuffer ring, WaveFormat format, int delayFrames)
        : IWaveProvider
    {
        private float[] scratch = [];
        private bool started;

        public WaveFormat WaveFormat { get; } = format;

        public float Volume { get; set; } = 1f;

        public int Read(byte[] buffer, int offset, int count)
        {
            var channels = this.WaveFormat.Channels;
            var frames = count / sizeof(float) / channels;
            if (frames <= 0)
                return 0;

            var bytesRequested = frames * channels * sizeof(float);

            // Hold the device on silence until the buffer has built up the requested delay. Once
            // reading starts the queue stays at roughly that depth by itself, so the offset
            // persists without anything being dropped or repeated.
            if (!this.started)
            {
                if (ring.Count < delayFrames)
                {
                    buffer.AsSpan(offset, bytesRequested).Clear();
                    return bytesRequested;
                }

                this.started = true;
            }

            var needed = frames * 2;
            if (this.scratch.Length < needed)
                this.scratch = new float[needed];

            ring.Read(this.scratch.AsSpan(0, needed));

            var bytes = frames * channels * sizeof(float);
            var destination = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, bytes));
            destination.Clear();

            var gain = this.Volume;

            if (channels == 1)
            {
                for (var f = 0; f < frames; f++)
                    destination[f] = (this.scratch[f * 2] + this.scratch[(f * 2) + 1]) * 0.5f * gain;
            }
            else
            {
                for (var f = 0; f < frames; f++)
                {
                    destination[f * channels] = this.scratch[f * 2] * gain;
                    destination[(f * channels) + 1] = this.scratch[(f * 2) + 1] * gain;
                }
            }

            // Always claim the full request: a short read ends the stream as far as NAudio cares,
            // and the ring already covers a producer shortfall by holding its last frame.
            return bytes;
        }
    }
}
