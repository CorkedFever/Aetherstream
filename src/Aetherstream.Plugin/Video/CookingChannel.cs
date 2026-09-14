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
/// The cooking show. Every episode is a real Culinarian recipe: the dish is introduced, each
/// ingredient gets its card with the amount and a line of patter, the method is talked through,
/// and the plate goes out with the item's own description. Ninety seconds an episode, one after
/// another, seeded by the clock so the schedule is the same for everyone.
/// </summary>
internal sealed class CookingChannel(BitmapFont font, Func<IReadOnlyList<Dish>> dishes, Func<uint, IconPixels?> icon) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;

    private const double TitleFor = 9.0;
    private const double IngredientFor = 7.0;
    private const double MethodFor = 11.0;
    private const double PlateFor = 13.0;

    private static readonly uint Cream = Canvas.Rgb(0xF6, 0xEC, 0xD8);
    private static readonly uint Warm = Canvas.Rgb(0x3A, 0x22, 0x14);
    private static readonly uint WarmLit = Canvas.Rgb(0x5A, 0x36, 0x1E);
    private static readonly uint Tomato = Canvas.Rgb(0xD9, 0x4F, 0x3D);
    private static readonly uint Sage = Canvas.Rgb(0x8F, 0xB5, 0x7A);

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
        "Work it until the progress bar in your head says done, then stop. Overcooking is quality lost.",
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

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);
        var list = dishes();

        Canvas.Fill(span, 0, 0, W, H, Warm);
        this.DrawHeader(span, now, all);

        if (list.Count == 0)
        {
            const string None = "THE KITCHEN IS CLOSED: NO RECIPES FOUND";
            font.Draw(span, W, None, (W - font.Measure(None, 2)) / 2, 330, Cream, 2, all);
            this.DrawFooter(span, all, "AETHERSTREAM KITCHEN");
            return;
        }

        // The schedule runs on the wall clock, so everyone watching sees the same episode.
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var (dish, episode, into) = Episode(list, unix);
        var ingredientsFor = dish.Ingredients.Count * IngredientFor;

        if (into < TitleFor)
            this.DrawTitle(span, dish, episode, seconds, all);
        else if (into < TitleFor + ingredientsFor)
            this.DrawIngredient(span, dish, (int)((into - TitleFor) / IngredientFor), episode, seconds, all);
        else if (into < TitleFor + ingredientsFor + MethodFor)
            this.DrawMethod(span, dish, episode, seconds, all);
        else
            this.DrawPlate(span, dish, episode, seconds, all);

        // Progress along the bottom edge: how far through the episode.
        var length = TitleFor + ingredientsFor + MethodFor + PlateFor;
        Canvas.Fill(span, 0, 676, (int)(W * (into / length)), 4, Tomato);
        this.DrawFooter(span, all, $"EPISODE {episode % 10000}  /  {dish.Ingredients.Count} INGREDIENTS  /  LEVEL {dish.Level}");
    }

    /// <summary>Which dish is on, which episode number, and how far into it, from the clock.</summary>
    private static (Dish Dish, int Episode, double Into) Episode(IReadOnlyList<Dish> list, long unix)
    {
        // Episodes have different lengths, so the schedule is walked from a fixed origin: cheap,
        // since a day is under a thousand episodes and this runs once a frame at most.
        const long Origin = 1_700_000_000;
        var t = Origin;
        var episode = 0;
        while (true)
        {
            var dish = list[Pick(episode, list.Count)];
            var length = TitleFor + (dish.Ingredients.Count * IngredientFor) + MethodFor + PlateFor;
            if (unix < t + length)
                return (dish, episode, unix - t);

            t += (long)length;
            episode++;
        }
    }

    private void DrawHeader(Span<uint> span, DateTime now, in BitmapFont.Clip all)
    {
        // A striped awning, as every kitchen set had.
        for (var x = 0; x < W; x += 64)
            Canvas.Fill(span, x, 0, 32, 56, Tomato);
        for (var x = 32; x < W; x += 64)
            Canvas.Fill(span, x, 0, 32, 56, Cream);
        Canvas.Fill(span, 0, 56, W, 4, Warm);

        const string Title = "THE EORZEAN KITCHEN";
        var tw = font.Measure(Title) + 32;
        Canvas.Fill(span, (W - tw) / 2, 4, tw, 48, Warm);
        font.Draw(span, W, Title, (W - font.Measure(Title)) / 2, 8, Cream, 1, all);

        var local = now.ToString("h:mm tt").ToUpperInvariant();
        var lw = font.Measure(local) + 24;
        Canvas.Fill(span, W - 24 - lw, 4, lw, 48, Warm);
        font.Draw(span, W, local, W - 24 - lw + 12, 8, Cream, 1, all);
    }

    private void DrawTitle(Span<uint> span, Dish dish, int episode, double seconds, in BitmapFont.Clip all)
    {
        ChefSprite.Draw(span, ChefSprite.Action.Wave, seconds, 960, 190, 7);
        const string Today = "TODAY WE ARE MAKING";
        font.Draw(span, W, Today, (W - font.Measure(Today)) / 2, 120, Sage, 1, all);

        this.DrawIcon(span, dish.Icon, (W - 240) / 2, 176, 3);

        var name = Canvas.Cut(Canvas.Plain(dish.Name).ToUpperInvariant(), font.Fit(W - 48, 2));
        font.Draw(span, W, name, (W - font.Measure(name, 2)) / 2, 440, Cream, 2, all);

        var sub = $"A LEVEL {dish.Level} CULINARIAN RECIPE" + (dish.Book ? "  /  FROM A MASTER'S TOME" : string.Empty) + (dish.Serves > 1 ? $"  /  MAKES {dish.Serves}" : string.Empty);
        font.Draw(span, W, sub, (W - font.Measure(sub)) / 2, 540, Canvas.Rgb(0xC8, 0xB0, 0x90), 1, all);

        var eps = $"EPISODE {episode % 10000}";
        font.Draw(span, W, eps, (W - font.Measure(eps)) / 2, 600, Sage, 1, all);
    }

    private void DrawIngredient(Span<uint> span, Dish dish, int index, int episode, double seconds, in BitmapFont.Clip all)
    {
        ChefSprite.Draw(span, ChefSprite.Action.Chop, seconds, 1000, 452, 7);
        index = Math.Clamp(index, 0, dish.Ingredients.Count - 1);
        var (name, amount, iconId) = dish.Ingredients[index];

        var head = $"YOU WILL NEED  /  {index + 1} OF {dish.Ingredients.Count}";
        font.Draw(span, W, head, 40, 84, Sage, 1, all);

        // The card: the icon on a cream plate, the name and amount beside it.
        Canvas.Fill(span, 40, 136, 300, 300, Cream);
        Canvas.Rect(span, 40, 136, 300, 300, WarmLit);
        this.DrawIcon(span, iconId, 70, 166, 3);

        var amountText = $"{amount}X";
        font.Draw(span, W, amountText, 380, 150, Tomato, 2, all);
        var nameText = Canvas.Plain(name).ToUpperInvariant();
        var y = 246;
        foreach (var line in Canvas.Wrap(nameText, font.Fit(W - 380 - 40, 2), 2))
        {
            font.Draw(span, W, line, 380, y, Cream, 2, all);
            y += 88;
        }

        // The patter, seeded by the episode and the ingredient so a repeat reads the same.
        var line0 = IngredientPatter[Pick(episode * 31 + index, IngredientPatter.Length)]
            .Replace("{name}", Canvas.Plain(name).ToLowerInvariant())
            .Replace("{amount}", Amount(amount));
        y = 470;
        foreach (var line in Canvas.Wrap(line0.ToUpperInvariant(), font.Fit(920), 3))
        {
            font.Draw(span, W, line, 40, y, Cream, 1, all);
            y += 40;
        }

        // The list so far, small, along the right: what is already in.
        var lx = 900;
        var ly = 150;
        for (var i = 0; i < dish.Ingredients.Count && ly < 440; i++)
        {
            var (n, a, _) = dish.Ingredients[i];
            var mark = i < index ? "+" : i == index ? ">" : " ";
            var text = Canvas.Cut($"{mark} {a}X {Canvas.Plain(n)}".ToUpperInvariant(), font.Fit(W - lx - 24));
            font.Draw(span, W, text, lx, ly, i <= index ? Cream : Canvas.Rgb(0x8A, 0x70, 0x58), 1, all);
            ly += 36;
        }
    }

    private void DrawMethod(Span<uint> span, Dish dish, int episode, double seconds, in BitmapFont.Clip all)
    {
        ChefSprite.Draw(span, ChefSprite.Action.Stir, seconds, 870, 430, 7);
        font.Draw(span, W, "THE METHOD", 40, 84, Sage, 1, all);

        var y = 150;
        var steps = 3;
        for (var i = 0; i < steps; i++)
        {
            var line = Method[Pick((episode * 7) + i, Method.Length)];
            var head = $"{i + 1}.";
            font.Draw(span, W, head, 40, y, Tomato, 1, all);
            foreach (var wrapped in Canvas.Wrap(line.ToUpperInvariant(), font.Fit(760), 3))
            {
                font.Draw(span, W, wrapped, 100, y, Cream, 1, all);
                y += 40;
            }

            y += 24;
        }

        // The dish, small, in the corner, so you remember what all this is for.
        this.DrawIcon(span, dish.Icon, W - 40 - 160, 470, 2);
        var name = Canvas.Cut(Canvas.Plain(dish.Name).ToUpperInvariant(), 24);
        font.Draw(span, W, name, W - 40 - font.Measure(name), 640, Canvas.Rgb(0xC8, 0xB0, 0x90), 1, all);
    }

    private void DrawPlate(Span<uint> span, Dish dish, int episode, double seconds, in BitmapFont.Clip all)
    {
        ChefSprite.Draw(span, ChefSprite.Action.Present, seconds, 1040, 490, 6);
        font.Draw(span, W, "PLATING", 40, 84, Sage, 1, all);

        // The plate: a big cream disc with the dish on it.
        Canvas.Disc(span, 220, 360, 170, Cream);
        Canvas.Disc(span, 220, 360, 150, Canvas.Rgb(0xE8, 0xDC, 0xC4));
        this.DrawIcon(span, dish.Icon, 220 - 120, 360 - 120, 3);

        var name = Canvas.Plain(dish.Name).ToUpperInvariant();
        var y = 140;
        foreach (var line in Canvas.Wrap(name, font.Fit(W - 440 - 40, 2), 2))
        {
            font.Draw(span, W, line, 440, y, Cream, 2, all);
            y += 88;
        }

        y += 8;
        foreach (var line in Canvas.Wrap(Canvas.Plain(dish.Description).ToUpperInvariant(), font.Fit(W - 440 - 40), 5))
        {
            font.Draw(span, W, line, 440, y, Canvas.Rgb(0xD8, 0xC4, 0xA8), 1, all);
            y += 40;
        }

        var patter = PlatePatter[Pick(episode * 13, PlatePatter.Length)];
        y = Math.Max(y + 24, 520);
        foreach (var line in Canvas.Wrap(patter.ToUpperInvariant(), font.Fit(580), 3))
        {
            font.Draw(span, W, line, 440, y, Sage, 1, all);
            y += 40;
        }
    }

    private void DrawFooter(Span<uint> span, in BitmapFont.Clip all, string right)
    {
        Canvas.Fill(span, 0, 680, W, 40, WarmLit);
        font.Draw(span, W, "AETHERSTREAM KITCHEN", 24, 680, Cream, 1, all);
        right = Canvas.Cut(right, font.Fit(W - 48 - font.Measure("AETHERSTREAM KITCHEN") - 24));
        font.Draw(span, W, right, W - 24 - font.Measure(right), 680, Canvas.Rgb(0xC8, 0xB0, 0x90), 1, all);
    }

    /// <summary>An item icon, scaled up, blended over what is already there.</summary>
    private void DrawIcon(Span<uint> span, uint iconId, int x, int y, int scale)
    {
        if (icon(iconId) is not { } px)
        {
            Canvas.Rect(span, x, y, 80 * scale, 80 * scale, WarmLit);
            return;
        }

        for (var sy = 0; sy < px.Height; sy++)
        {
            for (var sx = 0; sx < px.Width; sx++)
            {
                var p = px.Pixels[(sy * px.Width) + sx];
                var a = (int)(p >> 24);
                if (a == 0)
                    continue;

                for (var dy = 0; dy < scale; dy++)
                {
                    var ty = y + (sy * scale) + dy;
                    if (ty < 0 || ty >= H)
                        continue;

                    for (var dx = 0; dx < scale; dx++)
                    {
                        var tx = x + (sx * scale) + dx;
                        if (tx < 0 || tx >= W)
                            continue;

                        var i = (ty * W) + tx;
                        span[i] = a >= 250 ? p | 0xFF000000u : Canvas.Lerp(span[i], p | 0xFF000000u, a / 255f);
                    }
                }
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
