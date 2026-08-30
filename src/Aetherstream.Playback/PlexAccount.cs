using System.Net.Http.Headers;
using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// Signs in to a Plex account and finds its servers, the way a TV app does.
/// <para>
/// This is what plex.tv's web app does for you and why typing a seedbox address by hand felt
/// wrong. You are shown a short code, you enter it on plex.tv yourself, and the account hands back
/// a token — no password is ever typed here or seen by this plugin. Plex then reports every server
/// on the account with its addresses, remote ones included, so a seedbox is picked from a list
/// rather than transcribed.
/// </para>
/// </summary>
public sealed class PlexAccount(HttpClient http)
{
    private const string Product = "Aetherstream";

    /// <summary>Identifies this application to the account. Stable, so a link is remembered.</summary>
    private const string ClientId = "aetherstream-ffxiv";

    /// <summary>A sign-in in progress: show <see cref="Code"/>, then poll with <see cref="Id"/>.</summary>
    public readonly record struct Pin(int Id, string Code);

    /// <summary>One server on the account, with the address that answered.</summary>
    public readonly record struct Server(string Name, string Uri, bool IsLocal);

    /// <summary>Starts a sign-in. The code is entered by the user at https://plex.tv/link.</summary>
    public async Task<Pin> BeginSignInAsync(CancellationToken ct)
    {
        // Deliberately NOT strong=true. A strong pin returns a long code intended for programmatic
        // exchange; plex.tv/link only accepts the short four-character one.
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://plex.tv/api/v2/pins");
        Identify(request);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return new Pin(
            json.RootElement.GetProperty("id").GetInt32(),
            json.RootElement.GetProperty("code").GetString() ?? string.Empty);
    }

    /// <summary>
    /// Checks whether the code has been entered yet. Returns the account token once it has, and
    /// null while it has not — this is polled, because there is nothing to call back to.
    /// </summary>
    public async Task<string?> PollSignInAsync(Pin pin, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://plex.tv/api/v2/pins/{pin.Id}");
        Identify(request);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.TryGetProperty("authToken", out var token)
            ? token.GetString()
            : null;
    }

    /// <summary>
    /// Lists the servers on the account. Each reports several addresses — a LAN one and a remote
    /// one — and the remote address is a plex.direct name with a real certificate, which is what
    /// makes a seedbox reachable without any port or certificate wrangling.
    /// </summary>
    public async Task<List<Server>> ListServersAsync(string accountToken, CancellationToken ct)
    {
        var servers = new List<Server>();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://plex.tv/api/v2/resources?includeHttps=1&includeRelay=1");

        Identify(request);
        request.Headers.Add("X-Plex-Token", accountToken);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return servers;

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        foreach (var resource in json.RootElement.EnumerateArray())
        {
            var provides = resource.TryGetProperty("provides", out var p) ? p.GetString() : null;
            if (provides is null || !provides.Contains("server", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = resource.TryGetProperty("name", out var n) ? n.GetString() ?? "server" : "server";

            if (!resource.TryGetProperty("connections", out var connections))
                continue;

            foreach (var connection in connections.EnumerateArray())
            {
                var uri = connection.TryGetProperty("uri", out var u) ? u.GetString() : null;
                if (uri is null)
                    continue;

                var local = connection.TryGetProperty("local", out var l) && l.GetBoolean();
                servers.Add(new Server(name, uri, local));
            }
        }

        // Remote addresses first: a seedbox has no reachable LAN address from here, and trying one
        // just means waiting for a timeout.
        servers.Sort((a, b) => a.IsLocal.CompareTo(b.IsLocal));
        return servers;
    }

    private static void Identify(HttpRequestMessage request)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Plex-Product", Product);
        request.Headers.Add("X-Plex-Client-Identifier", ClientId);
    }
}
