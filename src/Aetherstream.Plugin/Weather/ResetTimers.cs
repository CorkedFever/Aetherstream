namespace Aetherstream.Plugin.Weather;

/// <summary>
/// Everything in the game that runs on the real-world clock: the resets, the cactpot draw, and
/// the ocean fishing boat. All of it is arithmetic on UTC; none of it needs the game running.
/// </summary>
internal static class ResetTimers
{
    /// <summary>Daily reset: 15:00 UTC. Beast tribe allowances, daily hunts, mini cactpot tickets.</summary>
    public static DateTime NextDaily(DateTime utc) => NextAt(utc, 15, 0);

    /// <summary>Grand Company reset: 20:00 UTC. Supply and provisioning missions.</summary>
    public static DateTime NextGrandCompany(DateTime utc) => NextAt(utc, 20, 0);

    /// <summary>Weekly reset: Tuesday 08:00 UTC. Tomestones, raids, deliveries, Wondrous Tails, the challenge log.</summary>
    public static DateTime NextWeekly(DateTime utc) => NextWeeklyAt(utc, DayOfWeek.Tuesday, 8, 0);

    /// <summary>
    /// The Jumbo Cactpot draw, once a week per region at a fixed UTC moment — the wikis quote it
    /// as "6 PM PST / 7 PM PDT" and so on, which is one UTC time seen through daylight saving:
    /// Japan Saturday 12:00, Europe Saturday 19:00, Oceania Saturday 09:00, North America
    /// Sunday 02:00. Region ids are the game's datacenter table: 1 Japan, 2 North America,
    /// 3 Europe, 4 Oceania.
    /// </summary>
    public static DateTime? NextJumboCactpot(DateTime utc, uint region) => region switch
    {
        1 => NextWeeklyAt(utc, DayOfWeek.Saturday, 12, 0),
        2 => NextWeeklyAt(utc, DayOfWeek.Sunday, 2, 0),
        3 => NextWeeklyAt(utc, DayOfWeek.Saturday, 19, 0),
        4 => NextWeeklyAt(utc, DayOfWeek.Saturday, 9, 0),
        _ => null,
    };

    public readonly record struct Voyage(DateTime DepartsUtc, string IndigoDestination, string IndigoTime, string RubyDestination, string RubyTime);

    // Ocean fishing, as the community schedule tools compute it: boats leave every two hours at
    // odd hours JST; each route cycles its destinations in order and its times of day in blocks,
    // and the first boat of a JST day skips one, so the pattern repeats every twelve days.
    private static readonly string[] IndigoDestinations = ["The Bloodbrine Sea", "The Rothlyt Sound", "The Northern Strait of Merlthor", "The Rhotano Sea"];
    private static readonly string[] IndigoTimes = ["Sunset", "Sunset", "Sunset", "Sunset", "Night", "Night", "Night", "Night", "Day", "Day", "Day", "Day"];
    private static readonly string[] RubyDestinations = ["The One River", "The Ruby Sea"];
    private static readonly string[] RubyTimes = ["Day", "Day", "Sunset", "Sunset", "Night", "Night"];

    private static readonly DateTime OceanEpoch = new(2020, 6, 28, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Jst = TimeSpan.FromHours(9);
    private static readonly TimeSpan Registration = TimeSpan.FromMinutes(15);

    /// <summary>The next boat after a moment, with what each route sails to.</summary>
    public static Voyage NextVoyage(DateTime utc)
    {
        // Work in JST, shifted back fifteen minutes so a boat that is still boarding counts as next.
        var shifted = utc + Jst - Registration;
        var day = (int)Math.Floor((shifted - OceanEpoch).TotalDays);
        var hour = shifted.Hour;
        hour += (hour & 1) == 0 ? 1 : 2;
        if (hour > 23)
        {
            day += 1;
            hour -= 24;
        }

        var slot = hour >> 1;
        var departsJst = OceanEpoch.AddDays(day).AddHours(hour);
        var departsUtc = departsJst - Jst;

        string Pick(string[] table) => table[(((day + slot) % table.Length) + table.Length) % table.Length];

        return new Voyage(
            DateTime.SpecifyKind(departsUtc, DateTimeKind.Utc),
            Pick(IndigoDestinations),
            Pick(IndigoTimes),
            Pick(RubyDestinations),
            Pick(RubyTimes));
    }

    private static DateTime NextAt(DateTime utc, int hour, int minute)
    {
        var candidate = new DateTime(utc.Year, utc.Month, utc.Day, hour, minute, 0, DateTimeKind.Utc);
        return candidate > utc ? candidate : candidate.AddDays(1);
    }

    private static DateTime NextWeeklyAt(DateTime utc, DayOfWeek day, int hour, int minute)
    {
        var daysAhead = ((int)day - (int)utc.DayOfWeek + 7) % 7;
        var candidate = new DateTime(utc.Year, utc.Month, utc.Day, hour, minute, 0, DateTimeKind.Utc).AddDays(daysAhead);
        return candidate > utc ? candidate : candidate.AddDays(7);
    }
}
