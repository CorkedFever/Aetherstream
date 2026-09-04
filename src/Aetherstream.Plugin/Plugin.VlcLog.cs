using LibVLCSharp.Shared;

namespace Aetherstream.Plugin;

/// <summary>
/// libvlc's own diagnostics, forwarded into the Dalamud log.
/// <para>
/// Everything that goes wrong with a stream — a rejected segment, a codec it has no decoder for, a
/// server that hung up — libvlc says out loud and, until this existed, the plugin threw away. That
/// left "it played for ten seconds and froze" with nothing behind it, reproducible only by running
/// the same URL through a desktop harness that <i>does</i> capture the log.
/// </para>
/// </summary>
public sealed partial class Plugin
{
    /// <summary>
    /// Messages allowed per <see cref="LogWindow"/>. A stream that is failing fails once per
    /// segment, so an unbounded forward turns one bad channel into thousands of lines a minute in a
    /// log shared with every other plugin.
    /// </summary>
    private const int LogBudget = 40;

    private static readonly TimeSpan LogWindow = TimeSpan.FromSeconds(10);

    private readonly object logGate = new();
    private DateTime logWindowStart = DateTime.UtcNow;
    private int logCount;
    private string lastVlcMessage = string.Empty;

    /// <summary>
    /// Things libvlc says at every start that mean nothing here.
    /// <para>
    /// Frames reach us through callbacks, so there is no window to set on top and the converters
    /// it probes for a windowed output are not the one it ends up using; the first audio packet of
    /// a live stream has no timestamp and libvlc dates it itself; and a broadcast transport stream
    /// has discontinuities by design. Each one is logged as an error or a warning by libvlc and is
    /// followed, every time, by playback working — so forwarding them would paint the log red on
    /// every channel change and teach everyone to ignore the lines that do matter.
    /// </para>
    /// </summary>
    private static bool IsRoutine(string message) =>
        message.StartsWith("Failed to set on top", StringComparison.Ordinal)
        || message.StartsWith("Failed to create video converter", StringComparison.Ordinal)
        || message.StartsWith("non-dated audio buffer received", StringComparison.Ordinal)
        || message.StartsWith("discontinuity received", StringComparison.Ordinal)
        || message.StartsWith("Broken stream: pid", StringComparison.Ordinal);

    /// <summary>
    /// Called on libvlc's own threads, so it does nothing but filter and hand off — and never
    /// throws, because an exception crossing back into native code takes the game with it.
    /// </summary>
    private void OnVlcLog(object? sender, LogEventArgs e)
    {
        try
        {
            // Notices and debug are the running commentary of a working stream. Only the levels that
            // describe something going wrong are worth the shared log.
            if (e.Level is not (LogLevel.Warning or LogLevel.Error))
                return;

            if (IsRoutine(e.Message))
                return;

            var text = $"[vlc:{e.Module}] {e.Message}";

            lock (this.logGate)
            {
                // libvlc repeats itself verbatim while a condition persists; the second identical
                // line adds nothing the first did not.
                if (text == this.lastVlcMessage)
                    return;

                var now = DateTime.UtcNow;
                if (now - this.logWindowStart > LogWindow)
                {
                    this.logWindowStart = now;
                    this.logCount = 0;
                }

                if (++this.logCount > LogBudget)
                {
                    // Said once per window rather than once per dropped line, so the throttle cannot
                    // become the flood it exists to prevent.
                    if (this.logCount == LogBudget + 1)
                        this.log.Warning("[vlc] further messages suppressed for a few seconds.");

                    return;
                }

                this.lastVlcMessage = text;
            }

            if (e.Level == LogLevel.Error)
                this.log.Error(text);
            else
                this.log.Warning(text);
        }
        catch
        {
            // Nothing useful to do, and throwing here would unwind into libvlc.
        }
    }
}
