using Aetherstream.Core;
using Aetherstream.Playback;
using LibVLCSharp.Shared;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Aetherstream.PoC;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var input = args.FirstOrDefault(a => !a.StartsWith("--"));
        var testPattern = args.Contains("--test-pattern") || input is null;
        // Audio is opt-in while the pipeline is being proven: a wrong format does not fail quietly,
        // it plays full-scale noise into someone's speakers. --probe-audio runs the whole decode
        // and ring path with no output device attached, so the delivered-rate diagnostic can be
        // read without anything reaching the speakers.
        var probeAudio = args.Contains("--probe-audio");
        var audioEnabled = args.Contains("--audio") || args.Contains("--vlc-audio") || probeAudio;
        var vlcAudio = args.Contains("--vlc-audio");
        var proveBuffer = args.Contains("--prove-buffer");
        var softwareDecode = args.Contains("--software");

        if (testPattern)
        {
            using var pattern = new PreviewForm(new TestPatternSource(1280, 720), null, proveBuffer, "test pattern");
            Application.Run(pattern);
            return;
        }

        // Fully qualified: our own Aetherstream.Core namespace shadows LibVLCSharp's Core class.
        LibVLCSharp.Shared.Core.Initialize();
        using var vlc = new LibVLC(args.Contains("--verbose") ? ["--verbose=2"] : []);
        StreamWriter? log = null;
        if (args.Contains("--verbose"))
        {
            // A WinExe has no console, so libvlc's log has to go somewhere it can be read.
            log = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "aetherstream.log"))
            {
                AutoFlush = true,
            };
            vlc.Log += (_, e) =>
            {
                lock (log)
                    log.WriteLine($"{e.Level} [{e.Module}] {e.Message}");
            };
        }
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        ResolvedStream stream;
        try
        {
            var resolver = StreamResolvers.For(input!, http, out var via);
            stream = resolver.ResolveAsync(input!, CancellationToken.None).GetAwaiter().GetResult();
            stream = stream with { DisplayName = $"{stream.DisplayName} ({via})" };
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not play '{input}'.\n\n{ex.Message}",
                "Aetherstream",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // Ask the endpoint what it actually wants before deciding anything about audio: shared-mode
        // WASAPI does no conversion, so the decoder is configured to match the device, not the
        // other way round.
        using var devices = new MMDeviceEnumerator();
        using var endpoint = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var mixFormat = endpoint.AudioClient.MixFormat;

        var useCallbackAudio = audioEnabled && !vlcAudio;

        using var source = new VlcStreamSource(
            vlc,
            sampleRate: useCallbackAudio ? mixFormat.SampleRate : 0,
            callbackAudio: useCallbackAudio,
            muteOutput: !audioEnabled || probeAudio);
        WasapiOut? audioOut = null;

        var desyncArg = args.FirstOrDefault(a => a.StartsWith("--desync="));
        var desync = desyncArg is null ? 0 : int.Parse(desyncArg["--desync=".Length..]);
        source.Play(stream, hardwareDecode: !softwareDecode, audioDesyncMs: desync);

        if (source.Audio is not null && !probeAudio)
        {
            // Start the device only once a cushion exists. Opening it first means WASAPI drains an
            // empty ring for however long the stream takes to arrive, and playback then rides at
            // near-zero fill for the whole session — one hiccup from an audible dropout.
            var waited = 0;
            while (source.Audio.Fill < 0.25f && waited < 5000)
            {
                Thread.Sleep(50);
                waited += 50;
            }

            audioOut = new WasapiOut(endpoint, AudioClientShareMode.Shared, useEventSync: true, latency: 60);
            audioOut.Init(new RingSampleProvider(source.Audio, mixFormat));
            audioOut.Play();
        }

        using var window = new PreviewForm(source, source.Stats, proveBuffer, stream.DisplayName);
        Application.Run(window);

        audioOut?.Stop();
        audioOut?.Dispose();
        log?.Dispose();
    }
}
