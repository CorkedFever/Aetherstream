using System.Text.Json;

using Aetherstream.Core;
using Aetherstream.Playback;

namespace Aetherstream.Plugin;

/// <summary>
/// Other addresses for a live channel whose listed one has died.
/// <para>
/// The public playlist carries one address per channel, and addresses on the free services move
/// without notice; the same project's API knows every address it has for a channel, keyed by the
/// channel id the playlist calls tvg-id. When a lineup channel dies, the others are probed, the
/// first that answers is remembered against the listed address, and the channel plays through it
/// from then on while the guide keeps showing it under its own number. An alternate that dies in
/// turn is dropped and the next is tried; when none answer, the channel is dead the ordinary way.
/// </para>
/// </summary>
public sealed partial class Plugin
{
    private const string StreamsApi = "https://iptv-org.github.io/api/streams.json";

    private readonly record struct Alternate(string Url, string Feed, bool Labelled);

    private Task<Dictionary<string, List<Alternate>>>? alternates;
    private readonly HashSet<string> alternateSearches = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The address a lineup channel actually plays from: its remembered alternate, or its own.</summary>
    private string AddressOf(string url) =>
        this.config.LiveTvAlternates.TryGetValue(url, out var alt) && alt.Length > 0 ? alt : url;

    /// <summary>A lineup channel's stream, pointed at its alternate when it has one, with the listed address kept as the origin.</summary>
    private ResolvedStream WithAlternate(ResolvedStream stream)
    {
        if (stream.Relayed || stream.Origin is not null)
            return stream;

        var alt = this.AddressOf(stream.PlaylistUrl);
        return alt == stream.PlaylistUrl ? stream : stream with { PlaylistUrl = alt, Origin = stream.PlaylistUrl, Relayable = HlsRelay.CanRelay(alt) };
    }

    /// <summary>
    /// Looks for another address for a lineup channel, in the background, and switches to it if
    /// the channel is still what is tuned. Once per channel per session, so a channel with nothing
    /// left to try does not keep asking.
    /// </summary>
    private void TryAlternate(M3uPlaylist.Channel channel, bool play)
    {
        if (channel.TvgId.Length == 0 || !this.alternateSearches.Add(channel.Url + "|" + this.AddressOf(channel.Url)))
            return;

        _ = Task.Run(async () =>
        {
            var found = await this.FindAlternateAsync(channel);
            if (found is null)
            {
                this.log.Information($"[health] no other address answers for '{channel.Name}'");
                return;
            }

            this.SetAlternate(channel.Url, found);
            this.MarkAlive(channel.Url);
            this.log.Information($"[health] '{channel.Name}' moved to another address: {found}");

            if (play && string.Equals(this.config.Source, channel.Url, StringComparison.OrdinalIgnoreCase))
            {
                this.window.Dial.MarkOnline(channel.Url);
                this.PlayResolved(channel.ToStream());
            }
        });
    }

    /// <summary>
    /// The first other address for the channel that answers like a playlist, or null. Addresses
    /// already found dead are skipped; the channel's own feed and unlabelled entries go first, and
    /// entries the index labels (geo-blocked, and the like) last.
    /// </summary>
    private async Task<string?> FindAlternateAsync(M3uPlaylist.Channel channel)
    {
        var id = channel.TvgId;
        var feed = string.Empty;
        var at = id.IndexOf('@');
        if (at >= 0)
        {
            feed = id[(at + 1)..];
            id = id[..at];
        }

        var all = await this.AlternatesAsync();
        if (!all.TryGetValue(id, out var candidates))
            return null;

        var current = this.AddressOf(channel.Url);
        var ordered = candidates
            .Where(c => !string.Equals(c.Url, channel.Url, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(c.Url, current, StringComparison.OrdinalIgnoreCase)
                && !this.IsDead(c.Url))
            .OrderBy(c => c.Labelled ? 1 : 0)
            .ThenBy(c => string.Equals(c.Feed, feed, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();

        foreach (var candidate in ordered)
        {
            if (await this.ProbeAsync(channel with { Url = candidate.Url }))
                return candidate.Url;

            this.MarkDead(candidate.Url, "an alternate that did not answer");
        }

        return null;
    }

    /// <summary>The index's streams by channel id, fetched once per session.</summary>
    private Task<Dictionary<string, List<Alternate>>> AlternatesAsync()
    {
        return this.alternates ??= Fetch();

        async Task<Dictionary<string, List<Alternate>>> Fetch()
        {
            var map = new Dictionary<string, List<Alternate>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await using var body = await this.http.GetStreamAsync(StreamsApi, cts.Token);
                using var doc = await JsonDocument.ParseAsync(body, cancellationToken: cts.Token);
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    var id = e.TryGetProperty("channel", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? string.Empty : string.Empty;
                    var url = e.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? string.Empty : string.Empty;
                    if (id.Length == 0 || url.Length == 0)
                        continue;

                    var feed = e.TryGetProperty("feed", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() ?? string.Empty : string.Empty;
                    var labelled = e.TryGetProperty("labels", out var l) && l.ValueKind == JsonValueKind.Array && l.GetArrayLength() > 0;
                    if (!map.TryGetValue(id, out var list))
                        map[id] = list = [];
                    list.Add(new Alternate(url, feed, labelled));
                }

                this.log.Information($"[health] {map.Count} channels with addresses in the index");
            }
            catch (Exception ex)
            {
                this.log.Warning($"[health] the index's streams could not be read: {ex.Message}");
            }

            return map;
        }
    }
}
