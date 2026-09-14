using System.Text.Json;

using Aetherstream.Plugin.Video;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aetherstream.Plugin;

/// <summary>
/// Venues for the venues channel, from ffxivvenues.com's public API, for the region the
/// character is in — every datacenter of it, since a venue on Aether is a walk away from Primal. The service resolves each venue's next opening itself, so "open now" and
/// "opens at" are read rather than computed. Fetched every ten minutes while the channel is up;
/// banners are fetched as they are featured and kept for the session.
/// </summary>
public sealed partial class Plugin
{
    private static readonly TimeSpan VenuesFreshFor = TimeSpan.FromMinutes(10);
    private const int BannerCacheLimit = 80;

    private VenuesSnapshot? venuesSnapshot;
    private long venuesRequestedAtMs = -1;
    private bool venuesFetching;
    private string venuesKey = string.Empty;

    private readonly Dictionary<string, uint[]?> banners = [];
    private readonly HashSet<string> bannersLoading = [];
    private readonly Queue<string> bannerOrder = new();

    private Dictionary<string, uint>? dataCenterRegions;

    private VenuesSnapshot? VenuesSnapshot()
    {
        var region = this.CurrentRegion();
        var dc = RegionName(region);
        var key = $"{region}|{this.config.VenuesSfwOnly}";
        var now = Environment.TickCount64;

        if (region == 0)
            return new VenuesSnapshot(string.Empty, [], [], DateTime.MinValue, "log in to a character to see a region");

        var stale = now - this.venuesRequestedAtMs > VenuesFreshFor.TotalMilliseconds;
        if (!this.venuesFetching && (key != this.venuesKey || stale))
        {
            this.venuesKey = key;
            this.venuesRequestedAtMs = now;
            this.FetchVenues(dc, region);
        }

        // Banners load lazily for whatever is listed; the snapshot is rebuilt with them as they land.
        if (this.venuesSnapshot is { } snapshot)
        {
            var changed = false;
            foreach (var venue in snapshot.OpenNow.Concat(snapshot.Soon))
            {
                if (venue.Banner is null && this.banners.TryGetValue(venue.Id, out var ready) && ready is not null)
                    changed = true;
            }

            if (changed)
            {
                this.venuesSnapshot = snapshot with
                {
                    OpenNow = snapshot.OpenNow.Select(this.WithBanner).ToList(),
                    Soon = snapshot.Soon.Select(this.WithBanner).ToList(),
                };
            }
        }

        return this.venuesSnapshot ?? new VenuesSnapshot(dc, [], [], DateTime.MinValue, "asking ffxivvenues.com");
    }

    private VenueRow WithBanner(VenueRow venue) =>
        venue.Banner is null && this.banners.TryGetValue(venue.Id, out var ready) && ready is not null
            ? venue with { Banner = ready }
            : venue;

    private void FetchVenues(string dc, uint region)
    {
        this.venuesFetching = true;
        _ = Task.Run(async () =>
        {
            try
            {
                // The whole list, then only the datacenters of this region: a couple of megabytes
                // every ten minutes, which is what the site's own page loads.
                var text = await this.http.GetStringAsync("https://api.ffxivvenues.com/venue");
                var (open, soon, bannerUris) = this.ParseVenues(text, region);

                this.venuesSnapshot = new VenuesSnapshot(dc, open, soon, DateTime.UtcNow,
                    open.Count + soon.Count == 0 ? "no venues listed for the next day" : string.Empty);
                this.log.Information($"[venues] {dc}: {open.Count} open, {soon.Count} coming up");

                // Banners, a few at a time, featured ones first.
                foreach (var (id, uri) in bannerUris)
                    this.LoadBanner(id, uri);
            }
            catch (Exception ex)
            {
                this.log.Warning($"[venues] fetch failed: {ex.Message}");
                this.venuesSnapshot ??= new VenuesSnapshot(dc, [], [], DateTime.MinValue, "could not reach ffxivvenues.com");
            }
            finally
            {
                this.venuesFetching = false;
            }
        });
    }

    /// <summary>
    /// Open venues, soonest to close; then venues opening within a day, soonest first. A venue
    /// with a closure override in force is left out, as the site itself would show it closed.
    /// </summary>
    private (List<VenueRow> Open, List<VenueRow> Soon, List<(string Id, string Uri)> Banners) ParseVenues(string json, uint region)
    {
        this.dataCenterRegions ??= this.ReadDataCenterRegions();
        var open = new List<(DateTime Key, VenueRow Row)>();
        var soon = new List<(DateTime Key, VenueRow Row)>();
        var bannerUris = new List<(string, string)>();
        var utc = DateTime.UtcNow;

        using var document = JsonDocument.Parse(json);
        foreach (var v in document.RootElement.EnumerateArray())
        {
            if (this.config.VenuesSfwOnly && v.TryGetProperty("sfw", out var sfw) && sfw.ValueKind == JsonValueKind.False)
                continue;

            if (v.TryGetProperty("approved", out var approved) && approved.ValueKind == JsonValueKind.False)
                continue;

            var id = Str(v, "id");
            var name = Str(v, "name");
            if (id.Length == 0 || name.Length == 0)
                continue;

            // A closure override that is in force now means closed, whatever the schedule says.
            var closedNow = false;
            if (v.TryGetProperty("scheduleOverrides", out var overrides) && overrides.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in overrides.EnumerateArray())
                {
                    var isOpen = o.TryGetProperty("open", out var op) && op.ValueKind == JsonValueKind.True;
                    var isNow = o.TryGetProperty("isNow", out var n) && n.ValueKind == JsonValueKind.True;
                    if (!isOpen && isNow)
                        closedNow = true;
                }
            }

            DateTime? start = null, end = null;
            var openNow = false;
            if (v.TryGetProperty("resolution", out var res) && res.ValueKind == JsonValueKind.Object)
            {
                start = Stamp(res, "start");
                end = Stamp(res, "end");
                openNow = res.TryGetProperty("isNow", out var isNowEl) && isNowEl.ValueKind == JsonValueKind.True;
            }

            if (closedNow)
                openNow = false;

            var location = string.Empty;
            var world = string.Empty;
            if (v.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object)
            {
                // Only this region's datacenters.
                var venueDc = Str(loc, "dataCenter");
                if (!this.dataCenterRegions.TryGetValue(venueDc, out var venueRegion) || venueRegion != region)
                    continue;

                world = $"{Str(loc, "world")} ({venueDc})";
                var district = Str(loc, "district");
                var ward = Num(loc, "ward");
                var plot = Num(loc, "plot");
                var apartment = Num(loc, "apartment");
                var room = Num(loc, "room");
                var sub = loc.TryGetProperty("subdivision", out var sd) && sd.ValueKind == JsonValueKind.True;
                location = apartment > 0
                    ? $"{district} W{ward}{(sub ? "s" : string.Empty)} Apt {apartment}"
                    : $"{district} W{ward} P{plot}{(room > 0 ? $" Rm {room}" : string.Empty)}";
            }

            var description = string.Empty;
            if (v.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.Array)
                description = string.Join(" ", desc.EnumerateArray().Select(d => d.GetString() ?? string.Empty).Where(d => d.Length > 0));

            var tags = string.Empty;
            if (v.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
                tags = string.Join(" / ", tagsEl.EnumerateArray().Select(t => t.GetString() ?? string.Empty).Where(t => t.Length > 0).Take(4));

            if (world.Length == 0)
                continue;

            var bannerUri = Str(v, "bannerUri");
            this.banners.TryGetValue(id, out var banner);

            var row = new VenueRow(id, name, location, world, description, tags, openNow, start, end, banner);

            if (openNow)
            {
                open.Add((end ?? utc.AddHours(12), row));
                if (bannerUri.Length > 0) bannerUris.Add((id, bannerUri));
            }
            else if (start is { } s && s > utc && s < utc.AddHours(24))
            {
                soon.Add((s, row));
                if (bannerUri.Length > 0) bannerUris.Add((id, bannerUri));
            }
        }

        return (
            open.OrderBy(o => o.Key).Select(o => o.Row).ToList(),
            soon.OrderBy(o => o.Key).Select(o => o.Row).Take(30).ToList(),
            bannerUris);
    }

    /// <summary>Fetches and decodes a banner to the channel's size, once, on a worker.</summary>
    private void LoadBanner(string id, string uri)
    {
        lock (this.banners)
        {
            if (this.banners.ContainsKey(id) || !this.bannersLoading.Add(id))
                return;
        }

        _ = Task.Run(async () =>
        {
            uint[]? pixels = null;
            try
            {
                var bytes = await this.http.GetByteArrayAsync(uri);
                using var image = Image.Load<Rgba32>(bytes);
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(VenuesChannel.BannerWidth, VenuesChannel.BannerHeight),
                    Mode = ResizeMode.Crop,
                }));

                var rgba = new Rgba32[VenuesChannel.BannerWidth * VenuesChannel.BannerHeight];
                image.CopyPixelDataTo(rgba);

                // ImageSharp's Rgba32 is R,G,B,A in memory, the same order libvlc uses, so the
                // packed value reads straight into the frame.
                pixels = new uint[rgba.Length];
                for (var i = 0; i < rgba.Length; i++)
                    pixels[i] = rgba[i].PackedValue | 0xFF000000u;
            }
            catch (Exception ex)
            {
                this.log.Debug($"[venues] banner for {id} failed: {ex.Message}");
            }

            lock (this.banners)
            {
                this.banners[id] = pixels;
                this.bannersLoading.Remove(id);
                this.bannerOrder.Enqueue(id);
                while (this.bannerOrder.Count > BannerCacheLimit)
                    this.banners.Remove(this.bannerOrder.Dequeue());
            }
        });
    }

    private static string RegionName(uint region) => region switch
    {
        1 => "Japan",
        2 => "North America",
        3 => "Europe",
        4 => "Oceania",
        _ => string.Empty,
    };

    /// <summary>Datacenter name to region id, from the game's own table, so a new datacenter needs no code.</summary>
    private Dictionary<string, uint> ReadDataCenterRegions()
    {
        var map = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var dc in this.dataManager.GetExcelSheet<Lumina.Excel.Sheets.WorldDCGroupType>())
            {
                var name = dc.Name.ToString();
                if (name.Length > 0)
                    map[name] = dc.Region.RowId;
            }
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "[venues] could not read the datacenter table.");
        }

        return map;
    }

    private static string Str(JsonElement e, string property) =>
        e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static int Num(JsonElement e, string property) =>
        e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
}
