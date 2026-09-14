namespace Aetherstream.Plugin.Video;

/// <summary>A creature from the hunting log: its name, where it lives, and its portrait.</summary>
internal sealed record Creature(string Name, string Zone, string Region, uint Icon, int Rank);

/// <summary>
/// The wildlife show. A moogle in a bush hat and a khaki vest, Stevie Mogwyn, more enthusiasm than sense, and a
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
    private const int HoverY = 290;

    private const double TitlesFor = 6.0;
    private const double ApproachFor = 12.0;
    private const double CloseFor = 9.0;
    private const double WrangleFor = 11.0;
    private const double SignOffFor = 8.0;
    private const double BitFor = 14.0;
    private const double EpisodeFor = TitlesFor + ApproachFor + CloseFor + WrangleFor + SignOffFor + BitFor;

    private static readonly uint Khaki = Canvas.Rgb(0xC8, 0xB0, 0x78);
    private static readonly uint Ink = Canvas.Rgb(0x1E, 0x1A, 0x14);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Danger = Canvas.Rgb(0xE2, 0x4B, 0x4A);

    private static readonly string[] Approach =
    [
        "Kupo! Look at this. We are in {zone}, and somewhere out here is a {name}, kupo.",
        "Now, you have to be quiet in {zone}, kupo. The {name} has ears like you would not believe. Bigger than mine!",
        "Right, we are going in, kupo. {zone} is {name} country, and that is exactly where a moogle wants to be.",
        "Isn't {zone} gorgeous, kupo? And what makes it gorgeous is what lives in it. Today: the {name}!",
        "Keep low, kupo. A {name} does not like surprises, and neither does my pom, but here we are.",
    ];

    private static readonly string[] Close =
    [
        "Isn't she gorgeous, kupo? Look at the size of her! A {name}, in the wild, in {zone}.",
        "What a beauty, kupo. The {name} is one of {zone}'s absolute treasures, and people just walk past it.",
        "Look at the colours on this one, kupo! A {name}. You do not see that every day, and I say that every day.",
        "This is a {name}, kupo. Absolutely magnificent. And absolutely not to be touched, which brings me to my next point.",
        "Danger, danger, danger, kupo! That is a {name}, and that is as close as sensible moogles get.",
    ];

    private static readonly string[] Wrangle =
    [
        "I am just going to get a little bit closer, kupo. Woah! She is feisty! Good on ya, kupo!",
        "Now, if I just... KUPO! She has got a bit of go in her! Beautiful!",
        "Watch this, watch this. Ohh, she does not like that at all, kupo! What a champion!",
        "Careful now... she is coming round... she is coming ROUND. KUPOOO! Ha ha! Gorgeous!",
        "Gently, gently... nope! Kupo! She has had enough of me, and fair enough, kupo!",
    ];

    private static readonly string[] SignOff =
    [
        "What an animal, kupo. The {name}: leave her be, and she will leave you be. Mostly. See you next time, kupo!",
        "That is the {name}, and that is {zone}, kupo. Look after them both. I am off to find a healer, kupo.",
        "Kupo, what a day! Remember: if it has teeth, it has a reason. Until next time, kupo!",
        "The {name}, kupo. Remember the name, respect the name, and for the love of the Mogfather, keep your distance.",
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
        Bit,
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
            : into < TitlesFor + ApproachFor + CloseFor + WrangleFor + SignOffFor ? (Phase.SignOff, into - TitlesFor - ApproachFor - CloseFor - WrangleFor)
            : (Phase.Bit, into - TitlesFor - ApproachFor - CloseFor - WrangleFor - SignOffFor);

        var seed = episode * 31;
        if (phase == Phase.Bit)
        {
            this.DrawBit(span, episode, creature, next, biome, daylight, seed, t, seconds, all);
            this.DrawFooter(span, episode, creature, into, all);
            return;
        }

        var pan = phase == Phase.Approach ? t * 30.0 : phase == Phase.Titles ? 0 : ApproachFor * 30.0;
        Scenery.Paint(span, W, 0, Horizon, Ground, biome, daylight, seed, pan);

        // The creature stands in the middle distance: its portrait on a shadow, breathing.
        var breathe = (int)(Math.Sin(seconds * 2.0) * 3);
        var creatureX = 820;
        var creatureY = Horizon - 20;
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
                HostSprites.DrawRanger(span, HostSprites.Ranger.Stand, seconds, 200, HoverY + (int)(Math.Sin(seconds * 2.4) * 8), 7);
                break;
            case Phase.Approach:
            {
                var x = 120 + (int)(Math.Min(t, 9.0) / 9.0 * 380);
                HostSprites.DrawRanger(span, t < 9.0 ? HostSprites.Ranger.Creep : HostSprites.Ranger.Point, seconds, x, HoverY + (int)(Math.Sin(seconds * 2.4) * 8), 7);
                break;
            }

            case Phase.Close:
                HostSprites.DrawRanger(span, HostSprites.Ranger.Point, seconds, 500, HoverY + (int)(Math.Sin(seconds * 2.4) * 8), 7);
                break;
            case Phase.Wrangle:
                if (t < 6.0)
                {
                    var x = 500 + (int)(t / 6.0 * 160);
                    HostSprites.DrawRanger(span, HostSprites.Ranger.Grab, seconds, x, HoverY + (int)(Math.Sin(seconds * 2.4) * 8), 7);
                }
                else
                {
                    var x = 660 - (int)((t - 6.0) * 220);
                    HostSprites.DrawRanger(span, HostSprites.Ranger.Run, seconds, x, HoverY + (int)(Math.Sin(seconds * 2.4) * 8), 7, flip: true);
                }

                break;
            default:
                HostSprites.DrawRanger(span, HostSprites.Ranger.Stand, seconds, 200, HoverY + (int)(Math.Sin(seconds * 2.4) * 8), 7);
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
            const string With = "WITH STEVIE MOGWYN, THE WIVRE WRANGLER, KUPO";
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
            this.DrawLowerThird(span, phase == Phase.Wrangle && t > 6.0 ? "DANGER" : "MOGWYN", line, phase == Phase.Wrangle && t > 6.0, all);
        }

        if (phase != Phase.Titles)
        {
            var bug = Canvas.Cut(creature.Zone.ToUpperInvariant(), 28);
            Canvas.Fill(span, 24, 70, font.Measure(bug) + 24, 40, Ink);
            font.Draw(span, W, bug, 36, 70, Cream, 1, all);
        }

        this.DrawFooter(span, episode, creature, into, all);
    }

    private void DrawFooter(Span<uint> span, int episode, Creature creature, double into, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 676, W, 4, Ink);
        Canvas.Fill(span, 0, 676, (int)(W * (into / EpisodeFor)), 4, Khaki);
        Canvas.Fill(span, 0, 680, W, 40, Ink);
        font.Draw(span, W, "AETHERSTREAM WILDLIFE", 24, 680, Khaki, 1, all);
        var right = Canvas.Cut($"EP {episode % 10000}  /  {Canvas.Plain(creature.Name).ToUpperInvariant()}", 50);
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Cream, 1, all);
    }

    // -- between episodes ----------------------------------------------------------------------------------------

    private static readonly string[] NoteLines =
    [
        "{name}: BIGGER THAN IT LOOKS. IT ALWAYS IS, KUPO.",
        "FOUND IN {zone}. DO NOT ASK HOW I KNOW.",
        "TEMPERAMENT: HAS ONE.",
        "DIET: NOT MOOGLES. (TESTED.)",
        "APPROACH FROM DOWNWIND. THERE IS NO DOWNWIND.",
        "POM STATUS AFTER FILMING: RUFFLED.",
        "WOULD I GO BACK? KUPO, I AM ALREADY PACKING.",
        "NOTE TO SELF: THE {name} CAN JUMP.",
    ];

    private static readonly (string Question, string Answer)[] Letters =
    [
        ("DEAR MOGWYN, WHY DO YOU GET SO CLOSE?", "Because from far away, kupo, they all look like rocks. And the rocks are not the interesting bit."),
        ("DEAR MOGWYN, DOES YOUR POM HURT WHEN IT GETS BITTEN?", "Only when I think about it. So I try to think about the animal instead, kupo. Isn't she gorgeous?"),
        ("DEAR MOGWYN, WHAT IS THE MOST DANGEROUS THING IN EORZEA?", "A closed gate with a sign on it, kupo. Everything worth seeing is on the other side."),
        ("DEAR MOGWYN, HAVE YOU EVER BEEN SCARED?", "Constantly, kupo! That is how you know you are looking at something real."),
        ("DEAR MOGWYN, DO YOU HAVE A FAVOURITE?", "Whichever one is in front of me, kupo. Ask me again next episode."),
        ("DEAR MOGWYN, WHERE DO YOU GET THE HATS?", "There is only the one hat, kupo. It has been through a lot. We do not talk about the hat."),
    ];

    /// <summary>
    /// The bit between episodes, so the show is more than the hunt: field notes in his own hand,
    /// a snack on a rock, the injury tally, a letter from a viewer, or a chase after the last
    /// beast when it turns out not to have gone far.
    /// </summary>
    private void DrawBit(Span<uint> span, int episode, Creature creature, Creature next, Biome biome, float daylight, int seed, double t, double seconds, in BitmapFont.Clip all)
    {
        var bob = (int)(Math.Sin(seconds * 2.4) * 8);
        var name = Canvas.Plain(creature.Name);
        switch (Pick(episode * 13, 5))
        {
            case 0:
            {
                // Field notes: a page from the notebook, filled in line by line, the beast sketched in the corner.
                Scenery.Paint(span, W, 0, Horizon, Ground, biome, daylight, seed, 0);
                Canvas.Fill(span, 200, 90, 880, 470, Cream);
                Canvas.Fill(span, 200, 90, 880, 470, Canvas.Lerp(Cream, Canvas.Rgb(0xE8, 0xD8, 0xB0), 0.4f));
                Canvas.Rect(span, 200, 90, 880, 470, Ink, 3);
                for (var r = 0; r < 9; r++)
                    Canvas.Fill(span, 230, 170 + (r * 44), 820, 2, Canvas.Rgb(0xC8, 0xB8, 0x98));
                Canvas.Fill(span, 300, 90, 4, 470, Canvas.Rgb(0xE0, 0x80, 0x80));
                for (var h = 0; h < 6; h++)
                    Canvas.Disc(span, 214, 130 + (h * 72), 8, Ink);
                font.Draw(span, W, "MOGWYN'S FIELD NOTES, KUPO", 320, 104, Ink, 1, all);
                var lines = (int)Math.Min(4, t / 2.5);
                for (var l = 0; l < lines; l++)
                {
                    var text = NoteLines[Pick((episode * 5) + l, NoteLines.Length)].Replace("{name}", name.ToUpperInvariant()).Replace("{zone}", creature.Zone.ToUpperInvariant());
                    text = Canvas.Cut(text, font.Fit(720));
                    // The line being written appears letter by letter.
                    if (l == lines - 1)
                        text = text[..Math.Min(text.Length, (int)((t - (l * 2.5)) / 2.5 * text.Length * 1.3))];
                    font.Draw(span, W, text, 320, 176 + (l * 44), Canvas.Rgb(0x2A, 0x3A, 0x6A), 1, all);
                }

                Canvas.Rect(span, 860, 360, 190, 170, Ink, 3);
                this.DrawIcon(span, creature.Icon, 885, 375, 140);
                HostSprites.DrawRanger(span, HostSprites.Ranger.Point, seconds, 20, 330 + bob, 6);
                break;
            }

            case 1:
            {
                // Snack break: on a rock, a kupo nut going down in bites.
                Scenery.Paint(span, W, 0, Horizon, Ground, biome, daylight, seed, 0);
                Canvas.Disc(span, 620, Ground - 40, 110, Ink);
                Canvas.Disc(span, 700, Ground - 20, 80, Ink);
                var bites = (int)(t / 2.5);
                var nutR = Math.Max(0, 44 - (bites * 10));
                var chew = (int)(t % 2.5 * 8) % 2 == 0 ? 0 : 4;
                HostSprites.DrawRanger(span, HostSprites.Ranger.Stand, seconds, 520, Ground - 40 - 250 + chew, 7);
                if (nutR > 0)
                {
                    Canvas.Disc(span, 700, Ground - 150, nutR, Canvas.Rgb(0xC0, 0x60, 0x40));
                    Canvas.Disc(span, 700 - (nutR / 3), Ground - 150 - (nutR / 3), nutR / 3, Canvas.Rgb(0xE0, 0x90, 0x60));
                    Canvas.Fill(span, 698, Ground - 150 - nutR - 12, 4, 14, Canvas.Rgb(0x4A, 0x8A, 0x3A));
                }

                var crumbs = Math.Min(12, bites * 4);
                for (var i = 0; i < crumbs; i++)
                    Canvas.Disc(span, 640 + ((i * 37) % 120), Ground - 46 - ((i * 13) % 20), 3, Canvas.Rgb(0xC0, 0x60, 0x40));
                this.DrawLowerThird(span, "SNACK", nutR > 0 ? "Snack break, kupo. Kupo nuts: the only thing out here that does not bite back." : "That was the last one. Right. Where were we, kupo?", false, all);
                break;
            }

            case 2:
            {
                // The injury tally: bandaged, counting on a board.
                Scenery.Paint(span, W, 0, Horizon, Ground, biome, daylight, seed, 0);
                Canvas.Fill(span, 700, 120, 420, 340, Canvas.Rgb(0x2A, 0x3A, 0x2E));
                Canvas.Rect(span, 700, 120, 420, 340, Canvas.Rgb(0x8A, 0x6A, 0x3A), 6);
                font.Draw(span, W, "INJURIES THIS SEASON", 724, 136, Cream, 1, all);
                var count = 7 + (episode % 9) + (t > 6.0 ? 1 : 0);
                for (var i = 0; i < count; i++)
                {
                    var g = i / 5;
                    var k = i % 5;
                    var x = 740 + ((g % 4) * 90) + (k * 14);
                    var y = 200 + ((g / 4) * 70);
                    if (k < 4)
                        Canvas.Fill(span, x, y, 4, 40, Cream);
                    else
                        Canvas.Line(span, x - 60, y + 36, x + 4, y + 4, Cream);
                }

                if (t > 6.0 && t < 6.6)
                    Canvas.Fill(span, 700, 120, 420, 340, Canvas.Lerp(Canvas.White, Canvas.Rgb(0x2A, 0x3A, 0x2E), 0.5f));
                HostSprites.DrawRanger(span, HostSprites.Ranger.Stand, seconds, 260, HoverY + bob, 7);
                // Bandages: strips across the body and one over the pom.
                Canvas.Fill(span, 300, HoverY + bob + 150, 120, 12, Cream);
                Canvas.Fill(span, 320, HoverY + bob + 176, 90, 10, Cream);
                Canvas.Fill(span, 350, HoverY + bob - 6, 40, 8, Cream);
                Canvas.Fill(span, 318, HoverY + bob + 100, 48, 8, Cream);
                this.DrawLowerThird(span, "TALLY", t > 6.0 ? $"...and one more, from the {name}. They were all worth it, kupo. Mostly." : "Let us see. The claws, the tail, the tail again, the thing with the teeth, kupo...", false, all);
                break;
            }

            case 3:
            {
                // A letter from a viewer, read out and answered.
                Scenery.Paint(span, W, 0, Horizon, Ground, biome, daylight, seed, 0);
                var (q, a) = Letters[Pick(episode * 7, Letters.Length)];
                var open = Math.Clamp(t / 1.5, 0.0, 1.0);
                Canvas.Fill(span, 560, 120, 560, 300, Cream);
                Canvas.Rect(span, 560, 120, 560, 300, Ink, 3);
                for (var i = 0; i < (int)(280 * (1.0 - open)); i++)
                    Canvas.Fill(span, 560 + (i / 2) + 1, 120 + i, 560 - i - 2, 1, Canvas.Lerp(Cream, Canvas.Rgb(0xD8, 0xC8, 0xA0), 0.5f));
                if (open >= 1.0)
                {
                    font.Draw(span, W, "A LETTER FROM A VIEWER", 584, 134, Canvas.Rgb(0x8A, 0x3A, 0x2A), 1, all);
                    var y = 190;
                    foreach (var l in Canvas.Wrap(q, font.Fit(520), 3))
                    {
                        font.Draw(span, W, l, 584, y, Ink, 1, all);
                        y += 40;
                    }
                }

                HostSprites.DrawRanger(span, t < 1.5 ? HostSprites.Ranger.Stand : HostSprites.Ranger.Point, seconds, 220, HoverY + bob, 7);
                if (t > 4.0)
                    this.DrawLowerThird(span, "MOGWYN", a, false, all);
                break;
            }

            default:
            {
                // The chase: the beast turns out not to have gone far.
                Scenery.Paint(span, W, 0, Horizon, Ground, biome, daylight, seed, t * 80.0);
                var bx = -200 + (int)(t * 130);
                var hop = (int)(Math.Abs(Math.Sin(t * 8.0)) * 20);
                Canvas.Disc(span, bx + 70, Horizon + 140, 60, Canvas.Lerp(Canvas.Black, Canvas.Rgb(0x40, 0x40, 0x40), 0.5f));
                this.DrawIcon(span, creature.Icon, bx, Horizon - 10 - hop, 120);
                HostSprites.DrawRanger(span, HostSprites.Ranger.Run, seconds, bx - 260 + (int)(Math.Sin(t * 3.0) * 20), HoverY + bob, 7, flip: false);
                for (var i = 0; i < 6; i++)
                    Canvas.Disc(span, bx - 300 - (i * 30), Ground - 30 - ((i * 7) % 20), 6 - i, Canvas.Rgb(0xC8, 0xB0, 0x78));
                this.DrawLowerThird(span, "KUPO", t < 7.0 ? $"Get back here, kupo! I only want to measure you!" : $"Fine. FINE. We will find you next week, {name}. Probably.", t < 7.0, all);
                break;
            }
        }
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
