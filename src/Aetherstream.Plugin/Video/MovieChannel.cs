namespace Aetherstream.Plugin.Video;

/// <summary>
/// The film channel. Every five minutes a whole film, generated from a seed: a genre, a title,
/// two or three of the trailer cast, nine scenes with their own country, hour, weather and
/// lines, then the credits. Three genres in rotation: a Starlight romance about a big-city
/// someone coming home, a science fiction film about a signal, and an action film about a
/// crystal that must not fall into the wrong hands. Every film is the same for everyone
/// watching, and none of them exist.
/// </summary>
internal sealed class MovieChannel(BitmapFont font) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double FilmFor = 300.0;
    private const double TitleFor = 8.0;
    private const double SceneFor = 28.0;
    private const int Scenes = 9;
    private const double CreditsFor = 30.0;
    private const int Bar = 90;

    private static readonly uint Ink = Canvas.Rgb(0x0C, 0x0A, 0x10);
    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Gold = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint Rose = Canvas.Rgb(0xFF, 0x8A, 0xB0);
    private static readonly uint Cyan = Canvas.Rgb(0x6A, 0xD8, 0xFF);
    private static readonly uint Faint = Canvas.Rgb(0x9A, 0x96, 0xA8);

    private enum Genre
    {
        Romance,
        SciFi,
        Action,
    }

    private enum Shot
    {
        Card,
        Arrive,
        Talk,
        Weather,
        Chase,
        Boom,
        Ship,
        Close,
        Standoff,
    }

    private sealed record Scene(Shot Shot, Biome Biome, float Daylight, string Card, string LineA, string LineB, bool Rain, bool Snow);

    private sealed record Film(Genre Genre, string Title, int Lead, int Foil, int Third, Scene[] Scenes, string Tagline);

    // -- the writers' room -------------------------------------------------------------------------------------

    private static readonly string[] RomanceTitles = ["A Starlight in {place}", "The Baker of {place}", "Coming Home to {place}", "Snow on {place}", "{place} for the Holidays", "Love, Kupo", "The Last Ferry to {place}", "An Ishgardian Starlight"];
    private static readonly string[] SciFiTitles = ["Signal", "Beyond the Sea of Stars", "Allagan Dawn", "The Ultima Protocol", "Station Azys", "Dark Aether", "First Contact at Mare Lamentorum", "The Voidgate"];
    private static readonly string[] ActionTitles = ["Crystal Force", "Die Kupo", "The Garlean Job", "Hard Aether", "Rush to Radz-at-Han", "Maximum Chocobo", "Fists of Thal", "Speed Tram"];
    private static readonly string[] Places = ["Gridania", "Ishgard", "Kugane", "Limsa", "Idyllshire", "Ala Mhigo", "Tuliyollal", "the Lavender Beds"];

    private static readonly string[] RomanceCards = ["{place}. Starlight Eve. Three days before the festival.", "The bakery. It is not doing well.", "Seven years since she left.", "The festival. Everyone is here. Everyone.", "The last ferry leaves at dawn."];
    private static readonly string[] SciFiCards = ["Year 3 of the Seventh Astral Era. Somewhere above the clouds.", "The signal repeats every twenty-three minutes.", "Station Azys. Life support at forty percent.", "They are not answering.", "The Sea of Stars. Nobody has come back from here."];
    private static readonly string[] ActionCards = ["Ul'dah. 0400 hours.", "The crystal is moving. So are they.", "Somewhere under Garlemald.", "Twelve minutes to the tram.", "This time, it's personal. It was also personal last time."];

    private static readonly (string A, string B)[] RomanceLines =
    [
        ("You came back.", "The bakery was going to close. Somebody had to."),
        ("You still make the star buns?", "Every Starlight. Even the year you didn't come."),
        ("The city was fine.", "Then why are you standing in the snow?"),
        ("I have a meeting on the fifth.", "The festival is on the fourth. Funny how that works."),
        ("You kept the sign.", "You painted it. I couldn't take it down."),
        ("I forgot how quiet it is here.", "It isn't. You just stopped listening."),
        ("Come to the festival. One dance.", "One dance. Then I really have to go."),
        ("Stay.", "...Say it again."),
    ];

    private static readonly (string A, string B)[] SciFiLines =
    [
        ("It's the same pattern. Twenty-three minutes.", "That's not a pattern. That's a heartbeat."),
        ("Hull's holding.", "The hull is not what I'm worried about."),
        ("What was that?", "The thing we were told wasn't here."),
        ("It's beautiful.", "It's looking at us."),
        ("We can't go back. Not with this.", "Then we go forward. Set a course for the noise."),
        ("Life support's at forty.", "Then talk less. Fly more."),
        ("Do you hear that?", "I've been hearing it since Azys Lla."),
        ("Whatever built this... it's still here.", "Then let's introduce ourselves."),
    ];

    private static readonly (string A, string B)[] ActionLines =
    [
        ("Where's the crystal?", "Somewhere you'll never find it. My inventory."),
        ("We have twelve minutes.", "I've done it in nine. Drunk."),
        ("You brought a chocobo?", "I brought THE chocobo."),
        ("They've got the tram.", "Then we take the other tram."),
        ("Nobody moves. Nobody gets hurt.", "That's not how this usually goes."),
        ("You're bleeding.", "It's not mine. It's mostly not mine."),
        ("Get to the aetheryte!", "It's on cooldown!"),
        ("Was that the last one?", "That was the last one. Probably."),
    ];

    private static readonly string[] RomanceTags = ["This Starlight, the way home is the way back.", "Some recipes are worth staying for.", "She left for the city. Her heart stayed for the buns."];
    private static readonly string[] SciFiTags = ["Something is calling. Nobody is answering.", "In the Sea of Stars, every light is a question.", "We were never alone. We were never even quiet."];
    private static readonly string[] ActionTags = ["One crystal. One tram. One very bad morning.", "They took his chocobo. Now he's taking everything.", "Twelve minutes. Twelve gods. One shot."];

    private static readonly string[] Crew = ["DIRECTED BY A MOOGLE", "PRODUCED BY THE MAKERS OF THINGS", "MUSIC BY WHATEVER IS ON THE JUKEBOX", "KEY GRIP: A CHOCOBO", "STUNTS: THE CAST, REGRETTABLY", "CATERING: THE EORZEAN KITCHEN", "SHOT ON LOCATION IN A SPAN", "NO NAMAZU WERE HARMED", "THE FILM YOU HAVE JUST SEEN DOES NOT EXIST", "ANY RESEMBLANCE TO ACTUAL FILMS IS THE JOKE"];

    private readonly Dictionary<int, Film> films = [];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var clock = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0) - 1_700_000_000;
        var index = (int)(clock / FilmFor);
        var into = clock % FilmFor;
        this.RenderAt(target, index, into, seconds);
    }

    private void RenderAt(uint[] target, int index, double into, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var film = this.Write(index);

        if (into < TitleFor)
        {
            this.TitleCard(span, film, into, all);
        }
        else if (into < TitleFor + (Scenes * SceneFor))
        {
            var n = (int)((into - TitleFor) / SceneFor);
            var t = into - TitleFor - (n * SceneFor);
            if (t < 0.15)
                Canvas.Fill(span, 0, 0, W, H, Ink);
            else
                this.Play(span, film, film.Scenes[n], n, t, seconds, all);
        }
        else if (into < TitleFor + (Scenes * SceneFor) + CreditsFor)
        {
            this.Credits(span, film, into - TitleFor - (Scenes * SceneFor), all);
        }
        else
        {
            this.Intermission(span, this.Write(index + 1), FilmFor - into, seconds, all);
        }

        Canvas.Fill(span, 0, 0, W, Bar, Ink);
        Canvas.Fill(span, 0, H - Bar, W, Bar, Ink);
        font.Draw(span, W, "AETHERSTREAM CINEMA", 24, 8, Faint, 1, all);
        var name = Canvas.Cut(film.Title.ToUpperInvariant(), 40);
        font.Draw(span, W, name, W - 24 - font.Measure(name), 8, Faint, 1, all);
    }

    // -- writing the film -------------------------------------------------------------------------------------

    private Film Write(int index)
    {
        if (this.films.TryGetValue(index, out var have))
            return have;

        if (this.films.Count > 8)
            this.films.Clear();

        var genre = (Genre)(index % 3);
        var seed = index * 31;
        var place = Places[Pick(seed + 1, Places.Length)];
        var title = genre switch
        {
            Genre.Romance => RomanceTitles[Pick(seed + 2, RomanceTitles.Length)],
            Genre.SciFi => SciFiTitles[Pick(seed + 2, SciFiTitles.Length)],
            _ => ActionTitles[Pick(seed + 2, ActionTitles.Length)],
        };
        title = title.Replace("{place}", place);
        var lead = Pick(seed + 3, 6);
        var foil = (lead + 1 + Pick(seed + 4, 5)) % 6;
        var third = (foil + 1 + Pick(seed + 5, 4)) % 6;
        if (third == lead)
            third = (third + 1) % 6;

        var cards = genre switch { Genre.Romance => RomanceCards, Genre.SciFi => SciFiCards, _ => ActionCards };
        var lines = genre switch { Genre.Romance => RomanceLines, Genre.SciFi => SciFiLines, _ => ActionLines };
        var tags = genre switch { Genre.Romance => RomanceTags, Genre.SciFi => SciFiTags, _ => ActionTags };

        // The shape of each genre's story, scene by scene.
        var shape = genre switch
        {
            Genre.Romance => new[] { Shot.Card, Shot.Arrive, Shot.Talk, Shot.Weather, Shot.Talk, Shot.Card, Shot.Talk, Shot.Weather, Shot.Close },
            Genre.SciFi => new[] { Shot.Card, Shot.Ship, Shot.Talk, Shot.Ship, Shot.Card, Shot.Talk, Shot.Weather, Shot.Ship, Shot.Close },
            _ => new[] { Shot.Card, Shot.Arrive, Shot.Chase, Shot.Talk, Shot.Boom, Shot.Card, Shot.Standoff, Shot.Boom, Shot.Chase },
        };

        var scenes = new Scene[Scenes];
        for (var i = 0; i < Scenes; i++)
        {
            var biome = genre switch
            {
                Genre.Romance => Pick(seed + 10 + i, 3) == 0 ? Biome.Forest : Biome.Snow,
                Genre.SciFi => Pick(seed + 10 + i, 2) == 0 ? Biome.Night : Biome.Highland,
                _ => (Biome)Pick(seed + 10 + i, 7),
            };
            var daylight = genre == Genre.SciFi ? 0.05f : Pick(seed + 20 + i, 3) == 0 ? 0.15f : 0.8f;
            var (a, b) = lines[(Pick(seed + 30, lines.Length) + i) % lines.Length];
            scenes[i] = new Scene(
                shape[i],
                biome,
                daylight,
                cards[(Pick(seed + 40, cards.Length) + (i / 2)) % cards.Length].Replace("{place}", place),
                a,
                b,
                Rain: genre != Genre.Romance && Pick(seed + 50 + i, 3) == 0,
                Snow: genre == Genre.Romance && biome == Biome.Snow);
        }

        var film = new Film(genre, title, lead, foil, third, scenes, tags[Pick(seed + 6, tags.Length)]);
        this.films[index] = film;
        return film;
    }

    // -- playing it ---------------------------------------------------------------------------------------------

    private void TitleCard(Span<uint> span, Film film, double t, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 0, W, H, Ink);
        var colour = film.Genre switch { Genre.Romance => Rose, Genre.SciFi => Cyan, _ => Gold };
        var title = film.Title.ToUpperInvariant();
        var scale = font.Measure(title, 2) <= W - 120 ? 2 : 1;
        if (t > 1.0)
            font.Draw(span, W, title, (W - font.Measure(title, scale)) / 2, 280, colour, scale, all);
        if (t > 3.5)
        {
            var star = $"STARRING {HostSprites.CastNames[film.Lead]} AND {HostSprites.CastNames[film.Foil]}".ToUpperInvariant();
            font.Draw(span, W, star, (W - font.Measure(star)) / 2, 400, Cream, 1, all);
        }

        if (t > 5.5)
            font.Draw(span, W, film.Tagline.ToUpperInvariant(), (W - font.Measure(film.Tagline.ToUpperInvariant())) / 2, 450, Faint, 1, all);
    }

    private void Play(Span<uint> span, Film film, Scene scene, int n, double t, double seconds, in BitmapFont.Clip all)
    {
        // The country, then the shot, then the weather over it, then the words.
        if (scene.Shot == Shot.Ship)
            Space(span, seconds);
        else
            Scenery.Paint(span, W, 0, 380, H, scene.Biome, scene.Daylight, (n * 7) + 3, scene.Shot == Shot.Chase ? seconds * 120 : 0);

        var lead = film.Lead;
        var foil = film.Foil;
        switch (scene.Shot)
        {
            case Shot.Card:
                for (var y = Bar; y < H - Bar; y++)
                {
                    var row = span.Slice(y * W, W);
                    for (var x = 0; x < W; x++)
                        row[x] = Canvas.Lerp(row[x], Ink, 0.6f);
                }

                foreach (var (line, i) in Canvas.Wrap(scene.Card.ToUpperInvariant(), font.Fit(W - 200), 3).Select((l, i) => (l, i)))
                    font.Draw(span, W, line, 100, 150 + (i * 40), Cream, 1, all);
                if (t > 14)
                    HostSprites.DrawCast(span, lead, walk: true, seconds, -80 + (int)((t - 14) * 40), 330, 7);
                break;

            case Shot.Arrive:
                HostSprites.DrawCast(span, lead, walk: true, seconds, Math.Min(500, -80 + (int)(t * 45)), 330, 7);
                if (t > 10)
                    HostSprites.DrawCast(span, foil, walk: false, seconds, 820, 330, 7, flip: true);
                if (t > 14)
                    this.Subtitle(span, t < 21 ? scene.LineA : scene.LineB, all);
                break;

            case Shot.Talk:
                HostSprites.DrawCast(span, lead, walk: false, seconds, 340, 300, 8);
                HostSprites.DrawCast(span, foil, walk: false, seconds, 760, 300, 8, flip: true);
                if (t > 3 && t < 12)
                    this.Subtitle(span, scene.LineA, all);
                else if (t > 14 && t < 24)
                    this.Subtitle(span, scene.LineB, all);
                break;

            case Shot.Weather:
                HostSprites.DrawCast(span, lead, walk: false, seconds, 560, 240, 10);
                if (t > 6 && t < 16)
                    this.Subtitle(span, scene.LineA, all);
                else if (t > 18)
                    this.Subtitle(span, scene.LineB, all);
                break;

            case Shot.Chase:
                HostSprites.DrawCast(span, lead, walk: true, seconds, 700 - (int)(Math.Sin(t * 0.7) * 120), 330, 7, flip: true);
                HostSprites.DrawCast(span, foil, walk: true, seconds, 1000 - (int)(Math.Sin(t * 0.7 + 0.5) * 120), 330, 7, flip: true);
                if (film.Genre == Genre.Action)
                    HostSprites.DrawCast(span, film.Third, walk: true, seconds, 1200 - (int)(Math.Sin(t * 0.7 + 1.0) * 100), 330, 7, flip: true);
                if (t > 8 && t < 15)
                    this.Subtitle(span, scene.LineA, all);
                else if (t > 17 && t < 25)
                    this.Subtitle(span, scene.LineB, all);
                break;

            case Shot.Boom:
                HostSprites.DrawCast(span, lead, walk: true, seconds, 200 + (int)(t * 30), 330, 7);
                HostSprites.DrawCast(span, foil, walk: true, seconds, 60 + (int)(t * 30), 330, 7);
                for (var b = 0; b < 3; b++)
                {
                    var at = 6.0 + (b * 6.0);
                    if (t > at && t < at + 2.5)
                        Explosion(span, 900 + (b * 120) - (b * b * 90), 440, t - at);
                }

                if (t > 20)
                    this.Subtitle(span, scene.LineB, all);
                break;

            case Shot.Ship:
                Ship(span, 200 + (int)(Math.Sin(seconds * 0.5) * 30), 300 + (int)(Math.Sin(seconds * 0.9) * 14), t);
                HostSprites.DrawCast(span, lead, walk: false, seconds, 900, 380, 6, flip: true);
                if (t > 4 && t < 12)
                    this.Subtitle(span, scene.LineA, all);
                else if (t > 15 && t < 24)
                    this.Subtitle(span, scene.LineB, all);
                break;

            case Shot.Close:
                // The two of them, closer each second, and hearts or stars by genre.
                var gap = Math.Max(60, 300 - (int)(t * 12));
                HostSprites.DrawCast(span, lead, walk: false, seconds, 640 - gap - 96, 260, 9);
                HostSprites.DrawCast(span, foil, walk: false, seconds, 640 + gap - 96, 260, 9, flip: true);
                if (t > 16)
                {
                    for (var i = 0; i < 6; i++)
                    {
                        var hx = 560 + (i * 40) + (int)(Math.Sin(seconds * 2 + i) * 10);
                        var hy = 200 - (int)(((t - 16) * 20 + (i * 30)) % 160);
                        if (film.Genre == Genre.Romance)
                            Heart(span, hx, hy, Rose);
                        else
                            Canvas.Disc(span, hx, hy, 3, Cyan);
                    }
                }

                if (t > 4 && t < 12)
                    this.Subtitle(span, scene.LineA, all);
                else if (t > 20)
                    this.Subtitle(span, scene.LineB, all);
                break;

            case Shot.Standoff:
                HostSprites.DrawCast(span, lead, walk: false, seconds, 200, 300, 8);
                HostSprites.DrawCast(span, foil, walk: false, seconds, 900, 300, 8, flip: true);
                HostSprites.DrawCast(span, film.Third, walk: false, seconds, 560, 320, 7, flip: t % 2 < 1);
                if ((int)(t * 2) % 7 == 0)
                    Canvas.Fill(span, 0, Bar, W, H - (2 * Bar), Canvas.Lerp(Cream, Ink, 0.4f));
                if (t > 5 && t < 13)
                    this.Subtitle(span, scene.LineA, all);
                else if (t > 16 && t < 26)
                    this.Subtitle(span, scene.LineB, all);
                break;
        }

        if (scene.Rain)
            Rain(span, seconds);
        if (scene.Snow)
            Snow(span, seconds);
    }

    private void Credits(Span<uint> span, Film film, double t, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 0, W, H, Ink);
        var lines = new List<string> { film.Title.ToUpperInvariant(), string.Empty, $"{HostSprites.CastNames[film.Lead].ToUpperInvariant()} AS THE ONE WHO CAME BACK", $"{HostSprites.CastNames[film.Foil].ToUpperInvariant()} AS THE ONE WHO STAYED", $"{HostSprites.CastNames[film.Third].ToUpperInvariant()} AS A THIRD PERSON", string.Empty };
        lines.AddRange(Crew);
        var y = H - (int)(t * 40);
        foreach (var line in lines)
        {
            if (y > Bar - 40 && y < H - Bar)
                font.Draw(span, W, line, (W - font.Measure(line)) / 2, y, line == lines[0] ? Gold : Cream, 1, all);
            y += 44;
        }
    }

    private void Intermission(Span<uint> span, Film next, double left, double seconds, in BitmapFont.Clip all)
    {
        Canvas.Fill(span, 0, 0, W, H, Ink);
        Canvas.Disc(span, W / 2, 330, 120 + (int)(Math.Sin(seconds) * 6), Canvas.Lerp(Ink, Gold, 0.15f));
        font.Draw(span, W, "NEXT ON AETHERSTREAM CINEMA", (W - font.Measure("NEXT ON AETHERSTREAM CINEMA")) / 2, 240, Faint, 1, all);
        var title = next.Title.ToUpperInvariant();
        font.Draw(span, W, title, (W - font.Measure(title)) / 2, 300, Gold, 1, all);
        var genre = next.Genre switch { Genre.Romance => "A STARLIGHT ROMANCE", Genre.SciFi => "SCIENCE FICTION", _ => "ACTION" };
        font.Draw(span, W, genre, (W - font.Measure(genre)) / 2, 340, Cream, 1, all);
        var count = $"IN {(int)left}";
        font.Draw(span, W, count, (W - font.Measure(count)) / 2, 400, Faint, 1, all);
    }

    // -- effects ------------------------------------------------------------------------------------------------

    private static void Space(Span<uint> span, double seconds)
    {
        Canvas.Fill(span, 0, 0, W, H, Canvas.Rgb(0x04, 0x06, 0x14));
        for (var i = 0; i < 160; i++)
        {
            var x = ((i * 97) - (int)(seconds * (10 + (i % 3) * 8))) % W;
            if (x < 0)
                x += W;
            Canvas.Plot(span, x, (i * 53) % H, i % 4 == 0 ? Cyan : Cream);
        }

        Canvas.Disc(span, 1040, 520, 160, Canvas.Rgb(0x3A, 0x2A, 0x6A));
        Canvas.Disc(span, 1000, 490, 120, Canvas.Rgb(0x5A, 0x3E, 0x8A));
    }

    private static void Ship(Span<uint> span, int x, int y, double t)
    {
        var hull = Canvas.Rgb(0xB8, 0xBC, 0xC8);
        var dark = Canvas.Rgb(0x6A, 0x70, 0x80);
        Canvas.Fill(span, x, y, 260, 60, hull);
        Canvas.Fill(span, x + 260, y + 10, 60, 40, dark);
        Canvas.Fill(span, x + 320, y + 22, 24, 16, Cyan);
        Canvas.Fill(span, x + 40, y - 30, 120, 30, hull);
        Canvas.Fill(span, x + 60, y - 20, 40, 14, Cyan);
        Canvas.Fill(span, x - 40, y + 14, 40, 32, dark);
        var flame = 20 + (int)(Math.Sin(t * 30) * 8);
        Canvas.Fill(span, x - 40 - flame, y + 22, flame, 16, Canvas.Rgb(0xFF, 0xB0, 0x40));
    }

    private static void Explosion(Span<uint> span, int cx, int cy, double t)
    {
        var r = (int)(t * 80);
        Canvas.Disc(span, cx, cy, r, Canvas.Lerp(Canvas.Rgb(0xFF, 0x8A, 0x20), Ink, (float)Math.Min(1.0, t / 2.5)));
        Canvas.Disc(span, cx, cy, r / 2, Canvas.Lerp(Canvas.Rgb(0xFF, 0xE0, 0x80), Ink, (float)Math.Min(1.0, t / 2.0)));
        if (t < 0.3)
            Canvas.Fill(span, 0, Bar, W, H - (2 * Bar), Canvas.Lerp(Cream, Canvas.Rgb(0xFF, 0xB0, 0x40), 0.5f));
    }

    private static void Heart(Span<uint> span, int x, int y, uint colour)
    {
        Canvas.Disc(span, x - 4, y, 5, colour);
        Canvas.Disc(span, x + 4, y, 5, colour);
        for (var i = 0; i < 8; i++)
            Canvas.Fill(span, x - 8 + i, y + i, 17 - (2 * i), 1, colour);
    }

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

    private static void Snow(Span<uint> span, double seconds)
    {
        for (var i = 0; i < 140; i++)
        {
            var x = ((i * 97) + (int)(Math.Sin(seconds + i) * 20)) % W;
            var y = ((i * 53) + (int)(seconds * 60)) % H;
            Canvas.Disc(span, x, y, i % 3 == 0 ? 3 : 2, Cream);
        }
    }

    private void Subtitle(Span<uint> span, string line, in BitmapFont.Clip all)
    {
        var text = $"\"{line.ToUpperInvariant()}\"";
        var w = font.Measure(text);
        Canvas.Fill(span, (W - w) / 2 - 16, 560, w + 32, 44, Canvas.Lerp(Ink, Canvas.Black, 0.2f));
        font.Draw(span, W, text, (W - w) / 2, 562, Cream, 1, all);
    }

    private static int Pick(int seed, int count)
    {
        var x = (uint)seed * 2654435761u;
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
