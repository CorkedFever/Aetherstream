using Aetherstream.Core;

namespace Aetherstream.Playback;

/// <summary>
/// Turns a party code into whatever that group is streaming right now.
/// <para>
/// The code holds nothing; the service decides what this caller may know. Membership is checked
/// there, so a code that was never shared with you resolves to nothing at all — not even a name.
/// </para>
/// </summary>
public sealed class PartyCodeResolver(PartyDirectory directory, string host, string key)
    : IStreamResolver
{
    public async Task<ResolvedStream> ResolveAsync(string input, CancellationToken ct)
    {
        var code = PartyDirectory.Normalise(input);

        var group = await directory.LookupAsync(host, key, code, ct)
            ?? throw new InvalidOperationException(
                $"No party {PartyDirectory.Pretty(code)}, or you are not in it.\n\n"
                + "Join it from the Party tab first — a code you have not joined shows nothing.");

        if (!group.Live)
        {
            throw new InvalidOperationException(
                group.Name.Length > 0
                    ? $"{group.Name} is not streaming right now."
                    : "Nobody is streaming to that party right now.");
        }

        return new ResolvedStream(group.WatchUrl, group.Name.Length > 0 ? group.Name : "Party");
    }
}
