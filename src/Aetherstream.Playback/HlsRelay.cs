using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// A loopback HLS relay that hides short-lived upstream tokens from the decoder.
/// <para>
/// Several public IPTV portals answer their advertised URL with a redirect to a tokenised playlist
/// that is valid for only a few seconds. A player resolves that redirect once and then refreshes the
/// tokenised URL forever, so the first refresh — and every one after it — is rejected (this host
/// answers HTTP 509). The player runs out the handful of segments it already has and stops dead
/// about twenty seconds in. Plain VLC behaves identically; it is not a decoder problem.
/// </para>
/// <para>
/// The token is short-lived but re-resolving the original URL mints a fresh one, so the way through
/// is to give the decoder a stable address that never expires and re-resolve behind it. libvlc sees
/// one unchanging playlist URL and monotonic segment numbers; the token churn happens here.
/// </para>
/// <para>
/// This speaks HTTP itself over a raw socket rather than using <c>HttpListener</c>, which on Windows
/// needs an administrative URL reservation for any prefix a normal user process wants to bind — not
/// something to demand of someone installing a plugin. The surface is one loopback port answering
/// GET, and the only client is the decoder in this same process.
/// </para>
/// </summary>
public sealed class HlsRelay : IDisposable
{
    /// <summary>
    /// How long a fetched playlist is served before the upstream is read again.
    /// <para>
    /// Live playlists advertise a target duration of about ten seconds and players poll rather
    /// faster than that, so this only has to be short enough that a poll never sees a list it has
    /// already seen twice running. Every read costs a fresh redirect resolution upstream, which is
    /// the thing worth being frugal with.
    /// </para>
    /// </summary>
    private static readonly TimeSpan RefreshAfter = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Segment addresses remembered per channel. The live window is six or so; this leaves room for
    /// a player that lags behind the edge without letting the map grow without bound.
    /// </summary>
    private const int SegmentMemory = 64;

    private readonly HttpClient http;
    private readonly Action<string> log;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource stopping = new();
    private readonly ConcurrentDictionary<int, Channel> channels = new();
    private int nextId;

    public HlsRelay(HttpClient client, Action<string> logLine)
    {
        this.http = client;
        this.log = logLine;

        // Port zero: the operating system picks a free one. A fixed port would collide with whatever
        // else the machine happens to be running, and nothing outside this process needs to guess it.
        this.listener = new TcpListener(IPAddress.Loopback, 0);
        this.listener.Start();

        this.Port = ((IPEndPoint)this.listener.LocalEndpoint).Port;
        this.log($"[relay] listening on 127.0.0.1:{this.Port}");

        _ = Task.Run(this.AcceptLoopAsync);
    }

    public int Port { get; }

    /// <summary>
    /// Whether a source is the kind of thing this can relay at all.
    /// <para>
    /// The whole mechanism is playlist-shaped: re-read a list of segments, rewrite it, serve the
    /// pieces. Pointed at a plain video file it would read the file as playlist text and hand the
    /// decoder nonsense, so this has to be an HLS playlist — which is also, conveniently, the exact
    /// test that keeps a finished film from being restarted as though it had stalled.
    /// </para>
    /// </summary>
    public static bool CanRelay(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("http" or "https"))
            return false;

        // Matched on the path so a query string mentioning m3u8 does not count, and vice versa: a
        // playlist served with parameters after it still ends in the extension.
        return uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Points a stream at the relay, returning one the decoder can open.
    /// <para>
    /// The headers travel with the channel rather than with the decoder: it is the relay that talks
    /// to the origin now, so a user agent or referrer the origin insists on has to be applied here.
    /// </para>
    /// </summary>
    public ResolvedStream Publish(ResolvedStream stream)
    {
        var id = Interlocked.Increment(ref this.nextId);
        this.channels[id] = new Channel(stream.PlaylistUrl, stream.HttpHeaders);

        // Older channels are dropped rather than kept: only one thing plays at a time, and a
        // stale channel holds a segment map for a stream nobody is watching.
        foreach (var old in this.channels.Keys.Where(k => k < id).ToList())
            this.channels.TryRemove(old, out _);

        this.log($"[relay] channel {id} -> {stream.PlaylistUrl}");

        return stream with
        {
            PlaylistUrl = $"http://127.0.0.1:{this.Port}/c/{id}/index.m3u8",

            // The relay applies these upstream now, and passing them on as well would only set
            // libvlc's headers for requests to our own loopback socket, which ignores them.
            HttpHeaders = null,
            Relayed = true,
            Origin = stream.Origin ?? stream.PlaylistUrl,
        };
    }

    private async Task AcceptLoopAsync()
    {
        while (!this.stopping.IsCancellationRequested)
        {
            try
            {
                var client = await this.listener.AcceptTcpClientAsync(this.stopping.Token);

                // Each connection is served on its own task: a player fetches the next segment while
                // still reading the current one, and a serialised loop would turn that into a stall
                // of exactly the kind this class exists to prevent.
                _ = Task.Run(() => this.ServeAsync(client));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SocketException)
            {
                return;
            }
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;

                using var stream = client.GetStream();
                var path = await ReadRequestPathAsync(stream, this.stopping.Token);
                if (path is null)
                    return;

                await this.RouteAsync(path, stream, this.stopping.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException)
            {
                // The decoder hangs up on every seek, channel change and teardown. That is the
                // normal end of a connection here, not a fault worth reporting.
            }
            catch (Exception ex)
            {
                this.log($"[relay] request failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Reads the request line and discards the headers, returning the path.
    /// <para>
    /// Deliberately minimal. The only client is libvlc asking for two kinds of file over loopback,
    /// so there is nothing here to negotiate — and a hand-written parser that accepted more would be
    /// more to get wrong, not more useful.
    /// </para>
    /// </summary>
    private static async Task<string?> ReadRequestPathAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var filled = 0;

        // Read until the blank line that ends the headers, so the request is consumed whole even
        // when it arrives split across packets.
        while (filled < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(filled), ct);
            if (read == 0)
                break;

            filled += read;

            var text = Encoding.ASCII.GetString(buffer, 0, filled);
            if (!text.Contains("\r\n\r\n") && !text.Contains("\n\n"))
                continue;

            var line = text.Split('\n', 2)[0].Trim();
            var parts = line.Split(' ');

            return parts.Length >= 2 && parts[0] == "GET" ? parts[1] : null;
        }

        return null;
    }

    private async Task RouteAsync(string path, NetworkStream stream, CancellationToken ct)
    {
        // /c/{id}/index.m3u8 or /c/{id}/s/{name}
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts[0] != "c" || !int.TryParse(parts[1], out var id)
            || !this.channels.TryGetValue(id, out var channel))
        {
            await WriteStatusAsync(stream, 404, ct);
            return;
        }

        if (parts[2] == "index.m3u8")
        {
            var playlist = await channel.PlaylistAsync(this.http, id, this.Port, this.log, ct);
            if (playlist is null)
            {
                await WriteStatusAsync(stream, 502, ct);
                return;
            }

            await WriteAsync(stream, 200, "application/vnd.apple.mpegurl", Encoding.UTF8.GetBytes(playlist), ct);
            return;
        }

        if (parts.Length >= 4 && parts[2] is "s" or "k" && channel.Resolve(parts[2], parts[3]) is { } upstream)
        {
            await this.ProxyAsync(upstream, channel, stream, ct);
            return;
        }

        await WriteStatusAsync(stream, 404, ct);
    }

    /// <summary>
    /// Streams an upstream segment straight through to the decoder.
    /// <para>
    /// Copied rather than answered with a redirect because the address being copied from is
    /// tokenised and dies within seconds — handing the decoder a URL it has to fetch for itself
    /// reintroduces exactly the expiry this class exists to hide.
    /// </para>
    /// </summary>
    private async Task ProxyAsync(string url, Channel channel, NetworkStream stream, CancellationToken ct)
    {
        // Through the same redirect-following path as the playlist: a segment host is free to
        // redirect too, and the HTTPS-to-HTTP downgrade would stop it dead just the same.
        var (response, _) = await channel.SendAsync(
            this.http, url, HttpCompletionOption.ResponseHeadersRead, ct);

        using var _response = response;

        if (!response.IsSuccessStatusCode)
        {
            this.log($"[relay] segment {(int)response.StatusCode} for {url}");
            await WriteStatusAsync(stream, (int)response.StatusCode, ct);
            return;
        }

        var length = response.Content.Headers.ContentLength;
        var type = response.Content.Headers.ContentType?.ToString() ?? "video/mp2t";

        // Without a length the response has to be delimited by closing the connection, which is
        // legal in HTTP/1.0 terms and is what the header below tells the client to expect.
        var head = new StringBuilder()
            .Append("HTTP/1.1 200 OK\r\n")
            .Append($"Content-Type: {type}\r\n")
            .Append(length is { } n ? $"Content-Length: {n}\r\n" : string.Empty)
            .Append("Connection: close\r\n\r\n")
            .ToString();

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct);

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        await body.CopyToAsync(stream, ct);
    }

    private static Task WriteStatusAsync(NetworkStream stream, int code, CancellationToken ct) =>
        WriteAsync(stream, code, "text/plain", [], ct);

    private static async Task WriteAsync(
        NetworkStream stream, int code, string type, byte[] body, CancellationToken ct)
    {
        var head = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {code} {(code == 200 ? "OK" : "Error")}\r\n" +
            $"Content-Type: {type}\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n");

        await stream.WriteAsync(head, ct);

        if (body.Length > 0)
            await stream.WriteAsync(body, ct);
    }

    public void Dispose()
    {
        this.stopping.Cancel();

        try
        {
            this.listener.Stop();
        }
        catch (SocketException)
        {
            // Already down.
        }

        this.stopping.Dispose();
        this.channels.Clear();
    }

    /// <summary>One relayed source: where it came from, and where its segments currently live.</summary>
    private sealed class Channel(string source, IReadOnlyDictionary<string, string>? headers)
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly Dictionary<string, string> segments = [];
        private readonly List<string> order = [];
        private string? cached;
        private DateTime fetchedAt = DateTime.MinValue;

        public void ApplyHeaders(HttpRequestMessage request)
        {
            if (headers is null)
                return;

            foreach (var (name, value) in headers)
                request.Headers.TryAddWithoutValidation(name, value);
        }

        public string? Resolve(string kind, string name)
        {
            lock (this.segments)
                return this.segments.GetValueOrDefault($"{kind}/{name}");
        }

        public async Task<string?> PlaylistAsync(
            HttpClient client, int id, int port, Action<string> log, CancellationToken ct)
        {
            // One fetch at a time per channel. libvlc opens more than one connection, and letting
            // both miss the cache would double the upstream load for one playlist.
            await this.gate.WaitAsync(ct);

            try
            {
                if (this.cached is not null && DateTime.UtcNow - this.fetchedAt < RefreshAfter)
                    return this.cached;

                var media = await this.FetchMediaAsync(client, log, ct);
                if (media is null)
                    return this.cached;

                this.cached = this.Rewrite(media.Value.Text, media.Value.Base, id, port);
                this.fetchedAt = DateTime.UtcNow;

                return this.cached;
            }
            finally
            {
                this.gate.Release();
            }
        }

        /// <summary>
        /// Fetches a media playlist, flattening a master playlist away if one is served.
        /// <para>
        /// Variants are collapsed to a single choice rather than passed through because the picture
        /// lands on a 1280x720 texture, so nothing above that resolution is visible — and offering
        /// the decoder a ladder would mean relaying every rung's playlist to support switching
        /// between qualities that all look the same once drawn.
        /// </para>
        /// </summary>
        private async Task<(string Text, Uri Base)?> FetchMediaAsync(
            HttpClient client, Action<string> log, CancellationToken ct)
        {
            var fetched = await this.GetAsync(client, source, log, ct);
            if (fetched is null)
                return null;

            if (!fetched.Value.Text.Contains("#EXT-X-STREAM-INF"))
                return fetched;

            var variant = PickVariant(fetched.Value.Text, fetched.Value.Base);
            if (variant is null)
                return fetched;

            return await this.GetAsync(client, variant, log, ct) ?? fetched;
        }

        private async Task<(string Text, Uri Base)?> GetAsync(
            HttpClient client, string url, Action<string> log, CancellationToken ct)
        {
            var (response, actual) = await this.SendAsync(client, url, HttpCompletionOption.ResponseContentRead, ct);

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    log($"[relay] upstream {(int)response.StatusCode} for {url}");
                    return null;
                }

                // The address after redirects, not the one asked for: segment paths are relative to
                // wherever the playlist actually came from, which is a different host entirely once a
                // shortener has been followed.
                return (await response.Content.ReadAsStringAsync(ct), actual);
            }
        }

        /// <summary>
        /// Fetches a URL, following redirects by hand.
        /// <para>
        /// <see cref="HttpClient"/> will not follow a redirect from HTTPS to plain HTTP, on the
        /// sound general principle that silently downgrading a secure request is not something a
        /// library should do behind the caller's back. But that downgrade is precisely what these
        /// portals do — an HTTPS shortener pointing at a bare-IP HTTP origin — so the redirect has
        /// to be followed deliberately here, or the relay never gets past the first hop.
        /// </para>
        /// <para>
        /// The consequence is worth being clear about: the playlist and segments travel in the
        /// clear. That is already true of any player that opens these channels, and it is the
        /// origin's choice rather than ours; nothing secret is being sent either way.
        /// </para>
        /// </summary>
        public async Task<(HttpResponseMessage Response, Uri Final)> SendAsync(
            HttpClient client, string url, HttpCompletionOption completion, CancellationToken ct)
        {
            var current = new Uri(url);

            for (var hop = 0; ; hop++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                this.ApplyHeaders(request);

                var response = await client.SendAsync(request, completion, ct);

                var location = response.Headers.Location;
                var redirect = (int)response.StatusCode is 301 or 302 or 303 or 307 or 308;

                // Five hops is more than any of these chains uses, and a cap is what stops a
                // misconfigured origin that redirects to itself from spinning here forever.
                if (!redirect || location is null || hop >= 5)
                {
                    // The client follows same-scheme redirects on its own, so the last address it
                    // actually requested is not necessarily the one this loop asked for — only the
                    // downgrading hops come back here. Reporting the hop we know about instead of
                    // the one the response came from resolves every segment path against the wrong
                    // host, which the origin answers with 403.
                    return (response, response.RequestMessage?.RequestUri ?? current);
                }

                current = new Uri(current, location);
                response.Dispose();
            }
        }

        /// <summary>Picks the variant closest to the texture's own height without going far over.</summary>
        private static string? PickVariant(string master, Uri baseUri)
        {
            var lines = master.Split('\n');
            var best = (string?)null;
            var bestScore = long.MaxValue;

            for (var i = 0; i < lines.Length - 1; i++)
            {
                if (!lines[i].StartsWith("#EXT-X-STREAM-INF", StringComparison.Ordinal))
                    continue;

                var uri = lines[i + 1].Trim();
                if (uri.Length == 0 || uri[0] == '#')
                    continue;

                var height = HeightOf(lines[i]);
                var score = Math.Abs(height - 720L);

                // An unlabelled variant is worth taking only if nothing better turned up.
                if (height == 0)
                    score = long.MaxValue - 1;

                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = Uri.TryCreate(baseUri, uri, out var absolute) ? absolute.ToString() : null;
            }

            return best;
        }

        private static long HeightOf(string streamInf)
        {
            var at = streamInf.IndexOf("RESOLUTION=", StringComparison.Ordinal);
            if (at < 0)
                return 0;

            var value = streamInf[(at + "RESOLUTION=".Length)..].Split(',')[0].Trim();
            var by = value.IndexOf('x');

            return by > 0 && long.TryParse(value[(by + 1)..], out var height) ? height : 0;
        }

        /// <summary>
        /// Rewrites every address in a media playlist to point back at the relay.
        /// <para>
        /// Segments are renamed by media sequence rather than keeping their upstream file names.
        /// Sequence numbers are unique and monotonic by definition, whereas plenty of servers reuse
        /// one name for every segment and distinguish them by a query token — and a playlist whose
        /// six entries all had the same address would look to the decoder like one segment repeated.
        /// </para>
        /// </summary>
        private string Rewrite(string playlist, Uri baseUri, int id, int port)
        {
            var sequence = SequenceOf(playlist);
            var output = new StringBuilder(playlist.Length + 256);
            var root = $"http://127.0.0.1:{port}/c/{id}";

            foreach (var raw in playlist.Split('\n'))
            {
                var line = raw.TrimEnd('\r');

                if (line.Length == 0)
                {
                    output.Append('\n');
                    continue;
                }

                if (line[0] == '#')
                {
                    // A decryption key or an fMP4 initialisation segment is fetched from the same
                    // expiring host as the media, so both have to travel the same way. Left alone
                    // they would also be resolved by the player against the relay's own address,
                    // which holds nothing but this playlist.
                    output.Append(line.Contains("URI=\"", StringComparison.Ordinal)
                        ? this.RewriteTagUri(line, baseUri, root)
                        : line).Append('\n');

                    continue;
                }

                if (!Uri.TryCreate(baseUri, line, out var absolute))
                    continue;

                var name = $"{sequence}.ts";
                this.Remember($"s/{name}", absolute.ToString());
                output.Append($"{root}/s/{name}").Append('\n');
                sequence++;
            }

            return output.ToString();
        }

        /// <summary>
        /// Rewrites the <c>URI="…"</c> attribute a tag carries.
        /// <para>
        /// Keys and initialisation segments are proxied, because they are opaque bytes the relay can
        /// hand over unchanged. Anything else naming a URI — an alternate rendition, say — points at
        /// another playlist, which would need rewriting of its own to be any use; those are only
        /// made absolute, so the player fetches them from the origin directly rather than asking the
        /// relay for something it does not have.
        /// </para>
        /// </summary>
        private string RewriteTagUri(string line, Uri baseUri, string root)
        {
            const string marker = "URI=\"";

            var at = line.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
                return line;

            var start = at + marker.Length;
            var end = line.IndexOf('"', start);
            if (end < 0)
                return line;

            var uri = line[start..end];
            if (!Uri.TryCreate(baseUri, uri, out var absolute))
                return line;

            var proxied = line.StartsWith("#EXT-X-KEY", StringComparison.Ordinal)
                || line.StartsWith("#EXT-X-SESSION-KEY", StringComparison.Ordinal)
                || line.StartsWith("#EXT-X-MAP", StringComparison.Ordinal);

            if (!proxied)
                return string.Concat(line[..start], absolute.ToString(), line[end..]);

            // Keyed by content so a rotating key gets its own address rather than overwriting the
            // one segments already in the playlist still need.
            var name = Math.Abs(absolute.ToString().GetHashCode()).ToString();
            this.Remember($"k/{name}", absolute.ToString());

            return string.Concat(line[..start], $"{root}/k/{name}", line[end..]);
        }

        private void Remember(string key, string url)
        {
            lock (this.segments)
            {
                if (this.segments.TryAdd(key, url))
                    this.order.Add(key);
                else
                    this.segments[key] = url;

                while (this.order.Count > SegmentMemory)
                {
                    this.segments.Remove(this.order[0]);
                    this.order.RemoveAt(0);
                }
            }
        }

        private static long SequenceOf(string playlist)
        {
            const string tag = "#EXT-X-MEDIA-SEQUENCE:";

            foreach (var line in playlist.Split('\n'))
            {
                if (line.StartsWith(tag, StringComparison.Ordinal)
                    && long.TryParse(line[tag.Length..].Trim(), out var value))
                {
                    return value;
                }
            }

            return 0;
        }
    }
}
