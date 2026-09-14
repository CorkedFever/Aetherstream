namespace Aetherstream.Plugin.Video;

/// <summary>A dish the show can make: the recipe from the game's tables, with everything it needs.</summary>
internal sealed record Dish(
    string Name,
    string Description,
    int Level,
    int Serves,
    uint Icon,
    bool Book,
    IReadOnlyList<(string Name, int Amount, uint Icon)> Ingredients);

/// <summary>An icon's pixels in the frame's own format, or null when the game has none.</summary>
internal sealed record IconPixels(uint[] Pixels, int Width, int Height);

/// <summary>
/// The cooking show, staged. A kitchen set: tiled wall, a window on the real sky, shelves, a
/// counter with a board, a stove and a plate stand. The chef stays on set and walks between
/// stations. Every episode is a real Culinarian recipe: the opening titles, the mise en place
/// laid out along the counter, each ingredient chopped in close-up and tossed in the pot, the
/// pot stirred while the method is talked through, the dish plated, and a hand-off to the next
/// episode. Cuts between the wide shot and close-ups, lower-thirds for the patter, and an
/// audience that reacts. Ninety-odd seconds an episode, on a schedule from the clock.
/// </summary>
internal sealed class CookingChannel(BitmapFont font, Func<IReadOnlyList<Dish>> dishes, Func<uint, IconPixels?> icon) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;

    // The running order, in seconds.
    private const double TitlesFor = 7.0;
    private const double MiseFor = 6.0;
    private const double IngredientFor = 6.5;
    private const double CookFor = 12.0;
    private const double PlateFor = 11.0;
    private const double OutroFor = 6.0;

    // The set.
    private const int CounterTop = 430;
    private const int CounterFront = 500;
    private const int Floor = 676;
    private const int BoardX = 380, BoardW = 220;
    private const int StoveX = 780, StoveW = 220;
    private const int PlateX = 120, PlateW = 180;

    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Tile = Canvas.Rgb(0xE4, 0xD6, 0xBC);
    private static readonly uint TileDark = Canvas.Rgb(0xCF, 0xBE, 0xA0);
    private static readonly uint Grout = Canvas.Rgb(0xB8, 0xA6, 0x88);
    private static readonly uint Wood = Canvas.Rgb(0x8A, 0x5A, 0x2A);
    private static readonly uint WoodLit = Canvas.Rgb(0xA8, 0x72, 0x3A);
    private static readonly uint WoodDark = Canvas.Rgb(0x5E, 0x3C, 0x1A);
    private static readonly uint Board = Canvas.Rgb(0xC8, 0x98, 0x5A);
    private static readonly uint Steel = Canvas.Rgb(0x8A, 0x90, 0x98);
    private static readonly uint SteelDark = Canvas.Rgb(0x50, 0x54, 0x5C);
    private static readonly uint Tomato = Canvas.Rgb(0xD9, 0x4F, 0x3D);
    private static readonly uint Sage = Canvas.Rgb(0x8F, 0xB5, 0x7A);
    private static readonly uint Ink = Canvas.Rgb(0x2A, 0x1C, 0x12);
    private static readonly uint Flame = Canvas.Rgb(0xFF, 0x9A, 0x2E);
    private static readonly uint FlameHot = Canvas.Rgb(0xFF, 0xD8, 0x6A);
    private static readonly uint Steam = Canvas.Rgb(0xF0, 0xF0, 0xF0);

    private static readonly string[] IngredientPatter =
    [
        "Now, {name}. {amount} will do; any more and it takes over.",
        "{amount} of {name}, and please, the good stuff. The market board knows the difference.",
        "Here is where people go wrong: {name}. Add it and leave it be.",
        "{name}, {amount}. My mother used twice this and we all lived.",
        "A word on {name}: if it smells like the vendor's basket, walk away.",
        "{amount} of {name}. Not optional, whatever the Lalafell at the counter says.",
        "{name} next. This is the part that makes it taste like something.",
        "You will want {amount} of {name}. You will be tempted to skip it. Do not.",
        "Now {name}. Handle it like it cost you a venture, because it did.",
        "{amount} of {name} goes in here. Stir like you mean it.",
        "Some say {name} is optional. Those people eat at the Quicksand.",
        "{name}, {amount}. Fresh from Gridania if you can get it; from a retainer if you cannot.",
    ];

    private static readonly string[] Method =
    [
        "Combine everything in the order we went through it, and do not rush the first stir.",
        "Work it until it feels done, then stop. Overcooking is quality lost.",
        "Keep the heat steady. If the pan hisses at you, it is telling you something.",
        "Taste as you go. A Culinarian who does not taste is a blacksmith with a spoon.",
        "Give it time. The Keeper turns the glass; the dish decides when it is ready.",
        "Season at the end, not the start. You can add; you cannot take away.",
        "If it looks wrong, it probably is. If it smells right, you are nearly there.",
        "A little patience here saves a trip back to the market board.",
    ];

    private static readonly string[] PlatePatter =
    [
        "And there it is. Serve it warm, serve it to someone, and do not save the good plates.",
        "Plate it like you are proud of it. You should be.",
        "That is the dish. If it does not look like the picture, the picture was lying.",
        "Serve at once. Cold, it is still food; hot, it is a reason to come home.",
        "There we are. Eat it before the buff runs out.",
        "Done. Put it on the table and let them fight over it.",
    ];

    private static readonly string[] Reactions = ["OOOH", "AAAH", "MMM", "CLAP CLAP CLAP", "YES CHEF"];

    private enum Phase
    {
        Titles,
        Mise,
        Ingredient,
        Cook,
        Plate,
        Outro,
    }

    private readonly record struct Beat(Phase Phase, int Index, double Into, double Length);

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var list = dishes();

        if (list.Count == 0)
        {
            Canvas.Fill(span, 0, 0, W, H, Ink);
            const string None = "THE KITCHEN IS CLOSED: NO RECIPES FOUND";
            font.Draw(span, W, None, (W - font.Measure(None, 2)) / 2, 330, Cream, 2, all);
            return;
        }

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var (dish, episode, into) = Episode(list, unix);
        var next = list[Pick(episode + 1, list.Count)];
        var beat = BeatAt(dish, into);
        var length = Length(dish);

        switch (beat.Phase)
        {
            case Phase.Titles:
                this.DrawTitles(span, dish, beat, seconds, now, all);
                break;
            case Phase.Outro:
                this.DrawOutro(span, dish, next, beat, seconds, now, all);
                break;
            default:
                this.DrawShow(span, dish, episode, beat, seconds, now, all);
                break;
        }

        // The episode's progress along the bottom, and the footer.
        Canvas.Fill(span, 0, Floor, W, 4, Ink);
        Canvas.Fill(span, 0, Floor, (int)(W * (into / length)), 4, Tomato);
        Canvas.Fill(span, 0, 680, W, 40, Ink);
        font.Draw(span, W, "AETHERSTREAM KITCHEN", 24, 680, Cream, 1, all);
        var right = Canvas.Cut($"EP {episode % 10000}  /  {Canvas.Plain(dish.Name).ToUpperInvariant()}  /  LV {dish.Level}", 54);
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Canvas.Rgb(0xC8, 0xB0, 0x90), 1, all);
    }

    // -- the running order ----------------------------------------------------------------------------

    private static double Length(Dish d) => TitlesFor + MiseFor + (d.Ingredients.Count * IngredientFor) + CookFor + PlateFor + OutroFor;

    private static Beat BeatAt(Dish d, double into)
    {
        var t = into;
        if (t < TitlesFor)
            return new Beat(Phase.Titles, 0, t, TitlesFor);
        t -= TitlesFor;
        if (t < MiseFor)
            return new Beat(Phase.Mise, 0, t, MiseFor);
        t -= MiseFor;
        var ingredients = d.Ingredients.Count * IngredientFor;
        if (t < ingredients)
            return new Beat(Phase.Ingredient, (int)(t / IngredientFor), t % IngredientFor, IngredientFor);
        t -= ingredients;
        if (t < CookFor)
            return new Beat(Phase.Cook, 0, t, CookFor);
        t -= CookFor;
        if (t < PlateFor)
            return new Beat(Phase.Plate, 0, t, PlateFor);
        t -= PlateFor;
        return new Beat(Phase.Outro, 0, t, OutroFor);
    }

    // Where the schedule walk got to last time: episodes have different lengths, so the one on
    // now is found by walking from a fixed origin — but only once, and then from here on.
    private long walkedTo = 1_700_000_000;
    private int walkedEpisode;
    private int walkedCount = -1;

    /// <summary>Which dish is on, which episode number, and how far into it, from the clock.</summary>
    private (Dish Dish, int Episode, double Into) Episode(IReadOnlyList<Dish> list, long unix)
    {
        // A different list (the recipes loaded, or reloaded) means a different schedule: start over.
        if (list.Count != this.walkedCount || unix < this.walkedTo)
        {
            this.walkedTo = 1_700_000_000;
            this.walkedEpisode = 0;
            this.walkedCount = list.Count;
        }

        while (true)
        {
            var dish = list[Pick(this.walkedEpisode, list.Count)];
            var length = Length(dish);
            if (unix < this.walkedTo + length)
                return (dish, this.walkedEpisode, unix - this.walkedTo);

            this.walkedTo += (long)length;
            this.walkedEpisode++;
        }
    }

    // -- the titles and the outro -----------------------------------------------------------------------

    private void DrawTitles(Span<uint> span, Dish dish, Beat beat, double seconds, DateTime now, in BitmapFont.Clip all)
    {
        // The set behind, dimmed, and the logo over it: the show's opening.
        this.DrawSet(span, now, seconds, 0f);
        Dim(span, 0.55f);

        var t = beat.Into;
        var rise = Math.Clamp(t / 0.8, 0.0, 1.0);
        var y = 160 + (int)((1.0 - rise) * 120);

        const string Title = "THE EORZEAN KITCHEN";
        var tw = font.Measure(Title, 2) + 64;
        Canvas.Fill(span, (W - tw) / 2, y - 16, tw, 112, Tomato);
        Canvas.Fill(span, (W - tw) / 2, y - 16, tw, 6, Cream);
        Canvas.Fill(span, (W - tw) / 2, y + 90, tw, 6, Cream);
        font.Draw(span, W, Title, (W - font.Measure(Title, 2)) / 2, y, Cream, 2, all);

        if (t > 1.6)
        {
            const string With = "WITH CHEF NAMAZU";
            font.Draw(span, W, With, (W - font.Measure(With)) / 2, y + 130, Cream, 1, all);
        }

        if (t > 3.0)
        {
            var today = $"TODAY: {Canvas.Plain(dish.Name).ToUpperInvariant()}";
            today = Canvas.Cut(today, font.Fit(W - 200));
            font.Draw(span, W, today, (W - font.Measure(today)) / 2, y + 200, Sage, 1, all);
            this.DrawIcon(span, dish.Icon, (W - 96) / 2, y + 250, 96);
        }

        // The chef walks on from the left during the titles.
        var walk = Math.Clamp((t - 2.0) / 3.0, 0.0, 1.0);
        var chefX = -200 + (int)(walk * (BoardX + 20 + 200));
        ChefSprite.Draw(span, walk < 1.0 ? ChefSprite.Action.Wave : ChefSprite.Action.Present, seconds, chefX, CounterTop - 150, 7);
    }

    private void DrawOutro(Span<uint> span, Dish dish, Dish next, Beat beat, double seconds, DateTime now, in BitmapFont.Clip all)
    {
        this.DrawSet(span, now, seconds, 1f);
        this.DrawPlateOnStand(span, dish, 1f);
        ChefSprite.Draw(span, ChefSprite.Action.Wave, seconds, PlateX + PlateW + 30, CounterTop - 150, 7);
        Dim(span, 0.4f);

        const string Head = "NEXT TIME ON THE EORZEAN KITCHEN";
        font.Draw(span, W, Head, (W - font.Measure(Head)) / 2, 80, Sage, 1, all);

        this.DrawIcon(span, next.Icon, (W - 160) / 2, 130, 160);
        var name = Canvas.Cut(Canvas.Plain(next.Name).ToUpperInvariant(), font.Fit(W - 120, 2));
        font.Draw(span, W, name, (W - font.Measure(name, 2)) / 2, 310, Cream, 2, all);

        var count = $"{next.Ingredients.Count} INGREDIENTS  /  LEVEL {next.Level}" + (next.Book ? "  /  FROM A MASTER'S TOME" : string.Empty);
        font.Draw(span, W, count, (W - font.Measure(count)) / 2, 410, Canvas.Rgb(0xC8, 0xB0, 0x90), 1, all);

        // Credits crawl up the right.
        var credits = new[] { "CHEF: NAMAZU", "RECIPES: THE CULINARIANS' GUILD", "SET: THE MIST, WARD 1", "PRODUCED BY AETHERSTREAM", "THANK YOU FOR WATCHING" };
        var cy = H - (int)(beat.Into * 40);
        foreach (var line in credits)
        {
            if (cy > 460 && cy < 660)
                font.Draw(span, W, line, W - 40 - font.Measure(line), cy, Cream, 1, all);
            cy += 40;
        }
    }

    // -- the show itself ----------------------------------------------------------------------------------

    private void DrawShow(Span<uint> span, Dish dish, int episode, Beat beat, double seconds, DateTime now, in BitmapFont.Clip all)
    {
        var ingredients = dish.Ingredients;
        var n = ingredients.Count;

        // How many are on the counter, how many are in the pot.
        var laidOut = beat.Phase == Phase.Mise ? (int)Math.Min(n, Math.Floor(beat.Into / (MiseFor / (n + 1)))) : n;
        var inPot = beat.Phase switch
        {
            Phase.Ingredient => beat.Index + (beat.Into > IngredientFor - 1.2 ? 1 : 0),
            Phase.Cook or Phase.Plate => n,
            _ => 0,
        };
        var pot = (float)inPot / Math.Max(1, n);

        // A close-up on the board for the middle of each ingredient beat, on the pot in the
        // middle of the cook, on the plate at the end; otherwise the wide shot. A hard cut, as it should be.
        var closeUp = beat.Phase switch
        {
            Phase.Ingredient => beat.Into is > 1.2 and < 4.6,
            Phase.Cook => beat.Into is > 3.0 and < 8.0,
            Phase.Plate => beat.Into > 6.0,
            _ => false,
        };

        if (closeUp)
        {
            this.DrawCloseUp(span, dish, beat, inPot, seconds, all);
        }
        else
        {
            this.DrawSet(span, now, seconds, pot);
            this.DrawCounterItems(span, dish, laidOut, inPot, beat, seconds);
            if (beat.Phase == Phase.Plate)
                this.DrawPlateOnStand(span, dish, (float)Math.Clamp(beat.Into / 3.0, 0.0, 1.0));

            // The chef at his station.
            var (station, action) = beat.Phase switch
            {
                Phase.Mise => (BoardX + 20, ChefSprite.Action.Present),
                Phase.Ingredient => (BoardX + 20, ChefSprite.Action.Chop),
                Phase.Cook => (StoveX - 150, ChefSprite.Action.Stir),
                _ => (PlateX + PlateW + 30, ChefSprite.Action.Present),
            };
            ChefSprite.Draw(span, action, seconds, station, CounterTop - 150, 7);
        }

        // The lower third: who and what.
        var (tab, line) = beat.Phase switch
        {
            Phase.Mise => ("MISE EN PLACE", $"Today: {Canvas.Plain(dish.Name)}. {n} ingredients, level {dish.Level}."),
            Phase.Ingredient => ($"{beat.Index + 1} OF {n}", IngredientPatter[Pick((episode * 31) + beat.Index, IngredientPatter.Length)]
                .Replace("{name}", Canvas.Plain(ingredients[beat.Index].Name).ToLowerInvariant())
                .Replace("{amount}", Amount(ingredients[beat.Index].Amount))),
            Phase.Cook => ("THE METHOD", Method[Pick((episode * 7) + (int)(beat.Into / 4.0), Method.Length)]),
            _ => ("PLATING", PlatePatter[Pick(episode * 13, PlatePatter.Length)]),
        };
        this.DrawLowerThird(span, tab, line, all);

        // The audience, at the moments that earn it.
        var react = beat.Phase switch
        {
            Phase.Ingredient => beat.Into > IngredientFor - 1.2,
            Phase.Plate => beat.Into is > 2.5 and < 5.0,
            _ => false,
        };
        if (react)
        {
            var word = Reactions[Pick((episode * 17) + beat.Index + (beat.Phase == Phase.Plate ? 99 : 0), Reactions.Length)];
            var ry = 300 - (int)((beat.Into % 1.2) * 30);
            font.Draw(span, W, word, W - 60 - font.Measure(word), ry, Canvas.Rgb(0xFF, 0xD8, 0x6A), 1, all);
        }

        // The "LIVE" bug, because every show had one and none of them were.
        Canvas.Fill(span, W - 100, 70, 76, 32, Tomato);
        font.Draw(span, W, "LIVE", W - 100 + 6, 66, Cream, 1, all);
    }

    // -- the set ------------------------------------------------------------------------------------------

    private void DrawSet(Span<uint> span, DateTime now, double seconds, float pot)
    {
        // The tiled wall.
        for (var y = 0; y < CounterTop; y += 40)
        {
            for (var x = 0; x < W; x += 40)
            {
                var dark = ((x / 40) + (y / 40)) % 2 == 0;
                Canvas.Fill(span, x, y, 40, 40, dark ? TileDark : Tile);
                Canvas.Fill(span, x, y, 40, 2, Grout);
                Canvas.Fill(span, x, y, 2, 40, Grout);
            }
        }

        // A window on the real sky, by the local hour.
        var hour = now.Hour + (now.Minute / 60f);
        var day = Math.Clamp(1f - (Math.Abs(hour - 13f) / 7f), 0f, 1f);
        var sky = Canvas.Lerp(Canvas.Rgb(0x14, 0x1C, 0x3A), Canvas.Rgb(0x8F, 0xD6, 0xFF), day);
        Canvas.Fill(span, 1000, 60, 220, 200, Wood);
        Canvas.Fill(span, 1012, 72, 196, 176, sky);
        if (day < 0.3f)
        {
            Canvas.Disc(span, 1160, 110, 14, Canvas.Rgb(0xF0, 0xF0, 0xE0));
            Canvas.Disc(span, 1040, 200, 1, Cream);
            Canvas.Disc(span, 1070, 100, 1, Cream);
        }
        else
        {
            Canvas.Disc(span, 1160, 110, 16, Canvas.Rgb(0xFF, 0xD1, 0x5C));
        }

        Canvas.Fill(span, 1108, 72, 4, 176, Wood);
        Canvas.Fill(span, 1012, 158, 196, 4, Wood);

        // A shelf with jars and a pan.
        Canvas.Fill(span, 60, 150, 560, 12, WoodDark);
        Canvas.Fill(span, 60, 150, 560, 4, WoodLit);
        var jars = new[] { Canvas.Rgb(0xC8, 0x5A, 0x3A), Sage, Canvas.Rgb(0xE0, 0xB8, 0x4A), Canvas.Rgb(0x8A, 0x5A, 0x2A), Canvas.Rgb(0xA0, 0x40, 0x60), Sage };
        for (var i = 0; i < jars.Length; i++)
        {
            var jx = 90 + (i * 70);
            Canvas.Fill(span, jx, 96, 40, 54, jars[i]);
            Canvas.Fill(span, jx + 6, 88, 28, 10, WoodDark);
            Canvas.Fill(span, jx + 4, 110, 32, 14, Cream);
        }

        Canvas.Fill(span, 520, 100, 70, 50, SteelDark);
        Canvas.Fill(span, 590, 118, 40, 8, WoodDark);

        // The counter: a worktop, a front, and the floor.
        Canvas.Fill(span, 0, CounterTop, W, CounterFront - CounterTop, WoodLit);
        Canvas.Fill(span, 0, CounterTop, W, 6, Cream);
        Canvas.Fill(span, 0, CounterFront, W, Floor - CounterFront, Wood);
        for (var x = 0; x < W; x += 160)
            Canvas.Fill(span, x, CounterFront + 20, 4, Floor - CounterFront - 40, WoodDark);
        Canvas.Fill(span, 0, Floor - 30, W, 30, Canvas.Rgb(0x3A, 0x2A, 0x20));

        // The plate stand, the board, the stove.
        Canvas.Fill(span, PlateX, CounterTop - 8, PlateW, 10, Steel);
        Canvas.Disc(span, PlateX + (PlateW / 2), CounterTop - 12, 80, Cream);
        Canvas.Disc(span, PlateX + (PlateW / 2), CounterTop - 12, 66, Canvas.Rgb(0xE8, 0xDC, 0xC4));

        Canvas.Fill(span, BoardX, CounterTop - 14, BoardW, 16, Board);
        Canvas.Fill(span, BoardX, CounterTop - 14, BoardW, 3, Canvas.Rgb(0xE0, 0xB0, 0x70));

        Canvas.Fill(span, StoveX - 20, CounterTop - 10, StoveW + 40, 12, SteelDark);
        this.DrawPot(span, StoveX + 30, CounterTop - 110, 160, pot, seconds, false);
    }

    private void DrawPot(Span<uint> span, int x, int y, int size, float full, double seconds, bool big)
    {
        var h = size * 5 / 8;
        Canvas.Fill(span, x, y + (size - h), size, h, SteelDark);
        Canvas.Fill(span, x + 6, y + (size - h) + 6, size - 12, h - 12, Steel);
        Canvas.Fill(span, x - 14, y + (size - h) + 10, 14, 12, SteelDark);
        Canvas.Fill(span, x + size, y + (size - h) + 10, 14, 12, SteelDark);

        // What is in it rises as ingredients go in, and bubbles once it is cooking.
        if (full > 0f)
        {
            var depth = (int)((h - 20) * (0.3f + (0.7f * full)));
            var stew = Canvas.Lerp(Canvas.Rgb(0xC8, 0x8A, 0x3A), Canvas.Rgb(0x9A, 0x4A, 0x1E), full);
            Canvas.Fill(span, x + 10, y + size - 10 - depth, size - 20, depth, stew);
            for (var b = 0; b < 5; b++)
            {
                var phase = (seconds * 1.7) + (b * 1.3);
                var bx = x + 20 + (int)((size - 40) * (((b * 0.23) + 0.1) % 1.0));
                var by = y + size - 12 - depth + (int)(Math.Abs(Math.Sin(phase)) * 8);
                Canvas.Disc(span, bx, by, big ? 6 : 3, Canvas.Rgb(0xE0, 0xB0, 0x60));
            }
        }

        // Steam, when there is anything to steam.
        if (full > 0.3f)
        {
            for (var s = 0; s < 4; s++)
            {
                var t = ((seconds * 0.6) + (s * 0.25)) % 1.0;
                var sx = x + 24 + (s * ((size - 48) / 3)) + (int)(Math.Sin((seconds * 2.0) + s) * 8);
                var sy = y + (size - h) - (int)(t * 110);
                var r = (int)((big ? 10 : 5) + (t * (big ? 12 : 6)));
                Canvas.Disc(span, sx, sy, r, Canvas.Lerp(Steam, Tile, (float)t));
            }
        }

        // The flame under it, always lit.
        var flick = (int)(Math.Sin(seconds * 9.0) * 4);
        for (var f = 0; f < 3; f++)
        {
            var fx = x + (size / 4) + (f * (size / 4));
            Canvas.Disc(span, fx, y + size + 8, (big ? 14 : 8) + (f == 1 ? flick : -flick), Flame);
            Canvas.Disc(span, fx, y + size + 8, big ? 7 : 4, FlameHot);
        }
    }

    private void DrawCounterItems(Span<uint> span, Dish dish, int laidOut, int inPot, Beat beat, double seconds)
    {
        // The mise en place: bowls along the back of the counter, one per ingredient, the ones
        // already in the pot emptied. The one in hand sits on the board, and at the end of its
        // beat it hops into the pot.
        var n = dish.Ingredients.Count;
        var slot = Math.Min(96, (W - 80) / Math.Max(1, n));
        var size = Math.Min(64, slot - 12);
        var startX = (W - (slot * n)) / 2;

        for (var i = 0; i < laidOut; i++)
        {
            var (_, amount, iconId) = dish.Ingredients[i];
            var bx = startX + (i * slot);
            var by = CounterTop - 28;
            Canvas.Fill(span, bx + 4, by, slot - 8, 26, Cream);
            Canvas.Fill(span, bx + 8, by + 4, slot - 16, 18, Canvas.Rgb(0xE8, 0xDC, 0xC4));
            if (i >= inPot && !(beat.Phase == Phase.Ingredient && i == beat.Index))
            {
                this.DrawIcon(span, iconId, bx + ((slot - size) / 2), by - size + 8, size);
                if (amount > 1)
                    font.Draw(span, W, $"{amount}", bx + slot - 26, by - 40, Ink, 1, new BitmapFont.Clip(0, 0, W, H));
            }
        }

        if (beat.Phase == Phase.Ingredient && beat.Index < n)
        {
            var (_, _, iconId) = dish.Ingredients[beat.Index];
            var hop = beat.Into - (IngredientFor - 1.2);
            if (hop < 0)
            {
                // On the board, jiggling under the knife.
                var jig = (int)(Math.Sin(seconds * 12.0) * 2);
                this.DrawIcon(span, iconId, BoardX + (BoardW / 2) - 40 + jig, CounterTop - 14 - 80, 80);
            }
            else
            {
                // Into the pot, in an arc.
                var t = Math.Clamp(hop / 1.0, 0.0, 1.0);
                var fromX = BoardX + (BoardW / 2) - 30;
                var toX = StoveX + 30 + 50;
                var x = fromX + (int)((toX - fromX) * t);
                var y = CounterTop - 14 - 60 - (int)(Math.Sin(t * Math.PI) * 180) + (int)(t * 20);
                this.DrawIcon(span, iconId, x, y, 60);
            }
        }
    }

    private void DrawPlateOnStand(Span<uint> span, Dish dish, float rise)
    {
        // The finished dish rising onto the plate, then a sparkle.
        var cx = PlateX + (PlateW / 2);
        var y = CounterTop - 12 - 60 - (int)(rise * 40);
        this.DrawIcon(span, dish.Icon, cx - 50, y, 100);
        if (rise >= 1f)
        {
            Canvas.Fill(span, cx + 60, y - 30, 4, 16, Cream);
            Canvas.Fill(span, cx + 54, y - 24, 16, 4, Cream);
            Canvas.Fill(span, cx - 70, y + 10, 3, 12, Cream);
            Canvas.Fill(span, cx - 74, y + 14, 12, 3, Cream);
        }
    }

    private void DrawCloseUp(Span<uint> span, Dish dish, Beat beat, int inPot, double seconds, in BitmapFont.Clip all)
    {
        // A tighter frame: the tiled wall behind, the station big, the chef big beside it.
        for (var y = 0; y < H; y += 80)
        {
            for (var x = 0; x < W; x += 80)
            {
                var dark = ((x / 80) + (y / 80)) % 2 == 0;
                Canvas.Fill(span, x, y, 80, 80, dark ? TileDark : Tile);
                Canvas.Fill(span, x, y, 80, 4, Grout);
                Canvas.Fill(span, x, y, 4, 80, Grout);
            }
        }

        Canvas.Fill(span, 0, 560, W, Floor - 560, WoodLit);
        Canvas.Fill(span, 0, 560, W, 10, Cream);

        switch (beat.Phase)
        {
            case Phase.Ingredient:
            {
                var (name, amount, iconId) = dish.Ingredients[Math.Min(beat.Index, dish.Ingredients.Count - 1)];
                Canvas.Fill(span, 340, 520, 620, 44, Board);
                Canvas.Fill(span, 340, 520, 620, 8, Canvas.Rgb(0xE0, 0xB0, 0x70));
                var jig = (int)(Math.Sin(seconds * 12.0) * 4);
                this.DrawIcon(span, iconId, 520 + jig, 300, 220);
                ChefSprite.Draw(span, ChefSprite.Action.Chop, seconds, 60, 560 - 288, 12);

                var label = $"{amount}X  {Canvas.Plain(name).ToUpperInvariant()}";
                label = Canvas.Cut(label, font.Fit(W - 80));
                var lw = font.Measure(label) + 32;
                Canvas.Fill(span, W - 40 - lw, 100, lw, 48, Ink);
                font.Draw(span, W, label, W - 40 - lw + 16, 104, Cream, 1, all);
                break;
            }

            case Phase.Cook:
                this.DrawPot(span, 560, 200, 360, (float)inPot / Math.Max(1, dish.Ingredients.Count), seconds, true);
                ChefSprite.Draw(span, ChefSprite.Action.Stir, seconds, 120, 560 - 288, 12);
                break;

            default:
            {
                Canvas.Disc(span, 700, 520, 190, Cream);
                Canvas.Disc(span, 700, 520, 160, Canvas.Rgb(0xE8, 0xDC, 0xC4));
                this.DrawIcon(span, dish.Icon, 700 - 120, 520 - 150, 240);
                ChefSprite.Draw(span, ChefSprite.Action.Present, seconds, 60, 560 - 288, 12);

                var name = Canvas.Cut(Canvas.Plain(dish.Name).ToUpperInvariant(), font.Fit(W - 80));
                var lw = font.Measure(name) + 32;
                Canvas.Fill(span, W - 40 - lw, 100, lw, 48, Ink);
                font.Draw(span, W, name, W - 40 - lw + 16, 104, Cream, 1, all);
                break;
            }
        }
    }

    private void DrawLowerThird(Span<uint> span, string tab, string line, in BitmapFont.Clip all)
    {
        const int Top = 560;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - 40 - font.Measure(tab) - 48), 2);
        var height = 16 + (lines.Count * 40);

        Canvas.Fill(span, 40, Top, W - 80, height, Ink);
        Canvas.Fill(span, 40, Top, W - 80, 4, Tomato);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, Top + 4, tabW, height - 4, Tomato);
        font.Draw(span, W, tab, 56, Top + 8, Cream, 1, all);

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

    /// <summary>An item icon resampled to <paramref name="size"/> square, blended over what is there.</summary>
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

                var sx = tx * px.Width / size;
                var p = px.Pixels[(sy * px.Width) + sx];
                var a = (int)(p >> 24);
                if (a == 0)
                    continue;

                var i = (yy * W) + xx;
                span[i] = a >= 250 ? p | 0xFF000000u : Canvas.Lerp(span[i], p | 0xFF000000u, a / 255f);
            }
        }
    }

    private static string Amount(int n) => n switch
    {
        1 => "one",
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        _ => n.ToString(),
    };

    private static int Pick(int seed, int count)
    {
        var x = (uint)seed * 2654435761u;
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
