namespace Aetherstream.Plugin.Video;

/// <summary>
/// A channel that is drawn rather than decoded: the guide, the weather, anything made of data
/// and a font. The session paints one of these instead of the video while it is selected, into
/// the same frame the video would go in, so it shows wherever the picture does.
/// </summary>
internal interface IFrameChannel
{
    /// <summary>False when the artwork or font it needs did not load; the button stays dark.</summary>
    bool Available { get; }

    /// <summary>Whether it wants the live picture handed in, to show small somewhere.</summary>
    bool WantsPicture { get; }

    /// <summary>
    /// Paints a whole 1280x720 frame. <paramref name="picture"/> is the video (or the test card)
    /// when <see cref="WantsPicture"/> and something is on; <paramref name="seconds"/> climbs
    /// steadily for anything that moves.
    /// </summary>
    void Render(uint[] target, uint[]? picture, DateTime now, double seconds);
}
