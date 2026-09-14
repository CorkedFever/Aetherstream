using Aetherstream.Plugin.Video;

using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Aetherstream.Plugin;

/// <summary>
/// Whose reading the horoscope card is: the character's own guardian, read from the player
/// state, unless Setup names another.
/// </summary>
public sealed partial class Plugin
{
    private unsafe HoroscopeSnapshot? HoroscopeSnapshot()
    {
        if (this.config.HoroscopeDeity is >= 1 and <= 12)
            return new HoroscopeSnapshot(this.config.HoroscopeDeity, false);

        try
        {
            var player = PlayerState.Instance();
            if (player != null && player->GuardianDeity is >= 1 and <= 12)
                return new HoroscopeSnapshot(player->GuardianDeity, true);
        }
        catch (Exception)
        {
        }

        // Not in the world yet: the First Astral Moon's own god will do.
        return new HoroscopeSnapshot(1, false);
    }
}
