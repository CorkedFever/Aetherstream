namespace Aetherstream.Plugin.Video;

/// <summary>Something for sale in an advert: a marketable item and what kind of thing it is.</summary>
internal sealed record AdItem(uint Id, string Name, string Category);

/// <summary>
/// The commercial breaks. Every five minutes a show stops for forty-eight seconds of adverts:
/// a promo for another show, an item off the market board with a slogan written for it, a
/// trailer for a film that does not exist, a sponsor's card from one of the hosts' side
/// businesses, and a bumper each side. Wraps a show; the info and ambience channels are left
/// alone, because nobody wants an advert in the middle of a fireplace.
/// </summary>
internal sealed class CommercialBreak(BitmapFont font, Func<IReadOnlyList<(string Name, string Blurb)>> shows, Func<IReadOnlyList<AdItem>> items, Func<uint, IconPixels?> itemIcon)
{
    private const int W = Canvas.Width;

    private BitmapFont Font => font;
    private const int H = Canvas.Height;
    private const long Cycle = 300;
    private const long BreakFor = 48;
    private const double SpotFor = 12.0;

    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Ink = Canvas.Rgb(0x0C, 0x0A, 0x10);
    private static readonly uint Gold = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint Rose = Canvas.Rgb(0xFF, 0x6A, 0x9A);
    private static readonly uint Cyan = Canvas.Rgb(0x6A, 0xD8, 0xFF);
    private static readonly uint Mint = Canvas.Rgb(0x8A, 0xE8, 0xC0);
    private static readonly uint Faint = Canvas.Rgb(0x9A, 0x96, 0xA8);

    // Slogans, with the item's name and category dropped in. Written to fit anything.
    private static readonly string[] Slogans =
    [
        "{name}. You have gone this long without one. Why?",
        "Tired of not having {a}? So were we. Then we bought one.",
        "{name}: the {category} that started a conversation. Several, actually.",
        "Nine out of ten retainers recommend {name}. The tenth is on a venture.",
        "Is it {name}? Is it happiness? At this price, does it matter?",
        "{name}. Because your inventory has room. We checked. It does not, but still.",
        "Ask your local market board about {name}. It will not answer. Buy it anyway.",
        "Finally, {a} you can point at and say: that. That one. Mine.",
        "{name}. Available wherever {category}s are sold. Which is one place.",
        "This is {name}. This is not an advert. This is a warning that you want it.",
    ];

    private static readonly string[] Fine =
    [
        "Prices set by the market. Not by us. We would have gone higher.",
        "Not sold in stores. Sold in the other kind of store.",
        "Side effects may include owning it.",
        "No chocobos were consulted.",
        "Your retainer was not paid to say this.",
    ];

    // A film that does not exist: a title from two lists, a tagline, and a rating.
    private static readonly string[] TitleA = ["The Last", "A Fistful of", "Kupo", "Return of the", "Nights in", "The Wandering", "Ul'dah", "Twelve Angry", "Whispers of", "Mogwyn's", "The Thavnairian", "Escape from", "Once Upon a Time in", "Three Namazu and a"];
    private static readonly string[] TitleB = ["Chocobo", "Gil", "Nation", "Retainer", "Limsa", "Tonberry", "Confidential", "Moogles", "the Deep", "Revenge", "Job", "Ishgard", "La Noscea", "Baby"];
    private static readonly string[] Taglines =
    [
        "This Starlight, one hero. One chocobo. One very long fetch quest.",
        "They said it could not be crafted. They were mostly right.",
        "In a world without teleport tickets, one man walks.",
        "Some fish are worth waiting for. This one took eleven years.",
        "He had one job. It was a Disciple of the Hand.",
        "The market board giveth. The market board taketh away.",
        "Every family has a secret. Theirs is a free company chest.",
        "You have never seen Ul'dah like this. Mostly because of the fog.",
    ];
    private static readonly string[] Starring = ["a Namazu with a dream", "the Moogle who knew too much", "two Lalafells in a coat", "a very tired Roegadyn", "the goblin from Shopping", "an Elezen who will not sit down", "a chocobo, as himself"];
    private static readonly string[] Ratings = ["RATED K FOR KUPO", "RATED G FOR GIL", "RATED PG: SOME FISHING", "RATED M FOR MOOGLES", "NOT YET RATED BY THE ADVENTURERS' GUILD"];

    // The hosts' side businesses.
    private static readonly (string Name, string Line)[] Sponsors =
    [
        ("BARGAINBIX'S DISCOUNT EMPORIUM", "Everything must go. Everything already went. Come anyway."),
        ("MOGWYN'S BUSH HATS", "For the ranger who gets too close. Kupo-tested, mark-approved."),
        ("LEVARR'S LANTERN OIL", "Burns all night. Do not ask what he tells the lantern."),
        ("POM ROSS PAINT & CANVAS", "Happy little strokes since the Seventh Astral Era."),
        ("LISTINGWAY ESTATES", "He has read a great deal about houses. Now read about his."),
        ("KO BI'S LUCKY NUMBERS", "Kobold picks. You pay. Kobold picks again."),
        ("WAVV'S BAIT & TACKLE", "Down by the water. Considerably down."),
        ("NIMBLY'S RAINCOATS", "She knows when it will rain. You will know when you are wet."),
        ("THE EORZEAN KITCHEN CATERING", "A Namazu will cook at your event. He will also stay."),
    ];

    /// <summary>Whether a show should be in a break at this second, and how far in.</summary>
    public static bool InBreak(double clock, out double into)
    {
        var at = clock % Cycle;
        into = at - (Cycle - BreakFor);
        return into >= 0;
    }

    /// <summary>A show with breaks in it.</summary>
    public IFrameChannel Wrap(string name, IFrameChannel show) => new Wrapped(this, name, show);

    private sealed class Wrapped(CommercialBreak breaks, string name, IFrameChannel show) : IFrameChannel
    {
        public bool Available => show.Available;

        public bool WantsPicture => show.WantsPicture;

        public bool WantsMusic => show.WantsMusic;

        public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
        {
            var clock = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            if (!InBreak(clock, out var into))
            {
                show.Render(target, picture, now, seconds);
                return;
            }

            breaks.RenderBreak(target, name, (int)(clock / Cycle), into, seconds);
        }
    }

    /// <summary>The adverts back to back, every kind in turn, with a label: for looking at them on their own.</summary>
    public IFrameChannel Reel() => new ReelChannel(this);

    private sealed class ReelChannel(CommercialBreak breaks) : IFrameChannel
    {
        public bool Available => breaks.Font.Available;

        public bool WantsPicture => false;

        public bool WantsMusic => true;

        public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
        {
            // Four in a row, 44 seconds a set: the trailer takes its twenty, the rest eight each.
            var clock = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            const double Set = 54.0;
            var set = (int)(clock / Set);
            var at = clock % Set;
            var kind = at < 8 ? 0 : at < 16 ? 1 : at < 46 ? 2 : 3;
            var t = kind switch { 0 => at, 1 => at - 8, 2 => at - 16, _ => at - 46 };
            var index = (set * 4) + kind;
            var span = target.AsSpan();
            switch (kind)
            {
                case 0: breaks.Promo(span, index, t, seconds); break;
                case 1: breaks.Item(span, index, t, seconds); break;
                case 2: breaks.Trailer(span, index, t, seconds); break;
                default: breaks.Sponsor(span, index, t, seconds); break;
            }

            var length = kind == 2 ? 30 : 8;
            var label = $"AD REEL  #{index % 10000}  {kind switch { 0 => "PROMO", 1 => "ITEM", 2 => "TRAILER", _ => "SPONSOR" }}  {(int)t + 1}/{length}";
            var all = new BitmapFont.Clip(0, 0, W, H);
            Canvas.Fill(span, 0, 0, breaks.Font.Measure(label) + 24, 36, Ink);
            breaks.Font.Draw(span, W, label, 12, -2, Gold, 1, all);
        }
    }

    /// <summary>The three spots of a break: their kinds and lengths, which add up to the whole break.</summary>
    private static (int Kind, double Length)[] Plan(int cycle)
    {
        var kinds = new[] { Pick(cycle * 7, 5), 1 + Pick((cycle * 7) + 1, 4), Pick((cycle * 7) + 5, 5) };
        // At most one trailer a break, and it gets twenty seconds; the other two share the rest.
        var trailerAt = Array.IndexOf(kinds, 2);
        for (var i = 0; i < 3; i++)
        {
            if (kinds[i] == 2 && i != trailerAt)
                kinds[i] = 3;
        }

        var plan = new (int, double)[3];
        for (var i = 0; i < 3; i++)
            plan[i] = (kinds[i], trailerAt < 0 ? BreakFor / 3.0 : i == trailerAt ? 30.0 : (BreakFor - 30.0) / 2.0);
        return plan;
    }

    /// <summary>One frame of a break: which spot this is, and the bumpers at either end.</summary>
    private void RenderBreak(uint[] target, string returningTo, int cycle, double into, double seconds)
    {
        var span = target.AsSpan();
        var plan = Plan(cycle);
        var spot = 0;
        var within = into;
        while (spot < 2 && within >= plan[spot].Length)
        {
            within -= plan[spot].Length;
            spot++;
        }

        var seed = (cycle * 7) + spot;
        switch (plan[spot].Kind)
        {
            case 0: this.Promo(span, seed, within, seconds); break;
            case 1: this.Item(span, seed, within, seconds); break;
            case 2: this.Trailer(span, seed, within, seconds); break;
            case 3: this.Sponsor(span, seed, within, seconds); break;
            default: this.Item(span, seed + 11, within, seconds); break;
        }

        if (spot == 0 && within < 2.0)
            this.Bumper(span, "WE'LL BE RIGHT BACK", within);
        else if (spot == 2 && within > plan[2].Length - 2.0)
            this.Bumper(span, $"AND NOW, BACK TO {returningTo.ToUpperInvariant()}", plan[2].Length - within);
    }

    // -- the spots ----------------------------------------------------------------------------------------------

    private void Promo(Span<uint> span, int seed, double t, double seconds)
    {
        var list = shows();
        var all = new BitmapFont.Clip(0, 0, W, H);
        if (list.Count == 0)
        {
            Canvas.Fill(span, 0, 0, W, H, Ink);
            return;
        }

        var (name, blurb) = list[Pick(seed, list.Count)];
        var hue = Canvas.Lerp(Canvas.Rgb(0x2A, 0x14, 0x4A), Canvas.Rgb(0x0C, 0x1E, 0x3A), (float)(Pick(seed + 1, 100) / 100.0));
        for (var y = 0; y < H; y++)
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(hue, Ink, y / (float)H));
        for (var i = 0; i < 40; i++)
        {
            var x = (i * 173 + (int)(seconds * 20)) % W;
            var y = (i * 97) % H;
            Canvas.Plot(span, x, y, Canvas.Lerp(Cream, hue, 0.5f));
        }

        var slide = (int)(Math.Min(1.0, t / 0.6) * 200) - 200;
        font.Draw(span, W, "TONIGHT ON AETHERSTREAM", 80 + slide, 160, Gold, 1, all);
        var title = name.ToUpperInvariant();
        font.Draw(span, W, title, 80 + slide, 210, Cream, 2, all);
        var y2 = 320;
        foreach (var line in Canvas.Wrap(Canvas.Plain(blurb).ToUpperInvariant(), font.Fit(W - 160), 3))
        {
            font.Draw(span, W, line, 80, y2, Faint, 1, all);
            y2 += 40;
        }

        if (t > 3.0)
            font.Draw(span, W, "ALWAYS ON. NEVER RESCHEDULED. CHANGE THE CHANNEL AND IT IS ALREADY PLAYING.", 80, 520, Mint, 1, all);
        font.Draw(span, W, "THE CHANNELS TAB, UNDER SHOWS", 80, 600, Faint, 1, all);
    }

    private void Item(Span<uint> span, int seed, double t, double seconds)
    {
        var list = items();
        var all = new BitmapFont.Clip(0, 0, W, H);
        if (list.Count == 0)
        {
            this.Sponsor(span, seed, t, seconds);
            return;
        }

        var item = list[Pick(seed, list.Count)];
        var back = Pick(seed + 2, 3) switch { 0 => Canvas.Rgb(0xF4, 0xE8, 0xC8), 1 => Canvas.Rgb(0xD8, 0xE8, 0xF4), _ => Canvas.Rgb(0xE8, 0xD8, 0xF0) };
        Canvas.Fill(span, 0, 0, W, H, back);
        for (var i = 0; i < 6; i++)
            Canvas.Disc(span, 200 + (i * 180), 120 + ((i % 2) * 400) + (int)(Math.Sin(seconds + i) * 10), 60, Canvas.Lerp(back, Cream, 0.5f));

        // The product, large, on a plinth that turns to the light.
        var size = 260;
        var px = 160;
        var py = 200 + (int)(Math.Sin(seconds * 2) * 6);
        Canvas.Disc(span, px + (size / 2), py + size + 20, 150, Canvas.Lerp(back, Ink, 0.15f));
        if (itemIcon(item.Id) is { } icon)
            Blit(span, icon, px, py, size, size);
        else
            Canvas.Rect(span, px, py, size, size, Ink, 4);

        var name = Canvas.Plain(item.Name);
        var a = "AEIOU".Contains(char.ToUpperInvariant(name[0])) ? "an " + name : "a " + name;
        var slogan = Slogans[Pick(seed + 5, Slogans.Length)].Replace("{name}", name).Replace("{a}", a).Replace("{category}", item.Category.ToLowerInvariant());
        var y = 180;
        var upper = name.ToUpperInvariant();
        if (upper.Length <= font.Fit(720, 2))
        {
            font.Draw(span, W, upper, 500, 100, Ink, 2, all);
        }
        else
        {
            var ty = 100;
            foreach (var l in Canvas.Wrap(upper, font.Fit(720), 2))
            {
                font.Draw(span, W, l, 500, ty, Ink, 1, all);
                ty += 40;
            }
        }
        var lines = Canvas.Wrap(slogan.ToUpperInvariant(), font.Fit(720), 4);
        var shown = Math.Min(lines.Count, (int)(t / 1.2) + 1);
        for (var i = 0; i < shown; i++)
        {
            font.Draw(span, W, lines[i], 500, y + 20, Canvas.Lerp(Ink, back, 0.2f), 1, all);
            y += 44;
        }

        if (t > 6.0)
        {
            var price = Pick(seed + 9, 2) == 0 ? "ASK YOUR RETAINER" : "PRICED BY THE BOARD";
            Canvas.Fill(span, 500, 460, font.Measure(price) + 32, 44, Rose);
            font.Draw(span, W, price, 516, 462, Cream, 1, all);
        }

        font.Draw(span, W, Fine[Pick(seed + 4, Fine.Length)].ToUpperInvariant(), 40, 660, Canvas.Lerp(Ink, back, 0.5f), 1, all);
    }

    // Lines the actors say in the middle of a trailer, in pairs.
    private static readonly (string A, string B)[] Dialogue =
    [
        ("You came back.", "I never left. I was in the queue."),
        ("They said you were dead.", "They said a lot of things. Mostly about my glamour."),
        ("We had a deal.", "We had a market board. It is not the same."),
        ("How many are out there?", "All of them. And they have all done the roulette."),
        ("Is it true? About the fish?", "Everything is true about the fish."),
        ("You can't go alone.", "I have a chocobo. It counts. Legally."),
        ("What do we do now?", "We wait for the weather. Then we do nothing, faster."),
    ];

    private static readonly string[] Studios = ["FROM THE STUDIO THAT BROUGHT YOU", "FROM THE MAKERS OF", "FROM A PRODUCER WHO ONCE SAW"];

    /// <summary>
    /// A trailer in five shots, thirty seconds: an actor walking into a wide country as the tagline
    /// arrives, a close-up in the dusk with a line, the two of them at night by a fire with
    /// lightning, the title, and the card. Hard cuts, letterbox, and the cast drawn from the hosts.
    /// </summary>
    private void Trailer(Span<uint> span, int seed, double t, double seconds)
    {
        var all = new BitmapFont.Clip(0, 0, W, H);
        var title = $"{TitleA[Pick(seed + 2, TitleA.Length)]} {TitleB[Pick(seed + 3, TitleB.Length)]}".ToUpperInvariant();
        var tagline = Taglines[Pick(seed + 4, Taglines.Length)].ToUpperInvariant();
        var rating = Ratings[Pick(seed + 6, Ratings.Length)];
        var lead = Pick(seed + 7, 6);
        var foil = (lead + 1 + Pick(seed + 8, 5)) % 6;
        var star = $"STARRING {HostSprites.CastNames[lead]} AND {HostSprites.CastNames[foil]}".ToUpperInvariant();
        var biomeA = (Biome)Pick(seed, 7);
        var biomeB = (Biome)Pick(seed + 9, 7);
        var (lineA, lineB) = Dialogue[Pick(seed + 10, Dialogue.Length)];
        var other = $"{TitleA[Pick(seed + 12, TitleA.Length)]} {TitleB[Pick(seed + 13, TitleB.Length)]}".ToUpperInvariant();
        var rains = Pick(seed + 11, 3) == 0;
        const int Bar = 90;
        var titleScale = font.Measure(title, 2) <= W - 120 ? 2 : 1;
        var titleW = font.Measure(title, titleScale);

        // Shot boundaries, and a beat of black at each cut.
        var cuts = new[] { 0.0, 8.0, 13.0, 18.5, 23.0 };
        var shot = 0;
        for (var i = 1; i < cuts.Length; i++)
        {
            if (t >= cuts[i])
                shot = i;
        }

        var into = t - cuts[shot];
        if (shot > 0 && into < 0.12)
        {
            Canvas.Fill(span, 0, 0, W, H, Ink);
            return;
        }

        switch (shot)
        {
            case 0:
            {
                // Wide: the lead walks in from the left as the words arrive.
                Scenery.Paint(span, W, 0, 380, H, biomeA, 0.85f, seed, into * 40);
                var x = -80 + (int)(into * 45);
                HostSprites.DrawCast(span, lead, walk: true, seconds, x, 330, 7);
                if (rains)
                    Rain(span, seconds);
                var lines = Canvas.Wrap(tagline, font.Fit(W - 200), 3);
                var total = lines.Sum(l => l.Split(' ').Length);
                var shown = Math.Min(total, (int)(into / 7.0 * (total + 1)));
                var y = 140;
                foreach (var line in lines)
                {
                    var words = line.Split(' ');
                    var take = Math.Clamp(shown, 0, words.Length);
                    shown -= take;
                    if (take > 0)
                        Shadowed(span, string.Join(' ', words.Take(take)), (W - font.Measure(line)) / 2, y, Cream, 1, all);
                    y += 40;
                }

                break;
            }

            case 1:
            {
                // Close-up at dusk: the foil, large, and a line.
                Scenery.Paint(span, W, 0, 380, H, biomeB, 0.3f, seed + 1, 0);
                HostSprites.DrawCast(span, foil, walk: false, seconds, 760, 160 + (int)(into * 6), 12, flip: true);
                Subtitle(span, lineA, all);
                break;
            }

            case 2:
            {
                // Night: the two of them by a fire, lightning if it rains, the answer.
                Scenery.Paint(span, W, 0, 380, H, biomeB, 0.05f, seed + 2, 0);
                var flicker = (int)(Math.Sin(seconds * 9) * 6);
                Canvas.Disc(span, W / 2, 470, 90 + flicker, Canvas.Rgb(0x6A, 0x30, 0x10));
                Canvas.Disc(span, W / 2, 470, 40 + (flicker / 2), Canvas.Rgb(0xFF, 0x9A, 0x30));
                Canvas.Disc(span, W / 2, 458, 16, Canvas.Rgb(0xFF, 0xE0, 0x80));
                HostSprites.DrawCast(span, lead, walk: false, seconds, 300, 300, 8);
                HostSprites.DrawCast(span, foil, walk: false, seconds, 780, 300, 8, flip: true);
                if (rains)
                {
                    Rain(span, seconds);
                    if ((int)(seconds * 10) % 17 == 0)
                        Canvas.Fill(span, 0, 0, W, H, Canvas.Lerp(Cream, Canvas.Rgb(0xC8, 0xD8, 0xFF), 0.5f));
                }

                Subtitle(span, lineB, all);
                break;
            }

            case 3:
            {
                // The title, over the night scene gone dark.
                Scenery.Paint(span, W, 0, 380, H, biomeB, 0.05f, seed + 2, 0);
                for (var y = 0; y < H; y++)
                {
                    var row = span.Slice(y * W, W);
                    for (var x = 0; x < W; x++)
                        row[x] = Canvas.Lerp(row[x], Ink, 0.7f);
                }

                Shadowed(span, title, (W - titleW) / 2, 300, Gold, titleScale, all);
                if (into > 2.2)
                    Shadowed(span, star, (W - font.Measure(star)) / 2, 300 + (titleScale * 44) + 20, Cream, 1, all);
                break;
            }

            default:
            {
                Canvas.Fill(span, 0, 0, W, H, Ink);
                var studio = Studios[Pick(seed + 14, Studios.Length)];
                Shadowed(span, studio, (W - font.Measure(studio)) / 2, 150, Faint, 1, all);
                Shadowed(span, $"'{other}'", (W - font.Measure($"'{other}'")) / 2, 190, Cream, 1, all);
                Shadowed(span, title, (W - titleW) / 2, 280, Gold, titleScale, all);
                Shadowed(span, "COMING THIS STARLIGHT", (W - font.Measure("COMING THIS STARLIGHT")) / 2, 400, Cream, 1, all);
                Shadowed(span, "TO A CHANNEL THAT DOES NOT EXIST", (W - font.Measure("TO A CHANNEL THAT DOES NOT EXIST")) / 2, 440, Faint, 1, all);
                Canvas.Rect(span, (W / 2) - 260, 500, 520, 48, Cream, 2);
                font.Draw(span, W, rating, (W - font.Measure(rating)) / 2, 504, Cream, 1, all);
                break;
            }
        }

        Canvas.Fill(span, 0, 0, W, Bar, Ink);
        Canvas.Fill(span, 0, H - Bar, W, Bar, Ink);
    }

    /// <summary>One frame of a trailer into an array; the harness calls this, since it cannot pass a span.</summary>
    private void TrailerFrame(uint[] target, int seed, double t, double seconds) => this.Trailer(target.AsSpan(), seed, t, seconds);

    private static void Rain(Span<uint> span, double seconds)
    {
        var drop = Canvas.Rgb(0xB8, 0xC8, 0xE0);
        for (var i = 0; i < 160; i++)
        {
            var x = ((i * 97) + (int)(seconds * 40)) % W;
            var y = ((i * 53) + (int)(seconds * 900)) % H;
            Canvas.Line(span, x, y, x - 4, y + 18, drop);
        }
    }

    private void Subtitle(Span<uint> span, string line, in BitmapFont.Clip all)
    {
        var text = $"\"{line.ToUpperInvariant()}\"";
        var w = font.Measure(text);
        Canvas.Fill(span, (W - w) / 2 - 16, 560, w + 32, 44, Canvas.Lerp(Ink, Canvas.Black, 0.2f));
        font.Draw(span, W, text, (W - w) / 2, 562, Cream, 1, all);
    }

    private void Sponsor(Span<uint> span, int seed, double t, double seconds)
    {
        var all = new BitmapFont.Clip(0, 0, W, H);
        var (name, line) = Sponsors[Pick(seed, Sponsors.Length)];
        var hue = Canvas.Rgb(0x1A, 0x22, 0x2A);
        Canvas.Fill(span, 0, 0, W, H, hue);
        var r = 200 + (int)(Math.Sin(seconds * 0.8) * 20);
        Canvas.Disc(span, W / 2, 300, r + 8, Canvas.Lerp(hue, Gold, 0.15f));
        Canvas.Disc(span, W / 2, 300, r, Canvas.Lerp(hue, Gold, 0.3f));
        font.Draw(span, W, "THIS PROGRAMME WAS BROUGHT TO YOU BY", (W - font.Measure("THIS PROGRAMME WAS BROUGHT TO YOU BY")) / 2, 120, Faint, 1, all);
        var y = 260;
        foreach (var l in Canvas.Wrap(name, font.Fit(W - 200), 2))
        {
            font.Draw(span, W, l, (W - font.Measure(l)) / 2, y, Gold, 1, all);
            y += 40;
        }

        if (t > 2.0)
        {
            y = 420;
            foreach (var l in Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 200), 3))
            {
                font.Draw(span, W, l, (W - font.Measure(l)) / 2, y, Cream, 1, all);
                y += 40;
            }
        }

        font.Draw(span, W, "AND BY VIEWERS LIKE YOU", (W - font.Measure("AND BY VIEWERS LIKE YOU")) / 2, 620, Faint, 1, all);
    }

    // -- bits ---------------------------------------------------------------------------------------------------

    private void Bumper(Span<uint> span, string text, double left)
    {
        // A card that slides in and out over whatever is under it.
        var all = new BitmapFont.Clip(0, 0, W, H);
        var k = (float)Math.Clamp(left / 0.5, 0, 1);
        var h = (int)(120 * k);
        Canvas.Fill(span, 0, (H / 2) - (h / 2), W, h, Ink);
        Canvas.Fill(span, 0, (H / 2) - (h / 2), W, 3, Gold);
        Canvas.Fill(span, 0, (H / 2) + (h / 2) - 3, W, 3, Gold);
        if (h > 60)
            font.Draw(span, W, Canvas.Cut(text, font.Fit(W - 80)), (W - font.Measure(Canvas.Cut(text, font.Fit(W - 80)))) / 2, (H / 2) - 20, Cream, 1, all);
    }

    private void Shadowed(Span<uint> span, string text, int x, int y, uint colour, int scale, in BitmapFont.Clip all)
    {
        font.Draw(span, W, text, x + (2 * scale), y + (2 * scale), Ink, scale, all);
        font.Draw(span, W, text, x, y, colour, scale, all);
    }

    private static void Blit(Span<uint> span, IconPixels art, int x, int y, int w, int h)
    {
        for (var ty = 0; ty < h; ty++)
        {
            var yy = y + ty;
            if (yy < 0 || yy >= H)
                continue;
            var sy = ty * art.Height / h;
            for (var tx = 0; tx < w; tx++)
            {
                var xx = x + tx;
                if (xx < 0 || xx >= W)
                    continue;
                var p = art.Pixels[(sy * art.Width) + (tx * art.Width / w)];
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
