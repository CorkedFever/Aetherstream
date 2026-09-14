namespace Aetherstream.Plugin.Video;

/// <summary>
/// The wildlife show's host, drawn the way the chef is: a moogle in a bush hat, a few frames per
/// action. The story hour's teller and its figures are silhouettes, drawn where they are used.
/// </summary>
internal static class HostSprites
{
    public enum Ranger
    {
        Stand,
        Creep,
        Grab,
        Run,
        Point,
    }

    public enum Bard
    {
        Read,
        Turn,
        Gesture,
        Wave,
    }

    // The hosts' colours. The chair's velvet is 'v' on the tonberry too: the moogle's wings and
    // the chair share a purple, which is fine, they are never in the same frame.
    private static readonly Dictionary<char, uint> Palette = new()
    {
        ['k'] = Canvas.Rgb(0x1A, 0x14, 0x10),
        // The moogle.
        ['w'] = Canvas.Rgb(0xF6, 0xF2, 0xEA),
        ['W'] = Canvas.Rgb(0xD8, 0xD0, 0xC4),
        ['p'] = Canvas.Rgb(0xE8, 0x5A, 0x8A),
        ['P'] = Canvas.Rgb(0xFF, 0x9A, 0xC0),
        ['v'] = Canvas.Rgb(0x8A, 0x5A, 0xB0),
        ['V'] = Canvas.Rgb(0xB0, 0x88, 0xD8),
        ['e'] = Canvas.Rgb(0x10, 0x10, 0x10),
        ['m'] = Canvas.Rgb(0xE8, 0x6A, 0x8A),
        ['o'] = Canvas.Rgb(0x8A, 0x3A, 0x4A),
        ['h'] = Canvas.Rgb(0xB8, 0x9A, 0x5A),
        ['H'] = Canvas.Rgb(0x96, 0x7A, 0x44),
        ['x'] = Canvas.Rgb(0x5A, 0x3C, 0x22),
        ['d'] = Canvas.Rgb(0x30, 0x20, 0x18),
        ['t'] = Canvas.Rgb(0xC8, 0xB0, 0x78),
        ['n'] = Canvas.Rgb(0x9A, 0x84, 0x54),
        ['S'] = Canvas.Rgb(0xE8, 0xB8, 0x90),
        // The tonberry.
        ['g'] = Canvas.Rgb(0x5A, 0x9A, 0x4A),
        ['G'] = Canvas.Rgb(0x3E, 0x74, 0x34),
        ['y'] = Canvas.Rgb(0xFF, 0xE0, 0x60),
        ['r'] = Canvas.Rgb(0x7A, 0x52, 0x2A),
        ['R'] = Canvas.Rgb(0x5A, 0x3A, 0x1E),
        ['c'] = Canvas.Rgb(0x6A, 0x2A, 0x2A),
        ['L'] = Canvas.Rgb(0xFF, 0xB8, 0x40),
        ['B'] = Canvas.Rgb(0x3A, 0x5A, 0x8A),
        ['q'] = Canvas.Rgb(0xF6, 0xF0, 0xE0),
        ['l'] = Canvas.Rgb(0x50, 0x54, 0x5C),
        ['b'] = Canvas.Rgb(0x3A, 0x22, 0x44),
        // The loporrit.
        ['u'] = Canvas.Rgb(0x4A, 0x6A, 0xB0),
        ['j'] = Canvas.Rgb(0x8A, 0x62, 0x3A),
        // The pixie, the kobold, the sahagin.
        ['i'] = Canvas.Rgb(0xC8, 0xB0, 0xE8),
        ['F'] = Canvas.Rgb(0x6A, 0xC8, 0x8A),
        ['G'] = Canvas.Rgb(0x3E, 0x8A, 0x54),
        ['I'] = Canvas.Rgb(0xE0, 0xF0, 0xFF),
        ['T'] = Canvas.Rgb(0x3A, 0x9A, 0x9A),
        ['C'] = Canvas.Rgb(0xE8, 0x7A, 0x5A),
        // The goblin.
        ['z'] = Canvas.Rgb(0xC8, 0xE8, 0xFF),
        ['a'] = Canvas.Rgb(0x30, 0x30, 0x38),
    };

    // The ranger is a moogle in a bush hat and a khaki vest: 32 wide, 36 tall. He hovers, so the
    // wings do the walking; the pom bobs above the hat.
    private static readonly string[][] RangerStand =
    [
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            ".........hhhhhhhhhhhhh..........",
            "........hhhhhhhhhhhhhhh.........",
            "........hhhhhhhhhhhhhhh.........",
            "........hxxxxxxxxxxxxxh.........",
            ".....hhhhhhhhhhhhhhhhhhhhh......",
            "...hhHHHHHHHHHHHHHHHHHHHHHhh....",
            ".....ddddddddddddddddddd........",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwwwwwwwmmwwwwwwwk........",
            "..vv.kwwwwwwwmmmmwwwwwwk.vv.....",
            ".vVvvkwwwwwwwwmmwwwwwwwkvvVv....",
            "vVVvvkkwwwwwwwwwwwwwwwkkvvVVv...",
            ".VVvvwkkwwwwooowwwwwwkkwvvVV....",
            "..VvvwwkkwwwwwwwwwwwkkwwvvV.....",
            "...vvwwwkttttttttttttkwwwvv.....",
            ".....kwwkttttnntttttttkwwk......",
            ".....kwwkttttnnttttttttkwwk.....",
            "......kkttttttttttttttttkk......",
            "........kttttttttttttttk........",
            "........kWWttttttttttWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            ".........hhhhhhhhhhhhh..........",
            "........hhhhhhhhhhhhhhh.........",
            "........hhhhhhhhhhhhhhh.........",
            "........hxxxxxxxxxxxxxh.........",
            ".....hhhhhhhhhhhhhhhhhhhhh......",
            "...hhHHHHHHHHHHHHHHHHHHHHHhh....",
            ".....ddddddddddddddddddd........",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwwwwwwwmmwwwwwwwk........",
            ".....kwwwwwwwmmmmwwwwwwk........",
            "..vv.kwwwwwwwwmmwwwwwwwk.vv.....",
            ".vVvvkkwwwwwwwwwwwwwwwkkvvVv....",
            "vVVvvwkkwwwwooowwwwwwkkwvvVVv...",
            ".VVvvwwkkwwwwwwwwwwwkkwwvvVV....",
            "..VvvwwwkttttttttttttkwwwvvV....",
            ".....kwwkttttnntttttttkwwk......",
            ".....kwwkttttnnttttttttkwwk.....",
            "......kkttttttttttttttttkk......",
            "........kttttttttttttttk........",
            "........kWWttttttttttWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
    ];

    private static readonly string[][] RangerCreep =
    [
        [
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            ".....................pp.........",
            "....................pPpp........",
            "....................pppp........",
            ".....................pp.........",
            ".....................k..........",
            "...............hhhhhhhhhhhhh....",
            "..............hhhhhhhhhhhhhhh...",
            "..............hhhhhhhhhhhhhhh...",
            "..............hxxxxxxxxxxxxxh...",
            "...........hhhhhhhhhhhhhhhhhhhhh",
            ".........hhHHHHHHHHHHHHHHHHHHHHH",
            "...........ddddddddddddddddddd..",
            ".............kwwwwwwwwwwwwwk....",
            "............kwwwwwwwwwwwwwwwk...",
            "...........kwwwwwwwwwwwwwwwwwk..",
            "...........kwwweekwwwwwkeewwwk..",
            "...........kwwweekwwwwwkeewwwk..",
            "....vv.....kwwwwwwwwmmwwwwwwwk..",
            "...vVvv....kwwwwwwwmmmmwwwwwwk..",
            "..vVVvvv...kwwwwwwwwmmwwwwwwwk..",
            "...VVvvwwwwkkwwwwwwwwwwwwwwwwk..",
            "....VvvwwwwwkkwwwwooowwwwwwkkS..",
            ".....vvwwwwwwkkwwwwwwwwwwwkk....",
            ".......kwwwwwwkttttttttttttk....",
            "........kwwwwkkttttnnttttttk....",
            ".........kkkkttttttnntttttttk...",
            "............kkttttttttttttttk...",
            "..............kWWttttttttttWWk..",
            "...............kWWWWWWWWWWWWk...",
            "................kkkkkkkkkkkk....",
        ],
        [
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            ".....................pp.........",
            "....................pPpp........",
            "....................pppp........",
            ".....................pp.........",
            ".....................k..........",
            "...............hhhhhhhhhhhhh....",
            "..............hhhhhhhhhhhhhhh...",
            "..............hhhhhhhhhhhhhhh...",
            "..............hxxxxxxxxxxxxxh...",
            "...........hhhhhhhhhhhhhhhhhhhhh",
            ".........hhHHHHHHHHHHHHHHHHHHHHH",
            "...........ddddddddddddddddddd..",
            ".............kwwwwwwwwwwwwwk....",
            "............kwwwwwwwwwwwwwwwk...",
            "...........kwwwwwwwwwwwwwwwwwk..",
            "...........kwwweekwwwwwkeewwwk..",
            "...........kwwweekwwwwwkeewwwk..",
            "...........kwwwwwwwwmmwwwwwwwk..",
            "....vv.....kwwwwwwwmmmmwwwwwwk..",
            "...vVvv....kwwwwwwwwmmwwwwwwwk..",
            "..vVVvvwwwwkkwwwwwwwwwwwwwwwwk..",
            "...VVvvwwwwwkkwwwwooowwwwwwkkS..",
            "....Vvvwwwwwwkkwwwwwwwwwwwkk....",
            ".......kwwwwwwkttttttttttttk....",
            "........kwwwwkkttttnnttttttk....",
            ".........kkkkttttttnntttttttk...",
            "............kkttttttttttttttk...",
            "..............kWWttttttttttWWk..",
            "...............kWWWWWWWWWWWWk...",
            "................kkkkkkkkkkkk....",
        ],
    ];

    private static readonly string[][] RangerGrab =
    [
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            ".........hhhhhhhhhhhhh..........",
            "........hhhhhhhhhhhhhhh.........",
            "........hhhhhhhhhhhhhhh.........",
            "........hxxxxxxxxxxxxxh.........",
            ".....hhhhhhhhhhhhhhhhhhhhh......",
            "...hhHHHHHHHHHHHHHHHHHHHHHhh....",
            ".....ddddddddddddddddddd........",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwwwwwwwmmwwwwwwwk........",
            "..vv.kwwwwwwwmmmmwwwwwwk.vv.....",
            ".vVvvkwwwwwwwwmmwwwwwwwkvvVv....",
            "vVVvvkkwwwwwwwwwwwwwwwkkvvVVv...",
            ".VVvvwkkwwwwooowwwwwwkkwvvVV....",
            "..VvvwwkkwwwwwwwwwwwkkwwvvV.....",
            "...vvwwwkttttttttttttkwwwvv.....",
            ".....kwwkttttnntttttttkwwk......",
            ".....kwwkttttnnttttttttkwwk.....",
            "......kkttttttttttttttttkk......",
            "........kttttttttttttttk........",
            "........kWWttttttttttWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            ".........hhhhhhhhhhhhh..........",
            "........hhhhhhhhhhhhhhh.........",
            "........hhhhhhhhhhhhhhh.........",
            "........hxxxxxxxxxxxxxh.........",
            ".....hhhhhhhhhhhhhhhhhhhhh......",
            "...hhHHHHHHHHHHHHHHHHHHHHHhh....",
            ".....ddddddddddddddddddd........",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwwwwwwwmmwwwwwwwk........",
            "..vv.kwwwwwwwmmmmwwwwwwk.vv.....",
            ".vVvvkwwwwwwwwmmwwwwwwwkvvVv....",
            "vVVvvkkwwwwwwwwwwwwwwwkkvvVVv...",
            ".VVvvwkkwwwwooowwwwwwkkwvvVV....",
            "..VvvwwkkwwwwwwwwwwwkkwwvvV.....",
            "...vvwwwkttttttttttttkwwwvv.....",
            ".....kwwkttttnntttttttkwwk......",
            ".....kwwkttttnnttttttttkwwk.....",
            "......kkttttttttttttttttkk......",
            "........kttttttttttttttk........",
            "........kWWttttttttttWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
    ];

    private static readonly string[][] RangerRun =
    [
        [
            "................................",
            "................................",
            "................................",
            ".....pp.........................",
            "....pPpp........................",
            "....pppp........................",
            ".....pp.k.......................",
            ".......kk..hhhhhhhhhhhhh........",
            "........khhhhhhhhhhhhhhh........",
            "........khhhhhhhhhhhhhhh........",
            "........khxxxxxxxxxxxxxh........",
            ".....hhhhhhhhhhhhhhhhhhhhhhh....",
            "...hhHHHHHHHHHHHHHHHHHHHHHHHhh..",
            ".....ddddddddddddddddddddddd....",
            "..vVv......kwwwwwwwwwwwwwk......",
            ".vVVVv....kwwwwwwwwwwwwwwwk.....",
            ".vVVVvv..kwwwwwwwwwkeewwwwwk....",
            "..vVVvvv.kwwwwwwwwwkeewwwwwk....",
            "...vvvvvkwwwwwwwwwwwwwwwmmwwk...",
            "....vvvkwwwwwwwwwwwwwwwmmmmwk...",
            "..vVv.kwwwwwwwwwwwwwwwwwmmwwk...",
            ".vVVvvkkwwwwwwwwwwwwwooowwwk....",
            ".vVVVvvwkkwwwwwwwwwwwwwwwkkwww..",
            "..vVvvvwwwkkttttttttttttkkwwwww.",
            "...vvvvwwwwkttttnnttttttkwwwwwww",
            "....kwwwwwwkttttnnttttttttkkkkk.",
            ".....kkwwwwkttttttttttttttk.....",
            "......kkWWkkkWWttttttttWWk......",
            ".........kkkkkkWWWWWWWWWk.......",
            "..............kkkkkkkkkk........",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
        [
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            ".....pp.........................",
            "....pPpp........................",
            "....pppp.k.hhhhhhhhhhhhh........",
            ".....pp.kkhhhhhhhhhhhhhh........",
            "........khhhhhhhhhhhhhhh........",
            "........khxxxxxxxxxxxxxh........",
            ".....hhhhhhhhhhhhhhhhhhhhhhh....",
            "...hhHHHHHHHHHHHHHHHHHHHHHHHhh..",
            ".....ddddddddddddddddddddddd....",
            "...........kwwwwwwwwwwwwwk......",
            "..........kwwwwwwwwwwwwwwwk.....",
            ".........kwwwwwwwwwkeewwwwwk....",
            ".........kwwwwwwwwwkeewwwwwk....",
            "........kwwwwwwwwwwwwwwwmmwwk...",
            ".......kwwwwwwwwwwwwwwwmmmmwk...",
            "......kwwwwwwwwwwwwwwwwwmmwwk...",
            "..vv..kkwwwwwwwwwwwwwooowwwk....",
            ".vVvvv.wkkwwwwwwwwwwwwwwwkkwww..",
            ".vVVvvvwwwkkttttttttttttkkwwwww.",
            "..vVVvvvwwwkttttnnttttttkwwwwwww",
            "....kwwwwwwkttttnnttttttttkkkkk.",
            ".....kkwwwwkttttttttttttttk.....",
            "......kkWWkkkWWttttttttWWk......",
            ".........kkkkkkWWWWWWWWWk.......",
            "..............kkkkkkkkkk........",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
    ];

    private static readonly string[][] RangerPoint =
    [
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            ".........hhhhhhhhhhhhh..........",
            "........hhhhhhhhhhhhhhh.........",
            "........hhhhhhhhhhhhhhh.........",
            "........hxxxxxxxxxxxxxh.........",
            ".....hhhhhhhhhhhhhhhhhhhhh......",
            "...hhHHHHHHHHHHHHHHHHHHHHHhh....",
            ".....ddddddddddddddddddd........",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwwwwwwwmmwwwwwwwk........",
            "..vv.kwwwwwwwmmmmwwwwwwk.vv.....",
            ".vVvvkwwwwwwwwmmwwwwwwwkvvVv....",
            "vVVvvkkwwwwwwwwwwwwwwwkkvvVVv...",
            ".VVvvwkkwwwwooowwwwwwkkwvvVV....",
            "..VvvwwkkwwwwwwwwwwwkkwwvvV.....",
            "...vvwwwkttttttttttttkwwwwwwwww.",
            ".....kwwkttttnntttttttkwwkkkkkk.",
            ".....kwwkttttnnttttttttkwwk.....",
            "......kkttttttttttttttttkk......",
            "........kttttttttttttttk........",
            "........kWWttttttttttWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
    ];








    public enum Goblin
    {
        Talk,
        Excited,
    }

    // The shopping host is a goblin in a hood and goggles with a headset: 24 wide, 30 tall.
    // 'z' is the goggle glass, 'a' the headset arm.
    private static readonly string[][] GoblinTalk =
    [
        [
            "........rrrrrrrr........",
            "......rrrrrrrrrrrr......",
            ".....rrrrrrrrrrrrrr.....",
            "....rrrrrrrrrrrrrrrr....",
            "....rrkkkkkkkkkkkkrr....",
            "...rrkzzzkkkkkkzzzkrr...",
            "...rrkzyzkkkkkkzyzkrr...",
            "...rrkzzzkkkkkkzzzkrr...",
            "...rrkkkkkkkkkkkkkkrra..",
            "....rrggggggggggggrraa..",
            "....rggggggggggggggr.a..",
            "....rggggGGGGGGggggr.a..",
            ".....gggGkkkkkkGggg.aa..",
            ".....gggGGGGGGGGggg.....",
            "......ggggggggggg.......",
            ".......gggggggg.........",
            "....hhhhhhhhhhhhhhhh....",
            "...hhhhxxxxxxxxxxhhhh...",
            "..gghhxxxxxxxxxxxxhhgg..",
            "..gghhxxxxxxxxxxxxhhgg..",
            "..gghhxxxxxxxxxxxxhhgg..",
            "..gg.hxxxxxxxxxxxxh.gg..",
            ".....hxxxxxxxxxxxxh.....",
            ".....hhhhhhhhhhhhhh.....",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            ".....kkkkkk..kkkkkk.....",
            ".....kkkkkk..kkkkkk.....",
        ],
        [
            "........rrrrrrrr........",
            "......rrrrrrrrrrrr......",
            ".....rrrrrrrrrrrrrr.....",
            "....rrrrrrrrrrrrrrrr....",
            "....rrkkkkkkkkkkkkrr....",
            "...rrkzzzkkkkkkzzzkrr...",
            "...rrkzyzkkkkkkzyzkrr...",
            "...rrkzzzkkkkkkzzzkrr...",
            "...rrkkkkkkkkkkkkkkrra..",
            "....rrggggggggggggrraa..",
            "....rggggggggggggggr.a..",
            "....rggggGGGGGGggggr.a..",
            ".....gggGkkkkkkGggg.aa..",
            ".....gggGkkkkkkGggg.....",
            "......gggGGGGGGgg.......",
            ".......gggggggg.........",
            "....hhhhhhhhhhhhhhhh....",
            "...hhhhxxxxxxxxxxhhhh...",
            "..gghhxxxxxxxxxxxxhhgg..",
            ".gg.hhxxxxxxxxxxxxhh.gg.",
            ".gg.hhxxxxxxxxxxxxhh.gg.",
            ".....hxxxxxxxxxxxxh.....",
            ".....hxxxxxxxxxxxxh.....",
            ".....hhhhhhhhhhhhhh.....",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            ".....kkkkkk..kkkkkk.....",
            ".....kkkkkk..kkkkkk.....",
        ],
    ];

    private static readonly string[][] GoblinExcited =
    [
        [
            "gg......rrrrrrrr......gg",
            "gg....rrrrrrrrrrrr....gg",
            "gg...rrrrrrrrrrrrrr...gg",
            "gg..rrrrrrrrrrrrrrrr..gg",
            ".g..rrkkkkkkkkkkkkrr..g.",
            ".g.rrkzzzkkkkkkzzzkrr.g.",
            ".g.rrkzyzkkkkkkzyzkrr.g.",
            ".g.rrkzzzkkkkkkzzzkrr.g.",
            ".g.rrkkkkkkkkkkkkkkrrag.",
            ".g..rrggggggggggggrraag.",
            ".g..rggggggggggggggr.ag.",
            ".g..rggggGGGGGGggggr.ag.",
            ".g...gggGkkkkkkGggg.aag.",
            ".g...gggGkkkkkkGggg...g.",
            ".g....gggGGGGGGgg.....g.",
            ".g.....gggggggg.......g.",
            ".ghhhhhhhhhhhhhhhhhhhhg.",
            "..hhhhhxxxxxxxxxxhhhhh..",
            "....hhxxxxxxxxxxxxhh....",
            ".....hxxxxxxxxxxxxh.....",
            ".....hxxxxxxxxxxxxh.....",
            ".....hxxxxxxxxxxxxh.....",
            ".....hxxxxxxxxxxxxh.....",
            ".....hhhhhhhhhhhhhh.....",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            ".....kkkkkk..kkkkkk.....",
            ".....kkkkkk..kkkkkk.....",
        ],
        [
            "........rrrrrrrr........",
            "gg....rrrrrrrrrrrr....gg",
            "gg...rrrrrrrrrrrrrr...gg",
            "gg..rrrrrrrrrrrrrrrr..gg",
            "gg..rrkkkkkkkkkkkkrr..gg",
            ".g.rrkzzzkkkkkkzzzkrr.g.",
            ".g.rrkzyzkkkkkkzyzkrr.g.",
            ".g.rrkzzzkkkkkkzzzkrr.g.",
            ".g.rrkkkkkkkkkkkkkkrrag.",
            ".g..rrggggggggggggrraag.",
            ".g..rggggggggggggggr.ag.",
            ".g..rggggGGGGGGggggr.ag.",
            ".g...gggGkkkkkkGggg.aag.",
            ".g...gggGGGGGGGGggg...g.",
            ".g....ggggggggggg.....g.",
            ".g.....gggggggg.......g.",
            ".ghhhhhhhhhhhhhhhhhhhhg.",
            "..hhhhhxxxxxxxxxxhhhhh..",
            "....hhxxxxxxxxxxxxhh....",
            ".....hxxxxxxxxxxxxh.....",
            ".....hxxxxxxxxxxxxh.....",
            ".....hxxxxxxxxxxxxh.....",
            ".....hxxxxxxxxxxxxh.....",
            ".....hhhhhhhhhhhhhh.....",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            "......rrrrr..rrrrr......",
            ".....kkkkkk..kkkkkk.....",
            ".....kkkkkk..kkkkkk.....",
        ],
    ];

    public enum Painter
    {
        Stand,
        Stroke,
        Wave,
    }

    // The painter is the same moogle in a beret and a smock, made from the ranger's frames.
    private static readonly string[][] PainterStand =
    [
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            "............cccccc..............",
            ".........cccccccccccc...........",
            "........cccccccccccccc..........",
            ".......cccccccccccccccc.........",
            ".......cccccccccccccccc.........",
            "........cccccccccccccc..........",
            "..........cccccccccc............",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwwwwwwwmmwwwwwwwk........",
            "..vv.kwwwwwwwmmmmwwwwwwk.vv.....",
            ".vVvvkwwwwwwwwmmwwwwwwwkvvVv....",
            "vVVvvkkwwwwwwwwwwwwwwwkkvvVVv...",
            ".VVvvwkkwwwwooowwwwwwkkwvvVV....",
            "..VvvwwkkwwwwwwwwwwwkkwwvvV.....",
            "...vvwwwkqqqqqqqqqqqqkwwwvv.....",
            ".....kwwkqqqqBBqqqqqqqkwwk......",
            ".....kwwkqqqqBBqqqqqqqqkwwk.....",
            "......kkqqqqqqqqqqqqqqqqkk......",
            "........kqqqqqqqqqqqqqqk........",
            "........kWWqqqqqqqqqqWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
    ];

    private static readonly string[][] PainterStroke =
    [
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            "............cccccc..............",
            ".........cccccccccccc...........",
            "........cccccccccccccc..........",
            ".......cccccccccccccccc.........",
            ".......cccccccccccccccc.........",
            "........cccccccccccccc..........",
            "..........cccccccccc............",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwwwwwwwmmwwwwwwwk........",
            "..vv.kwwwwwwwmmmmwwwwwwk.vv.....",
            ".vVvvkwwwwwwwwmmwwwwwwwkvvVv....",
            "vVVvvkkwwwwwwwwwwwwwwwkkvvVVv...",
            ".VVvvwkkwwwwooowwwwwwkkwvvVV....",
            "..Vvvwwkkwwwwwwwwwwwkkwwwwrrrrll",
            "...vvwwwkqqqqqqqqqqqqkwww.......",
            ".....kwwkqqqqBBqqqqqqqkwwk......",
            ".....kwwkqqqqBBqqqqqqqqkwwk.....",
            "......kkqqqqqqqqqqqqqqqqkk......",
            "........kqqqqqqqqqqqqqqk........",
            "........kWWqqqqqqqqqqWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            "............cccccc..............",
            ".........cccccccccccc...........",
            "........cccccccccccccc..........",
            ".......cccccccccccccccc.........",
            ".......cccccccccccccccc.........",
            "........cccccccccccccc..........",
            "..........cccccccccc............",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwwwwwwwmmwwwwwwwk........",
            "..vv.kwwwwwwwmmmmwwwwwwk.vv.....",
            ".vVvvkwwwwwwwwmmwwwwwwwkvvVv....",
            "vVVvvkkwwwwwwwwwwwwwwwkkvvVVv...",
            ".VVvvwkkwwwwooowwwwwwkkwvvVV....",
            "..VvvwwkkwwwwwwwwwwwkkwwvvV.....",
            "...vvwwwkqqqqqqqqqqqqkwwwvv.....",
            ".....kwwkqqqqBBqqqqqqqkwwwrrrrll",
            ".....kwwkqqqqBBqqqqqqqqww.......",
            "......kkqqqqqqqqqqqqqqqqkk......",
            "........kqqqqqqqqqqqqqqk........",
            "........kWWqqqqqqqqqqWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
    ];

    private static readonly string[][] PainterWave =
    [
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            "............cccccc..............",
            ".........cccccccccccc...........",
            "........cccccccccccccc..........",
            ".......cccccccccccccccc.........",
            ".......cccccccccccccccc.........",
            "........cccccccccccccc..........",
            "..........cccccccccc............",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk........",
            ".....kwwweekwwwwwkeewwwkww......",
            ".....kwwwwwwwwmmwwwwwwwkww......",
            "..vv.kwwwwwwwmmmmwwwwwwkwvv.....",
            ".vVvvkwwwwwwwwmmwwwwwwwkvvVv....",
            "vVVvvkkwwwwwwwwwwwwwwwkkvvVVv...",
            ".VVvvwkkwwwwooowwwwwwkkwvvVV....",
            "..VvvwwkkwwwwwwwwwwwkkwwvvV.....",
            "...vvwwwkqqqqqqqqqqqqkwwwvv.....",
            ".....kwwkqqqqBBqqqqqqqkwwk......",
            ".....kwwkqqqqBBqqqqqqqqkwwk.....",
            "......kkqqqqqqqqqqqqqqqqkk......",
            "........kqqqqqqqqqqqqqqk........",
            "........kWWqqqqqqqqqqWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
        [
            "...............pp...............",
            "..............pPpp..............",
            "..............pppp..............",
            "...............pp...............",
            "...............k................",
            "............cccccc..............",
            ".........cccccccccccc...........",
            "........cccccccccccccc..........",
            ".......cccccccccccccccc.........",
            ".......cccccccccccccccc.........",
            "........cccccccccccccc..........",
            "..........cccccccccc............",
            ".......kwwwwwwwwwwwwwk..........",
            "......kwwwwwwwwwwwwwwwk.........",
            ".....kwwwwwwwwwwwwwwwwwk........",
            ".....kwwweekwwwwwkeewwwk.ww.....",
            ".....kwwweekwwwwwkeewwwk.ww.....",
            ".....kwwwwwwwwmmwwwwwwwkw.......",
            "..vv.kwwwwwwwmmmmwwwwwwk.vv.....",
            ".vVvvkwwwwwwwwmmwwwwwwwkvvVv....",
            "vVVvvkkwwwwwwwwwwwwwwwkkvvVVv...",
            ".VVvvwkkwwwwooowwwwwwkkwvvVV....",
            "..VvvwwkkwwwwwwwwwwwkkwwvvV.....",
            "...vvwwwkqqqqqqqqqqqqkwwwvv.....",
            ".....kwwkqqqqBBqqqqqqqkwwk......",
            ".....kwwkqqqqBBqqqqqqqqkwwk.....",
            "......kkqqqqqqqqqqqqqqqqkk......",
            "........kqqqqqqqqqqqqqqk........",
            "........kWWqqqqqqqqqqWWk........",
            ".........kWWWWWWWWWWWWk.........",
            "..........kkwwwwwwwwkk..........",
            "...........kwwk..kwwk...........",
            "...........kWWk..kWWk...........",
            "...........kkkk..kkkk...........",
            "................................",
            "................................",
        ],
    ];

    public static void DrawPainter(Span<uint> target, Painter action, double seconds, int x, int y, int scale, bool flip = false)
    {
        var (frames, rate) = action switch
        {
            Painter.Stroke => (PainterStroke, 2.0),
            Painter.Wave => (PainterWave, 3.0),
            _ => (PainterStand, 1.0),
        };

        var bob = (int)(Math.Sin(seconds * 2.0) * 3);
        var frame = frames[(int)(seconds * rate) % frames.Length];
        Canvas.Sprite(target, frame, c => Palette.GetValueOrDefault(c, 0u), x, y + bob, scale, flip);
    }

    public enum Loporrit
    {
        Talk,
        Point,
    }

    // The estate agent is a Loporrit: a round white rabbit with long ears, a collar and a
    // clipboard, 24 wide, 32 tall. The ears twitch; the clipboard is always with him.
    private static readonly string[][] LoporritTalk =
    [
        [
            ".....ww..........ww.....",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            ".....wPw........wPw.....",
            ".....wPw........wPw.....",
            "......ww........ww......",
            ".....wwwwwwwwwwwwww.....",
            "....wwwwwwwwwwwwwwww....",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwweewwwwwweewwww...",
            "...wwwweewwwwwweewwww...",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwwwwwwPPwwwwwwww...",
            "....wwwwwwkkkkwwwwww....",
            ".....wwwwwwwwwwwwww.....",
            "......uuuuuuuuuuuu......",
            ".....wwwwwwwwwwwwwww....",
            "....wwwwwwwwwwwwwwwjjj..",
            "...wwwwwwwwwwwwwwwwjqj..",
            "...wwwwwwwwwwwwwwwwjqj..",
            "...wwwwwwwwwwwwwwwwjqj..",
            "...wwwwwwwwwwwwwwwwjjj..",
            "....wwwwwwwwwwwwwwww....",
            ".....wwwwwwwwwwwwww.....",
            "......wwwwwwwwwwww......",
            ".......wwww..wwww.......",
            ".......wwww..wwww.......",
            ".......WWWW..WWWW.......",
            "........................",
            "........................",
        ],
        [
            "....ww............ww....",
            "...wPPw..........wPPw...",
            "...wPPw..........wPPw...",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            ".....wPw........wPw.....",
            ".....wPw........wPw.....",
            "......ww........ww......",
            ".....wwwwwwwwwwwwww.....",
            "....wwwwwwwwwwwwwwww....",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwweewwwwwweewwww...",
            "...wwwweewwwwwweewwww...",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwwwwwwPPwwwwwwww...",
            "....wwwwwwkeekwwwwww....",
            ".....wwwwwwwwwwwwww.....",
            "......uuuuuuuuuuuu......",
            ".....wwwwwwwwwwwwwww....",
            "....wwwwwwwwwwwwwwwjjj..",
            "...wwwwwwwwwwwwwwwwjqj..",
            "...wwwwwwwwwwwwwwwwjqj..",
            "...wwwwwwwwwwwwwwwwjqj..",
            "...wwwwwwwwwwwwwwwwjjj..",
            "....wwwwwwwwwwwwwwww....",
            ".....wwwwwwwwwwwwww.....",
            "......wwwwwwwwwwww......",
            ".......wwww..wwww.......",
            ".......wwww..wwww.......",
            ".......WWWW..WWWW.......",
            "........................",
            "........................",
        ],
    ];

    private static readonly string[][] LoporritPoint =
    [
        [
            ".....ww..........ww.....",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            ".....wPw........wPw.....",
            ".....wPw........wPw.....",
            "......ww........ww......",
            ".....wwwwwwwwwwwwww.....",
            "....wwwwwwwwwwwwwwww....",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwweewwwwwweewwww...",
            "...wwwweewwwwwweewwww...",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwwwwwwPPwwwwwwww...",
            "....wwwwwwkkkkwwwwww....",
            ".....wwwwwwwwwwwwww.....",
            "......uuuuuuuuuuuu......",
            ".....wwwwwwwwwwwwwwwww..",
            "....wwwwwwwwwwwwwwwwwwww",
            "...wwwwwwwwwwwwwwwwww.ww",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwwwwwwwwwwwwwwww...",
            "....wwwwwwwwwwwwwwww....",
            ".....wwwwwwwwwwwwww.....",
            "......wwwwwwwwwwww......",
            ".......wwww..wwww.......",
            ".......wwww..wwww.......",
            ".......WWWW..WWWW.......",
            "........................",
            "........................",
        ],
        [
            ".....ww..........ww.....",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            "....wPPw........wPPw....",
            ".....wPw........wPw.....",
            ".....wPw........wPw.....",
            "......ww........ww......",
            ".....wwwwwwwwwwwwww.....",
            "....wwwwwwwwwwwwwwww....",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwweewwwwwweewwww...",
            "...wwwweewwwwwweewwww...",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwwwwwwPPwwwwwwww...",
            "....wwwwwwkkkkwwwwww....",
            ".....wwwwwwwwwwwwww.....",
            "......uuuuuuuuuuuu....ww",
            ".....wwwwwwwwwwwwwwwwwww",
            "....wwwwwwwwwwwwwwwwww..",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwwwwwwwwwwwwwwww...",
            "...wwwwwwwwwwwwwwwwww...",
            "....wwwwwwwwwwwwwwww....",
            ".....wwwwwwwwwwwwww.....",
            "......wwwwwwwwwwww......",
            ".......wwww..wwww.......",
            ".......wwww..wwww.......",
            ".......WWWW..WWWW.......",
            "........................",
            "........................",
        ],
    ];

    public static void DrawLoporrit(Span<uint> target, Loporrit action, double seconds, int x, int y, int scale, bool flip = false)
    {
        var (frames, rate) = action switch
        {
            Loporrit.Point => (LoporritPoint, 3.0),
            _ => (LoporritTalk, 2.5),
        };

        var frame = frames[(int)(seconds * rate) % frames.Length];
        Canvas.Sprite(target, frame, c => Palette.GetValueOrDefault(c, 0u), x, y, scale, flip);
    }

    public enum Pixie
    {
        Hover,
        Point,
    }

    public enum Kobold
    {
        Talk,
        Cheer,
    }

    public enum Sahagin
    {
        Talk,
        Point,
    }

    // The forecaster is a pixie in a yellow raincoat, 24 by 28, wings beating.
    private static readonly string[][] PixieHover =
    [
        [
            "........FFFFFFF.........",
            ".......FFFFFFFFF........",
            "......FFFFFFFFFFF.......",
            "..I..FFiiiiiiiiFF...I...",
            ".III.FiiiiiiiiiiF..III..",
            ".IIIiiiieeiiiieeiiiIII..",
            "..IIiiiieeiiiieeiiIII...",
            "...Iiiiiiiiiiiiiii.I....",
            "....iiiiiiiiiiiiii......",
            ".....iiiiiimmiiii.......",
            "......iiiiiiiiii........",
            ".......yyyyyyyy.........",
            "......yyyyyyyyyy........",
            ".....yyyyyyyyyyyy.......",
            "....iiyyyyyyyyyyii......",
            "....iiyyyyyyyyyyii......",
            ".....yyyyyyyyyyyy.......",
            ".....yyyyyyyyyyyy.......",
            ".....yyyyyyyyyyyy.......",
            "......yyyyyyyyyy........",
            "......yyyyyyyyyy........",
            ".......iii..iii.........",
            ".......iii..iii.........",
            ".......kkk..kkk.........",
            "........................",
            "........................",
            "........................",
            "........................",
        ],
        [
            "........FFFFFFF.........",
            ".......FFFFFFFFF........",
            "......FFFFFFFFFFF.......",
            ".....FFiiiiiiiiFF.......",
            "...I.FiiiiiiiiiiF.I.....",
            "..IIiiiieeiiiieeiiiII...",
            "..IIiiiieeiiiieeiiII....",
            "...Iiiiiiiiiiiiiii.I....",
            "....iiiiiiiiiiiiii......",
            ".....iiiiiimmiiii.......",
            "......iiiiiiiiii........",
            ".......yyyyyyyy.........",
            "......yyyyyyyyyy........",
            ".....yyyyyyyyyyyy.......",
            "....iiyyyyyyyyyyii......",
            "....iiyyyyyyyyyyii......",
            ".....yyyyyyyyyyyy.......",
            ".....yyyyyyyyyyyy.......",
            ".....yyyyyyyyyyyy.......",
            "......yyyyyyyyyy........",
            "......yyyyyyyyyy........",
            ".......iii..iii.........",
            ".......iii..iii.........",
            ".......kkk..kkk.........",
            "........................",
            "........................",
            "........................",
            "........................",
        ],
    ];

    private static readonly string[][] PixiePoint =
    [
        [
            "........FFFFFFF.........",
            ".......FFFFFFFFF........",
            "......FFFFFFFFFFF.......",
            "..I..FFiiiiiiiiFF...I...",
            ".III.FiiiiiiiiiiF..III..",
            ".IIIiiiieeiiiieeiiiIII..",
            "..IIiiiieeiiiieeiiIII...",
            "...Iiiiiiiiiiiiiii.I....",
            "....iiiiiiiiiiiiii......",
            ".....iiiiiimmiiii.......",
            "......iiiiiiiiii........",
            ".......yyyyyyyy.........",
            "......yyyyyyyyyy........",
            ".....yyyyyyyyyyyyiiii...",
            "....iiyyyyyyyyyyyy......",
            "....iiyyyyyyyyyy........",
            ".....yyyyyyyyyyyy.......",
            ".....yyyyyyyyyyyy.......",
            ".....yyyyyyyyyyyy.......",
            "......yyyyyyyyyy........",
            "......yyyyyyyyyy........",
            ".......iii..iii.........",
            ".......iii..iii.........",
            ".......kkk..kkk.........",
            "........................",
            "........................",
            "........................",
            "........................",
        ],
        [
            "........FFFFFFF.........",
            ".......FFFFFFFFF........",
            "......FFFFFFFFFFF.......",
            ".....FFiiiiiiiiFF.......",
            "...I.FiiiiiiiiiiF.I.....",
            "..IIiiiieeiiiieeiiiII...",
            "..IIiiiieeiiiieeiiII....",
            "...Iiiiiiiiiiiiiii.I....",
            "....iiiiiiiiiiiiii......",
            ".....iiiiiimmiiii.......",
            "......iiiiiiiiii........",
            ".......yyyyyyyy.........",
            "......yyyyyyyyyy........",
            ".....yyyyyyyyyyyyiiii...",
            "....iiyyyyyyyyyyyy......",
            "....iiyyyyyyyyyy........",
            ".....yyyyyyyyyyyy.......",
            ".....yyyyyyyyyyyy.......",
            ".....yyyyyyyyyyyy.......",
            "......yyyyyyyyyy........",
            "......yyyyyyyyyy........",
            ".......iii..iii.........",
            ".......iii..iii.........",
            ".......kkk..kkk.........",
            "........................",
            "........................",
            "........................",
            "........................",
        ],
    ];

    // The game-show host is a kobold in a hood, 24 by 30, eyes lit.
    private static readonly string[][] KoboldTalk =
    [
        [
            "..........rrrr..........",
            ".........rrrrrr.........",
            "........rrrrrrrr........",
            ".......rrrrrrrrrr.......",
            "......rrrrrrrrrrrr......",
            ".....rrrrrrrrrrrrrr.....",
            ".....rrkkkkkkkkkkrr.....",
            "....rrkkkkkkkkkkkkrr....",
            "....rrkkyykkkkyykkrr....",
            "....rrkkyykkkkyykkrr....",
            "....rrkkkkkkkkkkkkrr....",
            "....rrrkkkkkkkkkkrrr....",
            ".....rrrkkkkkkkkrrr.....",
            "......rrrrrrrrrrrr......",
            ".......rrrrrrrrrr.......",
            "......ddddddddddddd.....",
            ".....ddddddddddddddd....",
            "....dddddddddddddddd....",
            "...rrdddddddddddddddrr..",
            "...rrdddddddddddddddrr..",
            "...rr.ddddddddddddd.rr..",
            "......ddddddddddddd.....",
            "......ddddddddddddd.....",
            ".......ddddddddddd......",
            ".......rrrr...rrrr......",
            ".......rrrr...rrrr......",
            ".......rrrr...rrrr......",
            "......kkkkk...kkkkk.....",
            "........................",
            "........................",
        ],
        [
            "..........rrrr..........",
            ".........rrrrrr.........",
            "........rrrrrrrr........",
            ".......rrrrrrrrrr.......",
            "......rrrrrrrrrrrr......",
            ".....rrrrrrrrrrrrrr.....",
            ".....rrkkkkkkkkkkrr.....",
            "....rrkkkkkkkkkkkkrr....",
            "....rrkkyykkkkyykkrr....",
            "....rrkkkkkkkkkkkkrr....",
            "....rrkkkkkkkkkkkkrr....",
            "....rrrkkkkkkkkkkrrr....",
            ".....rrrkkkkkkkkrrr.....",
            "......rrrrrrrrrrrr......",
            ".......rrrrrrrrrr.......",
            "......ddddddddddddd.....",
            ".....ddddddddddddddd....",
            "....dddddddddddddddd....",
            "...rrdddddddddddddddrr..",
            "...rrdddddddddddddddrr..",
            "..rrr.ddddddddddddd.rrr.",
            "......ddddddddddddd.....",
            "......ddddddddddddd.....",
            ".......ddddddddddd......",
            ".......rrrr...rrrr......",
            ".......rrrr...rrrr......",
            ".......rrrr...rrrr......",
            "......kkkkk...kkkkk.....",
            "........................",
            "........................",
        ],
    ];

    private static readonly string[][] KoboldCheer =
    [
        [
            "..........rrrr..........",
            ".........rrrrrr.........",
            "........rrrrrrrr........",
            ".......rrrrrrrrrr.......",
            "......rrrrrrrrrrrr......",
            ".....rrrrrrrrrrrrrr.....",
            ".....rrkkkkkkkkkkrr.....",
            "....rrkkkkkkkkkkkkrr....",
            "....rrkkyykkkkyykkrr....",
            "....rrkkyykkkkyykkrr....",
            "....rrkkkkkkkkkkkkrr....",
            "....rrrkkkkkkkkkkrrr....",
            ".....rrrkkkkkkkkrrr.....",
            "......rrrrrrrrrrrr......",
            ".......rrrrrrrrrr.......",
            "..rr..ddddddddddddd..rr.",
            "..rr.ddddddddddddddd.rr.",
            "..rrdddddddddddddddddrr.",
            "...rrdddddddddddddddrr..",
            "....ddddddddddddddddd...",
            "......ddddddddddddd.....",
            "......ddddddddddddd.....",
            "......ddddddddddddd.....",
            ".......ddddddddddd......",
            ".......rrrr...rrrr......",
            ".......rrrr...rrrr......",
            ".......rrrr...rrrr......",
            "......kkkkk...kkkkk.....",
            "........................",
            "........................",
        ],
        [
            "..........rrrr..........",
            ".........rrrrrr.........",
            "........rrrrrrrr........",
            ".......rrrrrrrrrr.......",
            "......rrrrrrrrrrrr......",
            ".....rrrrrrrrrrrrrr.....",
            ".....rrkkkkkkkkkkrr.....",
            "....rrkkkkkkkkkkkkrr....",
            "....rrkkyykkkkyykkrr....",
            "....rrkkyykkkkyykkrr....",
            "....rrkkkkkkkkkkkkrr....",
            "....rrrkkkkkkkkkkrrr....",
            ".....rrrkkkkkkkkrrr.....",
            "......rrrrrrrrrrrr......",
            ".rr....rrrrrrrrrr....rr.",
            ".rr...ddddddddddddd...rr",
            "..rr.ddddddddddddddd.rr.",
            "..rrdddddddddddddddddrr.",
            "...rrdddddddddddddddrr..",
            "....ddddddddddddddddd...",
            "......ddddddddddddd.....",
            "......ddddddddddddd.....",
            "......ddddddddddddd.....",
            ".......ddddddddddd......",
            ".......rrrr...rrrr......",
            ".......rrrr...rrrr......",
            ".......rrrr...rrrr......",
            "......kkkkk...kkkkk.....",
            "........................",
            "........................",
        ],
    ];

    // The newsreader is a sahagin with a spear, 24 by 34, fins up.
    private static readonly string[][] SahaginTalk =
    [
        [
            "...........CC...........",
            "..........CCCC..........",
            ".........CCCCCC.........",
            "........CCTTTTCC........",
            ".......CTTTTTTTTC.......",
            "......TTTTTTTTTTTT......",
            ".....TTTTTTTTTTTTTT.....",
            ".....TTeeeTTTTeeeTT.....",
            ".....TTeyeTTTTeyeTT.....",
            ".....TTeeeTTTTeeeTT.....",
            ".....TTTTTTTTTTTTTT.....",
            "......TTTTkkkkkkTT......",
            "......TTTTkTkTkkTT......",
            ".......TTTTTTTTTT.......",
            "........TTTTTTTT........",
            ".......TTTTTTTTTT.......",
            "......TTTTTTTTTTTT.....r",
            ".....TTTTTTTTTTTTTT....r",
            "....TTTTTTTTTTTTTTTT..r.",
            "....TTTTTTTTTTTTTTTTTTr.",
            "....TT.TTTTTTTTTT.TTr...",
            "......TTTTTTTTTTTT.r....",
            "......TTTTTTTTTTTT.r....",
            "......TTTTTTTTTTTT.r....",
            ".......TTTTTTTTTT..r....",
            ".......TTTT..TTTT..r....",
            ".......TTTT..TTTT..r....",
            "......TTTTT..TTTTT.r....",
            ".....CCTTT....TTTCC.....",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
        ],
        [
            "...........CC...........",
            "..........CCCC..........",
            ".........CCCCCC.........",
            "........CCTTTTCC........",
            ".......CTTTTTTTTC.......",
            "......TTTTTTTTTTTT......",
            ".....TTTTTTTTTTTTTT.....",
            ".....TTeeeTTTTeeeTT.....",
            ".....TTeyeTTTTeyeTT.....",
            ".....TTeeeTTTTeeeTT.....",
            ".....TTTTTTTTTTTTTT.....",
            "......TTTTkkkkkkTT......",
            "......TTTTkkkkkkTT......",
            ".......TTTkTkTkTT.......",
            "........TTTTTTTT........",
            ".......TTTTTTTTTT.......",
            "......TTTTTTTTTTTT.....r",
            ".....TTTTTTTTTTTTTT....r",
            "....TTTTTTTTTTTTTTTT..r.",
            "....TTTTTTTTTTTTTTTTTTr.",
            "....TT.TTTTTTTTTT.TTr...",
            "......TTTTTTTTTTTT.r....",
            "......TTTTTTTTTTTT.r....",
            "......TTTTTTTTTTTT.r....",
            ".......TTTTTTTTTT..r....",
            ".......TTTT..TTTT..r....",
            ".......TTTT..TTTT..r....",
            "......TTTTT..TTTTT.r....",
            ".....CCTTT....TTTCC.....",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
        ],
    ];

    private static readonly string[][] SahaginPoint =
    [
        [
            "...........CC...........",
            "..........CCCC..........",
            ".........CCCCCC.........",
            "........CCTTTTCC........",
            ".......CTTTTTTTTC.......",
            "......TTTTTTTTTTTT......",
            ".....TTTTTTTTTTTTTT.....",
            ".....TTeeeTTTTeeeTT.....",
            ".....TTeyeTTTTeyeTT.....",
            ".....TTeeeTTTTeeeTT.....",
            ".....TTTTTTTTTTTTTT.....",
            "......TTTTkkkkkkTT......",
            "......TTTTkTkTkkTT......",
            ".......TTTTTTTTTT.......",
            "........TTTTTTTT........",
            ".......TTTTTTTTTT.......",
            "......TTTTTTTTTTTT.....r",
            ".....TTTTTTTTTTTTTTTTTTT",
            "....TTTTTTTTTTTTTTTT....",
            "....TTTTTTTTTTTTTTTT....",
            "....TT.TTTTTTTTTT.TT....",
            "......TTTTTTTTTTTT.r....",
            "......TTTTTTTTTTTT.r....",
            "......TTTTTTTTTTTT.r....",
            ".......TTTTTTTTTT..r....",
            ".......TTTT..TTTT..r....",
            ".......TTTT..TTTT..r....",
            "......TTTTT..TTTTT.r....",
            ".....CCTTT....TTTCC.....",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
        ],
        [
            "...........CC...........",
            "..........CCCC..........",
            ".........CCCCCC.........",
            "........CCTTTTCC........",
            ".......CTTTTTTTTC.......",
            "......TTTTTTTTTTTT......",
            ".....TTTTTTTTTTTTTT.....",
            ".....TTeeeTTTTeeeTT.....",
            ".....TTeyeTTTTeyeTT.....",
            ".....TTeeeTTTTeeeTT.....",
            ".....TTTTTTTTTTTTTT.....",
            "......TTTTkkkkkkTT......",
            "......TTTTkkkkkkTT......",
            ".......TTTkTkTkTT.......",
            "........TTTTTTTT........",
            ".......TTTTTTTTTT.......",
            "......TTTTTTTTTTTT..TTT.",
            ".....TTTTTTTTTTTTTTTT...",
            "....TTTTTTTTTTTTTTTT....",
            "....TTTTTTTTTTTTTTTT....",
            "....TT.TTTTTTTTTT.TT....",
            "......TTTTTTTTTTTT.r....",
            "......TTTTTTTTTTTT.r....",
            "......TTTTTTTTTTTT.r....",
            ".......TTTTTTTTTT..r....",
            ".......TTTT..TTTT..r....",
            ".......TTTT..TTTT..r....",
            "......TTTTT..TTTTT.r....",
            ".....CCTTT....TTTCC.....",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
        ],
    ];

    public static void DrawPixie(Span<uint> target, Pixie action, double seconds, int x, int y, int scale, bool flip = false)
    {
        var frames = action == Pixie.Point ? PixiePoint : PixieHover;
        var frame = frames[(int)(seconds * 8.0) % frames.Length];
        var bob = (int)(Math.Sin(seconds * 3.0) * 4);
        Canvas.Sprite(target, frame, c => Palette.GetValueOrDefault(c, 0u), x, y + bob, scale, flip);
    }

    public static void DrawKobold(Span<uint> target, Kobold action, double seconds, int x, int y, int scale, bool flip = false)
    {
        var (frames, rate) = action == Kobold.Cheer ? (KoboldCheer, 6.0) : (KoboldTalk, 3.0);
        var frame = frames[(int)(seconds * rate) % frames.Length];
        Canvas.Sprite(target, frame, c => Palette.GetValueOrDefault(c, 0u), x, y, scale, flip);
    }

    public static void DrawSahagin(Span<uint> target, Sahagin action, double seconds, int x, int y, int scale, bool flip = false)
    {
        var frames = action == Sahagin.Point ? SahaginPoint : SahaginTalk;
        var frame = frames[(int)(seconds * 2.5) % frames.Length];
        var sway = (int)(Math.Sin(seconds * 1.2) * 2);
        Canvas.Sprite(target, frame, c => Palette.GetValueOrDefault(c, 0u), x + sway, y, scale, flip);
    }

    public enum Mandragora
    {
        Talk,
        Wild,
    }

    // The conspiracy host is a mandragora with a great deal of hair, 24 by 30.
    private static readonly string[][] MandragoraTalk =
    [
        [
            "......F....FF....F......",
            ".....FF.F.FFF.F.FF......",
            "....FFF.FFFFFFF.FFF.....",
            "....FFFFFFFFFFFFFFF.....",
            ".....FFFGFFFFFGFFF......",
            "......FFFFFFFFFFF.......",
            ".......GGGGGGGGG........",
            "......wwwwwwwwwww.......",
            ".....wwwwwwwwwwwww......",
            "....wwwwwwwwwwwwwww.....",
            "....wwweewwwwwweeww.....",
            "....wwweewwwwwweeww.....",
            "....wwwwwwwwwwwwwww.....",
            "....wwwwwkkkkkkwwww.....",
            ".....wwwwkwwwwkwww......",
            ".....wwwwwkkkkwwww......",
            "......wwwwwwwwwww.......",
            "...ww.wwwwwwwwwww.ww....",
            "..ww..wwwwwwwwwww..ww...",
            "..ww.wwwwwwwwwwwww.ww...",
            ".....wwwwwwwwwwwww......",
            ".....wwwwwwwwwwwww......",
            "......wwwwwwwwwww.......",
            ".......wwwwwwwww........",
            "........wwwwwww.........",
            ".........wwwww..........",
            "..........www...........",
            "...........w............",
            "........................",
            "........................",
        ],
        [
            "......F...FFF...F.......",
            ".....FF.F.FFF.F.FF......",
            "....FFF.FFFFFFF.FFF.....",
            "....FFFFFFFFFFFFFFF.....",
            ".....FFFGFFFFFGFFF......",
            "......FFFFFFFFFFF.......",
            ".......GGGGGGGGG........",
            "......wwwwwwwwwww.......",
            ".....wwwwwwwwwwwww......",
            "....wwwwwwwwwwwwwww.....",
            "....wwweewwwwwweeww.....",
            "....wwweewwwwwweeww.....",
            "....wwwwwwwwwwwwwww.....",
            "....wwwwwkkkkkkwwww.....",
            ".....wwwwkkkkkkwww......",
            ".....wwwwwwwwwwwww......",
            "......wwwwwwwwwww.......",
            "...ww.wwwwwwwwwww.ww....",
            "..ww..wwwwwwwwwww..ww...",
            "..ww.wwwwwwwwwwwww.ww...",
            ".....wwwwwwwwwwwww......",
            ".....wwwwwwwwwwwww......",
            "......wwwwwwwwwww.......",
            ".......wwwwwwwww........",
            "........wwwwwww.........",
            ".........wwwww..........",
            "..........www...........",
            "...........w............",
            "........................",
            "........................",
        ],
    ];

    private static readonly string[][] MandragoraWild =
    [
        [
            "...F..F...FF...F..F.....",
            "..FF.FF.F.FF.F.FF.FF....",
            "...FFFF.FFFFFFF.FFFF....",
            "....FFFFFFFFFFFFFFF.....",
            "...FFFFFGFFFFFGFFFFF....",
            "....FFFFFFFFFFFFFFF.....",
            ".......GGGGGGGGG........",
            "......wwwwwwwwwww.......",
            ".....wwwwwwwwwwwww......",
            "....wwwwwwwwwwwwwww.....",
            "....wweeewwwwweeeww.....",
            "....wweeewwwwweeeww.....",
            "....wwwwwwwwwwwwwww.....",
            "....wwwwkkkkkkkwwww.....",
            ".....wwwkwwwwwkwww......",
            ".....wwwwkkkkkwwww......",
            "ww....wwwwwwwwwww....ww.",
            ".ww...wwwwwwwwwww...ww..",
            "..ww.wwwwwwwwwwwww.ww...",
            "...wwwwwwwwwwwwwwwww....",
            ".....wwwwwwwwwwwww......",
            ".....wwwwwwwwwwwww......",
            "......wwwwwwwwwww.......",
            ".......wwwwwwwww........",
            "........wwwwwww.........",
            ".........wwwww..........",
            "..........www...........",
            "...........w............",
            "........................",
            "........................",
        ],
        [
            "..F...F...FF...F...F....",
            "...FF.F.F.FF.F.F.FF.....",
            "...FFFF.FFFFFFF.FFFF....",
            "....FFFFFFFFFFFFFFF.....",
            "...FFFFFGFFFFFGFFFFF....",
            "....FFFFFFFFFFFFFFF.....",
            ".......GGGGGGGGG........",
            "......wwwwwwwwwww.......",
            ".....wwwwwwwwwwwww......",
            "....wwwwwwwwwwwwwww.....",
            "....wweeewwwwweeeww.....",
            "....wweeewwwwweeeww.....",
            "....wwwwwwwwwwwwwww.....",
            "....wwwwkkkkkkkwwww.....",
            ".....wwwkwwwwwkwww......",
            ".....wwwwkkkkkwwww......",
            ".ww...wwwwwwwwwww...ww..",
            "ww....wwwwwwwwwww....ww.",
            "..ww.wwwwwwwwwwwww.ww...",
            "...wwwwwwwwwwwwwwwww....",
            ".....wwwwwwwwwwwww......",
            ".....wwwwwwwwwwwww......",
            "......wwwwwwwwwww.......",
            ".......wwwwwwwww........",
            "........wwwwwww.........",
            ".........wwwww..........",
            "..........www...........",
            "...........w............",
            "........................",
            "........................",
        ],
    ];

    public static void DrawMandragora(Span<uint> target, Mandragora action, double seconds, int x, int y, int scale, bool flip = false)
    {
        var (frames, rate) = action == Mandragora.Wild ? (MandragoraWild, 7.0) : (MandragoraTalk, 3.0);
        var frame = frames[(int)(seconds * rate) % frames.Length];
        Canvas.Sprite(target, frame, c => Palette.GetValueOrDefault(c, 0u), x, y, scale, flip);
    }

    /// <summary>The trailers' cast: the same peoples as the hosts, but other individuals, each with a look.</summary>
    public static readonly string[] CastNames = ["Kupo Reeves", "Sahagin L. Jackson", "Goblin Ford", "Pixie Blanchett", "Mandragora Freeman", "Loporrit Cruise"];

    private static readonly uint Shades = Canvas.Rgb(0x0A, 0x0A, 0x0E);
    private static readonly uint Coat = Canvas.Rgb(0x14, 0x12, 0x18);
    private static readonly uint Beret = Canvas.Rgb(0x5A, 0x2A, 0x7A);
    private static readonly uint Brim = Canvas.Rgb(0x4A, 0x30, 0x1E);
    private static readonly uint Crown = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint Beard = Canvas.Rgb(0xA8, 0xA8, 0xB0);

    /// <summary>
    /// One of the cast, walking or standing, at a size, facing a way. Each is a host's people in
    /// a costume: a coat and dark glasses, a beret, a brim, a crown, a beard, aviators. The costume
    /// is a few rectangles in the sprite's own grid, so it scales and flips with the sprite.
    /// </summary>
    public static void DrawCast(Span<uint> target, int who, bool walk, double seconds, int x, int y, int scale, bool flip = false)
    {
        switch (who % 6)
        {
            case 0:
                // Kupo Reeves: a moogle in a long black coat and dark glasses. He knows kupo fu.
                DrawPainter(target, walk ? Painter.Wave : Painter.Stand, seconds, x, y, scale, flip);
                Costume(target, x, y, scale, flip, 32, 9, 5, 14, 3, Coat);
                Costume(target, x, y, scale, flip, 32, 7, 8, 18, 4, Coat);
                Costume(target, x, y, scale, flip, 32, 6, 12, 3, 6, Coat);
                Costume(target, x, y, scale, flip, 32, 23, 12, 3, 6, Coat);
                Costume(target, x, y, scale, flip, 32, 8, 15, 16, 2, Shades);
                Costume(target, x, y, scale, flip, 32, 8, 23, 16, 7, Coat);

                break;
            case 1:
                // Sahagin L. Jackson: a beret, dark glasses, and a look that ends conversations.
                DrawSahagin(target, walk ? Sahagin.Point : Sahagin.Talk, seconds, x, y, scale, flip);
                Costume(target, x, y, scale, flip, 24, 6, 8, 12, 2, Shades);
                Costume(target, x, y, scale, flip, 24, 7, 2, 10, 3, Beret);
                Costume(target, x, y, scale, flip, 24, 5, 4, 14, 1, Beret);
                break;
            case 2:
                // Goblin Ford: the hood becomes a hat with a brim. He has a bad feeling about this.
                DrawGoblin(target, walk ? Goblin.Excited : Goblin.Talk, seconds, x, y, scale, flip);
                Costume(target, x, y, scale, flip, 24, 2, 4, 20, 1, Brim);
                Costume(target, x, y, scale, flip, 24, 6, 3, 12, 1, Shades);
                break;
            case 3:
                // Pixie Blanchett: a crown, and the bearing to go with it.
                DrawPixie(target, walk ? Pixie.Point : Pixie.Hover, seconds, x, y, scale, flip);
                Costume(target, x, y, scale, flip, 24, 7, 1, 10, 1, Crown);
                Costume(target, x, y, scale, flip, 24, 7, 0, 1, 1, Crown);
                Costume(target, x, y, scale, flip, 24, 11, 0, 2, 1, Crown);
                Costume(target, x, y, scale, flip, 24, 16, 0, 1, 1, Crown);
                break;
            case 4:
                // Mandragora Freeman: a white beard, and a voice you can hear from here.
                DrawMandragora(target, walk ? Mandragora.Wild : Mandragora.Talk, seconds, x, y, scale, flip);
                Costume(target, x, y, scale, flip, 24, 7, 16, 10, 2, Beard);
                Costume(target, x, y, scale, flip, 24, 8, 18, 8, 2, Beard);
                Costume(target, x, y, scale, flip, 24, 10, 20, 4, 1, Beard);
                break;
            default:
                // Loporrit Cruise: aviators. He does his own stunts, on the moon.
                DrawLoporrit(target, walk ? Loporrit.Point : Loporrit.Talk, seconds, x, y, scale, flip);
                Costume(target, x, y, scale, flip, 24, 6, 11, 5, 2, Shades);
                Costume(target, x, y, scale, flip, 24, 13, 11, 5, 2, Shades);
                Costume(target, x, y, scale, flip, 24, 11, 11, 2, 1, Shades);
                break;
        }
    }

    /// <summary>A rectangle of costume in a sprite's own grid: column, row, width and height in sprite pixels, mirrored when the sprite is.</summary>
    private static void Costume(Span<uint> target, int x, int y, int scale, bool flip, int spriteWidth, int col, int row, int w, int h, uint colour)
    {
        var c = flip ? spriteWidth - col - w : col;
        Canvas.Fill(target, x + (c * scale), y + (row * scale), w * scale, h * scale, colour);
    }

    public static void DrawGoblin(Span<uint> target, Goblin action, double seconds, int x, int y, int scale, bool flip = false)
    {
        var (frames, rate) = action switch
        {
            Goblin.Excited => (GoblinExcited, 6.0),
            _ => (GoblinTalk, 5.0),
        };

        var frame = frames[(int)(seconds * rate) % frames.Length];
        Canvas.Sprite(target, frame, c => Palette.GetValueOrDefault(c, 0u), x, y, scale, flip);
    }

    public static void DrawRanger(Span<uint> target, Ranger action, double seconds, int x, int y, int scale, bool flip = false)
    {
        var (frames, rate) = action switch
        {
            Ranger.Stand => (RangerStand, 4.0),
            Ranger.Creep => (RangerCreep, 4.0),
            Ranger.Grab => (RangerGrab, 4.0),
            Ranger.Run => (RangerRun, 8.0),
            _ => (RangerPoint, 1.0),
        };

        var frame = frames[(int)(seconds * rate) % frames.Length];
        Canvas.Sprite(target, frame, c => Palette.GetValueOrDefault(c, 0u), x, y, scale, flip);
    }

}
