using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;

namespace Aetherstream.Playback;

/// <summary>
/// Television listings in XMLTV, the format every IPTV guide uses: channels with an id and a
/// display name, programmes with a start, a stop, a title and a channel.
/// <para>
/// Guide files run to hundreds of megabytes for a whole country, so this reads them as a stream
/// and keeps only the programmes for the channels asked about, in a window of time. Channels are
/// matched by id first — a private server's ids line up exactly — and by display name second,
/// loosely, which is how a public playlist meets a community guide that names things its own way.
/// </para>
/// </summary>
public sealed class XmltvGuide
{
    public readonly record struct Programme(DateTime StartUtc, DateTime StopUtc, string Title);

    private readonly Dictionary<string, List<Programme>> programmes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> idsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> channelIds = new(StringComparer.OrdinalIgnoreCase);

    public int ChannelCount => this.channelIds.Count;

    public int ProgrammeCount => this.programmes.Values.Sum(p => p.Count);

    /// <summary>
    /// Reads a guide, gzipped or not, keeping programmes for the wanted ids only, and only
    /// those that end after <paramref name="fromUtc"/> and start before <paramref name="toUtc"/>.
    /// <paramref name="wantedNames"/> lets a channel without a matching id be picked up by name.
    /// </summary>
    public static XmltvGuide Parse(Stream raw, IReadOnlySet<string> wantedIds, IReadOnlyList<string> wantedNames, DateTime fromUtc, DateTime toUtc)
    {
        var guide = new XmltvGuide();

        // Sniff gzip by its magic bytes rather than trusting a file name.
        var buffered = new BufferedStream(raw, 1 << 16);
        var head = new byte[2];
        var n = buffered.Read(head, 0, 2);
        var stream = new ConcatStream(head.AsMemory(0, n), buffered);
        Stream text = n == 2 && head[0] == 0x1F && head[1] == 0x8B ? new GZipStream(stream, CompressionMode.Decompress) : stream;

        var wantedNameKeys = wantedNames.Select(Normalise).Where(k => k.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // First pass over channels happens inline: XMLTV lists every channel before any
        // programme, so by the time programmes arrive the name map is complete.
        var wanted = new HashSet<string>(wantedIds, StringComparer.OrdinalIgnoreCase);

        using var reader = XmlReader.Create(text, new XmlReaderSettings { IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Ignore, IgnoreComments = true });
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            if (reader.Name == "channel")
            {
                var id = reader.GetAttribute("id") ?? string.Empty;
                if (id.Length == 0)
                    continue;

                guide.channelIds.Add(id);

                // Every display-name the channel lists; the first one that matches a wanted name wins.
                using var sub = reader.ReadSubtree();
                while (sub.Read())
                {
                    if (sub.NodeType == XmlNodeType.Element && sub.Name == "display-name")
                    {
                        var name = Normalise(sub.ReadElementContentAsString());
                        if (name.Length > 0 && wantedNameKeys.Contains(name) && !guide.idsByName.ContainsKey(name))
                        {
                            guide.idsByName[name] = id;
                            wanted.Add(id);
                        }
                    }
                }

                continue;
            }

            if (reader.Name == "programme")
            {
                var channel = reader.GetAttribute("channel") ?? string.Empty;
                if (!wanted.Contains(channel))
                {
                    reader.Skip();
                    continue;
                }

                var start = Stamp(reader.GetAttribute("start"));
                var stop = Stamp(reader.GetAttribute("stop"));
                if (start is not { } s || stop is not { } e || e <= fromUtc || s >= toUtc)
                {
                    reader.Skip();
                    continue;
                }

                var title = string.Empty;
                using (var sub = reader.ReadSubtree())
                {
                    while (sub.Read())
                    {
                        if (sub.NodeType == XmlNodeType.Element && sub.Name == "title" && title.Length == 0)
                            title = sub.ReadElementContentAsString().Trim();
                    }
                }

                if (title.Length == 0)
                    continue;

                if (!guide.programmes.TryGetValue(channel, out var list))
                    guide.programmes[channel] = list = [];

                list.Add(new Programme(s, e, title));
            }
        }

        foreach (var list in guide.programmes.Values)
            list.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));

        return guide;
    }

    /// <summary>The guide's id for a playlist channel: its own id when the guide has it, else a name match, else null.</summary>
    public string? IdFor(string tvgId, string name)
    {
        if (tvgId.Length > 0 && this.channelIds.Contains(tvgId))
            return tvgId;

        return this.idsByName.TryGetValue(Normalise(name), out var id) ? id : null;
    }

    public IReadOnlyList<Programme> ProgrammesFor(string id) =>
        this.programmes.TryGetValue(id, out var list) ? list : [];

    /// <summary>The programme on at a moment, if the guide knows it.</summary>
    public Programme? At(string id, DateTime utc)
    {
        foreach (var p in this.ProgrammesFor(id))
        {
            if (p.StartUtc <= utc && p.StopUtc > utc)
                return p;
        }

        return null;
    }

    /// <summary>
    /// A channel name reduced to what two lists would agree on: lower case, no resolution tags,
    /// no "HD", no punctuation, single spaces. "BBC One HD (1080p)" and "bbc one" meet here.
    /// </summary>
    public static string Normalise(string name)
    {
        var s = name.ToLowerInvariant();
        s = Regex.Replace(s, @"\((\d+p|hd|sd|fhd|uhd|4k)\)", " ");
        s = Regex.Replace(s, @"\b(hd|sd|fhd|uhd|4k|1080p|720p|480p|576p)\b", " ");
        s = Regex.Replace(s, @"[^a-z0-9]+", " ");
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    /// <summary>XMLTV stamps look like "20260913000000 +0000"; the offset may be missing.</summary>
    private static DateTime? Stamp(string? value)
    {
        if (value is null || value.Length < 14)
            return null;

        if (!long.TryParse(value.AsSpan(0, 14), out var digits))
            return null;

        var year = (int)(digits / 10000000000L);
        var month = (int)(digits / 100000000L % 100);
        var day = (int)(digits / 1000000L % 100);
        var hour = (int)(digits / 10000L % 100);
        var minute = (int)(digits / 100L % 100);
        var second = (int)(digits % 100);

        DateTime local;
        try
        {
            local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }

        var rest = value[14..].Trim();
        if (rest.Length >= 5 && (rest[0] == '+' || rest[0] == '-')
            && int.TryParse(rest.AsSpan(1, 2), out var oh) && int.TryParse(rest.AsSpan(3, 2), out var om))
        {
            var offset = new TimeSpan(oh, om, 0);
            local = rest[0] == '+' ? local - offset : local + offset;
        }

        return local;
    }

    /// <summary>The two sniffed bytes put back in front of the rest of the stream.</summary>
    private sealed class ConcatStream(ReadOnlyMemory<byte> head, Stream rest) : Stream
    {
        private int headRead;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.headRead < head.Length)
            {
                var n = Math.Min(count, head.Length - this.headRead);
                head.Span.Slice(this.headRead, n).CopyTo(buffer.AsSpan(offset, n));
                this.headRead += n;
                return n;
            }

            return rest.Read(buffer, offset, count);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
