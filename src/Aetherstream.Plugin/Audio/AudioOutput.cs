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
    /// <param name="deviceId">
    /// A specific output, by endpoint id, or null for whatever Windows calls the default. Chosen
    /// explicitly because an endpoint opened by id is exactly that endpoint: Windows' per-app
    /// routing cannot move it, and the default changing later does not move it either — so
    /// "put it on my headset" has to be something the plugin itself offers.
    /// </param>
    public AudioOutput(StereoRingBuffer ring, int delayFrames = 0, string? deviceId = null, bool autoSync = false)
    {
        // The endpoint is queried and released here rather than held: this runs on the plugin load
        // thread, and holding a COM object across threads invites apartment trouble.
        using var enumerator = new MMDeviceEnumerator();
        using var endpoint = Resolve(enumerator, deviceId);

        this.MixFormat = endpoint.AudioClient.MixFormat;
        this.provider = new RingProvider(ring, this.MixFormat, delayFrames, autoSync);
        // 60ms, not less. The device buffer is latency, but it is also the only cushion against a
        // late callback: too small and the ring underruns, which is heard as crackle rather than as
        // tighter sync. Trading audible artefacts for 30ms is a bad trade.
        this.device = new WasapiOut(endpoint, AudioClientShareMode.Shared, useEventSync: true, latency: 60);
        this.device.Init(this.provider);
        this.device.Play();
    }

    public WaveFormat MixFormat { get; }

    /// <summary>How long the sound was held back before the device started, in ms; -1 until it has.</summary>
    public int HeldMs => this.provider?.HeldMs ?? -1;

    /// <summary>How many times a backlog was dropped to catch the picture up, and how much in total.</summary>
    public (int Times, int Ms) CatchUps => this.provider is { } p ? (p.CatchUps, p.CaughtUpMs) : (0, 0);

    /// <summary>Every active output on the machine, for the Sound tab's picker.</summary>
    public static List<(string Id, string Name)> Devices()
    {
        var found = new List<(string, string)>();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (device)
                    found.Add((device.ID, device.FriendlyName));
            }
        }
        catch (Exception)
        {
            // No audio subsystem is a valid state (remote desktop, a headless box); an empty list
            // is the honest answer.
        }

        return found;
    }

    /// <summary>The sample rate a given output runs at — what the decoder must be configured to.</summary>
    public static int MixRateOf(string? deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var endpoint = Resolve(enumerator, deviceId);
        return endpoint.AudioClient.MixFormat.SampleRate;
    }

    /// <summary>
    /// The chosen device, or the default when none is chosen or the chosen one is gone — a headset
    /// that was unplugged should fall back to the speakers, not to silence.
    /// </summary>
    private static MMDevice Resolve(MMDeviceEnumerator enumerator, string? deviceId)
    {
        if (!string.IsNullOrEmpty(deviceId))
        {
            try
            {
                var chosen = enumerator.GetDevice(deviceId);
                if (chosen.State == DeviceState.Active)
                    return chosen;

                chosen.Dispose();
            }
            catch (Exception)
            {
                // Unknown id: fall through to the default.
            }
        }

        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

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

    /// <summary>-1 is fully left, +1 fully right, 0 centred. Applied as the samples are handed over.</summary>
    public float Pan
    {
        get => this.provider?.Pan ?? 0f;
        set
        {
            if (this.provider is not null)
                this.provider.Pan = Math.Clamp(value, -1f, 1f);
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
    private sealed class RingProvider(StereoRingBuffer ring, WaveFormat format, int delayFrames, bool autoSync)
        : IWaveProvider
    {
        private float[] scratch = [];
        private bool started;
        private long firstDataTicks = -1;
        private int measuredLeadFrames = -1;

        // The ring's depth when things are steady, and how long it has sat well above that.
        private float steadyFrames = -1f;
        private int backlogReads;

        public int HeldMs { get; private set; } = -1;

        public int CatchUps { get; private set; }

        public int CaughtUpMs { get; private set; }

        public WaveFormat WaveFormat { get; } = format;

        public float Volume { get; set; } = 1f;

        public float Pan { get; set; }

        /// <summary>
        /// After a stall the device has played silence, and the audio that then arrives sits
        /// behind that silence for good: the sound trails the picture by the length of the gap
        /// until something flushes the ring. So the ring's steady depth is tracked, slowly, and
        /// when the depth has sat well above it for a second and a half the excess is dropped.
        /// One short skip, then the sound is back with the picture.
        /// </summary>
        private void CatchUp()
        {
            var depth = ring.Count;
            if (depth == 0)
                return;

            if (this.steadyFrames < 0f)
            {
                this.steadyFrames = depth;
                return;
            }

            var rate = this.WaveFormat.SampleRate;
            var backlog = depth - this.steadyFrames;
            if (backlog > rate * 0.5f)
            {
                // The device reads every sixty milliseconds or so; twenty-five reads is a second and a half.
                if (++this.backlogReads >= 25)
                {
                    var skipped = ring.Skip((int)backlog - (rate / 10));
                    this.CatchUps++;
                    this.CaughtUpMs += skipped * 1000 / rate;
                    this.backlogReads = 0;
                }

                return;
            }

            this.backlogReads = 0;
            this.steadyFrames += (depth - this.steadyFrames) * 0.01f;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var channels = this.WaveFormat.Channels;
            var frames = count / sizeof(float) / channels;
            if (frames <= 0)
                return 0;

            var bytesRequested = frames * channels * sizeof(float);

            if (!this.started)
            {
                if (autoSync)
                {
                    // libvlc hands audio over up to two seconds before it means it to be heard,
                    // trusting the output to play each block at its stamp. Played as it arrives,
                    // the sound runs that far ahead of the picture. So: wait for the opening burst,
                    // measure how much arrived beyond real time, and hold the device back by that.
                    // The queue then stays at that depth and every block plays when it was meant to.
                    var now = Environment.TickCount64;
                    if (ring.Count == 0 && this.firstDataTicks < 0)
                    {
                        buffer.AsSpan(offset, bytesRequested).Clear();
                        return bytesRequested;
                    }

                    if (this.firstDataTicks < 0)
                        this.firstDataTicks = now;

                    const int SettleMs = 400;
                    var rate = this.WaveFormat.SampleRate;
                    if (this.measuredLeadFrames < 0 && now - this.firstDataTicks >= SettleMs)
                        this.measuredLeadFrames = Math.Max(0, ring.Count - (SettleMs * rate / 1000));

                    var holdMs = this.measuredLeadFrames < 0
                        ? long.MaxValue
                        : (this.measuredLeadFrames * 1000L / rate) + (delayFrames * 1000L / rate);

                    if (now - this.firstDataTicks < holdMs)
                    {
                        buffer.AsSpan(offset, bytesRequested).Clear();
                        return bytesRequested;
                    }

                    this.HeldMs = (int)(now - this.firstDataTicks);
                    this.started = true;
                }
                else
                {
                    // Hold the device on silence until the buffer has built up the requested delay. Once
                    // reading starts the queue stays at roughly that depth by itself, so the offset
                    // persists without anything being dropped or repeated.
                    if (ring.Count < delayFrames)
                    {
                        buffer.AsSpan(offset, bytesRequested).Clear();
                        return bytesRequested;
                    }

                    this.HeldMs = 0;
                    this.started = true;
                }
            }

            this.CatchUp();

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
                // A balance, not a true pan: the far side is turned down, the near side is never
                // turned up, so the centre is unity and nothing can clip. Seven tenths at most —
                // a set across the room still reaches both ears.
                var pan = this.Pan;
                var left = gain * (1f - (Math.Max(0f, pan) * 0.7f));
                var right = gain * (1f + (Math.Min(0f, pan) * 0.7f));

                for (var f = 0; f < frames; f++)
                {
                    destination[f * channels] = this.scratch[f * 2] * left;
                    destination[(f * channels) + 1] = this.scratch[(f * 2) + 1] * right;
                }
            }

            // Always claim the full request: a short read ends the stream as far as NAudio cares,
            // and the ring already covers a producer shortfall by holding its last frame.
            return bytes;
        }
    }
}
