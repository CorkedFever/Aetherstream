namespace Aetherstream.Plugin.Video;

/// <summary>
/// The bard's tales of the Twelve, told the old way: the Sharlayan and Ishgardian tellings of
/// who the gods are and what they keep, as the almanacs and the temple priests give them. The
/// tellings stay on what the game itself says of each — the domain, the element, the symbol, the
/// city that keeps the shrine, the moon each rules, and the few kinships everyone knows — and
/// nothing is quoted from the game's own text.
/// </summary>
internal static class TwelveTales
{
    /// <summary>A tale: the god it is about (1 Halone … 12 Althyk), its title, six pages, and where it is set.</summary>
    public sealed record Tale(int Deity, string Title, Biome Where, string[] Pages);

    public static readonly Tale[] All =
    [
        // -- Halone, the Fury: ice, the spear, the First Astral Moon, Ishgard's -----------------------------
        new(1, "HOW HALONE CAME TO THE MOUNTAIN", Biome.Snow,
        [
            "Before there was an Ishgard there was a mountain, and before the mountain had a name it had a winter, and in that winter walked the Fury.",
            "Halone came with a spear in her hand and ice at her heel, and where she set her foot the snow held, and where she looked the wind fell quiet.",
            "The people who came after found the cold too hard and would have turned back. Halone did not soften the cold. She hardened the people.",
            "That is the bargain the Fury offers and the only one: I will not make the road easy. I will make you the sort who walks it.",
            "So they built on the mountain, and they built high, and every spire they raised was a spear held up to her so she would know them from far off.",
            "That is why the Ishgardian bows to the cold rather than cursing it. It is not the weather. It is the Fury, checking her people are still standing.",
        ]),
        new(1, "THE FURY AND THE MATRON", Biome.Snow,
        [
            "Halone and Nophica do not sit together at the table of the Twelve, and every child of the Holy See can tell you why.",
            "Nophica is the Matron, who makes things grow. Halone is the Fury, who cuts them down when they must be cut. Neither has ever thought the other necessary.",
            "The Matron says: what is the use of the spear, when the spring feeds everyone? The Fury says: who guards the spring, when the beast comes to drink?",
            "They have argued it since the first moon, and the priests of both will tell you their goddess has the better of it, and the priests are both right.",
            "The wise say the two are not enemies but a scale. Take away the spear and the garden is eaten. Take away the garden and the spear has nothing to guard.",
            "So when the Ishgardian scorns the Gridanian's soft ways, and the Gridanian frowns at the Ishgardian's hard ones, remember: their goddesses started it.",
        ]),

        // -- Menphina, the Lover: ice, the moon, the First Umbral Moon --------------------------------------------
        new(2, "WHY THE MOON FOLLOWS THE WANDERER", Biome.Highland,
        [
            "Menphina is the Lover, and the moon is her lamp, and there is a reason the moon crosses the whole sky every night and never stays in one place.",
            "Oschon is the Wanderer, who keeps no house and walks every road, and Menphina, who is the Lover, loved him from the first road he took.",
            "He would not stop, for it is not in a wanderer to stop, and she would not ask him to, for it is not in a lover to ask that.",
            "So she raised her lamp and followed him, and she follows him still: over the mountains, over the sea, over every road he has ever walked.",
            "That is why a traveller on a dark road need only look up. The Lover is looking for the Wanderer, and she lights the way for anyone who walks it.",
            "And that is why lovers meet under the moon. Not for the light. For the company of someone who understands.",
        ]),
        new(2, "THE LOVER'S PATIENCE", Biome.Coast,
        [
            "There is a telling that the Lover once grew tired of following, and set down her lamp, and the night went dark for a full moon's turn.",
            "The fishers could not find the harbour, and the shepherds could not find the flock, and the lovers, who had met under her light, could not find each other.",
            "It was not Oschon who came to her. It was the people, who climbed the highest hill they had and called up into the dark: we cannot see.",
            "And Menphina, who is the Lover, found she could no more leave them in the dark than she could leave the Wanderer to walk alone.",
            "She took the lamp up again, and she has not set it down since, and the fishers and the shepherds and the lovers have never been without her.",
            "So the priests say: the Lover's patience is not for one. It is for everyone who has ever waited for someone. She knows what that is.",
        ]),

        // -- Thaliak, the Scholar: water, the scroll, the Second Astral Moon, Sharlayan's -----------------------------
        new(3, "THE RIVER THAT REMEMBERS", Biome.Marsh,
        [
            "Thaliak is the Scholar, and his element is water, and the Sharlayans will tell you the river that runs through their city carries his name.",
            "In the old telling, knowledge was once a thing you had to be given: a father to a son, a master to a pupil, and lost when either died.",
            "Thaliak did not think much of that. He took a river, which forgets nothing that is poured into it, and he taught the people to write.",
            "Write it down, said the Scholar, and the river will carry it. The one who reads it a hundred years on will know what you knew, and you will not have died.",
            "That is why Sharlayan keeps its great library, and why its scholars would sooner lose a hand than a book. It is not pride. It is the river.",
            "And that is why the Scholar's symbol is a scroll and not a sword. A sword ends an argument. A scroll keeps it going forever, which is the point.",
        ]),
        new(3, "THALIAK AND THE QUESTION", Biome.Marsh,
        [
            "A student came to Thaliak's river with a question she could not answer, and she asked the Scholar to answer it for her.",
            "Thaliak, in the telling, said no. Not because he could not, but because an answer given is a coin spent, and an answer found is a coin earned.",
            "He asked her instead what she had already read, and what she had already tried, and what she thought the answer might be if she had to guess.",
            "By the time she had told him all of that, she had found it. She had had it since the second book. She had only wanted someone to say it was right.",
            "That is the Scholar's way, and the reason Sharlayan's teachers are so maddening: they will not hand you the thing. They will hand you the road to it.",
            "So when you are stuck, do as the student did. Say out loud what you know. Thaliak is listening, and he will not answer, and you will.",
        ]),

        // -- Nymeia, the Spinner: water, the spindle, the Second Umbral Moon -------------------------------------------
        new(4, "THE LOOM OF NYMEIA", Biome.Night,
        [
            "Nymeia is the Spinner, and hers is the spindle, and every life that has ever been lived is a thread she has drawn out and set on the loom.",
            "She does not choose the colour, in the old telling. She does not choose the length. She spins what comes, and she weaves it in where it fits.",
            "Some threads are short, and some are long, and the short ones are not lesser, for a cloth is not made of its longest threads but of all of them.",
            "The priests say you cannot see your own thread. You are inside the cloth. But you can see the threads beside you, and that is what neighbours are for.",
            "When someone dies, the Spinner does not cut the thread. It simply ends where it ends, and the weave carries on around the place where it was.",
            "So do not fear the Spinner. She is not the one who ends you. She is the one who makes sure you were part of something.",
        ]),
        new(4, "THE SPINNER AND THE KEEPER", Biome.Night,
        [
            "Althyk keeps time, and Nymeia spins fate, and the old telling puts them together at the beginning of things before any other god had a name.",
            "The Keeper turned the glass, and there was a before and an after. The Spinner drew the first thread, and there was a someone to live between them.",
            "Without the Keeper the thread would have nowhere to run. Without the Spinner the hours would pass with no one to spend them.",
            "That is why their moons sit side by side at the year's end: the Sixth Umbral is his, and the Second Umbral hers, and the year is strung between.",
            "The Sharlayans say every clock is a small prayer to Althyk, and every piece of weaving a small prayer to Nymeia, whether the maker means it or not.",
            "And so time and fate go together, and always have, and anyone who has watched a year pass and wondered where it went has met them both.",
        ]),

        // -- Llymlaen, the Navigator: wind, the ship, the Third Astral Moon, Limsa's ---------------------------------------
        new(5, "THE NAVIGATOR'S FIRST CHART", Biome.Coast,
        [
            "Llymlaen is the Navigator, and the sea is hers, and Limsa Lominsa keeps her shrine because no city owes more to the sea than that one.",
            "In the old telling the sea had no roads. A ship that left the shore was gone; it might come back, and it might not, and no one could say which.",
            "Llymlaen did not calm the sea. The sea is the sea. Instead she gave the sailors the stars to steer by, and the wind to read, and the chart to draw.",
            "A road on the sea is not a thing you can see, she told them. It is a thing you remember. Draw what you found, and the next ship will find it too.",
            "That is why the Lominsan trusts a chart over a prayer, and why the prayer is said anyway. The chart is the Navigator's gift. The prayer is thanks for it.",
            "And it is why her symbol is a ship and not a wave. The wave is the sea's. The ship is what she gave the people, so they could cross it.",
        ]),
        new(5, "THE NAVIGATOR AND THE STORM", Biome.Coast,
        [
            "A captain once asked the Navigator to hold back a storm so that a cargo would come home on time. That is the beginning of the telling.",
            "Llymlaen, who does not hold back storms, asked the captain instead whether the cargo or the crew was the thing she wanted home.",
            "The captain, who was honest, said the cargo. The Navigator said nothing, and the storm came, and the ship did not come home on time.",
            "It came home three days late, with every hand aboard, because the captain had run for harbour after all when it came to it, and the cargo was spoiled.",
            "The captain went to the shrine to complain, and found she could not, because she had got the thing she wanted and only learned it in the storm.",
            "So the Navigator's lesson, as Limsa tells it: she will not hold back the weather. She will show you what you would run for. Then it is up to you.",
        ]),

        // -- Oschon, the Wanderer: wind, the bow, the Third Umbral Moon ------------------------------------------------
        new(6, "THE WANDERER'S ROAD", Biome.Steppe,
        [
            "Oschon is the Wanderer, and the mountains are his, and the bow, and every road that has ever gone somewhere its maker did not expect.",
            "In the old telling the world was made and the gods each took a piece of it to keep. Oschon took none. He said: I will keep the space between.",
            "So the roads are his, and the passes, and the ferries, and the places a traveller sleeps that are nobody's home. He keeps what is on the way.",
            "That is why the traveller prays to the Wanderer at the start of a road and not the end. The end belongs to whoever lives there. The road is his.",
            "He does not promise you will arrive. He is the Wanderer; arriving is not his interest. He promises the road will be worth the walking.",
            "And the old ones say, if you have ever taken the long way round for no reason you could name, that was not you. That was Oschon, wanting company.",
        ]),
        new(6, "THE BOW OF THE WANDERER", Biome.Steppe,
        [
            "Oschon carries a bow, and the telling asks why a god who wants nothing and keeps nothing should carry a weapon at all.",
            "The answer the mountain folk give is that a bow is not a weapon to Oschon. It is a way of reaching something without stopping to walk there.",
            "He looses an arrow over the next ridge, and where it lands is where he goes, and that is how he decides. The arrow does not know either.",
            "That is why the hunter and the traveller both keep his shrine: one for the arrow, one for the not knowing where it lands.",
            "The Lover follows him with her lamp, and he does not slow for her, and she does not ask him to, and that is a whole other tale.",
            "So if you must choose a road and cannot, do as the Wanderer does. Pick one without reason. He will be on it either way, and glad of you.",
        ]),

        // -- Byregot, the Builder: lightning, the hammer, the Fourth Astral Moon --------------------------------------------
        new(7, "BYREGOT'S FIRST WALL", Biome.Highland,
        [
            "Byregot is the Builder, and the hammer is his, and the crafters of every city keep his shrine whether they build in stone or in cloth.",
            "The telling has him as Rhalgr's son, and it says he grew up watching his father break things, and decided early that he would rather make them.",
            "The first thing he built was a wall. Not to keep anything out, the priests say. To find out whether a thing could be made to stay.",
            "It stayed. He built a roof on it, and a door in it, and a fire under the roof, and that was the first house, and someone lived in it.",
            "That is the Builder's whole creed, as the guilds teach it: the thing must stay. Make it twice as strong as it needs to be, because it will need to be.",
            "So when you see a crafter measure a thing again that was already right, that is not doubt. That is Byregot, standing at the shoulder, saying: once more.",
        ]),
        new(7, "THE BUILDER AND THE DESTROYER", Biome.Highland,
        [
            "Rhalgr breaks and Byregot builds, and the telling says father and son do not argue about it, which surprises people who expect them to.",
            "The Builder's answer is that nothing can be built where nothing has been cleared. The Destroyer's is that nothing is worth breaking that will not be rebuilt.",
            "So the comet falls, and the hammer follows it, and the city that stands after is better than the one that stood before, or it is not, and the comet falls again.",
            "That is the pattern the old ones see in every age: a breaking, a building, a breaking. Not a punishment. A father and a son, taking turns.",
            "Ala Mhigo keeps the father's shrine and was broken. It is being rebuilt. The Builder's priests say that is the whole tale, told once more, in stone.",
            "So do not curse the breaking when it comes. Ask what the hammer will make of it. The Builder is already measuring.",
        ]),

        // -- Rhalgr, the Destroyer: lightning, the comet, the Fourth Umbral Moon, Ala Mhigo's ------------------------------
        new(8, "THE COMET OF RHALGR", Biome.Steppe,
        [
            "Rhalgr is the Destroyer, and the comet is his, and Ala Mhigo kept his shrine in a temple that was itself broken and remade more than once.",
            "The telling says his comet is not a threat but a promise: that nothing stands forever, and that a thing which cannot fall was never really standing.",
            "When a king grew too heavy for his throne, the Mhigans looked to the sky. When a wall grew too proud, they looked to the sky. The comet would come.",
            "It is not a kind god's symbol. But the old ones say that a people who kept the Destroyer's shrine were never surprised when the world changed.",
            "They had been told. They had painted it on the temple ceiling: the star with the tail, falling, and the field it fell on, green again after.",
            "So the Destroyer's prayer is short, and the Mhigans still say it: let it fall on what deserves it. And let me not be standing under it.",
        ]),
        new(8, "RHALGR AND THE MONKS", Biome.Steppe,
        [
            "The monks of the Fist kept Rhalgr's temple, and the telling says they came to him asking how to be strong, and he set them a strange lesson.",
            "He did not give them a weapon. He told them to break a stone with the hand, and they could not, and he told them to try again in the morning.",
            "They tried every morning for years, and the stone did not break, and their hands became something else, something the stone had not planned for.",
            "When at last the stone broke, it was not because the stone had weakened. It was because they had spent years becoming the kind of thing that breaks stones.",
            "That is the Destroyer's teaching, as the Fist tells it: the breaking is not the point. What you become in order to do it is the point.",
            "And so the monk bows to the Destroyer, and does not ask him for strength. She asks for a stone hard enough to be worth the years.",
        ]),

        // -- Azeyma, the Warden: fire, the sun, the Fifth Astral Moon ------------------------------------------------------
        new(9, "THE WARDEN'S EVEN LIGHT", Biome.Desert,
        [
            "Azeyma is the Warden, and the sun is hers, and her element is fire, and her care is for what is true, which is why the sun shows everything.",
            "In the old telling, the sun once rose only over the deserving: over the honest merchant's stall, and never over the thief's.",
            "The thieves complained, which was expected, and then the honest complained, which was not: for who could tell, in that light, which was which?",
            "So Azeyma made her light even. It falls on the thief and the merchant alike, and it is up to the merchant's customers to look and see.",
            "That is why her priests are asked the hard questions in the cities: they say the Warden shows, but does not judge. The judging is yours.",
            "And that is why the desert folk trust the noon: nothing hides at noon. If you want to know what a thing is, wait for the Warden to be overhead.",
        ]),
        new(9, "AZEYMA AND THE LIAR", Biome.Desert,
        [
            "A man in the telling made a good living by telling people what they wished to hear, and he had never once been caught, and he thanked the Warden for it.",
            "Azeyma, who is not in the business of hiding anything, heard the thanks and was puzzled, and came down to see what she was being thanked for.",
            "She stood in the man's stall as the sun stands, evenly, on everything, and the customers came and looked at him in that light, and left.",
            "He had not changed. Nothing had changed. But with the Warden's light on him, everyone could see him for what he was, and no one wanted to buy it.",
            "He went to her shrine, in the end, and asked what she had done to him, and the priest said: nothing. She showed you. That is all she ever does.",
            "So the Warden's gift is not comfortable, and the honest keep her shrine because they are the only ones with nothing to lose by it.",
        ]),

        // -- Nald'thal, the Traders: fire, the scales, the Fifth Umbral Moon, Ul'dah's -------------------------------------
        new(10, "THE TWO WHO ARE ONE", Biome.Desert,
        [
            "Nald'thal are the Traders, and the scales are theirs, and Ul'dah keeps their shrine, and the first thing to know is that there are two of them.",
            "Nald keeps the trade of the living: the coin, the market, the deal struck at the counter. Thal keeps the trade of the dead: what you take with you, and what you leave.",
            "They are twins, in the telling, and they are one god with two faces, and the Ul'dahn does not think that strange, for every coin has two faces too.",
            "Every bargain, the priests say, is struck before both. Nald weighs what you gain. Thal weighs what it cost. The scales are only level when both agree.",
            "So the Ul'dahn merchant prays to Nald in the morning and to Thal at night, and is careful, because the one who is not watching will be told.",
            "And that is why the scales are their symbol: not the coin, which is Nald's alone, but the thing that says what the coin was worth.",
        ]),
        new(10, "THAL'S LEDGER", Biome.Desert,
        [
            "Thal keeps a ledger, in the telling, and every soul that passes his way is entered in it, and the entry is not what they owned but what they owed.",
            "A rich man came to the ledger sure of his place, and Thal read the entry, and it was long, for the man had been owed a great deal and paid very little.",
            "A poor woman came after him, sure of nothing, and Thal read hers, and it was short: she had been owed little, and had paid it all, and more besides.",
            "The rich man asked how the poor woman's line could be shorter than his. Thal said: the ledger does not count the coin. It counts what was settled.",
            "That is why the Ul'dahn says: pay your debts before the Fifth Umbral Moon. Not because Thal is cruel. Because he is thorough.",
            "And that is why the Traders' shrine is busiest at the year's turning, when the merchants come to settle what they can while Nald is still the one keeping the book.",
        ]),

        // -- Nophica, the Matron: earth, the spring, the Sixth Astral Moon, Gridania's ----------------------------------------
        new(11, "THE MATRON'S SPRING", Biome.Forest,
        [
            "Nophica is the Matron, and the earth is hers, and the spring that feeds the Twelveswood is hers, and Gridania keeps her shrine in the trees.",
            "In the old telling the land was made and it was bare, and the gods looked at it and agreed it was finished, and Nophica did not agree.",
            "She put her hand to the ground, and a spring came up where she pressed, and from the spring a green, and from the green everything that has ever been eaten.",
            "The other gods asked what it was for. The Matron said: for whoever comes. And someone came, and they were hungry, and there was already bread.",
            "That is the Matron's way, as the Gridanians teach it: she plants for a harvest she will not see, for a mouth she has not met. She is always early.",
            "So the farmer keeps her shrine, and the cook, and anyone who has ever fed someone and not asked why. She knows why. It is enough.",
        ]),
        new(11, "THE MATRON AND THE FURY", Biome.Forest,
        [
            "Nophica and Halone are the two who do not sit together, and this is the Matron's side of it, as the Twelveswood tells it.",
            "The Fury says the spring must be guarded. The Matron says: guard it, then, and welcome, but do not tell me the guarding is the point.",
            "The point is the spring. The spear is a tool for keeping it. A tool is not the thing it keeps, and a people who forget that end up with only spears.",
            "So Gridania grows, and lets the Wood keep it, and looks askance at the spires of Ishgard, which are all spear and no spring.",
            "And the wise say, again, that neither goddess is wrong, and that the Wood has its Wood Wailers and Ishgard has its gardens, and the argument feeds them both.",
            "So eat something, says the Matron, before you take up the argument. She has put bread on the table for it. She always does.",
        ]),

        // -- Althyk, the Keeper: earth, the hourglass, the Sixth Umbral Moon -----------------------------------------------
        new(12, "THE KEEPER'S GLASS", Biome.Night,
        [
            "Althyk is the Keeper, and the hourglass is his, and time and space are his to keep, which is why his moon is the last one of the year.",
            "In the old telling there was no before and no after, only a now that went nowhere, and the Keeper looked at it and found it unbearable.",
            "So he turned the glass. The sand ran, and a grain that had fallen could not un-fall, and for the first time a thing that had happened stayed happened.",
            "That was the first gift and the hardest: that the past is kept. You cannot go back to it. But it is not lost. The Keeper has it.",
            "That is why the Sharlayan writes the date on everything, and why the old keep their calendars, and why a year's turning is a holy thing and a sad one.",
            "So when the glass runs low on a day, do not curse the Keeper. Thank him. He is the reason the day counted.",
        ]),
        new(12, "ALTHYK AND THE IMPATIENT", Biome.Night,
        [
            "A young man came to Althyk's shrine and asked the Keeper to hurry the sand, because he wanted to be old and wise and did not care to wait.",
            "Althyk, in the telling, obliged. He turned the glass faster, and the young man was old by morning, and looked in the glass, and was not wise.",
            "For the wisdom was not in the years, the Keeper told him. It was in the days inside the years, which you asked me to run past you.",
            "The old man asked to have them back. The Keeper said: no one has them back. That is what a day is. I told you at the beginning. You were not listening.",
            "So the Keeper's lesson, as the Sharlayans teach it to children who will not sit still: time is not a road to the end. It is the only place anything happens.",
            "And that is why the almanac counts the days one at a time, and marks each, and lets none of them run past. The Keeper would not forgive it.",
        ]),
    ];

    /// <summary>The god's name, epithet and symbol by row id, for the plates and the titles.</summary>
    public static (string Name, string Epithet, string Symbol) Of(int deity) => deity switch
    {
        1 => ("Halone", "the Fury", "spear"),
        2 => ("Menphina", "the Lover", "moon"),
        3 => ("Thaliak", "the Scholar", "scroll"),
        4 => ("Nymeia", "the Spinner", "spindle"),
        5 => ("Llymlaen", "the Navigator", "ship"),
        6 => ("Oschon", "the Wanderer", "bow"),
        7 => ("Byregot", "the Builder", "hammer"),
        8 => ("Rhalgr", "the Destroyer", "comet"),
        9 => ("Azeyma", "the Warden", "sun"),
        10 => ("Nald'thal", "the Traders", "scales"),
        11 => ("Nophica", "the Matron", "spring"),
        _ => ("Althyk", "the Keeper", "hourglass"),
    };

    /// <summary>The god's symbol as a cutout in the sky, drawn from primitives at <paramref name="size"/>.</summary>
    public static void DrawSymbol(Span<uint> span, int width, int deity, int cx, int cy, int size, uint ink, double seconds)
    {
        var s = size;
        switch (deity)
        {
            case 1: // A spear, upright, with a leaf-shaped head.
                Canvas.Fill(span, cx - (s / 24), cy - (s / 2), s / 12, s, ink);
                for (var i = 0; i < s / 3; i++)
                    Canvas.Fill(span, cx - (i * (s / 6) / (s / 3)) - 1, cy - (s / 2) - (s / 3) + i, (2 * i * (s / 6) / (s / 3)) + 2, 1, ink);
                for (var i = 0; i < s / 6; i++)
                    Canvas.Fill(span, cx - (s / 6) + (i * (s / 6) / (s / 6)), cy - (s / 2) + i, (s / 3) - (2 * i * (s / 6) / (s / 6)), 1, ink);
                break;
            case 2: // A crescent moon.
                Canvas.Disc(span, cx, cy, s / 2, ink);
                break;
            case 3: // A scroll: a rolled page with curled ends.
                Canvas.Fill(span, cx - (s / 2), cy - (s / 4), s, s / 2, ink);
                Canvas.Disc(span, cx - (s / 2), cy - (s / 4), s / 8, ink);
                Canvas.Disc(span, cx + (s / 2), cy + (s / 4), s / 8, ink);
                break;
            case 4: // A spindle: a whorl on a shaft, thread wound round.
                Canvas.Fill(span, cx - (s / 30) - 1, cy - (s / 2), (s / 15) + 2, s, ink);
                Canvas.Disc(span, cx, cy + (s / 4), s / 6, ink);
                for (var i = 0; i < 5; i++)
                    Canvas.Fill(span, cx - (s / 8), cy - (s / 3) + (i * (s / 12)), s / 4, s / 24, ink);
                break;
            case 5: // A ship: a hull and a sail.
                for (var i = 0; i < s / 5; i++)
                    Canvas.Fill(span, cx - (s / 2) + i, cy + (s / 6) + i, s - (2 * i), 1, ink);
                Canvas.Fill(span, cx - (s / 30), cy - (s / 2), s / 15, (2 * s) / 3, ink);
                for (var i = 0; i < s / 2; i++)
                    Canvas.Fill(span, cx + (s / 30), cy - (s / 2) + i, (i * (s / 2)) / (s / 2), 1, ink);
                break;
            case 6: // A bow, strung, with an arrow nocked.
                for (var a = -60; a <= 60; a += 2)
                {
                    var rad = a * Math.PI / 180.0;
                    var x = cx - (s / 6) + (int)(Math.Cos(rad) * (s / 2));
                    var y = cy + (int)(Math.Sin(rad) * (s / 2));
                    Canvas.Disc(span, x, y, s / 24, ink);
                }

                Canvas.Line(span, cx - (s / 6) + (int)(Math.Cos(-60 * Math.PI / 180.0) * (s / 2)), cy + (int)(Math.Sin(-60 * Math.PI / 180.0) * (s / 2)), cx - (s / 6) + (int)(Math.Cos(60 * Math.PI / 180.0) * (s / 2)), cy + (int)(Math.Sin(60 * Math.PI / 180.0) * (s / 2)), ink);
                Canvas.Fill(span, cx - (s / 2), cy - (s / 40) - 1, s, (s / 20) + 2, ink);
                break;
            case 7: // A hammer: a head on a haft.
                Canvas.Fill(span, cx - (s / 24), cy - (s / 4), s / 12, (3 * s) / 4, ink);
                Canvas.Fill(span, cx - (s / 3), cy - (s / 2), (2 * s) / 3, s / 4, ink);
                break;
            case 8: // A comet: a head with a tail streaming up and away.
                Canvas.Disc(span, cx + (s / 4), cy + (s / 4), s / 6, ink);
                for (var i = 0; i < s; i++)
                {
                    var t = i / (double)s;
                    var x = cx + (s / 4) - (int)(t * (3 * s / 4));
                    var y = cy + (s / 4) - (int)(t * (3 * s / 4));
                    Canvas.Disc(span, x, y, Math.Max(1, (int)((1.0 - t) * (s / 8))), ink);
                }

                break;
            case 9: // The sun: a disc with rays.
                Canvas.Disc(span, cx, cy, s / 3, ink);
                for (var r = 0; r < 12; r++)
                {
                    var rad = ((r * 30) + (seconds * 6.0)) * Math.PI / 180.0;
                    Canvas.Line(span, cx + (int)(Math.Cos(rad) * (s / 2.4)), cy + (int)(Math.Sin(rad) * (s / 2.4)), cx + (int)(Math.Cos(rad) * (s / 1.9)), cy + (int)(Math.Sin(rad) * (s / 1.9)), ink);
                    Canvas.Line(span, cx + 1 + (int)(Math.Cos(rad) * (s / 2.4)), cy + (int)(Math.Sin(rad) * (s / 2.4)), cx + 1 + (int)(Math.Cos(rad) * (s / 1.9)), cy + (int)(Math.Sin(rad) * (s / 1.9)), ink);
                }

                break;
            case 10: // Scales: a post, a beam, two pans.
                Canvas.Fill(span, cx - (s / 30) - 1, cy - (s / 2), (s / 15) + 2, s, ink);
                Canvas.Fill(span, cx - (s / 2), cy - (s / 2), s, s / 20, ink);
                Canvas.Line(span, cx - (s / 2), cy - (s / 2), cx - (s / 2), cy, ink);
                Canvas.Line(span, cx + (s / 2), cy - (s / 2), cx + (s / 2), cy + (s / 8), ink);
                Canvas.Fill(span, cx - (s / 2) - (s / 6), cy, s / 3, s / 16, ink);
                Canvas.Fill(span, cx + (s / 2) - (s / 6), cy + (s / 8), s / 3, s / 16, ink);
                Canvas.Fill(span, cx - (s / 4), cy + (s / 2) - (s / 20), s / 2, s / 20, ink);
                break;
            case 11: // A spring: a sickle over a rising well of water.
                for (var a = 200; a <= 340; a += 2)
                {
                    var rad = a * Math.PI / 180.0;
                    Canvas.Disc(span, cx + (int)(Math.Cos(rad) * (s / 2.2)), cy - (s / 8) + (int)(Math.Sin(rad) * (s / 2.2)), s / 20, ink);
                }

                Canvas.Fill(span, cx - (s / 2) - (s / 20), cy - (s / 8), s / 10, s / 2, ink);
                Canvas.Disc(span, cx, cy + (s / 3), s / 5, ink);
                for (var i = 0; i < 3; i++)
                    Canvas.Disc(span, cx - (s / 4) + (i * (s / 4)), cy + (s / 3) - (s / 6) - (int)(Math.Abs(Math.Sin((seconds * 2.0) + i)) * (s / 8)), s / 18, ink);
                break;
            default: // An hourglass.
                for (var i = 0; i < s / 2; i++)
                {
                    var w = (s / 2) - (i * (s / 2) / (s / 2)) + (s / 20);
                    Canvas.Fill(span, cx - (w / 2), cy - (s / 2) + i, w, 1, ink);
                    Canvas.Fill(span, cx - (w / 2), cy + (s / 2) - i, w, 1, ink);
                }

                break;
        }
    }
}
