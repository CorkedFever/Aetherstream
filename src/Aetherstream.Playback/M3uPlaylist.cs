using System.Text;

using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// Reads an extended M3U playlist — the format IPTV directories publish, and the one VLC has
/// understood for twenty years.
/// <para>
/// The interesting part is that its per-channel options are <em>libvlc</em> options: entries carry
/// <c>#EXTVLCOPT:http-user-agent=…</c> because the format grew up around VLC. Since libvlc is the
/// decoder here too, those map straight onto the header mechanism already built for yt-dlp instead
/// of needing anything new.
/// </para>
/// </summary>
public static class M3uPlaylist
{
    /// <summary>One channel: where it is, what to call it, and anything needed to open it.</summary>
    public readonly record struct Channel(
        string Name,
        string Url,
        string LogoUrl,
        string Group,
        string Country,
        string UserAgent,
        string Referrer)
    {
        /// <summary>
        /// Carries the per-channel headers through as libvlc options. Only the two libvlc 3 actually
        /// exposes are passed; anything else in the playlist is dropped rather than pretended about.
        /// </summary>
        public ResolvedStream ToStream()
        {
            Dictionary<string, string>? headers = null;

            if (this.UserAgent.Length > 0)
                (headers ??= [])["User-Agent"] = this.UserAgent;

            if (this.Referrer.Length > 0)
                (headers ??= [])["Referer"] = this.Referrer;

            return new ResolvedStream(this.Url, this.Name, headers);
        }
    }

    /// <summary>
    /// Parses a playlist. Malformed entries are skipped rather than throwing: these lists are
    /// community-maintained and thousands of lines long, and one bad line should not cost the
    /// other twelve thousand channels.
    /// </summary>
    public static List<Channel> Parse(string text)
    {
        var channels = new List<Channel>();
        if (string.IsNullOrEmpty(text))
            return channels;

        string name = string.Empty, logo = string.Empty, group = string.Empty;
        string country = string.Empty, agent = string.Empty, referrer = string.Empty;
        var pending = false;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimEnd('\r');
            if (line.Length == 0)
                continue;

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                // The display name is everything after the last comma; the attributes precede it.
                var comma = line.LastIndexOf(',');
                name = comma >= 0 ? line[(comma + 1)..].Trim() : string.Empty;

                logo = Attribute(line, "tvg-logo");
                group = Attribute(line, "group-title");
                country = CountryOf(Attribute(line, "tvg-id"));

                // Some lists put the user agent on the EXTINF line as well as in an EXTVLCOPT.
                agent = Attribute(line, "http-user-agent");
                referrer = Attribute(line, "http-referrer");
                pending = true;
                continue;
            }

            if (line.StartsWith("#EXTVLCOPT:", StringComparison.OrdinalIgnoreCase))
            {
                var option = line["#EXTVLCOPT:".Length..];
                var split = option.IndexOf('=');
                if (split <= 0)
                    continue;

                var key = option[..split].Trim();
                var value = option[(split + 1)..].Trim();

                if (key.Equals("http-user-agent", StringComparison.OrdinalIgnoreCase))
                    agent = value;
                else if (key.Equals("http-referrer", StringComparison.OrdinalIgnoreCase))
                    referrer = value;

                continue;
            }

            // Any other directive belongs to the entry being built and is not something we use.
            if (line.StartsWith('#'))
                continue;

            if (pending && IsStreamUrl(line))
            {
                channels.Add(new Channel(
                    name.Length > 0 ? name : "(unnamed)",
                    line,
                    logo,
                    group,
                    country,
                    agent,
                    referrer));
            }

            name = logo = group = country = agent = referrer = string.Empty;
            pending = false;
        }

        return channels;
    }

    /// <summary>
    /// Schemes libvlc can open, as an allowlist rather than "anything with a colon in it".
    /// <para>
    /// The URL ends up as a libvlc MRL, so what is accepted here is what the decoder will be asked
    /// to open. An allowlist keeps a community-maintained list from handing it something exotic,
    /// while still admitting the handful of rtmp, srt and mms entries that are perfectly playable —
    /// assuming http only would silently drop them.
    /// </para>
    /// </summary>
    private static readonly string[] StreamSchemes =
        ["http://", "https://", "rtmp://", "rtmps://", "rtsp://", "srt://", "mms://", "mmsh://"];

    private static bool IsStreamUrl(string line) =>
        StreamSchemes.Any(s => line.StartsWith(s, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Pulls the country out of a tvg-id like "00sReplay.us@SD". It is a convention rather than a
    /// specification, so anything that does not fit the shape yields nothing rather than a guess.
    /// </summary>
    private static string CountryOf(string tvgId)
    {
        if (tvgId.Length == 0)
            return string.Empty;

        var at = tvgId.IndexOf('@');
        var head = at > 0 ? tvgId[..at] : tvgId;

        var dot = head.LastIndexOf('.');
        if (dot < 0 || dot == head.Length - 1)
            return string.Empty;

        var code = head[(dot + 1)..];
        return code.Length == 2 && code.All(char.IsAsciiLetter)
            ? code.ToUpperInvariant()
            : string.Empty;
    }

    /// <summary>Reads a key="value" attribute off an EXTINF line.</summary>
    private static string Attribute(string line, string key)
    {
        var needle = key + "=\"";
        var start = line.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return string.Empty;

        start += needle.Length;
        var end = line.IndexOf('"', start);
        return end > start ? line[start..end] : string.Empty;
    }

    /// <summary>The distinct groups present, ordered by how many channels each holds.</summary>
    public static List<string> GroupsOf(IEnumerable<Channel> channels) =>
        channels
            .Where(c => c.Group.Length > 0)
            .GroupBy(c => c.Group)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();

    /// <summary>The distinct countries present, alphabetically.</summary>
    public static List<string> CountriesOf(IEnumerable<Channel> channels) =>
        channels
            .Where(c => c.Country.Length > 0)
            .Select(c => c.Country)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
}
