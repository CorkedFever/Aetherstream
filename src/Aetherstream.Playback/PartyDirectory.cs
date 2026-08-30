using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aetherstream.Playback;

/// <summary>
/// Talks to the party service: who you are, what groups you are in, and where tonight's stream is.
/// <para>
/// Identity is a secret key this install generates once and keeps. The server never sees a
/// password — it derives a user id from the key and stores only that, so there is nothing on the
/// box worth stealing and no account to recover.
/// </para>
/// <para>
/// The code carries nothing. Everything is resolved here, which is what keeps it six characters and
/// what keeps a group's stream path away from anyone who is not in it.
/// </para>
/// </summary>
public sealed class PartyDirectory(HttpClient http)
{
    /// <summary>A group you own or have joined.</summary>
    public readonly record struct Group(
        string Code,
        string Name,
        bool Owner,
        int Members,
        bool Live,
        string Title,
        string WatchUrl,
        string StreamPath,
        ScreenPreset? Screen);

    /// <summary>Everything this install needs on startup.</summary>
    public readonly record struct Me(
        string UserId,
        string Relay,
        string WatchHost,
        string SrtPassphrase,
        IReadOnlyList<Group> Groups);

    /// <summary>
    /// A key for this install, generated locally and never sent anywhere but the service.
    /// 192 bits — it is the only thing standing between someone and your parties.
    /// </summary>
    public static string NewKey() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    /// <summary>
    /// Cleans a code up the way someone will actually have typed it: any case, hyphens and spaces
    /// ignored, and Crockford's confusable letters folded onto their digits so an "O" read as an oh
    /// still finds the party.
    /// </summary>
    public static string Normalise(string input)
    {
        var builder = new StringBuilder(6);

        foreach (var raw in (input ?? string.Empty).ToUpperInvariant())
        {
            var c = raw switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                'U' => 'V',
                _ => raw,
            };

            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    public static bool LooksLikeCode(string input) => Normalise(input).Length == 6;

    /// <summary>Grouped in threes, the way a code meant to be read aloud should be.</summary>
    public static string Pretty(string code) =>
        code.Length == 6 ? $"{code[..3]}-{code[3..]}" : code;

    public Task<Me?> MeAsync(string host, string key, CancellationToken ct) =>
        this.ReadMeAsync(host, key, HttpMethod.Get, "/me", null, ct);

    public Task<Group?> CreateAsync(string host, string key, string name, CancellationToken ct) =>
        this.ReadGroupAsync(host, key, HttpMethod.Post, "/g", new { name }, ct);

    public Task<Group?> JoinAsync(string host, string key, string code, CancellationToken ct) =>
        this.ReadGroupAsync(host, key, HttpMethod.Post, $"/g/{code}/join", new { }, ct);

    public Task<Group?> LookupAsync(string host, string key, string code, CancellationToken ct) =>
        this.ReadGroupAsync(host, key, HttpMethod.Get, $"/g/{code}", null, ct);

    public async Task<bool> LeaveAsync(string host, string key, string code, CancellationToken ct)
    {
        using var response = await this.SendAsync(host, key, HttpMethod.Post, $"/g/{code}/leave", new { }, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(string host, string key, string code, CancellationToken ct)
    {
        using var response = await this.SendAsync(host, key, HttpMethod.Delete, $"/g/{code}", null, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Points a group at tonight's stream, and keeps it pointed. The service expires a live group
    /// after a minute, so this is a heartbeat — a host whose game closes stops advertising a stream
    /// nobody is feeding, without anyone having to notice or clean up.
    /// </summary>
    public Task<Group?> SetLiveAsync(
        string host,
        string key,
        string code,
        bool live,
        string title,
        ScreenPreset? screen,
        CancellationToken ct)
    {
        object body = live
            ? new
            {
                live = true,
                title,
                screen = screen is { } s
                    ? new
                    {
                        surface = s.SurfacePath,
                        material = s.MaterialIndex,
                        texture = s.TextureIndex,
                        mask = s.MaskPath,
                        brightness = s.Brightness,
                        fsx = s.FitScaleX,
                        fsy = s.FitScaleY,
                        fox = s.FitOffsetX,
                        foy = s.FitOffsetY,
                    }
                    : null,
            }
            : new { live = false };

        return this.ReadGroupAsync(host, key, HttpMethod.Post, $"/g/{code}/live", body, ct);
    }

    // -- plumbing ----------------------------------------------------------------------------

    private async Task<Me?> ReadMeAsync(
        string host, string key, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await this.SendAsync(host, key, method, path, body, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;

        var groups = new List<Group>();
        if (root.TryGetProperty("groups", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in list.EnumerateArray())
                groups.Add(ReadGroup(entry));
        }

        return new Me(
            Text(root, "user"),
            Text(root, "relay"),
            Text(root, "watchHost"),
            Text(root, "srtPassphrase"),
            groups);
    }

    private async Task<Group?> ReadGroupAsync(
        string host, string key, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await this.SendAsync(host, key, method, path, body, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ReadGroup(json.RootElement);
    }

    private Task<HttpResponseMessage> SendAsync(
        string host, string key, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, $"https://{host}{path}");
        request.Headers.Add("X-Party-Key", key);

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return http.SendAsync(request, ct);
    }

    private static Group ReadGroup(JsonElement e) => new(
        Text(e, "code"),
        Text(e, "name"),
        Flag(e, "owner"),
        (int)Number(e, "members", 0),
        Flag(e, "live"),
        Text(e, "title"),
        Text(e, "watch"),
        Text(e, "streamPath"),
        ReadScreen(e));

    private static ScreenPreset? ReadScreen(JsonElement root)
    {
        if (!root.TryGetProperty("screen", out var s) || s.ValueKind != JsonValueKind.Object)
            return null;

        var preset = new ScreenPreset(
            Text(s, "surface"),
            (int)Number(s, "material", -1),
            (int)Number(s, "texture", -1),
            Text(s, "mask"),
            Number(s, "brightness", 1f),
            Number(s, "fsx", 1f),
            Number(s, "fsy", 1f),
            Number(s, "fox", 0f),
            Number(s, "foy", 0f));

        return preset.IsUsable ? preset : null;
    }

    private static string Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static float Number(JsonElement e, string name, float fallback) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetSingle()
            : fallback;
}
