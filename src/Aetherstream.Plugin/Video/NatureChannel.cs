namespace Aetherstream.Plugin.Video;

/// <summary>A creature from the hunting log: its name, where it lives, and its portrait.</summary>
internal sealed record Creature(string Name, string Zone, string Region, uint Icon, int Rank);

/// <summary>
/// The wildlife show. A moogle in a bush hat and a khaki vest, more enthusiasm than sense, and a
/// creature from the hunting log every episode: the approach through its country, the close-up,
/// the moment he gets far too close, and the sign-off. Backdrops by the creature's region;
/// the sky by the local hour. On a schedule from the clock.
/// </summary>
internal sealed class NatureChannel(BitmapFont font, Func<IReadOnlyList<Creature>> creatures, Func<uint, IconPixels?> icon) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const int Horizon = 400;
    private const int Ground = 640;

    private const double TitlesFor = 6.0;
    private const double ApproachFor = 12.0;
    private const double CloseFor = 9.0;
    private const double WrangleFor = 11.0;
    private const double SignOffFor = 8.0;
    private const double EpisodeFor = TitlesFor + ApproachFor + CloseFor + WrangleFor + SignOffFor;

    private static readonly uint Khaki = Canvas.Rgb(0xC8, 0xB0, 0x78);
    private static readonly uint Ink = Canvas.Rgb(0x1E, 0x1A, 0x14);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Danger = Canvas.Rgb(0xE2, 0x4B, 0x4A);

    private static readonly string[] Approach =
    [
        "Crikey! Look at this. We are in {zone}, and somewhere out here is a {name}.",
        "Now, you have to be quiet in {zone}. The {name} has ears like you would not believe.",
        "Right, we are going in. {zone} is {name} country, and that is exactly where we want to be.",
        "Isn't {zone} gorgeous? And what makes it gorgeous is what lives in it. Today: the {name}.",
        "Keep low. A {name} does not like surprises, and neither do I, but here we are.",
    ];

    private static readonly string[] Close =
    [
        "Isn't she gorgeous? Look at the size of her. A {name}, in the wild, in {zone}.",
        "What a beauty. The {name} is one of {zone}'s absolute treasures, and people just walk past it.",
        "Look at the colours on this one. A {name}. You do not see that every day, and I say that every day.",
        "This is a {name}. Absolutely magnificent. And absolutely not to be touched, which brings me to my next point.",
        "Danger, danger, danger. That is a {name}, and that is as close as sensible people get.",
    ];

    private static readonly string[] Wrangle =
    [
        "I am just going to get a little bit closer. Woah! She is feisty! Good on ya!",
        "Now, if I just... crikey! She has got a bit of go in her! Beautiful!",
        "Watch this. Watch this. Ohh, she does not like that at all! What a champion!",
        "Careful now... she is coming round... she is coming ROUND. RUN. Ha ha! Gorgeous!",
        "Gently, gently... nope! No! She has had enough of me, and fair enough!",
    ];

    private static readonly string[] SignOff =
    [
        "What an animal. The {name}: leave her be, and she will leave you be. Mostly. See you next time.",
        "That is the {name}, and that is {zone}. Look after them both. I am off to find a healer.",
        "Crikey, what a day. Remember: if it has teeth, it has a reason. Until next time!",
        "The {name}. Remember the name, respect the name, and for the love of Nophica, keep your distance.",
    ];

    private static readonly string[] Facts =
    [
        "HUNTING LOG RANK {rank}",
        "FOUND IN {zone}",
        "REGION: {region}",
        "TEMPERAMENT: NOT GREAT",
        "ADVICE: DO NOT",
    ];

    private enum Phase
    {
        Titles,
        Approach,
        Close,
        Wrangle,
        SignOff,
    }

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var list = creatures();

        if (list.Count == 0)
        {
            Canvas.Fill(span, 0, 0, W, H, Ink);
            const string None = "THE HUNTING LOG IS EMPTY";
            font.Draw(span, W, None, (W - font.Measure(None, 2)) / 2, 330, Cream, 2, all);
            return;
        }

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var episode = (int)((unix - 1_700_000_000) / (long)EpisodeFor);
        var into = (unix - 1_700_000_000) % (long)EpisodeFor;
        var creature = list[Pick(episode, list.Count)];
        var next = list[Pick(episode + 1, list.Count)];
        var biome = Scenery.BiomeOf(creature.Region, creature.Zone);

        var hour = now.Hour + (now.Minute / 60f);
        var daylight = biome == Biome.Night ? 0.1f : Math.Clamp(1f - (Math.Abs(hour - 13f) / 7.5f), 0f, 1f);

        var (phase, t) = into < TitlesFor ? (Phase.Titles, into)
            : into < TitlesFor + ApproachFor ? (Phase.Approach, into - TitlesFor)
            : into < TitlesFor + ApproachFor + CloseFor ? (Phase.Close, into - TitlesFor - ApproachFor)
            : into < TitlesFor + ApproachFor + CloseFor + WrangleFor ? (Phase.Wrangle, into - TitlesFor - ApproachFor - CloseFor)
            : (Phase.SignOff, into - TitlesFor - ApproachFor - CloseFor - WrangleFor);

        var seed = episode * 31;
        var pan = phase == Phase.Approach ? t * 30.0 : phase == Phase.Titles ? 0 : ApproachFor * 30.0;
        Scenery.Paint(span, W, 0, Horizon, Ground, biome, daylight, seed, pan);

        // The creature stands in the middle distance: its portrait on a shadow, breathing.
        var breathe = (int)(Math.Sin(seconds * 2.0) * 3);
        var creatureX = 820;
        var creatureY = Horizon + 40;
        if (phase != Phase.Titles)
        {
            var shown = phase != Phase.Approach || t > 4.0;
            if (shown)
            {
                Canvas.Disc(span, creatureX + 70, creatureY + 150, 90, Canvas.Lerp(Canvas.Black, Canvas.Rgb(0x40, 0x40, 0x40), 0.5f));
                var bump = phase == Phase.Wrangle ? (int)(Math.Sin(seconds * 14.0) * 10) : 0;
                var lunge = phase == Phase.Wrangle && t > 6.0 ? (int)((t - 6.0) * -60) : 0;
                this.DrawIcon(span, creature.Icon, creatureX + bump + lunge, creatureY - breathe, 140);
            }
        }

        // The ranger.
        switch (phase)
        {
            case Phase.Titles:
                HostSprites.DrawRanger(span, HostSprites.Ranger.Stand, seconds, 200, Ground - 238, 7);
                break;
            case Phase.Approach:
            {
                var x = 120 + (int)(Math.Min(t, 9.0) / 9.0 * 380);
                HostSprites.DrawRanger(span, t < 9.0 ? HostSprites.Ranger.Creep : HostSprites.Ranger.Point, seconds, x, Ground - 238, 7);
                break;
            }

            case Phase.Close:
                HostSprites.DrawRanger(span, HostSprites.Ranger.Point, seconds, 500, Ground - 238, 7);
                break;
            case Phase.Wrangle:
                if (t < 6.0)
                {
                    var x = 500 + (int)(t / 6.0 * 160);
                    HostSprites.DrawRanger(span, HostSprites.Ranger.Grab, seconds, x, Ground - 238, 7);
                }
                else
                {
                    var x = 660 - (int)((t - 6.0) * 220);
                    HostSprites.DrawRanger(span, HostSprites.Ranger.Run, seconds, x, Ground - 238, 7, flip: true);
                }

                break;
            default:
                HostSprites.DrawRanger(span, HostSprites.Ranger.Stand, seconds, 200, Ground - 238, 7);
                break;
        }

        // The close-up inset: the portrait big with a fact card, cut in for the middle of the close phase.
        if (phase == Phase.Close && t is > 1.5 and < 7.5)
        {
            Scenery.Paint(span, W, 0, Horizon + 120, H, biome, daylight, seed + 5, pan * 2);
            this.DrawIcon(span, creature.Icon, 120, 120, 400);
            var y = 130;
            var name = Canvas.Cut(Canvas.Plain(creature.Name).ToUpperInvariant(), font.Fit(W - 600, 2));
            Canvas.Fill(span, 560, y - 8, font.Measure(name, 2) + 32, 96, Ink);
            font.Draw(span, W, name, 576, y, Cream, 2, all);
            y += 120;
            foreach (var fact in Facts)
            {
                var text = fact.Replace("{rank}", creature.Rank.ToString()).Replace("{zone}", creature.Zone.ToUpperInvariant()).Replace("{region}", creature.Region.ToUpperInvariant());
                text = Canvas.Cut(text, font.Fit(W - 600));
                Canvas.Fill(span, 560, y - 4, font.Measure(text) + 32, 44, Ink);
                font.Draw(span, W, text, 576, y, fact.StartsWith("ADVICE") ? Danger : Cream, 1, all);
                y += 52;
            }
        }

        // Titles and sign-off cards.
        if (phase == Phase.Titles)
        {
            Dim(span, 0.45f);
            const string Title = "WILD EORZEA";
            var tw = font.Measure(Title, 2) + 64;
            Canvas.Fill(span, (W - tw) / 2, 160, tw, 112, Khaki);
            font.Draw(span, W, Title, (W - font.Measure(Title, 2)) / 2, 176, Ink, 2, all);
            const string With = "WITH STEPPE IRWYN, THE WIVRE WRANGLER, KUPO";
            font.Draw(span, W, With, (W - font.Measure(With)) / 2, 300, Cream, 1, all);
            if (t > 2.5)
            {
                var today = Canvas.Cut($"THIS WEEK: THE {Canvas.Plain(creature.Name).ToUpperInvariant()} OF {creature.Zone.ToUpperInvariant()}", font.Fit(W - 100));
                font.Draw(span, W, today, (W - font.Measure(today)) / 2, 360, Khaki, 1, all);
            }
        }
        else if (phase == Phase.SignOff && t > 4.0)
        {
            var nextText = Canvas.Cut($"NEXT WEEK: THE {Canvas.Plain(next.Name).ToUpperInvariant()} OF {next.Zone.ToUpperInvariant()}", font.Fit(W - 100));
            Canvas.Fill(span, (W - font.Measure(nextText) - 32) / 2, 90, font.Measure(nextText) + 32, 48, Ink);
            font.Draw(span, W, nextText, (W - font.Measure(nextText)) / 2, 94, Khaki, 1, all);
        }

        // The narration in a lower-third, and a location bug.
        var line = phase switch
        {
            Phase.Titles => string.Empty,
            Phase.Approach => Approach[Pick(episode * 3, Approach.Length)],
            Phase.Close => Close[Pick(episode * 5, Close.Length)],
            Phase.Wrangle => Wrangle[Pick(episode * 7, Wrangle.Length)],
            _ => SignOff[Pick(episode * 11, SignOff.Length)],
        };
        if (line.Length > 0)
        {
            line = line.Replace("{name}", Canvas.Plain(creature.Name)).Replace("{zone}", creature.Zone);
            this.DrawLowerThird(span, phase == Phase.Wrangle && t > 6.0 ? "DANGER" : "STEPPE", line, phase == Phase.Wrangle && t > 6.0, all);
        }

        if (phase != Phase.Titles)
        {
            var bug = Canvas.Cut(creature.Zone.ToUpperInvariant(), 28);
            Canvas.Fill(span, 24, 70, font.Measure(bug) + 24, 40, Ink);
            font.Draw(span, W, bug, 36, 70, Cream, 1, all);
        }

        Canvas.Fill(span, 0, 676, W, 4, Ink);
        Canvas.Fill(span, 0, 676, (int)(W * (into / EpisodeFor)), 4, Khaki);
        Canvas.Fill(span, 0, 680, W, 40, Ink);
        font.Draw(span, W, "AETHERSTREAM WILDLIFE", 24, 680, Khaki, 1, all);
        var right = Canvas.Cut($"EP {episode % 10000}  /  {Canvas.Plain(creature.Name).ToUpperInvariant()}", 50);
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Cream, 1, all);
    }

    private void DrawLowerThird(Span<uint> span, string tab, string line, bool danger, in BitmapFont.Clip all)
    {
        const int Top = 556;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - 40 - font.Measure(tab) - 48), 2);
        var height = 16 + (lines.Count * 40);
        Canvas.Fill(span, 40, Top, W - 80, height, Ink);
        Canvas.Fill(span, 40, Top, W - 80, 4, danger ? Danger : Khaki);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, Top + 4, tabW, height - 4, danger ? Danger : Khaki);
        font.Draw(span, W, tab, 56, Top + 8, Ink, 1, all);
        var y = Top + 8;
        foreach (var l in lines)
        {
            font.Draw(span, W, l, 40 + tabW + 16, y, Cream, 1, all);
            y += 40;
        }
    }

    private static void Dim(Span<uint> span, float amount)
    {
        for (var i = 0; i < span.Length; i++)
            span[i] = Canvas.Lerp(span[i], 0xFF000000u, amount);
    }

    private void DrawIcon(Span<uint> span, uint iconId, int x, int y, int size)
    {
        if (icon(iconId) is not { } px)
        {
            Canvas.Rect(span, x, y, size, size, Ink);
            return;
        }

        for (var ty = 0; ty < size; ty++)
        {
            var yy = y + ty;
            if (yy < 0 || yy >= H)
                continue;

            var sy = ty * px.Height / size;
            for (var tx = 0; tx < size; tx++)
            {
                var xx = x + tx;
                if (xx < 0 || xx >= W)
                    continue;

                var p = px.Pixels[(sy * px.Width) + (tx * px.Width / size)];
                var a = (int)(p >> 24);
                if (a == 0)
                    continue;

                var i = (yy * W) + xx;
                span[i] = a >= 250 ? p | 0xFF000000u : Canvas.Lerp(span[i], p | 0xFF000000u, a / 255f);
            }
        }
    }

    private static int Pick(int seed, int count)
    {
        var x = (uint)seed * 2654435761u;
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
