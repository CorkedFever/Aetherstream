using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// Picks a resolver for whatever the user typed, so no single service is wired into the
/// application. Order is deliberate:
/// <list type="number">
/// <item>a direct media URL needs no resolution at all;</item>
/// <item>yt-dlp handles any site it knows, which is most of them;</item>
/// <item>the built-in Twitch path is the fallback that keeps working with nothing installed.</item>
/// </list>
/// </summary>
public static class StreamResolvers
{
    /// <summary>File extensions libvlc can open without any resolution step.</summary>
    private static readonly string[] DirectMedia = [".m3u8", ".mpd", ".mp4", ".mkv", ".ts", ".webm"];

    /// <summary>Where a Plex server lives, and the token that identifies you to it.</summary>
    /// <param name="MaxKilobits">
    /// Bitrate ceiling for server-side transcoding. Zero means direct play, which is right on a
    /// LAN and wrong across the internet — the original file can be tens of gigabytes.
    /// </param>
    public readonly record struct PlexSettings(string Server, string Token, int MaxKilobits = 0)
    {
        public bool IsConfigured => this.Server.Length > 0 && this.Token.Length > 0;
    }

    /// <summary>
    /// Where party codes are resolved, and the key that says who is asking. Membership is checked
    /// server-side, so a code you were never given resolves to nothing.
    /// </summary>
    public readonly record struct PartySettings(string ApiHost, string Key)
    {
        public bool IsConfigured => this.ApiHost?.Length > 0 && this.Key?.Length > 0;
    }

    public static IStreamResolver For(
        string input,
        HttpClient http,
        out string description,
        PlexSettings plex = default,
        PartySettings party = default,
        Tools tools = default)
    {
        // A party code, checked before anything else: it is the one thing a guest is ever asked to
        // paste, and it has to work wherever a URL would, including "/aether play ABC123".
        if (party.IsConfigured && PartyDirectory.LooksLikeCode(input))
        {
            description = "party code";
            return new PartyCodeResolver(new PartyDirectory(http), party.ApiHost, party.Key);
        }

        // Your own server, behind your own token — not something yt-dlp knows about.
        if (PlexResolver.Matches(input))
        {
            if (!plex.IsConfigured)
                throw new InvalidOperationException("Set the Plex server address and token first.");

            description = "Plex";
            return new PlexResolver(http, plex.Server, plex.Token, plex.MaxKilobits);
        }

        if (IsDirectMedia(input))
        {
            description = "direct URL";
            return new DirectUrlResolver();
        }

        if (YtDlpResolver.Locate(tools.YtDlpPath, tools.Directories ?? []) is { } ytDlp)
        {
            description = "yt-dlp";
            return new YtDlpResolver(ytDlp);
        }

        if (TwitchResolver.Matches(input))
        {
            description = "built-in Twitch";
            return new TwitchResolver(http);
        }

        // The first line is all the screen has room for, so it carries the fix. The restart is
        // not optional: PATH is read when the game starts, so yt-dlp installed while it is
        // running stays invisible until it is relaunched.
        throw new InvalidOperationException(
            "yt-dlp not found — \"winget install yt-dlp\" and restart, or point Setup at your copy.\n\n" +
            $"Nothing here can resolve '{input}' without it. yt-dlp plays YouTube, Kick and most " +
            "other sites; without it, only Twitch, Plex, live TV and direct stream URLs work. If " +
            "you already downloaded yt-dlp.exe somewhere, paste that location on the Setup tab.");
    }

    /// <summary>
    /// Where to look for external tools: a path the user typed, and the plugin's own folders in
    /// the order they should be searched.
    /// </summary>
    public readonly record struct Tools(string? YtDlpPath, IReadOnlyList<string>? Directories);

    private static bool IsDirectMedia(string input)
    {
        if (!DirectUrlResolver.Matches(input))
            return false;

        // Compare against the path only; query strings routinely carry unrelated extensions.
        var path = Uri.TryCreate(input, UriKind.Absolute, out var uri) ? uri.AbsolutePath : input;
        return DirectMedia.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}
