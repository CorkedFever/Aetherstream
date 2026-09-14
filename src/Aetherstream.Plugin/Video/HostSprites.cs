namespace Aetherstream.Plugin.Video;

/// <summary>
/// The other shows' hosts, drawn the way the chef is: a ranger in khaki with a bush hat for the
/// wildlife show, and a bard in a chair with a book for the story hour. A few frames each.
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
            ".....pp.........................",
            "....pPpp........................",
            "....pppp........................",
            ".....pp.........................",
            "......k.........................",
            "....hhhhhhhhhhhhh...............",
            "...hhhhhhhhhhhhhhh..............",
            "...hhhhhhhhhhhhhhh..............",
            "...hxxxxxxxxxxxxxh..............",
            "hhhhhhhhhhhhhhhhhhhhh...........",
            "HHHHHHHHHHHHHHHHHHHHHhh.........",
            "..ddddddddddddddddd.............",
            "....kwwwwwwwwwwwwwk.............",
            "...kwwwwwwwwwwwwwwwk............",
            "..kwwwwwwwwwwwwwwwwwk...........",
            "..kwwkeewwwwwkeewwwwk...........",
            "..kwwkeewwwwwkeewwwwk...........",
            "..kwwwwwwwmmwwwwwwwwk...vvv.....",
            "..kwwwwwwmmmmwwwwwwwk..vVVvv....",
            "..kwwwwwwwmmwwwwwwwwkkvVVVvvv...",
            "..kkwwwwoooowwwwwwwkkwvvVVvvv...",
            "...kkwwwwwwwwwwwwwkkwwwvvvvv....",
            "....kkttttttttttttkwwwwwww......",
            "......kttttnntttttkwwwwwwwww....",
            "......kttttnnttttttkkkkkkkkk....",
            ".......kttttttttttttk...........",
            "........kWWtttttttWWk...........",
            ".........kWWWWWWWWWk............",
            "..........kkwwwwwkkwwwww........",
            "...........kwwkkkkwwwwwwww......",
            "...........kWWk....kkkkkkkk.....",
            "...........kkkk.................",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
        [
            ".....pp.........................",
            "....pPpp........................",
            "....pppp........................",
            ".....pp.........................",
            "......k.........................",
            "....hhhhhhhhhhhhh...............",
            "...hhhhhhhhhhhhhhh..............",
            "...hhhhhhhhhhhhhhh..............",
            "...hxxxxxxxxxxxxxh..............",
            "hhhhhhhhhhhhhhhhhhhhh...........",
            "HHHHHHHHHHHHHHHHHHHHHhh.........",
            "..ddddddddddddddddd.............",
            "....kwwwwwwwwwwwwwk.............",
            "...kwwwwwwwwwwwwwwwk............",
            "..kwwwwwwwwwwwwwwwwwk...........",
            "..kwwkeewwwwwkeewwwwk...........",
            "..kwwkeewwwwwkeewwwwk...........",
            "..kwwwwwwwmmwwwwwwwwk...........",
            "..kwwwwwwmmmmwwwwwwwk...vvv.....",
            "..kwwwwwwwmmwwwwwwwwkkvVVvvv....",
            "..kkwwwwoooowwwwwwwkkwvVVVvvvv..",
            "...kkwwwwwwwwwwwwwkkwwwvvVvvvv..",
            "....kkttttttttttttkwwwwwww......",
            "......kttttnntttttkwwwwwwwww....",
            "......kttttnnttttttkkkkkkkkk....",
            ".......kttttttttttttk...........",
            "........kWWtttttttWWk...........",
            ".........kWWWWWWWWWk............",
            "..........kkwwwwwkkwwwww........",
            "...........kwwkkkkwwwwwwww......",
            "...........kWWk....kkkkkkkk.....",
            "...........kkkk.................",
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

    // The bard is a tonberry in a high-backed chair, a book on the lap and a lantern in the
    // other hand: 32 wide, 32 tall.
    private static readonly string[][] BardRead =
    [
        [
            "..cccccccccccccccc..............",
            ".cccccccccccccccccc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbrrrrrrrrbbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbrrkggggggkrrcc.............",
            ".ccbbrrkgyyggyygkcc.............",
            ".ccbbrrkgyeggyegkcc.............",
            ".ccbbrrkgggggggggkc.............",
            ".ccbbrrkggGgggGggkc.............",
            ".ccbbrrrkgggggggkcc.............",
            ".ccbbrrrrkkkkkkkrcc.............",
            ".ccbbrrrrrrrrrrrrcc......lll....",
            ".ccbbrrrrrrrrrrrrrcc.....lLl....",
            ".ccbbrrgrrrrrrrrrgrrrrrrrlLl....",
            ".ccbbrrgrrrrrrrrrgcc.....lLl....",
            ".ccbbkggBBBBBBBggk.......lll....",
            ".ccbbkggBqqqqqBggk..............",
            ".ccbbkggBqqqqqBgkk..............",
            ".ccbbkkkBBBBBBBkk...............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".cccccccccccccccccc.............",
            ".ccddddddddddddddcc.............",
            ".cc..............cc.............",
            ".cc..............cc.............",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
        [
            "..cccccccccccccccc..............",
            ".cccccccccccccccccc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbrrrrrrrrbbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbrrkggggggkrrcc.............",
            ".ccbbrrkgyyggyygkcc.............",
            ".ccbbrrkgeyggeygkcc.............",
            ".ccbbrrkgggggggggkc.............",
            ".ccbbrrkggGgggGggkc.............",
            ".ccbbrrrkgggggggkcc.............",
            ".ccbbrrrrkkkkkkkrcc.............",
            ".ccbbrrrrrrrrrrrrcc......lll....",
            ".ccbbrrrrrrrrrrrrrcc.....lLl....",
            ".ccbbrrgrrrrrrrrrgrrrrrrrlLl....",
            ".ccbbrrgrrrrrrrrrgcc.....lLl....",
            ".ccbbkggBBBBBBBggk.......lll....",
            ".ccbbkggBqqqqqBggk..............",
            ".ccbbkggBqqqqqBgkk..............",
            ".ccbbkkkBBBBBBBkk...............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".cccccccccccccccccc.............",
            ".ccddddddddddddddcc.............",
            ".cc..............cc.............",
            ".cc..............cc.............",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
    ];

    private static readonly string[][] BardTurn =
    [
        [
            "..cccccccccccccccc..............",
            ".cccccccccccccccccc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbrrrrrrrrbbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbrrkggggggkrrcc.............",
            ".ccbbrrkgyyggyygkcc.............",
            ".ccbbrrkgyeggyegkcc.............",
            ".ccbbrrkgggggggggkc.............",
            ".ccbbrrkggGgggGggkc.............",
            ".ccbbrrrkgggggggkcc.............",
            ".ccbbrrrrkkkkkkkrcc.............",
            ".ccbbrrrrrrrrrrrrcc......lll....",
            ".ccbbrrrrrrrrrrrrrcc.....lLl....",
            ".ccbbrrgrrrrrrrrrgrrrrrrrlLl....",
            ".ccbbrrgrrrrrrrrrgcc.....lLl....",
            ".ccbbkggBBBBBBBggk...gg..lll....",
            ".ccbbkggBqqqqqBgggggg...........",
            ".ccbbkggBqqqqqqq................",
            ".ccbbkkkBBBBBBBkk...............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".cccccccccccccccccc.............",
            ".ccddddddddddddddcc.............",
            ".cc..............cc.............",
            ".cc..............cc.............",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
    ];

    private static readonly string[][] BardGesture =
    [
        [
            "..cccccccccccccccc..............",
            ".cccccccccccccccccc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbrrrrrrrrbbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbrrkggggggkrrcc.............",
            ".ccbbrrkgyyggyygkcc.............",
            ".ccbbrrkgyeggyegkcc.............",
            ".ccbbrrkgggggggggkc.............",
            ".ccbbrrkggGgggGggkc.............",
            ".ccbbrrrkgggggggkcc.............",
            ".ccbbrrrrkkkkkkkrcc.............",
            ".ccbbrrrrrrrrrrrrcc..lll........",
            ".ccbbrrrrrrrrrrrrrcc.lLl........",
            ".ccbbrrgrrrrrrrrrgrrrlLl........",
            ".ccbbrrgrrrrrrrrrgcc.lll........",
            ".ccbbkggBBBBBBBggk..............",
            ".ccbbkggBqqqqqBggk..............",
            ".ccbbkggBqqqqqBgkk..............",
            ".ccbbkkkBBBBBBBkk...............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".cccccccccccccccccc.............",
            ".ccddddddddddddddcc.............",
            ".cc..............cc.............",
            ".cc..............cc.............",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
        [
            "..cccccccccccccccc..............",
            ".cccccccccccccccccc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbbbbbbbbbbbcc.............",
            ".ccbbbbrrrrrrrrbbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbbrrrrrrrrrrbcc.............",
            ".ccbbrrkggggggkrrcc.............",
            ".ccbbrrkgyyggyygkcc.............",
            ".ccbbrrkgyeggyegkcc.............",
            ".ccbbrrkgggggggggkc.............",
            ".ccbbrrkggGgggGggkc.............",
            ".ccbbrrrkgggggggkcc.............",
            ".ccbbrrrrkkkkkkkrcc.............",
            ".ccbbrrrrrrrrrrrrcc....lll......",
            ".ccbbrrrrrrrrrrrrrcc...lLl......",
            ".ccbbrrgrrrrrrrrrgrrrrrlLl......",
            ".ccbbrrgrrrrrrrrrgcc...lll......",
            ".ccbbkggBBBBBBBggk..............",
            ".ccbbkggBqqqqqBggk..............",
            ".ccbbkggBqqqqqBgkk..............",
            ".ccbbkkkBBBBBBBkk...............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".ccbbRRRRRRRRRRRcc..............",
            ".cccccccccccccccccc.............",
            ".ccddddddddddddddcc.............",
            ".cc..............cc.............",
            ".cc..............cc.............",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
    ];








    public enum Hero
    {
        Stand,
        Walk,
    }

    // The story's hero: a head for each of the eight peoples over a shared body — a cloak, a
    // tunic and belt, a pack, boots — with a walking staff added when the figure is composed.
    // Heads are nineteen rows; bodies twenty-seven, from the cloak's shoulders down.
    private static readonly Dictionary<string, string[]> HeroHeads = new()
    {
        ["Hyur"] =
        [
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "..........rrrrrrrrrr............",
            ".........rrrrrrrrrrrr...........",
            ".........krrrrrrrrrrk...........",
            ".........krsssssssssk...........",
            ".........ksseesssseesk..........",
            ".........kssssSsssssk...........",
            ".........ksssmmmmsssk...........",
            "..........kssssssssSk...........",
            "...........kkssssskk............",
            ".............kssssk.............",
            "..............kssk..............",
            "................................",
            "................................",
        ],
        ["Elezen"] =
        [
            "................................",
            "................................",
            "................................",
            "................................",
            "..........rrrrrrrrrr............",
            ".........rrrrrrrrrrrr...........",
            ".........krrrrrrrrrrk...........",
            ".........krrrrrrrrrrk...........",
            "......ss.kssssssssssk.ss........",
            ".....ssskssseesssseesksss.......",
            "......sskssssSsssssskss.........",
            ".........ksssssssssk............",
            ".........ksssmmmmssk............",
            ".........kssssssssSk............",
            "..........kssssssSk.............",
            "...........kkssskk..............",
            ".............kssk...............",
            "................................",
            "................................",
        ],
        ["Lalafell"] =
        [
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "..........rrrrrrrrrr............",
            ".........rrrrrrrrrrrr...........",
            ".........krrrrrrrrrrk...........",
            ".........krsssssssssk...........",
            ".........ksseesssseesk..........",
            ".........kssssSsssssk...........",
            ".........ksssmmmmsssk...........",
            "..........kssssssssSk...........",
            "...........kkssssskk............",
            ".............kssssk.............",
            "..............kssk..............",
            "................................",
            "................................",
        ],
        ["Miqo'te"] =
        [
            "................................",
            "................................",
            "........rr........rr............",
            "........rrr......rrr............",
            "........rsrr....rrsr............",
            "........rssrr..rrssr............",
            ".........rrrrrrrrrrr............",
            ".........rrrrrrrrrrrr...........",
            ".........krrrrrrrrrrk...........",
            ".........krsssssssssk...........",
            ".........ksseesssseesk..........",
            ".........kssssSsssssk...........",
            ".........kssSmmmmSssk...........",
            "..........kssssssssSk...........",
            "...........kkssssskk............",
            ".............kssssk.............",
            "..............kssk..............",
            "................................",
            "................................",
        ],
        ["Roegadyn"] =
        [
            "................................",
            "................................",
            "................................",
            "................................",
            ".........rrrrrrrrrrrrr..........",
            "........rrrrrrrrrrrrrrr.........",
            "........krrrrrrrrrrrrrk.........",
            "........krsssssssssssrk.........",
            "........kssseessssseessk........",
            "........ksssssSSSssssssk........",
            "........kssssssssssssssk........",
            "........kSsssmmmmmmsssSk........",
            "........kSssssssssssssSk........",
            ".........kkSSssssssSSkk.........",
            "...........kksssssskk...........",
            ".............kssssk.............",
            "..............kssk..............",
            "................................",
            "................................",
        ],
        ["Au Ra"] =
        [
            "................................",
            "................................",
            "................................",
            "......HH............HH..........",
            "......HHH..........HHH..........",
            ".......HHH........HHH...........",
            "........HHrrrrrrrrHH............",
            ".........rrrrrrrrrrrr...........",
            ".........krrrrrrrrrrk...........",
            ".........krsssssssssk...........",
            ".........kSseesssseeSk..........",
            ".........kSsssSsssssSk..........",
            ".........kSssmmmmsssSk..........",
            "..........kssssssssSk...........",
            "...........kkssssskk............",
            ".............kssssk.............",
            "..............kssk..............",
            "................................",
            "................................",
        ],
        ["Hrothgar"] =
        [
            "................................",
            "................................",
            "................................",
            "........rr..........rr..........",
            ".......rrrr........rrrr.........",
            ".......rrrrrrrrrrrrrrrr.........",
            ".......rrrrrrrrrrrrrrrr.........",
            "......rrrkssssssssssskrrr.......",
            "......rrrksseessssseeskrrr......",
            "......rrrkssssssssssssskrrr.....",
            "......rrrksssSSSSSSssskrrr......",
            "......rrrkssSSkkkkSSsskrrr......",
            ".......rrkssSSSSSSSSsskrr.......",
            ".......rrkSssmmmmmmssSkrr.......",
            "........rkkSSSSSSSSSSkkr........",
            ".........rkkssssssssskr.........",
            "...........kkssssssskk..........",
            ".............kssssk.............",
            "..............kssk..............",
        ],
        ["Viera"] =
        [
            ".........rr.......rr............",
            ".........rrr.....rrr............",
            ".........rsr.....rsr............",
            ".........rsrr...rrsr............",
            ".........rssr...rssr............",
            ".........rssr...rssr............",
            "..........rrrrrrrrr.............",
            ".........rrrrrrrrrrr............",
            ".........krrrrrrrrrrk...........",
            ".........krsssssssssk...........",
            ".........ksseesssseesk..........",
            ".........kssssSsssssk...........",
            ".........ksssmmmmsssk...........",
            "..........kssssssssSk...........",
            "...........kkssssskk............",
            ".............kssssk.............",
            "..............kssk..............",
            "................................",
            "................................",
        ],
    };

    private static readonly string[][] HeroBodyStand =
    [
        [
            "........ccccttttttcccc..........",
            ".......cccctttttttccccc.........",
            "......ccccttttnttttcccc.........",
            "......cccctttttttttccccq........",
            "......ccccttttaaatttccqQq.......",
            "......CcccttttttttttccqQq.......",
            "......CCcctttttttttccCqqq.......",
            "......CCCcttttttttccCCkkk.......",
            ".......CCCkttttttkCCC...........",
            ".........kkaaaaaakkk............",
            "..........kllllllk..............",
            "..........kllllllk..............",
            "..........kllkkllk..............",
            "..........klk..klk..............",
            "..........klk..klk..............",
            "..........kbk..kbk..............",
            "..........kbbk.kbbk.............",
            "..........kBBk.kBBk.............",
            "..........kkkk.kkkk.............",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
        [
            "........ccccttttttcccc..........",
            ".......cccctttttttccccc.........",
            "......ccccttttnttttcccc.........",
            "......cccctttttttttccccq........",
            "......ccccttttaaatttccqQq.......",
            "......CcccttttttttttccqQq.......",
            "......CCcctttttttttccCqqq.......",
            "......CCCcttttttttccCCkkk.......",
            ".......CCCkttttttkCCC...........",
            ".........kkaaaaaakkk............",
            "..........kllllllk..............",
            "..........kllllllk..............",
            "..........kllkkllk..............",
            "..........klk..klk..............",
            "..........klk..klk..............",
            "..........kbk..kbk..............",
            "..........kbbk.kbbk.............",
            "..........kBBk.kBBk.............",
            "..........kkkk.kkkk.............",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
    ];

    private static readonly string[][] HeroBodyWalk =
    [
        [
            "........ccccttttttcccc..........",
            ".......cccctttttttccccc.........",
            "......ccccttttnttttcccc.........",
            "......cccctttttttttccccq........",
            "......ccccttttaaatttccqQq.......",
            "......CcccttttttttttccqQq.......",
            "......CCcctttttttttccCqqq.......",
            "......CCCcttttttttccCCkkk.......",
            ".......CCCkttttttkCCC...........",
            ".........kkaaaaaakkk............",
            ".........klllllllllk............",
            "........klllllkllllk............",
            ".......klllk...kllllk...........",
            "......klk........klk............",
            ".....klk..........klk...........",
            "....kbk............kbk..........",
            "...kbbk.............kbbk........",
            "...kBBk.............kBBk........",
            "...kkkk.............kkkk........",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
        [
            "........ccccttttttcccc..........",
            ".......cccctttttttccccc.........",
            "......ccccttttnttttcccc.........",
            "......cccctttttttttccccq........",
            "......ccccttttaaatttccqQq.......",
            "......CcccttttttttttccqQq.......",
            "......CCcctttttttttccCqqq.......",
            "......CCCcttttttttccCCkkk.......",
            ".......CCCkttttttkCCC...........",
            ".........kkaaaaaakkk............",
            "..........kllllllk..............",
            "..........kllllllk..............",
            "..........kllkkllk..............",
            "..........klk..klk..............",
            "..........klk..klk..............",
            "..........kbk..kbk..............",
            "..........kbbk.kbbk.............",
            "..........kBBk.kBBk.............",
            "..........kkkk.kkkk.............",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
        ],
    ];

    private static readonly Dictionary<(string, Hero, int), string[]> HeroFrames = [];

    /// <summary>How big each people draws, relative to a Hyur.</summary>
    public static float HeroScale(string race) => race switch
    {
        "Lalafell" => 0.6f,
        "Roegadyn" or "Hrothgar" => 1.2f,
        "Elezen" or "Viera" => 1.08f,
        _ => 1f,
    };

    private static string[] ComposeHero(string race, Hero action, int frame)
    {
        var key = (race, action, frame);
        if (HeroFrames.TryGetValue(key, out var done))
            return done;

        var head = HeroHeads.GetValueOrDefault(race, HeroHeads["Hyur"]);
        var body = (action == Hero.Walk ? HeroBodyWalk : HeroBodyStand)[frame];
        var art = new char[head.Length + body.Length][];
        for (var i = 0; i < head.Length; i++)
            art[i] = head[i].ToCharArray();
        for (var i = 0; i < body.Length; i++)
            art[head.Length + i] = body[i].ToCharArray();

        // The staff: down the left, a knob at the top, the hand on it at the shoulder.
        var top = head.Length - 9;
        for (var r = top; r < head.Length + 19; r++)
            art[r][5] = 'f';
        for (var r = top - 1; r <= top + 1; r++)
        {
            art[r][4] = 'F';
            art[r][5] = 'F';
            art[r][6] = 'F';
        }

        for (var r = head.Length + 3; r <= head.Length + 5; r++)
        {
            art[r][4] = 's';
            art[r][6] = 's';
        }

        // Tails, for those that have them.
        if (race == "Miqo'te")
        {
            foreach (var (r, c) in new[] { (9, 24), (10, 25), (11, 26), (12, 26), (13, 26), (14, 25), (15, 24), (15, 23) })
                art[head.Length + r][c] = 'r';
        }
        else if (race == "Au Ra")
        {
            foreach (var (r, c) in new[] { (9, 24), (9, 25), (10, 25), (10, 26), (11, 26), (11, 27), (12, 27), (13, 27), (14, 26), (15, 25), (16, 24) })
                art[head.Length + r][c] = 'a';
        }

        var result = art.Select(r => new string(r)).ToArray();
        HeroFrames[key] = result;
        return result;
    }

    private static readonly Dictionary<char, uint> HeroPalette = new()
    {
        ['k'] = Canvas.Rgb(0x1A, 0x14, 0x10),
        ['e'] = Canvas.Rgb(0x10, 0x10, 0x10),
        ['m'] = Canvas.Rgb(0xC0, 0x50, 0x50),
        ['t'] = Canvas.Rgb(0xB8, 0xA0, 0x78),
        ['n'] = Canvas.Rgb(0x8A, 0x74, 0x50),
        ['a'] = Canvas.Rgb(0x4A, 0x30, 0x1A),
        ['l'] = Canvas.Rgb(0x5A, 0x4A, 0x3A),
        ['b'] = Canvas.Rgb(0x5A, 0x3C, 0x22),
        ['B'] = Canvas.Rgb(0x2A, 0x1E, 0x14),
        ['f'] = Canvas.Rgb(0x8A, 0x5A, 0x2A),
        ['F'] = Canvas.Rgb(0xE0, 0xB8, 0x4A),
        ['q'] = Canvas.Rgb(0x7A, 0x52, 0x2A),
        ['Q'] = Canvas.Rgb(0x9A, 0x6A, 0x3A),
        ['H'] = Canvas.Rgb(0x3A, 0x30, 0x2A),
    };

    /// <summary>The height of the composed figure in cells, for standing it on a ground line.</summary>
    public const int HeroRows = 46;

    /// <summary>The hero standing or walking, with the tale's people, hair, cloak and skin.</summary>
    public static void DrawHero(Span<uint> target, string race, Hero action, double seconds, int x, int y, int scale, uint hair, uint cloak, uint skin)
    {
        var frames = action == Hero.Walk ? HeroBodyWalk : HeroBodyStand;
        var rate = action == Hero.Walk ? 5.0 : 1.2;
        var frame = ComposeHero(race, action, (int)(seconds * rate) % frames.Length);
        var cloakShade = Canvas.Lerp(cloak, Canvas.Black, 0.3f);
        var skinShade = Canvas.Lerp(skin, Canvas.Black, 0.18f);
        Canvas.Sprite(target, frame, c => c switch
        {
            'r' => hair,
            'c' => cloak,
            'C' => cloakShade,
            's' => skin,
            'S' => skinShade,
            _ => HeroPalette.GetValueOrDefault(c, 0u),
        }, x, y, scale);
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

    public static void DrawBard(Span<uint> target, Bard action, double seconds, int x, int y, int scale)
    {
        var (frames, rate) = action switch
        {
            Bard.Read => (BardRead, 0.7),
            Bard.Turn => (BardTurn, 1.0),
            _ => (BardGesture, 1.6),
        };

        var frame = frames[(int)(seconds * rate) % frames.Length];
        Canvas.Sprite(target, frame, c => Palette.GetValueOrDefault(c, 0u), x, y, scale);
    }
}
