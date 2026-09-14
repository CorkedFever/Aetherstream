namespace Aetherstream.Plugin.Video;

/// <summary>What the stories are made from: the game's own places, creatures and things.</summary>
internal sealed record StoryStock(
    IReadOnlyList<(string Zone, string Region)> Places,
    IReadOnlyList<(string Name, uint Icon)> Beasts,
    IReadOnlyList<(string Name, uint Icon)> Treasures);

/// <summary>
/// The story hour. A tonberry in a high-backed chair by a fire, a book on his knee, a lantern in
/// his hand, and a tale a
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

    private static readonly uint[] Hairs = [Canvas.Rgb(0x2A, 0x1E, 0x14), Canvas.Rgb(0xD8, 0xB0, 0x60), Canvas.Rgb(0x8A, 0x3A, 0x2A), Canvas.Rgb(0xE8, 0xE0, 0xD0), Canvas.Rgb(0x4A, 0x3A, 0x6A), Canvas.Rgb(0x6A, 0x4A, 0x2A), Canvas.Rgb(0xC0, 0x70, 0xA0)];
    private static readonly uint[] Cloaks = [Canvas.Rgb(0x3A, 0x5A, 0x8A), Canvas.Rgb(0x6A, 0x2A, 0x2A), Canvas.Rgb(0x2E, 0x5C, 0x3C), Canvas.Rgb(0x5A, 0x3A, 0x6A), Canvas.Rgb(0x8A, 0x6A, 0x2A), Canvas.Rgb(0x3A, 0x3A, 0x44)];
    private static readonly uint[] Skins = [Canvas.Rgb(0xE8, 0xB8, 0x90), Canvas.Rgb(0xC8, 0x90, 0x60), Canvas.Rgb(0x8A, 0x5A, 0x3A), Canvas.Rgb(0xF0, 0xD8, 0xC0), Canvas.Rgb(0xA8, 0x9A, 0xC8)];

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

    private sealed record Story(string Hero, string Race, string Calling, string Home, string Far, Biome HomeBiome, Biome FarBiome, string Beast, uint BeastIcon, string Treasure, uint TreasureIcon, string God, string Title, string[] Pages, uint Hair, uint Cloak, uint Skin, string People);

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
            const string With = "WITH LEVARR BURTONBERRY";
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
        var hair = Hairs[Pick(episode, 20, Hairs.Length)];
        var cloak = Cloaks[Pick(episode, 21, Cloaks.Length)];
        var skin = Skins[Pick(episode, 22, Skins.Length)];
        return new Story(hero, race, calling, home.Zone, far.Zone, Scenery.BiomeOf(home.Region, home.Zone), Scenery.BiomeOf(far.Region, far.Zone), beast.Name, beast.Icon, treasure.Name, treasure.Icon, god, title, pages, hair, cloak, skin, people.Race);
    }

    private static string NextTitle(StoryStock s, int episode) => Compose(s, episode).Title;

    // -- the pictures ---------------------------------------------------------------------------------------

    private void DrawParlour(Span<uint> span, double seconds, HostSprites.Bard action, in BitmapFont.Clip all)
    {
        // The parlour by firelight: everything is a shape against the glow. The hearth on the
        // right throws the light; the teller sits in silhouette in the high-backed chair with the
        // book on the knee and the lantern up, and the bookcase is a dark wall of spines.
        var flicker = 0.85f + (0.15f * (float)Math.Sin(seconds * 9.0)) * (float)Math.Abs(Math.Sin(seconds * 2.3));
        var glow = Canvas.Lerp(Canvas.Rgb(0x6A, 0x3A, 0x1A), Canvas.Rgb(0xB8, 0x6A, 0x2A), flicker);
        for (var y = 0; y < H; y++)
        {
            for (var x = 0; x < W; x += 8)
            {
                var dx = (x - 1040) / 900f;
                var dy = (y - 430) / 520f;
                var d = MathF.Sqrt((dx * dx) + (dy * dy));
                var c = Canvas.Lerp(glow, Wall, Math.Clamp(d, 0f, 1f));
                Canvas.Fill(span, x, y, 8, 1, c);
            }
        }

        // The hearth: the opening, and the fire that lights the room.
        Canvas.Fill(span, 880, 240, 320, 320, Canvas.Lerp(Wall, Canvas.Black, 0.5f));
        Canvas.Fill(span, 910, 270, 260, 270, Canvas.Lerp(glow, Canvas.Rgb(0xFF, 0xC0, 0x60), 0.4f));
        for (var f = 0; f < 6; f++)
        {
            var fx = 950 + (f * 36) + (int)(Math.Sin((seconds * 5.0) + f) * 6);
            var fh = 70 + (int)(Math.Sin((seconds * 7.0) + (f * 1.3)) * 24);
            Canvas.Disc(span, fx, 520 - (fh / 2), 26, Canvas.Rgb(0xFF, 0x8A, 0x30));
            Canvas.Disc(span, fx, 520 - (fh / 2) - 10, 16, Canvas.Rgb(0xFF, 0xC8, 0x60));
            Canvas.Disc(span, fx, 520 - fh, 8, Canvas.Rgb(0xFF, 0xF0, 0xA0));
        }

        Canvas.Fill(span, 900, 520, 280, 24, Canvas.Lerp(Wall, Canvas.Black, 0.6f));

        // The bookcase in silhouette, spines as a ragged skyline.
        var ink = Canvas.Lerp(Wall, Canvas.Black, 0.75f);
        Canvas.Fill(span, 60, 80, 340, 480, ink);
        var rng = 7919u;
        for (var shelf = 0; shelf < 5; shelf++)
        {
            var sy = 160 + (shelf * 90);
            var bx = 70;
            while (bx < 390)
            {
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                var bw = 12 + (int)(rng % 14);
                var bh = 40 + (int)((rng >> 8) % 28);
                Canvas.Fill(span, bx, sy - bh, bw, bh, Canvas.Lerp(ink, glow, 0.08f + (0.06f * ((rng >> 16) % 3))));
                bx += bw + 2;
            }

            Canvas.Fill(span, 60, sy, 340, 6, Canvas.Lerp(ink, Canvas.Black, 0.5f));
        }

        // The floor and the rug, and the chair's shadow across them.
        Canvas.Fill(span, 0, 560, W, 120, Canvas.Lerp(Wall, Canvas.Black, 0.55f));
        Canvas.Fill(span, 380, 580, 520, 90, Canvas.Lerp(Canvas.Rgb(0x6A, 0x22, 0x22), Canvas.Black, 0.45f));

        // The teller: a silhouette, rim-lit on the fire's side.
        var rim = Canvas.Lerp(glow, Canvas.Rgb(0xFF, 0xD0, 0x80), 0.5f);
        var chairX = 470;
        Canvas.Fill(span, chairX, 250, 180, 330, ink);
        Canvas.Fill(span, chairX + 176, 250, 6, 330, rim);
        Canvas.Fill(span, chairX - 30, 440, 240, 20, ink);
        Canvas.Fill(span, chairX - 30, 460, 16, 100, ink);
        Canvas.Fill(span, chairX + 194, 460, 16, 100, ink);

        var bob = (int)(Math.Sin(seconds * 1.1) * 3);
        var headX = chairX + 100;
        var headY = 300 + bob;
        Canvas.Disc(span, headX, headY, 44, ink);
        Canvas.Disc(span, headX + 36, headY - 8, 8, rim);
        Canvas.Fill(span, headX - 60, headY + 30, 120, 130, ink);

        // The book on the knee, a pale shape the fire catches, and the page turning.
        var page = action == HostSprites.Bard.Turn ? (int)((seconds * 3.0) % 1.0 * 30) : 0;
        Canvas.Fill(span, headX - 70, 470, 140, 12, Canvas.Lerp(rim, Canvas.White, 0.3f));
        Canvas.Fill(span, headX - 70 + page, 456, 70 - page, 14, Canvas.Lerp(rim, Canvas.White, 0.5f));

        // The lantern in the far hand: raised when he gestures, its glass the brightest thing on the left.
        var raise = action is HostSprites.Bard.Gesture or HostSprites.Bard.Wave ? (int)(Math.Abs(Math.Sin(seconds * 1.6)) * 60) : 0;
        var lx = chairX - 40;
        var ly = 420 - raise;
        Canvas.Fill(span, lx - 30, ly - 10, 60, 8, ink);
        Canvas.Fill(span, lx - 3, ly - 40, 6, 32, ink);
        Canvas.Disc(span, lx, ly + 24, 40, Canvas.Lerp(glow, Canvas.Rgb(0xFF, 0xD8, 0x80), 0.25f));
        Canvas.Fill(span, lx - 18, ly, 36, 48, Canvas.Rgb(0xFF, 0xC8, 0x60));
        Canvas.Fill(span, lx - 22, ly - 4, 44, 6, ink);
        Canvas.Fill(span, lx - 22, ly + 46, 44, 6, ink);
        Canvas.Fill(span, lx - 22, ly, 4, 48, ink);
        Canvas.Fill(span, lx + 18, ly, 4, 48, ink);
    }

    private void DrawPicture(Span<uint> span, Story story, int page, double seconds, DateTime now, in BitmapFont.Clip all)
    {
        // A shadow play: a paper screen lit from behind, the light warmest at the middle and
        // flickering a little, in a wooden frame; every figure a black cutout on it.
        var atHome = page is 0 or 1 or 5;
        var biome = atHome ? story.HomeBiome : story.FarBiome;
        var flicker = 0.92f + (0.08f * (float)Math.Sin(seconds * 11.0) * (float)Math.Abs(Math.Sin(seconds * 3.1)));
        var paper = Canvas.Lerp(Canvas.Rgb(0xF4, 0xD8, 0xA0), Canvas.Rgb(0xFF, 0xEC, 0xC0), flicker);
        var paperEdge = Canvas.Rgb(0xC8, 0xA0, 0x60);
        const int Top = 60, Bottom = 540, GroundY = 470;

        Canvas.Fill(span, 0, 0, W, H, Wood);
        for (var y = Top; y < Bottom; y++)
        {
            for (var x = 0; x < W; x += 8)
            {
                var dx = (x - 640) / 640f;
                var dy = (y - 300) / 300f;
                var d = MathF.Sqrt((dx * dx) + (dy * dy));
                Canvas.Fill(span, x, y, 8, 1, Canvas.Lerp(paper, paperEdge, Math.Clamp(d * 0.9f, 0f, 1f)));
            }
        }

        // The night pages: the screen dims and a paper moon is cut in.
        if (page is 3 or 4)
        {
            Dim(span, 0.25f);
            Canvas.Disc(span, 1080, 150, 46, Canvas.Lerp(paper, Canvas.White, 0.5f));
        }

        // The country, in cut paper: two ranges of hills and the props, all black.
        var ink = Canvas.Rgb(0x14, 0x10, 0x0C);
        var pan = page == 2 ? seconds * 40.0 : seconds * 3.0;
        ShadowHills(span, GroundY, ink, (page * 17) + 3, 0.006, 70, pan * 0.3);
        ShadowHills(span, GroundY, ink, (page * 17) + 9, 0.011, 44, pan * 0.6);
        Canvas.Fill(span, 0, GroundY, W, Bottom - GroundY, ink);
        ShadowProps(span, biome, GroundY, ink, (page * 17) + 5, pan);

        // The hero: a cutout with a hood, a pack and a staff, walking on the road page.
        var heroX = page == 2 ? 200 + (int)((seconds * 30.0) % 700) : 300;
        var heroScale = HostSprites.HeroScale(story.People);
        ShadowHero(span, story.People, heroX, GroundY, heroScale, page == 2 ? seconds : 0.0, ink);

        // The beast, cut from its own portrait; the treasure, cut from its icon with a glow behind.
        if (page is 3 or 4)
        {
            var bump = page == 4 ? (int)(Math.Sin(seconds * 10.0) * 8) : (int)(Math.Sin(seconds * 1.5) * 3);
            this.DrawCutout(span, story.BeastIcon, 800 + bump, GroundY - 200, 200, ink);
        }

        if (page is 1 or 3 or 5)
        {
            var glow = (int)(Math.Sin(seconds * 3.0) * 4);
            var tx = page == 5 ? 560 : 700;
            Canvas.Disc(span, tx + 45, GroundY - 40 - glow, 60, Canvas.Lerp(paper, Canvas.Rgb(0xFF, 0xF4, 0xD0), 0.8f));
            this.DrawCutout(span, story.TreasureIcon, tx, GroundY - 90 - glow, 90, ink);
        }

        // The frame, and the plate's labels on it.
        Canvas.Fill(span, 0, Top - 12, W, 12, WoodDark);
        Canvas.Fill(span, 0, Bottom, W, 12, WoodDark);
        Canvas.Fill(span, 0, Top, 12, Bottom - Top, WoodDark);
        Canvas.Fill(span, W - 12, Top, 12, Bottom - Top, WoodDark);
        var caption = Canvas.Cut(atHome ? story.Home.ToUpperInvariant() : story.Far.ToUpperInvariant(), 30);
        font.Draw(span, W, caption, W - 30 - font.Measure(caption), 10, Cream, 1, all);
        var plate = $"PLATE {page + 1}";
        font.Draw(span, W, plate, 30, 10, Cream, 1, all);
    }

    private static readonly uint WoodDark = Canvas.Rgb(0x3A, 0x24, 0x10);

    private static void ShadowHills(Span<uint> span, int ground, uint ink, int seed, double freq, int height, double shift)
    {
        for (var x = 0; x < W; x++)
        {
            var wx = x + shift;
            var h = (Math.Sin((wx * freq) + seed) * 0.5) + (Math.Sin((wx * freq * 2.3) + (seed * 1.7)) * 0.3) + (Math.Sin((wx * freq * 5.1) + (seed * 0.4)) * 0.2);
            var top = ground - (int)(((h + 1.0) / 2.0) * height) - 4;
            Canvas.Fill(span, x, top, 1, ground - top, ink);
        }
    }

    private static void ShadowProps(Span<uint> span, Biome biome, int ground, uint ink, int seed, double scroll)
    {
        var rng = (uint)(seed * 11) | 1u;
        for (var i = 0; i < 7; i++)
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            var px = (int)((rng % (uint)(W + 200)) - 100 - (scroll * 0.9) % (W + 200));
            if (px < -100)
                px += W + 200;
            var scale = 2 + (int)((rng >> 8) % 2);
            switch (biome)
            {
                case Biome.Desert:
                    Canvas.Fill(span, px - (2 * scale), ground - (16 * scale), 4 * scale, 16 * scale, ink);
                    Canvas.Fill(span, px - (7 * scale), ground - (12 * scale), 5 * scale, 2 * scale, ink);
                    Canvas.Fill(span, px - (7 * scale), ground - (16 * scale), 2 * scale, 5 * scale, ink);
                    Canvas.Fill(span, px + (2 * scale), ground - (9 * scale), 5 * scale, 2 * scale, ink);
                    Canvas.Fill(span, px + (5 * scale), ground - (13 * scale), 2 * scale, 5 * scale, ink);
                    break;
                case Biome.Snow:
                    for (var t = 0; t < 3; t++)
                    {
                        var w = (10 - (t * 2)) * scale;
                        var ty = ground - (6 * scale) - (t * 6 * scale);
                        for (var r = 0; r < 6 * scale; r++)
                            Canvas.Fill(span, px - (w * r / (6 * scale)), ty - r, (2 * w * r / (6 * scale)) + 1, 1, ink);
                    }

                    break;
                case Biome.Coast:
                    for (var k = 0; k < 16 * scale; k++)
                        Canvas.Fill(span, px + (k / 4), ground - k, 3 * scale / 2, 1, ink);
                    for (var f = -2; f <= 2; f++)
                        Canvas.Line(span, px + (4 * scale), ground - (16 * scale), px + (4 * scale) + (f * 7 * scale), ground - (16 * scale) + (Math.Abs(f) * 3 * scale) - (2 * scale), ink);
                    break;
                case Biome.Highland:
                case Biome.Steppe:
                    Canvas.Disc(span, px, ground - (3 * scale), 6 * scale, ink);
                    break;
                default:
                    Canvas.Fill(span, px - (2 * scale), ground - (18 * scale), 4 * scale, 18 * scale, ink);
                    Canvas.Disc(span, px, ground - (22 * scale), 10 * scale, ink);
                    Canvas.Disc(span, px - (4 * scale), ground - (18 * scale), 7 * scale, ink);
                    break;
            }
        }
    }

    /// <summary>
    /// The hero as a cutout: a hooded head, a cloaked body that widens to the hem, legs that
    /// swing when walking, a pack, a staff. The people shows in the outline: ears, horns, a
    /// tail, a mane, or a smaller figure.
    /// </summary>
    private static void ShadowHero(Span<uint> span, string people, int x, int ground, float scale, double walk, uint ink)
    {
        var h = (int)(200 * scale);
        var top = ground - h;
        var cx = x + (int)(40 * scale);
        var headR = (int)(22 * scale);
        var headY = top + headR + (int)(10 * scale);

        // Head, and what the people adds to it.
        Canvas.Disc(span, cx, headY, headR, ink);
        switch (people)
        {
            case "Miqo'te":
                Canvas.Line(span, cx - headR + 4, headY - headR + 6, cx - headR - 2, headY - headR - (int)(18 * scale), ink);
                Canvas.Line(span, cx - headR + 12, headY - headR + 2, cx - headR - 2, headY - headR - (int)(18 * scale), ink);
                Canvas.Line(span, cx + headR - 4, headY - headR + 6, cx + headR + 2, headY - headR - (int)(18 * scale), ink);
                Canvas.Line(span, cx + headR - 12, headY - headR + 2, cx + headR + 2, headY - headR - (int)(18 * scale), ink);
                break;
            case "Viera":
                Canvas.Fill(span, cx - (int)(14 * scale), headY - headR - (int)(46 * scale), (int)(9 * scale), (int)(50 * scale), ink);
                Canvas.Fill(span, cx + (int)(5 * scale), headY - headR - (int)(46 * scale), (int)(9 * scale), (int)(50 * scale), ink);
                break;
            case "Elezen":
                Canvas.Line(span, cx - headR, headY, cx - headR - (int)(12 * scale), headY - (int)(10 * scale), ink);
                Canvas.Line(span, cx + headR, headY, cx + headR + (int)(12 * scale), headY - (int)(10 * scale), ink);
                break;
            case "Au Ra":
                Canvas.Line(span, cx - headR + 4, headY - headR + 8, cx - headR - (int)(10 * scale), headY - headR - (int)(14 * scale), ink);
                Canvas.Line(span, cx + headR - 4, headY - headR + 8, cx + headR + (int)(10 * scale), headY - headR - (int)(14 * scale), ink);
                break;
            case "Hrothgar":
                Canvas.Disc(span, cx, headY, headR + (int)(10 * scale), ink);
                Canvas.Disc(span, cx - headR + 2, headY - headR + 2, (int)(8 * scale), ink);
                Canvas.Disc(span, cx + headR - 2, headY - headR + 2, (int)(8 * scale), ink);
                Canvas.Disc(span, cx + headR - 4, headY + (int)(6 * scale), (int)(12 * scale), ink);
                break;
            case "Roegadyn":
                Canvas.Disc(span, cx, headY + (int)(6 * scale), headR + (int)(4 * scale), ink);
                break;
        }

        // The body: a cloak that widens to the hem, over the legs.
        var shoulderY = headY + headR - (int)(4 * scale);
        var hemY = ground - (int)(40 * scale);
        for (var y = shoulderY; y < hemY; y++)
        {
            var t = (float)(y - shoulderY) / Math.Max(1, hemY - shoulderY);
            var half = (int)((26 + (t * 22)) * scale);
            Canvas.Fill(span, cx - half, y, half * 2, 1, ink);
        }

        // The pack, behind the shoulder; the legs, swinging when walking; the staff.
        Canvas.Fill(span, cx + (int)(20 * scale), shoulderY + (int)(10 * scale), (int)(24 * scale), (int)(40 * scale), ink);
        var swing = walk > 0 ? (int)(Math.Sin(walk * 10.0) * 14 * scale) : 0;
        Canvas.Fill(span, cx - (int)(16 * scale) - swing, hemY, (int)(12 * scale), ground - hemY, ink);
        Canvas.Fill(span, cx + (int)(4 * scale) + swing, hemY, (int)(12 * scale), ground - hemY, ink);
        var sx = cx - (int)(40 * scale);
        Canvas.Fill(span, sx, top - (int)(10 * scale), (int)(5 * scale), ground - top + (int)(10 * scale), ink);
        Canvas.Disc(span, sx + (int)(2 * scale), top - (int)(10 * scale), (int)(6 * scale), ink);
        Canvas.Fill(span, sx, shoulderY + (int)(20 * scale), (int)(24 * scale), (int)(10 * scale), ink);

        // Tails.
        if (people is "Miqo'te" or "Au Ra")
        {
            var thick = people == "Au Ra" ? (int)(10 * scale) : (int)(6 * scale);
            var tx = cx + (int)(40 * scale);
            var ty = hemY - (int)(10 * scale);
            var wag = (int)(Math.Sin(walk > 0 ? walk * 6.0 : 0.0) * 6 * scale);
            Canvas.Line(span, tx, ty, tx + (int)(30 * scale), ty - (int)(30 * scale) + wag, ink);
            for (var i = 1; i < thick; i++)
                Canvas.Line(span, tx, ty + i, tx + (int)(30 * scale), ty - (int)(30 * scale) + wag + i, ink);
        }
    }

    /// <summary>An icon as a cutout: every pixel that is solid enough goes black.</summary>
    private void DrawCutout(Span<uint> span, uint iconId, int x, int y, int size, uint ink)
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
                var lum = ((p & 0xFF) + ((p >> 8) & 0xFF) + ((p >> 16) & 0xFF)) / 3;

                // The portrait frames are squares of mid-grey: those go too. What stays is the
                // figure itself, dark against its ground, and anything fully solid.
                var isFrame = tx < 4 || ty < 4 || tx >= size - 4 || ty >= size - 4;
                if (a < 128 || isFrame)
                    continue;

                if (lum < 150)
                    span[(yy * W) + xx] = ink;
            }
        }
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
