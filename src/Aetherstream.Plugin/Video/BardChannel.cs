namespace Aetherstream.Plugin.Video;

/// <summary>What the stories are made from: the game's own places, creatures and things.</summary>
internal sealed record StoryStock(
    IReadOnlyList<(string Zone, string Region)> Places,
    IReadOnlyList<(string Name, uint Icon)> Beasts,
    IReadOnlyList<(string Name, uint Icon)> Treasures);

/// <summary>
/// The story hour. A bard in a high-backed chair by a fire, a book on his knee, and a tale a
/// time: a hero of one of the peoples, a road through places that exist, a beast from the
/// bestiary, a treasure from the game's own tables, a god from the Twelve. Six pages, each
/// read from the chair and then shown as a picture. The bard's rule at the end is his own:
/// you don't have to take his word for it. On a schedule from the clock.
/// </summary>
internal sealed class BardChannel(BitmapFont font, Func<StoryStock?> stock, Func<uint, IconPixels?> icon) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;

    private const double TitlesFor = 6.0;
    private const double ReadFor = 6.0;
    private const double PictureFor = 7.0;
    private const int Pages = 6;
    private const double OutroFor = 7.0;
    private const double EpisodeFor = TitlesFor + (Pages * (ReadFor + PictureFor)) + OutroFor;

    private static readonly uint Velvet = Canvas.Rgb(0x3A, 0x22, 0x44);
    private static readonly uint Wall = Canvas.Rgb(0x2C, 0x1E, 0x1A);
    private static readonly uint Wood = Canvas.Rgb(0x5E, 0x3C, 0x1A);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Gold = Canvas.Rgb(0xE0, 0xB8, 0x4A);
    private static readonly uint Ink = Canvas.Rgb(0x1E, 0x16, 0x12);
    private static readonly uint Page = Canvas.Rgb(0xF2, 0xE8, 0xD0);

    private static readonly (string Race, string[] Names)[] Peoples =
    [
        ("Hyur", ["Aldric", "Mira", "Tobin", "Elsabet", "Corwin"]),
        ("Elezen", ["Aurelais", "Ysolde", "Laurentin", "Cerise", "Fournevaux"]),
        ("Lalafell", ["Popoto", "Nanamo", "Tataru", "Kokoro", "Wawalago"]),
        ("Miqo'te", ["Y'shtola", "M'naago", "R'kaji", "U'lani", "H'sabo"]),
        ("Roegadyn", ["Broenbhar", "Merlwyb", "Skaenyg", "Rhoswen", "Gruenlyng"]),
        ("Au Ra", ["Sadu", "Cirina", "Magnai", "Hien", "Yugiri"]),
        ("Hrothgar", ["Radovan", "Wuk Lamat", "Bakool", "Rurudo", "Gulool"]),
        ("Viera", ["Fran", "Cadence", "Ilsa", "Yasmin", "Lyra"]),
    ];

    private static readonly string[] Callings = ["a fisher", "a miner", "a dancer", "a cook", "a sellsword", "a scholar", "a retainer", "a chocobo keeper", "an astrologian", "a weaver"];

    private static readonly string[] Deities = ["Halone", "Menphina", "Thaliak", "Nymeia", "Llymlaen", "Oschon", "Byregot", "Rhalgr", "Azeyma", "Nald'thal", "Nophica", "Althyk"];

    // Six pages, each with a few tellings. {hero} {race} {calling} {home} {far} {beast} {treasure} {god}.
    private static readonly string[][] PageLines =
    [
        [
            "Once, in {home}, there lived {race} named {hero}, who was {calling} and content to be one.",
            "This is the story of {hero}, {race} of {home}, {calling} by trade, and by nothing else until the day it began.",
            "In {home} there was {calling} called {hero}. Nobody thought much of {hero}. That was about to change.",
        ],
        [
            "One morning a stranger came through {home} with a rumour: a {treasure}, lost long ago in {far}, and a {beast} sitting on it.",
            "{hero} heard it from a retainer, who heard it from a boat: in {far}, under the watch of a {beast}, lay a {treasure}.",
            "A letter came, unsigned. It said only: {far}. {beast}. {treasure}. Come alone. {hero} did not come alone. {hero} brought a chocobo.",
        ],
        [
            "The road from {home} to {far} is long, and {hero} walked all of it, praying to {god} at every aetheryte.",
            "{hero} went by ferry and by foot and by a chocobo that had opinions, and reached {far} thinner and wiser.",
            "It took a full moon to reach {far}. {hero} spent it learning that {calling} is no preparation for anything at all.",
        ],
        [
            "And there it was: the {beast}, larger than the rumour, asleep across the {treasure} like a cat on a letter.",
            "The {beast} was not asleep. It had been waiting. It had been waiting a very long time, and it was bored.",
            "{hero} found the {beast} at dusk. It looked at {hero}. {hero} looked at it. Neither of them was impressed.",
        ],
        [
            "{hero} did not fight it. {hero} did the one thing {calling} knows how to do, and did it so well the {beast} forgot to be angry.",
            "{god} answers those who ask properly. {hero} asked properly, and the {beast} sneezed, and rolled off the {treasure}.",
            "There was a fight. It was short, and the {beast} won it, and then it gave {hero} the {treasure} anyway, out of pity, which counts.",
        ],
        [
            "{hero} carried the {treasure} home to {home}, and put it on a shelf, and went back to being {calling}, and was happier for the shelf.",
            "The {treasure} sits in {home} to this day. The {beast} visits sometimes. Nobody in {home} mentions it.",
            "And that is how {hero} of {home} became the only {race} in Eorzea with a {treasure} and a {beast} for a friend. Mostly a friend.",
        ],
    ];

    private static readonly string[] Titles =
    [
        "THE {BEAST} OF {FAR}",
        "{HERO} AND THE {TREASURE}",
        "A LONG WAY FROM {HOME}",
        "WHAT {HERO} FOUND IN {FAR}",
        "THE {TREASURE} UNDER THE {BEAST}",
    ];

    private sealed record Story(string Hero, string Race, string Calling, string Home, string Far, Biome HomeBiome, Biome FarBiome, string Beast, uint BeastIcon, string Treasure, uint TreasureIcon, string God, string Title, string[] Pages);

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var s = stock();

        if (s is null || s.Places.Count == 0 || s.Beasts.Count == 0 || s.Treasures.Count == 0)
        {
            Canvas.Fill(span, 0, 0, W, H, Wall);
            const string None = "THE BARD HAS LOST HIS BOOK";
            font.Draw(span, W, None, (W - font.Measure(None, 2)) / 2, 330, Cream, 2, all);
            return;
        }

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var episode = (int)((unix - 1_700_000_000) / (long)EpisodeFor);
        var into = (unix - 1_700_000_000) % (long)EpisodeFor;
        var story = Compose(s, episode);

        if (into < TitlesFor)
        {
            this.DrawParlour(span, seconds, HostSprites.Bard.Wave, all);
            Dim(span, 0.4f);
            const string Show = "THE BARD'S HOUR";
            var tw = font.Measure(Show, 2) + 64;
            Canvas.Fill(span, (W - tw) / 2, 150, tw, 112, Velvet);
            Canvas.Fill(span, (W - tw) / 2, 150, tw, 6, Gold);
            Canvas.Fill(span, (W - tw) / 2, 256, tw, 6, Gold);
            font.Draw(span, W, Show, (W - font.Measure(Show, 2)) / 2, 166, Cream, 2, all);
            const string With = "WITH LEVARR BURTAINE";
            font.Draw(span, W, With, (W - font.Measure(With)) / 2, 300, Gold, 1, all);
            if (into > 2.5)
            {
                var title = Canvas.Cut($"TONIGHT: {story.Title}", font.Fit(W - 100));
                font.Draw(span, W, title, (W - font.Measure(title)) / 2, 380, Cream, 1, all);
            }
        }
        else if (into < TitlesFor + (Pages * (ReadFor + PictureFor)))
        {
            var t = into - TitlesFor;
            var page = (int)(t / (ReadFor + PictureFor));
            var inPage = t % (ReadFor + PictureFor);
            var text = story.Pages[page];

            if (inPage < ReadFor)
            {
                // From the chair: the bard reads, the page shown as a caption.
                this.DrawParlour(span, seconds, inPage < 0.8 ? HostSprites.Bard.Turn : (int)(inPage) % 3 == 2 ? HostSprites.Bard.Gesture : HostSprites.Bard.Read, all);
                this.DrawCaption(span, $"PAGE {page + 1}", text, all);
            }
            else
            {
                // The picture: the page's scene, and the words beneath it as in a picture book.
                this.DrawPicture(span, story, page, seconds, now, all);
                this.DrawCaption(span, $"PAGE {page + 1}", text, all);
            }
        }
        else
        {
            var t = into - TitlesFor - (Pages * (ReadFor + PictureFor));
            this.DrawParlour(span, seconds, t < 3.0 ? HostSprites.Bard.Gesture : HostSprites.Bard.Wave, all);
            var line = t < 3.5
                ? "And that is the end of the tale. Was it true? Every word. But you don't have to take my word for it."
                : $"Next time: {NextTitle(s, episode + 1)}. Bring a blanket.";
            this.DrawCaption(span, "THE END", line, all);
        }

        Canvas.Fill(span, 0, 676, W, 4, Ink);
        Canvas.Fill(span, 0, 676, (int)(W * (into / EpisodeFor)), 4, Gold);
        Canvas.Fill(span, 0, 680, W, 40, Ink);
        font.Draw(span, W, "AETHERSTREAM STORIES", 24, 680, Gold, 1, all);
        var right = Canvas.Cut($"TALE {episode % 10000}  /  {story.Title}", 52);
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Cream, 1, all);
    }

    // -- the story ------------------------------------------------------------------------------------------

    private static Story Compose(StoryStock s, int episode)
    {
        var people = Peoples[Pick(episode, 1, Peoples.Length)];
        var hero = people.Names[Pick(episode, 2, people.Names.Length)];
        var race = people.Race switch { "Au Ra" or "Elezen" => $"an {people.Race}", _ => $"a {people.Race}" };
        var calling = Callings[Pick(episode, 3, Callings.Length)];
        var home = s.Places[Pick(episode, 4, s.Places.Count)];
        var far = s.Places[Pick(episode, 5, s.Places.Count)];
        if (far.Zone == home.Zone)
            far = s.Places[(Pick(episode, 5, s.Places.Count) + 1) % s.Places.Count];
        var beast = s.Beasts[Pick(episode, 6, s.Beasts.Count)];
        var treasure = s.Treasures[Pick(episode, 7, s.Treasures.Count)];
        var god = Deities[Pick(episode, 8, Deities.Length)];

        string Fill(string text) => text
            .Replace("{hero}", hero).Replace("{race}", race).Replace("{calling}", calling)
            .Replace("{home}", home.Zone).Replace("{far}", far.Zone)
            .Replace("{beast}", Canvas.Plain(beast.Name)).Replace("{treasure}", Canvas.Plain(treasure.Name)).Replace("{god}", god)
            .Replace("{HERO}", hero.ToUpperInvariant()).Replace("{HOME}", home.Zone.ToUpperInvariant()).Replace("{FAR}", far.Zone.ToUpperInvariant())
            .Replace("{BEAST}", Canvas.Plain(beast.Name).ToUpperInvariant()).Replace("{TREASURE}", Canvas.Plain(treasure.Name).ToUpperInvariant());

        var pages = new string[Pages];
        for (var p = 0; p < Pages; p++)
            pages[p] = Fill(PageLines[p][Pick(episode, 10 + p, PageLines[p].Length)]);

        var title = Fill(Titles[Pick(episode, 9, Titles.Length)]);
        return new Story(hero, race, calling, home.Zone, far.Zone, Scenery.BiomeOf(home.Region, home.Zone), Scenery.BiomeOf(far.Region, far.Zone), beast.Name, beast.Icon, treasure.Name, treasure.Icon, god, title, pages);
    }

    private static string NextTitle(StoryStock s, int episode) => Compose(s, episode).Title;

    // -- the pictures ---------------------------------------------------------------------------------------

    private void DrawParlour(Span<uint> span, double seconds, HostSprites.Bard action, in BitmapFont.Clip all)
    {
        // The parlour: a dark wall, a bookcase, a fire in a hearth, a rug, and the chair.
        Canvas.Fill(span, 0, 0, W, H, Wall);
        for (var y = 0; y < 560; y += 28)
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(Wall, Canvas.Black, 0.3f));

        // The bookcase.
        Canvas.Fill(span, 60, 60, 340, 480, Wood);
        for (var shelf = 0; shelf < 5; shelf++)
        {
            var sy = 90 + (shelf * 90);
            Canvas.Fill(span, 70, sy + 70, 320, 8, Canvas.Lerp(Wood, Canvas.Black, 0.4f));
            var rng = (uint)(shelf * 7919) | 1u;
            var bx = 76;
            while (bx < 380)
            {
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                var bw = 12 + (int)(rng % 14);
                var bh = 40 + (int)((rng >> 8) % 28);
                var colour = (rng >> 16) % 5 switch
                {
                    0 => Canvas.Rgb(0x8A, 0x2A, 0x2A),
                    1 => Canvas.Rgb(0x2A, 0x4A, 0x7A),
                    2 => Canvas.Rgb(0x4A, 0x6A, 0x3A),
                    3 => Canvas.Rgb(0xB0, 0x8A, 0x3A),
                    _ => Canvas.Rgb(0x5A, 0x3A, 0x6A),
                };
                Canvas.Fill(span, bx, sy + 70 - bh, bw, bh, colour);
                bx += bw + 2;
            }
        }

        // The hearth, with a fire that moves.
        Canvas.Fill(span, 860, 260, 360, 300, Canvas.Rgb(0x6A, 0x5A, 0x50));
        Canvas.Fill(span, 900, 300, 280, 240, Canvas.Rgb(0x14, 0x0E, 0x0A));
        Canvas.Fill(span, 900, 520, 280, 20, Canvas.Rgb(0x3A, 0x2A, 0x20));
        for (var f = 0; f < 6; f++)
        {
            var fx = 950 + (f * 36) + (int)(Math.Sin((seconds * 5.0) + f) * 6);
            var fh = 60 + (int)(Math.Sin((seconds * 7.0) + (f * 1.3)) * 22);
            Canvas.Disc(span, fx, 520 - (fh / 2), 24, Canvas.Rgb(0xE0, 0x60, 0x20));
            Canvas.Disc(span, fx, 520 - (fh / 2) - 8, 14, Canvas.Rgb(0xFF, 0xB0, 0x40));
            Canvas.Disc(span, fx, 520 - fh, 8, Canvas.Rgb(0xFF, 0xE0, 0x80));
        }

        Canvas.Fill(span, 900, 300, 280, 4, Canvas.Rgb(0x8A, 0x7A, 0x70));

        // The rug and the floor.
        Canvas.Fill(span, 0, 560, W, 120, Canvas.Rgb(0x3A, 0x28, 0x1C));
        Canvas.Fill(span, 420, 580, 460, 90, Canvas.Rgb(0x7A, 0x2A, 0x2A));
        Canvas.Fill(span, 436, 596, 428, 58, Canvas.Rgb(0x9A, 0x3A, 0x3A));

        // The bard in his chair, lit by the fire.
        HostSprites.DrawBard(span, action, seconds, 480, 560 - 300, 10);
    }

    private void DrawPicture(Span<uint> span, Story story, int page, double seconds, DateTime now, in BitmapFont.Clip all)
    {
        // The page's scene: where it is set, and who is in it, framed like a plate in a book.
        var atHome = page is 0 or 1 or 5;
        var biome = atHome ? story.HomeBiome : story.FarBiome;
        var hour = now.Hour + (now.Minute / 60f);
        var daylight = page is 3 or 4 ? 0.25f : Math.Clamp(1f - (Math.Abs(hour - 13f) / 7.5f), 0f, 1f);

        Canvas.Fill(span, 0, 0, W, H, Page);
        Scenery.Paint(span, W, 60, 340, 540, biome, daylight, (page * 17) + 3, page == 2 ? seconds * 40.0 : seconds * 4.0);
        Canvas.Rect(span, 0, 60, W, 480, Ink, 6);

        // The hero: a small figure, walking on the road page, standing otherwise.
        var heroX = page == 2 ? 200 + (int)((seconds * 30.0) % 700) : 300;
        HostSprites.DrawRanger(span, page == 2 ? HostSprites.Ranger.Run : HostSprites.Ranger.Stand, seconds, heroX, 540 - 200, 5);

        // The beast on the pages it is on; the treasure where it is found and where it ends up.
        if (page is 3 or 4)
        {
            var bump = page == 4 ? (int)(Math.Sin(seconds * 10.0) * 8) : (int)(Math.Sin(seconds * 1.5) * 3);
            Canvas.Disc(span, 900, 520, 90, Canvas.Lerp(Canvas.Black, Canvas.Rgb(0x40, 0x40, 0x40), 0.5f));
            this.DrawIcon(span, story.BeastIcon, 820 + bump, 360, 160);
        }

        if (page is 1 or 3 or 5)
        {
            var glow = (int)(Math.Sin(seconds * 3.0) * 4);
            var tx = page == 5 ? 560 : 760;
            Canvas.Disc(span, tx + 40, 500 - glow, 52, Canvas.Lerp(Gold, Page, 0.5f));
            this.DrawIcon(span, story.TreasureIcon, tx, 460 - glow, 80);
        }

        var caption = Canvas.Cut(atHome ? story.Home.ToUpperInvariant() : story.Far.ToUpperInvariant(), 30);
        font.Draw(span, W, caption, W - 30 - font.Measure(caption), 20, Ink, 1, all);
        var plate = $"PLATE {page + 1}";
        font.Draw(span, W, plate, 30, 20, Ink, 1, all);
    }

    private void DrawCaption(Span<uint> span, string tab, string line, in BitmapFont.Clip all)
    {
        const int Top = 556;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - 40 - font.Measure(tab) - 48), 3);
        var height = 16 + (lines.Count * 40);
        var top = Math.Min(Top, 676 - height);
        Canvas.Fill(span, 40, top, W - 80, height, Velvet);
        Canvas.Fill(span, 40, top, W - 80, 4, Gold);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, top + 4, tabW, height - 4, Gold);
        font.Draw(span, W, tab, 56, top + 8, Ink, 1, all);
        var y = top + 8;
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
            return;

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

    private static int Pick(int episode, int salt, int count)
    {
        var x = (uint)((episode * 2654435761u) ^ (uint)(salt * 40503u));
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
