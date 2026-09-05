using System.Numerics;

using Aetherstream.Core;

using Dalamud.Bindings.ImGui;

namespace Aetherstream.Plugin.UI;

/// <summary>
/// The screen: a bezel with the live picture in it, and an on-screen display in its corners.
/// <para>
/// This replaces four rows of status text with the thing itself. Whether the stream is working is
/// answered by looking at it; what is playing, how far through, and whether it is live are said the
/// way a television says them — briefly, in a corner, and then out of the way. The states a stream
/// can be in are the words everyone already knows from a TV: <c>NO SIGNAL</c>, <c>TUNING</c>,
/// <c>SIGNAL LOST</c>.
/// </para>
/// </summary>
internal sealed class Screen(UiContext ui)
{
    /// <summary>Set by the plugin — restarting a stalled stream needs the already-resolved source.</summary>
    internal Action? ResumeStalled;

    /// <summary>Tallest the picture goes, so a wide window grows the library, not the monitor.</summary>
    private const float MaxPictureHeight = 300f;

    private const float BezelPadding = 8f;

    /// <summary>How long the channel info stays up after a change, in seconds.</summary>
    private const double OsdHold = 4.0;

    private const double OsdFade = 0.6;

    private ResolvedStream? lastSource;
    private double osdUntil;
    private float lastVolume = -1f;
    private bool lastMuted;
    private double volumeUntil;

    /// <summary>Seconds, while a scrub is in progress; -1 otherwise. See the seek note below.</summary>
    private int scrubbing = -1;

    /// <summary>The picture's rectangle this frame, for anything that wants to draw over it.</summary>
    public (Vector2 Min, Vector2 Max) Picture { get; private set; }

    public void Draw()
    {
        var session = ui.Session;
        var now = ImGui.GetTime();

        // A change of source restarts the channel-info hold.
        if (!ReferenceEquals(session.Current, this.lastSource))
        {
            this.lastSource = session.Current;
            this.osdUntil = now + OsdHold;
            this.scrubbing = -1;
        }

        // Touching the volume brings the bar up for a moment.
        if (this.lastVolume >= 0f && (ui.Config.Volume != this.lastVolume || session.Muted != this.lastMuted))
            this.volumeUntil = now + 2.0;

        this.lastVolume = ui.Config.Volume;
        this.lastMuted = session.Muted;

        // -- geometry ---------------------------------------------------------------------------

        var available = ImGui.GetContentRegionAvail().X;
        var pictureWidth = available - (BezelPadding * 2f);
        var pictureHeight = pictureWidth * 9f / 16f;

        if (pictureHeight > MaxPictureHeight)
        {
            pictureHeight = MaxPictureHeight;
            pictureWidth = pictureHeight * 16f / 9f;
        }

        var outer = new Vector2(pictureWidth, pictureHeight) + new Vector2(BezelPadding * 2f);

        // Centred: a monitor that hugs the left edge of a widened window stops looking like the
        // thing the window is built around.
        var slack = available - outer.X;
        if (slack > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (slack / 2f));

        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var p0 = start + new Vector2(BezelPadding);
        var p1 = p0 + new Vector2(pictureWidth, pictureHeight);
        this.Picture = (p0, p1);

        drawList.AddRectFilled(start, start + outer, Theme.U32(Theme.Bezel), 6f);

        // -- picture ----------------------------------------------------------------------------

        var uploader = session.Uploader;
        var playing = session.IsPlaying;
        var hasFrame = uploader is { HasFrame: true };

        if (hasFrame)
            drawList.AddImage(uploader!.Handle, p0, p1);
        else
            drawList.AddRectFilled(p0, p1, Theme.U32(Theme.Tube));

        // One item over the whole picture: it is the hover target for the OSD and the hit area for
        // the scrub strip along the bottom edge.
        ImGui.SetCursorScreenPos(p0);
        ImGui.InvisibleButton("##screen", new Vector2(pictureWidth, pictureHeight));
        var hovered = ImGui.IsItemHovered();

        // -- state ------------------------------------------------------------------------------

        var stalled = session.StalledAtMs > 0;
        var failed = session.Error is not null && !stalled;
        var tuning = playing && !hasFrame && !failed;
        var duration = session.DurationMs;
        var position = session.PositionMs;
        var live = playing && duration <= 0;

        if (!playing && !failed)
            this.DrawSignal("NO SIGNAL", Theme.TextFaint, "pick an input below");
        else if (failed)
            this.DrawSignal("NO PICTURE", Theme.Bad, Ui.Ellipsis(session.Error!.ReplaceLineEndings(" "), 96));
        else if (tuning)
            this.DrawSignal(session.Current is { Relayed: true } ? "RELAYING…" : "TUNING…", Theme.Accent, string.Empty);
        else if (stalled)
            this.DrawSignal("SIGNAL LOST", Theme.Warn, "press ↻ to pick up where it stopped");

        // -- OSD --------------------------------------------------------------------------------

        // Shown while hovered, for a few seconds after a change, and whenever there is no picture
        // to get in the way of. Faded out over the last part of the hold rather than snapped off.
        var alpha = hovered || !hasFrame ? 1f
            : (float)Math.Clamp((this.osdUntil - now) / OsdFade, 0.0, 1.0);

        if (alpha > 0f && playing)
        {
            var title = this.Title();
            this.OsdText(p0 + new Vector2(10f, 8f), Theme.Accent, Ui.Ellipsis(title, 40).ToUpperInvariant(), alpha);

            if (live)
            {
                this.OsdTextRight(new Vector2(p1.X - 10f, p0.Y + 8f), Theme.Bad, "● LIVE", alpha);
            }
            else if (duration > 0)
            {
                var shown = this.scrubbing >= 0 ? this.scrubbing * 1000L : position;
                this.OsdTextRight(new Vector2(p1.X - 10f, p0.Y + 8f), Theme.TextDim, $"{Ui.Clock(shown)} / {Ui.Clock(duration)}", alpha);
            }

            if (session.IsPaused)
                this.OsdText(p0 + new Vector2(10f, 30f), Theme.Warn, "PAUSED", alpha);
        }

        // -- progress and scrub -----------------------------------------------------------------

        if (playing && duration > 0 && position >= 0)
            this.DrawProgress(p0, p1, position, duration, hovered);

        // -- volume and distance ----------------------------------------------------------------

        if (playing && now < this.volumeUntil)
            this.DrawVolume(p0, p1);
        else if (playing && hasFrame && session.DistanceGain < 0.5f && ui.Config.AudioEnabled)
            this.OsdText(new Vector2(p0.X + 10f, p1.Y - 26f), Theme.TextDim, "◁ far from screen", 0.8f);

        // -- LED --------------------------------------------------------------------------------

        // The power light, in the bezel's corner. Off when nothing is playing, like a real one.
        var led = failed ? Theme.Bad
            : stalled ? Theme.Warn
            : tuning ? Theme.Accent
            : playing && session.IsPaused ? Theme.Warn
            : playing ? Theme.Good
            : Theme.Edge;

        drawList.AddCircleFilled(
            start + outer - new Vector2(BezelPadding * 0.5f + 4f, BezelPadding * 0.5f),
            2.5f,
            Theme.U32(led),
            12);

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(outer);
    }

    /// <summary>The big words in the middle of the tube, and a quieter line under them.</summary>
    private void DrawSignal(string word, Vector4 colour, string detail)
    {
        var (p0, p1) = this.Picture;
        var centre = (p0 + p1) * 0.5f;
        var drawList = ImGui.GetWindowDrawList();

        using (Theme.PushDisplayLarge())
        {
            var size = ImGui.CalcTextSize(word);
            var at = centre - (size * 0.5f) - new Vector2(0f, detail.Length > 0 ? 8f : 0f);
            drawList.AddText(at + new Vector2(2f, 2f), Theme.U32(Theme.Bezel), word);
            drawList.AddText(at, Theme.U32(colour), word);
        }

        if (detail.Length == 0)
            return;

        var detailSize = ImGui.CalcTextSize(detail);
        drawList.AddText(
            new Vector2(centre.X - (detailSize.X * 0.5f), centre.Y + 18f),
            Theme.U32(Theme.TextFaint),
            detail);
    }

    /// <summary>
    /// A thin strip along the bottom edge of the picture. It thickens on hover so it can be aimed
    /// at, and the seek is applied on release, not while dragging — seeking on every frame of a
    /// drag would restart the decoder dozens of times over one gesture.
    /// </summary>
    private void DrawProgress(Vector2 p0, Vector2 p1, long position, long duration, bool hovered)
    {
        var drawList = ImGui.GetWindowDrawList();
        var seekable = ui.Session.IsSeekable;
        var mouse = ImGui.GetMousePos();
        var inBand = hovered && mouse.Y > p1.Y - 24f;
        var thick = inBand || this.scrubbing >= 0 ? 6f : 3f;

        var fraction = this.scrubbing >= 0
            ? this.scrubbing * 1000f / duration
            : (float)position / duration;

        fraction = Math.Clamp(fraction, 0f, 1f);

        drawList.AddRectFilled(new Vector2(p0.X, p1.Y - thick), p1, Theme.U32(Theme.GlassEdge));
        drawList.AddRectFilled(
            new Vector2(p0.X, p1.Y - thick),
            new Vector2(p0.X + ((p1.X - p0.X) * fraction), p1.Y),
            Theme.U32(seekable ? Theme.Accent : Theme.TextFaint));

        if (!seekable)
            return;

        // The whole picture is one item; only a press that begins in the strip's band scrubs, so a
        // stray click in the middle of the picture does nothing.
        if (ImGui.IsItemActive() && (this.scrubbing >= 0 || inBand))
        {
            var t = Math.Clamp((mouse.X - p0.X) / (p1.X - p0.X), 0f, 1f);
            this.scrubbing = (int)(t * duration / 1000f);
        }

        if (this.scrubbing >= 0 && !ImGui.IsItemActive())
        {
            ui.Session.TrySeek(this.scrubbing * 1000L);
            this.scrubbing = -1;
        }
    }

    private void DrawVolume(Vector2 p0, Vector2 p1)
    {
        var drawList = ImGui.GetWindowDrawList();
        var muted = ui.Session.Muted || !ui.Config.AudioEnabled;
        var at = new Vector2(p0.X + 10f, p1.Y - 28f);

        this.OsdText(at, muted ? Theme.Warn : Theme.TextDim, muted ? "MUTE" : "VOL", 1f);

        if (muted)
            return;

        var barStart = at + new Vector2(44f, 6f);
        var barWidth = 120f;

        drawList.AddRectFilled(barStart, barStart + new Vector2(barWidth, 6f), Theme.U32(Theme.GlassEdge));
        drawList.AddRectFilled(
            barStart,
            barStart + new Vector2(barWidth * Math.Clamp(ui.Config.Volume, 0f, 1f), 6f),
            Theme.U32(Theme.Accent));
    }

    /// <summary>Display-face text with a one-pixel shadow, so it stays legible over any picture.</summary>
    private void OsdText(Vector2 at, Vector4 colour, string text, float alpha)
    {
        var drawList = ImGui.GetWindowDrawList();

        using (Theme.PushDisplay())
        {
            drawList.AddText(at + new Vector2(1f, 1f), Theme.U32(Theme.Bezel with { W = alpha }), text);
            drawList.AddText(at, Theme.U32(colour with { W = alpha }), text);
        }
    }

    private void OsdTextRight(Vector2 rightEdge, Vector4 colour, string text, float alpha)
    {
        using (Theme.PushDisplay())
        {
            var size = ImGui.CalcTextSize(text);
            this.OsdText(rightEdge - new Vector2(size.X, 0f), colour, text, alpha);
        }
    }

    /// <summary>What to call what is playing: the remembered label where there is one, the host otherwise.</summary>
    public string Title()
    {
        var recents = ui.Config.Recents;

        return recents.Count > 0
            && string.Equals(recents[0].Source, ui.Config.Source, StringComparison.OrdinalIgnoreCase)
            && recents[0].Label.Length > 0
                ? recents[0].Label
                : Ui.Pretty(ui.Config.Source);
    }
}
