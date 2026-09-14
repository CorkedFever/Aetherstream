namespace Aetherstream.Plugin.Video;

/// <summary>What the card is addressed to: a deity by the game's own row id (1 Halone … 12 Althyk).</summary>
internal sealed record HoroscopeSnapshot(int DeityId, bool OwnDeity);

/// <summary>
/// A Sharlayan reading, one a day, for those born under a guardian. Everything on the card is
/// the setting's own astrology: the Twelve and the moons they rule, their elements and symbols,
/// the six arcana with the meanings the astrologians give them, the diurnal and nocturnal sects.
/// The lines are written in that register and seeded by the day and the deity, so everyone
/// under the same god reads the same card, and tomorrow's is different.
/// </summary>
internal sealed class HoroscopeChannel(BitmapFont font, Func<HoroscopeSnapshot?> data) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;

    private sealed record Deity(string Name, string Epithet, string Element, string Symbol, string Colour, uint Tint, string[] Lines);

    private sealed record Arcana(string Name, string Meaning, string[] Art, string[] Lines);

    // Row order is the game's: the First Astral Moon is Halone's, the First Umbral Menphina's,
    // and so on round the year. Each element has an astral and an umbral deity; they are kin here.
    private static readonly Deity[] Twelve =
    [
        new("Halone", "the Fury", "ice", "the spear", "steel grey", Canvas.Rgb(0xB8, 0xC8, 0xE0),
        [
            "Halone asks for a straight back and a plain answer. Give both.",
            "The Fury does not reward the second-guess. Choose, then hold the line.",
            "A grievance kept is a spear carried point-down. Set it against something or set it down.",
            "The ice holds what is put into it. Be careful what you freeze today.",
            "Someone will test you before the sun is high. That is the whole of the lesson.",
        ]),
        new("Menphina", "the Lover", "ice", "the moon", "moonlit silver", Canvas.Rgb(0xD8, 0xE4, 0xFF),
        [
            "Menphina lights the road you have already chosen. Look up; it is there.",
            "The Lover's moon is patient with the shy. Say the thing, or write it.",
            "What warms you tonight was cold once. Remember that when you are asked for warmth.",
            "A kindness given at the wrong hour is still a kindness. Do not wait for the right one.",
            "Someone thinks of you more than you would guess. Let that be enough for a day.",
        ]),
        new("Thaliak", "the Scholar", "water", "the scroll", "river blue", Canvas.Rgb(0x6F, 0xB4, 0xE8),
        [
            "Thaliak favours the one who reads the whole page. Skip nothing today.",
            "A question well asked is half its own answer. Spend the morning on the question.",
            "The river carries what it is given. Give it something worth carrying downstream.",
            "You already know the thing you are pretending to look up.",
            "Write it down. The Scholar trusts ink over memory, and so should you.",
        ]),
        new("Nymeia", "the Spinner", "water", "the spindle", "pale blue", Canvas.Rgb(0xA8, 0xD8, 0xF0),
        [
            "Nymeia has a thread for you today, and it runs through someone else's hands.",
            "The Spinner does not knot; she lets the yarn find its own turn. Stop pulling.",
            "What looked like chance last moon will look like a pattern by this one.",
            "Cut the thread that has stopped weaving anything. She will not miss it.",
            "You are a strand in a larger cloth. Today you will feel the loom move.",
        ]),
        new("Llymlaen", "the Navigator", "wind", "the ship", "sea green", Canvas.Rgb(0x5D, 0xCA, 0xA5),
        [
            "Llymlaen sets a course and trusts the wind to argue. Argue back, then sail.",
            "A harbour is only a place to leave from. Do not confuse it for home.",
            "The Navigator sees the storm two bells out. If you feel one coming, you are right.",
            "Take the long way round the cape. It is long for a reason.",
            "Someone needs you at the tiller today. It is not the person you expect.",
        ]),
        new("Oschon", "the Wanderer", "wind", "the bow", "leaf green", Canvas.Rgb(0x8F, 0xD9, 0x8B),
        [
            "Oschon's road has no end, which is why he likes it. Walk a little of it today.",
            "The Wanderer keeps no schedule. Miss one thing on purpose and see what you find.",
            "An arrow loosed in the wind still lands. Loose it.",
            "You will meet someone going the other way. Trade directions.",
            "Rest is a kind of travel too, when it is chosen. Choose it before it chooses you.",
        ]),
        new("Byregot", "the Builder", "lightning", "the hammer", "burnished copper", Canvas.Rgb(0xE0, 0x9A, 0x5C),
        [
            "Byregot begins at the foundation. Whatever you start today, start it low and square.",
            "The Builder's hammer falls twice on every nail. The second blow is the one that holds.",
            "Something you made is being used by someone you will never meet. Make another.",
            "Measure it again. He will not mind, and the wood will.",
            "A tool set down carefully is a promise to tomorrow. Put things back today.",
        ]),
        new("Rhalgr", "the Destroyer", "lightning", "the comet", "storm violet", Canvas.Rgb(0xB8, 0x8C, 0xFF),
        [
            "Rhalgr breaks what has stopped serving. Look for what you have been sparing out of habit.",
            "The Destroyer's moon favours the bold and forgets the careful. Be remembered.",
            "A comet is only a stone that decided to be seen. Decide.",
            "Do not rebuild on the old footprint. He knocked it down for a reason.",
            "Someone will ask what you are afraid of. Tell them, and watch it shrink.",
        ]),
        new("Azeyma", "the Warden", "fire", "the sun", "sunlit gold", Canvas.Rgb(0xFF, 0xD1, 0x5C),
        [
            "Azeyma keeps the day honest. Nothing hidden this morning will stay hidden by dusk.",
            "The Warden's light is even; it falls on the deserving and the rest alike. Stand in it.",
            "Warmth spent on a stranger comes back as an ember. Spend some.",
            "Do the visible thing first. The sun sees you either way.",
            "You are being watched over, not watched. There is a difference, and today you will feel it.",
        ]),
        new("Nald'thal", "the Traders", "fire", "the scales", "coin gold", Canvas.Rgb(0xF2, 0xC1, 0x4E),
        [
            "Nald'thal weigh both pans. What you gain today will cost exactly what it should.",
            "The Traders keep the ledger of the living and the dead. Settle a small debt while you can.",
            "Every bargain has a second buyer standing behind you. Sell to the honest one.",
            "Count it twice, and then give a little of it away. The scales like it.",
            "A thing bought cheap was sold cheap by someone. Ask why before you are pleased.",
        ]),
        new("Nophica", "the Matron", "earth", "the spring", "harvest green", Canvas.Rgb(0x7F, 0xC8, 0x6B),
        [
            "Nophica's spring runs whether you drink or not. Drink.",
            "The Matron plants for a harvest she will not see. Do one thing today for a later you.",
            "Tend what is already growing before you break new ground.",
            "Soil remembers every hand that turned it. Be a gentle one.",
            "Feed someone today. The bread is not the point.",
        ]),
        new("Althyk", "the Keeper", "earth", "the hourglass", "slate", Canvas.Rgb(0x9F, 0xB0, 0xC8),
        [
            "Althyk turns the glass and does not hurry the sand. Neither should you.",
            "The Keeper's hour comes for the patient and the impatient at the same moment. Be the patient one.",
            "What you put off today will still be there, but smaller or larger. Decide which.",
            "Time is the only thing the Keeper will not lend. Spend the next bell as if you knew that.",
            "An old promise is due. You know the one.",
        ]),
    ];

    // The six arcana, with the meanings the astrologians give them: strength, endurance, haste,
    // boldness, the mind, the spirit. The art is a small glyph for each, in the card's colours.
    private static readonly Arcana[] Six =
    [
        new("The Balance", "strength",
        [
            "....######....",
            ".....#..#.....",
            "......##......",
            "..############",
            ".#.....#.....#",
            "#......#......#",
            "#......#......#",
            ".######.######",
            "......##......",
            "......##......",
            "......##......",
            "....######....",
        ],
        [
            "The Balance is drawn. What you carry, you can carry; put your shoulder to it.",
            "Strength today is the quiet kind: the weight taken without being asked.",
            "The Balance favours the one who lifts and says nothing. Lift.",
            "Do not measure yourself against the strongest in the room. Measure against yesterday.",
            "A burden shared is a burden halved, says the Balance, and it has never once been wrong.",
        ]),
        new("The Bole", "endurance",
        [
            "......##......",
            ".....####.....",
            "....######....",
            "...########...",
            "..##########..",
            "..##########..",
            "..##########..",
            "......##......",
            "......##......",
            "......##......",
            ".....####.....",
            "....######....",
        ],
        [
            "The Bole is drawn. Roots before branches; see to what holds you up.",
            "Endurance is not standing still. It is standing, still.",
            "The Bole asks patience of you today. It is not the same as waiting.",
            "What bends in the wind is still standing after it. Bend.",
            "You have outlasted worse than today. The tree remembers even if you do not.",
        ]),
        new("The Arrow", "haste",
        [
            "............##",
            "...........###",
            "..........####",
            ".........##.##",
            "........##..##",
            ".......##.....",
            "......##......",
            ".....##.......",
            "....##........",
            "...##.........",
            "..##..........",
            ".##...........",
        ],
        [
            "The Arrow is drawn. The moment is short; do not spend it deciding.",
            "Haste is a gift when it is aimed. Aim first, then do not hesitate.",
            "The Arrow does not return to the string. Be sure, then be quick.",
            "Today rewards the first mover. Be the first, and forgive yourself the rest.",
            "Speed without a target is only noise. Pick the target before noon.",
        ]),
        new("The Spear", "boldness",
        [
            "......##......",
            ".....####.....",
            "....######....",
            ".....####.....",
            "......##......",
            "......##......",
            "......##......",
            "......##......",
            "......##......",
            "......##......",
            "......##......",
            "......##......",
        ],
        [
            "The Spear is drawn. Say the bold thing plainly, once.",
            "Boldness is not loudness. The Spear is quiet until the moment it is not.",
            "A point aimed at the ground threatens no one. Raise it.",
            "The Spear favours the one who steps forward when everyone else steps back.",
            "There is a door you have been knocking on. Today, try the handle.",
        ]),
        new("The Ewer", "the mind",
        [
            "....######....",
            "...#......#...",
            "...#......#...",
            "..##......##..",
            ".#..........#.",
            ".#..........#.",
            ".#..........#.",
            ".#..........#.",
            "..#........#..",
            "...#......#...",
            "....######....",
            "..............",
        ],
        [
            "The Ewer is drawn. Pour out what you have been carrying; the mind holds more when it is emptied.",
            "Thought is water: it takes the shape of what it is poured into. Choose the vessel.",
            "The Ewer favours the one who listens twice and speaks once.",
            "A clear mind is not an empty one. Fill it with one thing and let the rest settle.",
            "Something you have been puzzling over will resolve if you stop pressing on it.",
        ]),
        new("The Spire", "the spirit",
        [
            "......##......",
            "......##......",
            ".....####.....",
            ".....####.....",
            "....######....",
            "....######....",
            "...########...",
            "...########...",
            "..##########..",
            "..##########..",
            ".############.",
            "##############",
        ],
        [
            "The Spire is drawn. Tend to the spirit before the sword; the sword can wait.",
            "The Spire rises one course at a time. So does resolve.",
            "What you believe is the thing you keep building when no one is watching. Keep building.",
            "The Spire favours the one who looks up when it is easier to look down.",
            "Rest is not a retreat from the climb. Take the landing.",
        ]),
    ];

    private static readonly string[] Diurnal =
    [
        "The sun is up and the diurnal sect holds: what is begun now is begun in the open.",
        "A diurnal reading. Favour the visible task, the direct word, the plain road.",
        "Under the diurnal sect the heavens are frank. Expect the same of people today.",
    ];

    private static readonly string[] Nocturnal =
    [
        "The stars are out and the nocturnal sect holds: what is begun now is begun in trust.",
        "A nocturnal reading. Favour the quiet work, the letter, the thing done for its own sake.",
        "Under the nocturnal sect the heavens keep counsel. Keep some of your own.",
    ];

    private static readonly string[] OwnMoon =
    [
        "This is your guardian's own moon. The year turns on your hinge; use the leverage.",
        "The moon of your birth is in. What you ask for in it is heard twice.",
        "Your guardian rules this moon. Small luck compounds; do not waste the small ones.",
    ];

    private static readonly string[] KinMoon =
    [
        "The moon belongs to your guardian's kin in element. Familiar weather; unfamiliar hands.",
        "A kindred moon is in. What is yours by element is lent, not given, this moon.",
        "Your element's other face rules this moon. Expect the same lesson from the opposite side.",
    ];

    private static readonly string[] OtherMoon =
    [
        "The moon is not yours. Borrow its ruler's virtue for a while and return it in better condition.",
        "A foreign moon. Its ruler will not mind if you keep your head down and your word.",
        "Another god keeps this moon. Be a good guest in it.",
    ];

    private static readonly string[] Counsel =
    [
        "Eat before you decide anything.",
        "The retainer has something to tell you. It is not important, but it is true.",
        "Somebody in your free company is right about the thing you argued over. Concede it.",
        "Do not buy the thing on the market board today. Tomorrow it will be cheaper, or you will not want it.",
        "A long walk between aetherytes will do what the teleport cannot.",
        "Answer the letter. The moogle has been patient.",
        "Fish for an hour and catch nothing. It counts.",
        "Someone's chocobo knows more than it lets on.",
        "Wear the other glamour. The one you keep not wearing.",
        "Visit the house you have not visited. The door is open.",
        "Say thank you to the healer. Out loud, in the party.",
        "Finish the quest you left at the last step. It is one step.",
        "Check the sky before you commit to the afternoon.",
        "A gil found is a gil to be spent on someone else.",
        "Sit on the bench in the Mist and watch the boats for a bell.",
        "Do not read the Lodestone before breakfast.",
        "The song in the jukebox you skip is the one someone else needs to hear.",
        "Log out at a sensible hour. The Keeper is watching the glass.",
        "Tell the Wandering Minstrel nothing. He will only put it in a song.",
        "Keep one venture for the retainer who always comes back with junk. Loyalty is a currency.",
    ];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);

        Canvas.Fill(span, 0, 0, W, H, Canvas.Glass);
        Canvas.Fill(span, 0, 0, W, 56, Canvas.GlassLit);
        Canvas.Fill(span, 0, 56, W, 2, Canvas.Edge);
        font.Draw(span, W, "SHARLAYAN ALMANAC", 24, 8, Canvas.Amber, 1, all);

        // Eorzean seconds since the epoch: 1440/70 of a real second each.
        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var etSeconds = unixMs * 144 / 7 / 1000;
        var etSecOfDay = etSeconds % 86400;
        var etHour = (int)(etSecOfDay / 3600);
        var et = $"ET {etHour:00}:{(int)(etSecOfDay / 60 % 60):00}";
        font.Draw(span, W, et, (W - font.Measure(et)) / 2, 8, Canvas.White, 1, all);

        var local = now.ToString("h:mm tt").ToUpperInvariant();
        font.Draw(span, W, local, W - 24 - font.Measure(local), 8, Canvas.White, 1, all);

        var snapshot = data();
        var deityIndex = Math.Clamp((snapshot?.DeityId ?? 1) - 1, 0, Twelve.Length - 1);
        var deity = Twelve[deityIndex];

        // The calendar: the moon in, and whose it is.
        var dayOfMoon = (int)(etSeconds / 86400 % 32) + 1;
        var moon = (int)(etSeconds / (86400L * 32) % 12) + 1;
        var ruler = Twelve[moon - 1];
        var diurnal = etHour is >= 6 and < 18;

        // One reading a day, the same for everyone under the same god. The arcana is the day's
        // alone, shared across all twelve; the lines are the day's and the deity's.
        var day = (int)(now.ToUniversalTime().Date - new DateTime(2013, 8, 27)).TotalDays;
        var arcana = Six[Pick(day, 7, Six.Length)];
        var deityLine = deity.Lines[Pick(day, 11 + deityIndex, deity.Lines.Length)];
        var arcanaLine = arcana.Lines[Pick(day, 13 + deityIndex, arcana.Lines.Length)];
        var sectLine = (diurnal ? Diurnal : Nocturnal)[Pick(day, 17 + deityIndex, 3)];
        var moonSet = moon - 1 == deityIndex ? OwnMoon : (moon - 1) / 2 == deityIndex / 2 ? KinMoon : OtherMoon;
        var moonLine = moonSet[Pick(day, 19 + deityIndex, 3)];
        var counsel = Counsel[Pick(day, 23 + deityIndex, Counsel.Length)];
        var lucky = 1 + Pick(day, 29 + deityIndex, 9);

        // The title.
        var whose = snapshot is { OwnDeity: true } ? "YOUR READING" : "A READING";
        var title = $"{whose}, FOR THOSE BORN UNDER {deity.Name.ToUpperInvariant()}, {deity.Epithet.ToUpperInvariant()}";
        title = Canvas.Cut(title, font.Fit(W - 48));
        font.Draw(span, W, title, (W - font.Measure(title)) / 2, 72, deity.Tint, 1, all);

        var date = $"THE {Ordinal(dayOfMoon)} SUN OF THE {Ordinal((moon + 1) / 2)} {(moon % 2 == 1 ? "ASTRAL" : "UMBRAL")} MOON, THE MOON OF {ruler.Name.ToUpperInvariant()}";
        date = Canvas.Cut(date, font.Fit(W - 48));
        font.Draw(span, W, date, (W - font.Measure(date)) / 2, 112, Canvas.Dim, 1, all);

        // Left: the arcana drawn, named, and its meaning; the sect under it.
        const int PanelLeft = 40, PanelWidth = 360, PanelTop = 168, PanelBottom = 660;
        Canvas.Fill(span, PanelLeft, PanelTop, PanelWidth, PanelBottom - PanelTop, Canvas.Tube);
        Canvas.Rect(span, PanelLeft, PanelTop, PanelWidth, PanelBottom - PanelTop, Canvas.Edge);

        const int Scale = 12;
        var artW = arcana.Art[0].Length * Scale;
        var artX = PanelLeft + ((PanelWidth - artW) / 2);
        Canvas.Sprite(span, arcana.Art, c => c == '#' ? deity.Tint : 0u, artX, PanelTop + 36, Scale);

        var arcanaName = arcana.Name.ToUpperInvariant();
        font.Draw(span, W, arcanaName, PanelLeft + ((PanelWidth - font.Measure(arcanaName)) / 2), PanelTop + 210, Canvas.White, 1, all);
        var meaning = $"FOR {arcana.Meaning.ToUpperInvariant()}";
        font.Draw(span, W, meaning, PanelLeft + ((PanelWidth - font.Measure(meaning)) / 2), PanelTop + 246, Canvas.Dim, 1, all);

        Canvas.Fill(span, PanelLeft + 24, PanelTop + 300, PanelWidth - 48, 2, Canvas.Edge);
        var sect = diurnal ? "DIURNAL SECT" : "NOCTURNAL SECT";
        font.Draw(span, W, sect, PanelLeft + ((PanelWidth - font.Measure(sect)) / 2), PanelTop + 316, diurnal ? Canvas.Amber : Canvas.Accent, 1, all);
        var element = $"{deity.Element.ToUpperInvariant()}  /  {deity.Symbol.ToUpperInvariant()}";
        font.Draw(span, W, element, PanelLeft + ((PanelWidth - font.Measure(element)) / 2), PanelTop + 356, Canvas.Faint, 1, all);

        var luckyLine = $"LUCKY NUMBER {lucky}";
        font.Draw(span, W, luckyLine, PanelLeft + ((PanelWidth - font.Measure(luckyLine)) / 2), PanelTop + 412, Canvas.White, 1, all);
        var colourLine = deity.Colour.ToUpperInvariant();
        font.Draw(span, W, colourLine, PanelLeft + ((PanelWidth - font.Measure(colourLine)) / 2), PanelTop + 448, deity.Tint, 1, all);

        // Right: the reading, five lines wrapped, each in its own colour so the eye can rest.
        const int TextLeft = 440, TextRight = 1240;
        var columns = font.Fit(TextRight - TextLeft);
        var y = PanelTop + 8;
        foreach (var (line, colour) in new[]
        {
            (deityLine, Canvas.White),
            (arcanaLine, deity.Tint),
            (moonLine, Canvas.Dim),
            (sectLine, Canvas.Dim),
            (counsel, Canvas.Amber),
        })
        {
            foreach (var wrapped in Canvas.Wrap(line.ToUpperInvariant(), columns, 3))
            {
                font.Draw(span, W, wrapped, TextLeft, y, colour, 1, all);
                y += 40;
            }

            y += 22;
            if (y > PanelBottom - 40)
                break;
        }

        Canvas.Fill(span, 0, 680, W, 40, Canvas.GlassLit);
        Canvas.Fill(span, 0, 680, W, 2, Canvas.Edge);
        font.Draw(span, W, "AETHERSTREAM HOROSCOPE", 24, 680, Canvas.Accent, 1, all);
        const string Note = "A NEW READING EACH DAY / THE DEITY IS SET IN SETUP";
        font.Draw(span, W, Note, W - 24 - font.Measure(Note), 680, Canvas.Faint, 1, all);
    }

    /// <summary>A deterministic pick: the same day and salt always land on the same index.</summary>
    private static int Pick(int day, int salt, int count)
    {
        var x = (uint)((day * 2654435761u) ^ (uint)(salt * 40503u));
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return (int)(x % (uint)count);
    }

    private static string Ordinal(int n) => n switch
    {
        1 => "FIRST", 2 => "SECOND", 3 => "THIRD", 4 => "FOURTH", 5 => "FIFTH", 6 => "SIXTH",
        7 => "SEVENTH", 8 => "EIGHTH", 9 => "NINTH", 10 => "TENTH", 11 => "ELEVENTH", 12 => "TWELFTH",
        13 => "THIRTEENTH", 14 => "FOURTEENTH", 15 => "FIFTEENTH", 16 => "SIXTEENTH", 17 => "SEVENTEENTH",
        18 => "EIGHTEENTH", 19 => "NINETEENTH", 20 => "TWENTIETH", 21 => "TWENTY-FIRST", 22 => "TWENTY-SECOND",
        23 => "TWENTY-THIRD", 24 => "TWENTY-FOURTH", 25 => "TWENTY-FIFTH", 26 => "TWENTY-SIXTH", 27 => "TWENTY-SEVENTH",
        28 => "TWENTY-EIGHTH", 29 => "TWENTY-NINTH", 30 => "THIRTIETH", 31 => "THIRTY-FIRST", 32 => "THIRTY-SECOND",
        _ => n.ToString(),
    };

    /// <summary>The Twelve by the game's row id, for the Setup picker.</summary>
    public static IReadOnlyList<(int Id, string Name)> Deities =>
        Twelve.Select((d, i) => (i + 1, $"{d.Name}, {d.Epithet}")).ToList();
}
