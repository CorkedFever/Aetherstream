using Aetherstream.Playback;

namespace Aetherstream.Plugin;

/// <summary>
/// Party groups: creating them, joining them, and keeping the one you are broadcasting to alive.
/// <para>
/// Split out because none of it touches rendering, surfaces or decode — it is network work on a
/// background loop, and keeping it out of <c>Plugin.cs</c> keeps that file about the game.
/// </para>
/// </summary>
public sealed partial class Plugin
{
    private void WireParty()
    {
        // Identity is generated once and then simply exists. There is no sign-up, no password and
        // nothing to recover — the service stores only a hash of this.
        if (this.config.PartyKey.Length == 0)
        {
            this.config.PartyKey = PartyDirectory.NewKey();
            this.configDirty = true;
        }

        this.window.Share.SignInAsHost = this.ConnectToService;
        this.window.Share.CreateParty = this.CreateGroup;
        this.window.Share.DeleteParty = this.DeleteGroup;
        this.window.Share.FollowParty = this.JoinGroup;
        this.window.Share.LeaveParty = this.LeaveGroup;
        this.window.Share.RefreshParties = this.RefreshGroups;

        this.partyLoop = new CancellationTokenSource();
        _ = Task.Run(() => this.PartyLoopAsync(this.partyLoop.Token));
    }

    private bool Connected => this.config.PartyApiHost.Length > 0 && this.config.PartyKey.Length > 0;

    /// <summary>
    /// Set when a broadcast starts, cleared once our own screen has been switched to the party.
    /// <para>
    /// Watching your own source instead of the relay means running two unrelated pipelines - a local
    /// decode and a separate encode - which drift independently, so the host is the one person who
    /// cannot tell what the room is actually getting. Watching the relay makes that impossible: one
    /// timeline, and anything wrong is wrong for everybody.
    /// </para>
    /// </summary>
    private bool watchOwnPartyWhenLive;

    /// <summary>
    /// Refreshes what everyone is doing, and heartbeats the group you are broadcasting to.
    /// <para>
    /// The heartbeat is the half that matters. The service expires a live group after a minute, so
    /// a host whose game closes stops advertising a stream nobody is feeding — without anyone
    /// noticing, and without leaving a mess only the crashed machine could clear.
    /// </para>
    /// </summary>
    private async Task PartyLoopAsync(CancellationToken ct)
    {
        var beat = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (this.Connected)
                {
                    if (this.broadcast.IsRunning && this.config.PartyCodeInUse.Length > 0)
                    {
                        var live = await this.directory.SetLiveAsync(
                            this.config.PartyApiHost,
                            this.config.PartyKey,
                            this.config.PartyCodeInUse,
                            live: true,
                            Path.GetFileNameWithoutExtension(this.config.PartyInput),
                            this.config.PartyShareScreen ? this.uiContext.CurrentScreen : null,
                            ct);

                        // The relay needs a few segments before it will serve anything, so this
                        // happens on the first heartbeat that comes back live rather than at the
                        // moment the push starts.
                        if (this.watchOwnPartyWhenLive && live is { Live: true, WatchUrl.Length: > 0 } group)
                        {
                            this.watchOwnPartyWhenLive = false;
                            this.log.Information("[party] switching to the party stream, so you see what the room sees.");
                            this.PlayAsync(group.WatchUrl);
                        }
                    }

                    // Groups change far more slowly than the heartbeat needs to fire, so the full
                    // list is re-read every third pass rather than every one.
                    if (beat % 3 == 0)
                        await this.RefreshGroupsAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // A service that is down must not stop the loop; it comes back.
                this.log.Debug($"[party] {ex.Message}");
            }

            beat++;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RefreshGroupsAsync(CancellationToken ct)
    {
        var me = await this.directory.MeAsync(this.config.PartyApiHost, this.config.PartyKey, ct);
        if (me is not { } details)
            return;

        // Told to us rather than typed. Reconnecting is how any of it gets corrected.
        this.config.PartyServer = details.Relay;
        this.config.PartyWatchHost = details.WatchHost;
        this.config.PartySrtPassphrase = details.SrtPassphrase;

        // Cached so starting a broadcast does not need a round trip, and so a group deleted
        // elsewhere cannot leave us pushing to a path we no longer own.
        this.currentStreamPath = details.Groups
            .FirstOrDefault(g => g.Owner && g.Code == this.config.PartyCodeInUse)
            .StreamPath ?? string.Empty;

        this.window.Share.SetParties([.. details.Groups]);
    }

    /// <summary>
    /// Connects to the service. The address is all anyone types — identity is already on disk.
    /// </summary>
    private void ConnectToService(string _, string pasted)
    {
        var host = pasted.Trim().TrimEnd('/');

        foreach (var scheme in (string[])["https://", "http://"])
        {
            if (host.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                host = host[scheme.Length..];
        }

        // Empty means "reconnect to the one I already have", which is what refresh does.
        if (host.Length == 0)
            host = this.config.PartyApiHost;

        if (host.Length == 0)
        {
            this.window.Share.SetStatus("Enter the party server's address.");
            return;
        }

        this.window.Share.SetStatus("Connecting…");

        this.PartyWork(async () =>
        {
            var me = await this.directory.MeAsync(host, this.config.PartyKey, CancellationToken.None);
            if (me is not { } details)
            {
                this.window.Share.SetStatus($"Could not reach {host}.");
                return;
            }

            this.config.PartyApiHost = host;
            this.config.PartyServer = details.Relay;
            this.config.PartyWatchHost = details.WatchHost;
            this.config.PartySrtPassphrase = details.SrtPassphrase;
            this.configDirty = true;

            this.window.Share.SetParties([.. details.Groups]);
            this.window.Share.SetStatus(
                details.Groups.Count > 0 ? string.Empty : "Connected. Make a party or join one.");
        });
    }

    private void RefreshGroups() => this.ConnectToService(string.Empty, string.Empty);

    private void CreateGroup(string name) => this.PartyWork(async () =>
    {
        var group = await this.directory.CreateAsync(
            this.config.PartyApiHost, this.config.PartyKey, name, CancellationToken.None);

        if (group is not { } made)
        {
            this.window.Share.SetStatus("The service refused that.");
            return;
        }

        if (this.config.PartyCodeInUse.Length == 0)
        {
            this.config.PartyCodeInUse = made.Code;
            this.configDirty = true;
        }

        await this.RefreshGroupsAsync(CancellationToken.None);
        this.window.Share.SetStatus($"Made {PartyDirectory.Pretty(made.Code)} — send that to the room.");
    });

    private void JoinGroup(string input, string _) => this.PartyWork(async () =>
    {
        var code = PartyDirectory.Normalise(input);
        if (code.Length != 6)
        {
            this.window.Share.SetStatus("A party code is six characters.");
            return;
        }

        var group = await this.directory.JoinAsync(
            this.config.PartyApiHost, this.config.PartyKey, code, CancellationToken.None);

        if (group is not { } joined)
        {
            this.window.Share.SetStatus($"No party {PartyDirectory.Pretty(code)}, or it is full.");
            return;
        }

        await this.RefreshGroupsAsync(CancellationToken.None);
        this.window.Share.SetStatus(
            $"Joined {(joined.Name.Length > 0 ? joined.Name : PartyDirectory.Pretty(code))}.");
    });

    private void LeaveGroup(string code) => this.PartyWork(async () =>
    {
        await this.directory.LeaveAsync(
            this.config.PartyApiHost, this.config.PartyKey, code, CancellationToken.None);

        await this.RefreshGroupsAsync(CancellationToken.None);
    });

    private void DeleteGroup(string code) => this.PartyWork(async () =>
    {
        await this.directory.DeleteAsync(
            this.config.PartyApiHost, this.config.PartyKey, code, CancellationToken.None);

        if (this.config.PartyCodeInUse == code)
        {
            this.config.PartyCodeInUse = string.Empty;
            this.configDirty = true;
        }

        await this.RefreshGroupsAsync(CancellationToken.None);
    });

    /// <summary>
    /// Stops the push and tells the service, so the room stops being shown a live party straight
    /// away rather than waiting out the expiry.
    /// </summary>
    private void StopBroadcast()
    {
        this.watchOwnPartyWhenLive = false;
        this.broadcast.Stop();

        if (!this.Connected || this.config.PartyCodeInUse.Length == 0)
            return;

        this.PartyWork(() => this.directory.SetLiveAsync(
            this.config.PartyApiHost,
            this.config.PartyKey,
            this.config.PartyCodeInUse,
            live: false,
            string.Empty,
            null,
            CancellationToken.None));
    }

    private void PartyWork(Func<Task> work) => _ = Task.Run(async () =>
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Party service call failed.");
            this.window.Share.SetStatus($"Failed: {ex.Message}");
        }
    });
}
