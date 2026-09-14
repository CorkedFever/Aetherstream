namespace Aetherstream.Plugin.Video;

internal enum SubjectKind
{
    Place,
    Creature,
    Thing,
}

/// <summary>A real thing from the game for the show to explain: what it is, where, and its picture.</summary>
internal sealed record ConspiracySubject(SubjectKind Kind, string Name, string Where, string Description, uint Picture, bool ItemPicture);

/// <summary>
/// Ancient Allagans. A mandragora with enormous hair, Doctor Sproutlington, takes a real thing
/// from the game, a vista, a creature, an item, and proves over ninety seconds that it was the
/// Allagans. Or the Ascians. Or the moon. Grain on the picture, red string between the photos,
/// an expert who agrees with him, and a question he is only asking. Every subject is real; every
/// conclusion is not.
/// </summary>
internal sealed class ConspiracyChannel(BitmapFont font, Func<IReadOnlyList<ConspiracySubject>> subjects, Func<ConspiracySubject, IconPixels?> picture) : IFrameChannel
{
    private const int W = Canvas.Width;
    private const int H = Canvas.Height;
    private const double EpisodeFor = 90.0;

    private static readonly uint Dark = Canvas.Rgb(0x0C, 0x0A, 0x0A);
    private static readonly uint Sepia = Canvas.Rgb(0x2A, 0x20, 0x16);
    private static readonly uint Cream = Canvas.Rgb(0xF0, 0xE4, 0xC8);
    private static readonly uint Faint = Canvas.Rgb(0x9A, 0x8A, 0x70);
    private static readonly uint Red = Canvas.Rgb(0xD8, 0x2A, 0x2A);
    private static readonly uint Gold = Canvas.Rgb(0xE8, 0xB8, 0x4A);
    private static readonly uint Paper = Canvas.Rgb(0xE8, 0xDC, 0xB8);
    private static readonly uint Ink = Canvas.Rgb(0x10, 0x0C, 0x0A);

    private static readonly string[] Culprits = ["THE ALLAGANS", "THE ASCIANS", "THE MOON", "THE ALLAGANS", "SHARLAYAN", "THE ALLAGANS", "THE NAMAZU"];

    private static readonly string[] OpenPlace =
    [
        "{name}, in {where}. The official story is that it is a place. Look closer.",
        "They call {name} a landmark. Landmarks do not have angles like these. Nothing natural does.",
        "Tourists visit {name} every day. Not one of them asks the obvious question: who built it, and why is it still humming?",
    ];

    private static readonly string[] OpenCreature =
    [
        "The {name}, of {where}. Ask yourself: why that many legs? Who needs that many legs? Somebody designed this.",
        "You have fought the {name}. Did you notice it was waiting for you? Exactly where the old maps said it would be?",
        "The {name}. The guild says it is a creature. The guild says a lot of things.",
    ];

    private static readonly string[] OpenThing =
    [
        "{name}. A {where}, they tell you. Sold openly on the market board. Why openly? To hide it in plain sight.",
        "Every crafter knows {name}. Not one of them knows where the first one came from. I do.",
        "{name}. Turn it over. There is a mark on the bottom. Nobody talks about the mark.",
    ];

    private static readonly string[] Evidence =
    [
        "The name has {letters} letters. Count the letters in 'Allag'. Then add the rest. It works if you add the rest.",
        "The Sharlayans have a whole shelf on it. The shelf is closed to the public. I have been to the shelf.",
        "It appears on no map before the Sixth Astral Era. Neither do the Allagans. Neither does my mother, but that is a different documentary.",
        "Nobody has ever seen one being made. Think about that. You have seen bread being made. Not this.",
        "The aetheryte nearest it hums at a different pitch. Stand there. Hum. You will hear it.",
        "It is exactly the size it is. That is not a coincidence. Nothing is exactly the size it is by accident.",
        "The Ironworks 'cannot comment'. That is not a no. That is a yes wearing a hat.",
        "There are twelve of something nearby. There are always twelve of something nearby. Twelve. Think.",
        "Crystal tower. Crystal. Tower. I am simply putting two words next to each other.",
        "A moogle told me. Moogles do not lie. They cannot; the pom prevents it. This is known.",
    ];

    private static readonly string[] Experts =
    [
        "\"Thirty years of study, and I am prepared to say: something.\" - a scholar who asked not to be named, then named himself",
        "\"Could it be? Yes. Is it? Also yes.\" - an expert in things",
        "\"The mainstream will not touch this. I touched it. It was warm.\" - a former something at the Ironworks",
        "\"We found no evidence. Which is exactly what you would find.\" - the official report, read in a voice",
        "\"I was there. I do not remember it. Draw your own conclusions.\" - a witness",
    ];

    private static readonly string[] Questions =
    [
        "IS IT POSSIBLE? I AM ONLY ASKING.",
        "COINCIDENCE? YOU DECIDE. I HAVE DECIDED.",
        "WHO BENEFITS? FOLLOW THE GIL.",
        "WHAT ARE THEY NOT TELLING US? EVERYTHING. THEY ARE TELLING US NOTHING.",
        "WHY IS IT STILL THERE? WHY IS ANYTHING STILL THERE?",
    ];

    private static readonly string[] Closers =
    [
        "I am not saying it was {culprit}. But it was {culprit}.",
        "The evidence speaks for itself. I have also spoken for it, at length.",
        "Next week: the aetheryte network, and what it is really for. Spoiler: {culprit}.",
        "Do your own research. I did mine. It took an afternoon and it was all true.",
    ];

    public bool Available => font.Available;

    public bool WantsPicture => false;

    public bool WantsMusic => true;

    public void Render(uint[] target, uint[]? picture, DateTime now, double seconds)
    {
        var clock = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0) - 1_700_000_000;
        var episode = (int)(clock / EpisodeFor);
        var into = clock % EpisodeFor;
        this.RenderAt(target, subjects(), episode, into, now, seconds);
    }

    private void RenderAt(uint[] target, IReadOnlyList<ConspiracySubject> list, int episode, double into, DateTime now, double seconds)
    {
        var span = target.AsSpan();
        var all = new BitmapFont.Clip(0, 0, W, H);

        // The set: near-black with a sepia glow, and film grain that never settles.
        for (var y = 0; y < H; y++)
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(Sepia, Dark, Math.Abs((y / (float)H) - 0.4f) * 2f));
        Grain(span, seconds);

        if (list.Count == 0)
        {
            font.Draw(span, W, "THEY HAVE TAKEN THE FILES. THIS PROVES EVERYTHING.", 60, 320, Red, 1, all);
            HostSprites.DrawMandragora(span, HostSprites.Mandragora.Wild, seconds, 60, 330, 7);
            return;
        }

        var subject = list[Pick(episode, list.Count)];
        var culprit = Culprits[Pick(episode * 3, Culprits.Length)];

        if (into < 6.0)
        {
            this.Titles(span, into, seconds, all);
            return;
        }

        // The corkboard: the subject's picture, pinned, with string to three "related" photos.
        this.Board(span, subject, into, seconds);

        // The host, and what he is saying at this point of the episode.
        var wild = into is > 30 and < 36 || into > 78;
        HostSprites.DrawMandragora(span, wild ? HostSprites.Mandragora.Wild : HostSprites.Mandragora.Talk, seconds, 40, 320, 7);

        var name = Canvas.Plain(subject.Name);
        var where = Canvas.Plain(subject.Where);
        string tab;
        string line;
        if (into < 22.0)
        {
            var pool = subject.Kind switch { SubjectKind.Place => OpenPlace, SubjectKind.Creature => OpenCreature, _ => OpenThing };
            line = pool[Pick(episode * 5, pool.Length)].Replace("{name}", name).Replace("{where}", where.ToLowerInvariant());
            tab = "DR. SPROUTLINGTON";
        }
        else if (into < 54.0)
        {
            var i = (int)((into - 22.0) / 8.0);
            line = Evidence[Pick((episode * 11) + i, Evidence.Length)].Replace("{letters}", name.Count(char.IsLetter).ToString());
            tab = $"EVIDENCE {i + 1}";
        }
        else if (into < 66.0)
        {
            line = Experts[Pick(episode * 13, Experts.Length)];
            tab = "AN EXPERT";
        }
        else if (into < 78.0)
        {
            line = Questions[Pick(episode * 17, Questions.Length)];
            tab = "QUESTION";
        }
        else
        {
            line = Closers[Pick(episode * 19, Closers.Length)].Replace("{culprit}", culprit.ToLowerInvariant());
            tab = "DR. SPROUTLINGTON";
        }

        this.DrawLowerThird(span, tab, line, all);

        // The stamp, at the end, and the crawl.
        if (into > 80.0)
        {
            var k = (float)Math.Min(1.0, (into - 80.0) / 0.4);
            var scale = 2;
            var w = font.Measure(culprit, scale);
            var x = (W - w) / 2 + 100;
            Canvas.Fill(span, x - 20, 200 - 10, w + 40, 100, Canvas.Lerp(Dark, Red, 0.2f * k));
            Canvas.Rect(span, x - 20, 200 - 10, w + 40, 100, Red, 6);
            font.Draw(span, W, culprit, x, 200, Red, scale, all);
        }

        Canvas.Fill(span, 0, 680, W, 40, Dark);
        var crawl = $"ANCIENT ALLAGANS   /   EPISODE {episode % 900 + 100}   /   TONIGHT: {name.ToUpperInvariant()}   /   NOT AFFILIATED WITH THE SONS OF SAINT COINACH   /   ";
        var width = font.Measure(crawl);
        var offset = (int)(seconds * 70.0) % Math.Max(1, width);
        var clip = new BitmapFont.Clip(0, 682, W, 720);
        font.Draw(span, W, crawl, -offset, 680, Faint, 1, clip);
        font.Draw(span, W, crawl, -offset + width, 680, Faint, 1, clip);
    }

    // -- parts -------------------------------------------------------------------------------------------------

    private void Titles(Span<uint> span, double into, double seconds, in BitmapFont.Clip all)
    {
        // A slow zoom on the words, a bar of red string under them, and the rumble of grain.
        var scale = into < 2.0 ? 1 : 2;
        const string Title = "ANCIENT ALLAGANS";
        var w = font.Measure(Title, scale);
        font.Draw(span, W, Title, (W - w) / 2, 280 - (scale * 10), Gold, scale, all);
        Canvas.Fill(span, (W - w) / 2, 280 + (scale * 44), w, 4, Red);
        if (into > 3.0)
            font.Draw(span, W, "WITH DOCTOR SPROUTLINGTON", (W - font.Measure("WITH DOCTOR SPROUTLINGTON")) / 2, 380, Faint, 1, all);
        if (into > 4.5)
            font.Draw(span, W, "THE TRUTH IS OUT THERE. ABOUT FOUR MALMS EAST.", (W - font.Measure("THE TRUTH IS OUT THERE. ABOUT FOUR MALMS EAST.")) / 2, 430, Cream, 1, all);
        HostSprites.DrawMandragora(span, HostSprites.Mandragora.Talk, seconds, (W / 2) - 84, 480, 7);
    }

    private void Board(Span<uint> span, ConspiracySubject subject, double into, double seconds)
    {
        var all = new BitmapFont.Clip(0, 0, W, H);

        // Cork, the main photo with its pin, and three smaller "related" photos joined by string.
        Canvas.Fill(span, 300, 70, 940, 500, Canvas.Rgb(0x6A, 0x4E, 0x30));
        for (var i = 0; i < 300; i++)
            Canvas.Plot(span, 300 + ((i * 131) % 940), 70 + ((i * 71) % 500), Canvas.Rgb(0x5A, 0x40, 0x26));

        var (pw, ph) = subject.Kind == SubjectKind.Place ? (400, 240) : (240, 240);
        var px = 360;
        var py = 110;
        Canvas.Fill(span, px - 10, py - 10, pw + 20, ph + 20, Paper);
        if (picture(subject) is { } art && art.Width > 0)
            Blit(span, art, px, py, pw, ph, sepia: true);
        else
        {
            Canvas.Fill(span, px, py, pw, ph, Canvas.Rgb(0x3A, 0x30, 0x24));
            font.Draw(span, W, "PHOTO REMOVED", px + 20, py + (ph / 2) - 20, Red, 1, all);
        }

        Canvas.Disc(span, px + (pw / 2), py - 6, 7, Red);
        var caption = Canvas.Cut(Canvas.Plain(subject.Name).ToUpperInvariant(), font.Fit(pw + 20));
        font.Draw(span, W, caption, px - 10, py + ph + 12, Ink, 1, all);

        // The related photos: shapes nobody can identify, each revealed as the evidence mounts.
        var related = new (int X, int Y, string Label)[] { (860, 100, "TOWER"), (1000, 300, "MOOGLE?"), (820, 400, "TWELVE") };
        var shown = Math.Clamp((int)((into - 22.0) / 8.0) + 1, 0, 3);
        for (var i = 0; i < shown; i++)
        {
            var (rx, ry, label) = related[i];
            Canvas.Fill(span, rx - 8, ry - 8, 136, 116, Paper);
            Canvas.Fill(span, rx, ry, 120, 80, Canvas.Rgb(0x4A, 0x3E, 0x2E));
            switch (i)
            {
                case 0:
                    for (var t = 0; t < 60; t++)
                        Canvas.Fill(span, rx + 60 - (t / 4), ry + 10 + t, t / 2, 1, Canvas.Rgb(0x8A, 0xA8, 0xC8));
                    break;
                case 1:
                    Canvas.Disc(span, rx + 60, ry + 45, 22, Canvas.Rgb(0xC8, 0xC0, 0xB0));
                    Canvas.Disc(span, rx + 60, ry + 14, 6, Canvas.Rgb(0xC8, 0x7A, 0x9A));
                    break;
                default:
                    font.Draw(span, W, "XII", rx + 36, ry + 20, Cream, 1, all);
                    break;
            }

            font.Draw(span, W, label, rx - 8, ry + 82, Ink, 1, all);
            Canvas.Disc(span, rx + 60, ry - 4, 5, Red);

            // The string, from the main pin, drawn thick.
            var (ax, ay) = (px + (pw / 2), py - 6);
            for (var d = -1; d <= 1; d++)
                Canvas.Line(span, ax + d, ay, rx + 60 + d, ry - 4, Red);
        }

        // A stamp over the corner once the expert has spoken.
        if (into > 54.0)
        {
            const string Stamp = "SUPPRESSED";
            var sw = font.Measure(Stamp) + 24;
            Canvas.Rect(span, 1240 - sw - 20, 90, sw, 44, Red, 3);
            font.Draw(span, W, Stamp, 1240 - sw - 8, 92, Red, 1, all);
        }
    }

    private static void Grain(Span<uint> span, double seconds)
    {
        var seed = (uint)(seconds * 60) * 2654435761u;
        for (var i = 0; i < 1400; i++)
        {
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            var x = (int)(seed % W);
            var y = (int)((seed >> 8) % H);
            var idx = (y * W) + x;
            span[idx] = Canvas.Lerp(span[idx], (seed & 1) == 0 ? Cream : Dark, 0.18f);
        }
    }

    private static void Blit(Span<uint> span, IconPixels art, int x, int y, int w, int h, bool sepia)
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
                if (sepia)
                {
                    var l = (((p & 0xFF) * 3) + (((p >> 8) & 0xFF) * 6) + ((p >> 16) & 0xFF)) / 10;
                    p = Canvas.Lerp(Canvas.Rgb((int)l, (int)l, (int)l), Canvas.Rgb(0xC8, 0xA0, 0x60), 0.35f);
                    p = p | 0xFF000000u;
                }

                var i = (yy * W) + xx;
                span[i] = a >= 250 ? p | 0xFF000000u : Canvas.Lerp(span[i], p | 0xFF000000u, a / 255f);
            }
        }
    }

    private void DrawLowerThird(Span<uint> span, string tab, string line, in BitmapFont.Clip all)
    {
        const int Top = 586;
        var lines = Canvas.Wrap(line.ToUpperInvariant(), font.Fit(W - 80 - font.Measure(tab) - 64), 2);
        var height = 16 + (lines.Count * 40);
        Canvas.Fill(span, 40, Top, W - 80, height, Ink);
        Canvas.Fill(span, 40, Top, W - 80, 4, Red);
        var tabW = font.Measure(tab) + 32;
        Canvas.Fill(span, 40, Top + 4, tabW, height - 4, Canvas.Rgb(0x5A, 0x1A, 0x1A));
        font.Draw(span, W, tab, 56, Top + 8, Cream, 1, all);
        var y = Top + 8;
        foreach (var l in lines)
        {
            font.Draw(span, W, l, 40 + tabW + 16, y, Cream, 1, all);
            y += 40;
        }
    }

    private static int Pick(int seed, int count)
    {
        var x = (uint)seed * 2654435761u;
        x ^= x >> 15; x *= 2246822519u; x ^= x >> 13; x *= 3266489917u; x ^= x >> 16;
        return count == 0 ? 0 : (int)(x % (uint)count);
    }
}
