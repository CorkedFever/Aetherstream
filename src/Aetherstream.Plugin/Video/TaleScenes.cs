namespace Aetherstream.Plugin.Video;

/// <summary>What a page shows. Each is a picture of that page's words, not a generic pose.</summary>
internal enum Scene
{
    // Halone
    MountainClimb, SpearInRock, DragonComes, DragonFight, PeopleBuild, SpiresLeft,
    WoodEdge, MatronMeets, GardenTalk, StrikeTree, SpearUp, ColdGarden,
    // Menphina
    DarkNight, OschonOverRidge, MakeMoon, MoonlitRoads, WolfHowl, FollowWanderer,
    WolfAlone, ApproachWolf, WolfLunge, HandOut, HeadUnderHand, WalkTogether,
    // Thaliak
    DryLand, AskStones, CutRiver, TeachWrite, PagesFloat, Upstream,
    Shore, NavigatorMeets, ShipsTalk, PageOnWater, TakesChart, ChartDepart,
    // Nymeia
    KeeperWaits, KeeperMeets, SpinThread, ThreadThroughGlass, Weave, LoomMoves,
    KnotOnLoom, MarshTown, TurnKnot, BoatChapel, ThreeThreads, KnotDepart,
    // Llymlaen
    SeaAlone, Fisherfolk, SplitTree, FirstShip, GiveShip, Harbour,
    FleetOut, Headland, Storm, RaiseArm, FleetHome, SunsetShore,
    // Oschon
    Between, WindingRoads, LooseArrow, Traveller, Lake, OverRidge,
    RidgeStop, LightBehind, LoverMeets, LoverTalk, ArrowIntoDark, WalkSlow,
    // Byregot
    FatherBreaks, BareHill, StrikeStone, WallRises, House, WallDepart,
    Rubble, DestroyerMeets, RubbleTalk, PickHammer, NewCity, TurnsDepart,
    // Rhalgr
    Palace, HillComet, RaiseComet, ThrowComet, GreenField, CometDepart,
    Monks, Stone, Waiting, StoneBreaks, Nod, StoneDepart,
    // Azeyma
    SunInHands, MarketDark, ThrowSun, SunHigh, Noon, BehindSun,
    Gate, TradersMeet, SunOverMarket, ScalesLevel, Ledger, DawnDepart,
    // Nald'thal
    TwinsDesert, ArgueScales, Deal, CounterAndTomb, Handshake, IntoCity,
    Sultan, WeighNothing, DebtsHeavy, WaterSeller, Exact, SettleDepart,
    // Nophica
    BareLand, Kneel, SpringTree, FuryAsks, Bread, SickleDepart,
    BrownWood, Shrine, Dig, WaterFound, GreenBack, DigDepart,
    // Althyk
    NoNow, MakeGlass, TurnGlass, Kept, SpinnerComes, YearEnd,
    YoungMan, KeeperLooks, TipGlass, Mirror, LowerChamber, Children,
    // The ogre
    SwampSign, Doorstep, ShortLord, TowerDragon, RoadHome, Wedding,
}

/// <summary>
/// The pictures for the tonberry's tales: every page of every tale drawn to its words. Cut-paper
/// country, cutout mortals and props, and the god in colour doing the thing the page says.
/// </summary>
internal static class TaleScenes
{
    public const int W = Canvas.Width;
    public const int Ground = 470;

    /// <summary>What a scene needs from the plate: colours, time, and how to draw the gods.</summary>
    public sealed class Context
    {
        public required uint Paper;
        public required uint Ink;
        public required double Seconds;
        public required double Into;
        public required string Element;
        public required Action<int, int, bool, float> God;
        public required Action<int, int, bool, float> Other;
        public required int GodWidth;
        public required int GodHeight;
        public required int OtherWidth;
        public required int OtherHeight;
    }

    private static readonly uint Ice = Canvas.Rgb(0x9A, 0xD8, 0xFF);
    private static readonly uint Water = Canvas.Rgb(0x4A, 0x8A, 0xE0);
    private static readonly uint Leaf = Canvas.Rgb(0x6A, 0xB0, 0x5A);
    private static readonly uint Spark = Canvas.Rgb(0xC0, 0x8A, 0xFF);
    private static readonly uint Ember = Canvas.Rgb(0xFF, 0x8A, 0x2E);
    private static readonly uint Dust = Canvas.Rgb(0x9A, 0x6A, 0x3A);
    private static readonly uint SunGold = Canvas.Rgb(0xFF, 0xD1, 0x5C);
    private static readonly uint MoonWhite = Canvas.Rgb(0xF8, 0xF8, 0xFF);
    private static readonly uint Green = Canvas.Rgb(0x5A, 0xA0, 0x4A);

    public static void Draw(Span<uint> span, Scene scene, Context c)
    {
        var t = c.Into;
        var s = c.Seconds;
        var ink = c.Ink;
        var ease = 1.0 - Math.Pow(1.0 - Math.Clamp(t / 2.5, 0.0, 1.0), 3.0);
        var stride = t < 2.5 ? (int)(Math.Abs(Math.Sin(t * 9.0)) * -10) : (int)(Math.Sin(s * 2.0) * 4);
        var godX = 760;
        var godY = Ground - c.GodHeight;

        switch (scene)
        {
            // -- Halone ------------------------------------------------------------------------------------------------
            case Scene.MountainClimb:
                Mountain(span, ink, 900, 120);
                c.God(200 + (int)(ease * 560), Ground - c.GodHeight - (int)(ease * 180) + stride, false, 0.6f);
                break;
            case Scene.SpearInRock:
                Mountain(span, ink, 900, 120);
                Spear(span, ink, 900, 130, 180);
                Snow(span, s, Math.Clamp(t / 4.0, 0.0, 1.0));
                c.God(godX - 200, Ground - c.GodHeight - 160, false, 0.9f);
                break;
            case Scene.DragonComes:
                Mountain(span, ink, 900, 120);
                Spear(span, ink, 900, 130, 180);
                Snow(span, s, 0.4);
                Dragon(span, ink, 1400 - (int)(ease * 520), 90, s);
                c.God(godX - 260, Ground - c.GodHeight - 160, false, 0.9f);
                break;
            case Scene.DragonFight:
            {
                Mountain(span, ink, 900, 120);
                Snow(span, s, 1.0);
                var clash = (int)(Math.Sin(s * 6.0) * 30);
                Dragon(span, ink, 880 + clash, 90 - Math.Abs(clash / 2), s);
                if ((int)(s * 4) % 11 == 0)
                    Flash(span, c.Paper);
                c.God(godX - 260 - clash, Ground - c.GodHeight - 160 - Math.Abs(clash), false, 1.2f);
                Burst(span, Ice, 800, 250, s, 30);
                break;
            }

            case Scene.PeopleBuild:
                Mountain(span, ink, 900, 120);
                Spear(span, ink, 900, 130, 180);
                Snow(span, s, 0.3);
                for (var i = 0; i < 3; i++)
                    Housework(span, ink, 200 + (i * 140), Ground, Math.Clamp((t - (i * 1.5)) / 3.0, 0.0, 1.0));
                Mortal(span, ink, 560, Ground, 0.5f, false);
                c.God(godX - 200, Ground - c.GodHeight - 160, false, 1.0f);
                break;
            case Scene.SpiresLeft:
                Mountain(span, ink, 900, 120);
                Spear(span, ink, 900, 130, 180);
                for (var i = 0; i < 5; i++)
                    Spire(span, ink, 160 + (i * 90), Ground, 120 + ((i * 37) % 80));
                c.God(godX - 200, Ground - c.GodHeight - 160 - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.4f);
                break;
            case Scene.WoodEdge:
                Trees(span, ink, 60, 5, s);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.7f);
                break;
            case Scene.MatronMeets:
                Trees(span, ink, 60, 5, s);
                c.God(godX, godY + stride, false, 0.8f);
                c.Other(-c.OtherWidth - 40 + (int)((300 + c.OtherWidth + 40) * ease), Ground - c.OtherHeight + stride, true, 0.8f);
                break;
            case Scene.GardenTalk:
                Trees(span, ink, 60, 5, s);
                Garden(span, ink, 420, Ground);
                c.God(godX, godY + stride, false, 0.8f);
                c.Other(300, Ground - c.OtherHeight + stride, true, 0.8f);
                break;
            case Scene.StrikeTree:
            {
                Trees(span, ink, 60, 5, s);
                var shake = t is > 2.0 and < 3.5 ? (int)(Math.Sin(s * 50.0) * 8) : 0;
                BigTree(span, ink, 560 + shake, Ground);
                if (t > 3.0)
                    Eyes(span, 560, 180, Math.Clamp((t - 3.0) / 2.0, 0.0, 1.0), c.Paper);
                var lunge = t is > 1.5 and < 2.5 ? (int)(Math.Sin((t - 1.5) * Math.PI) * -120) : 0;
                c.God(godX + lunge, godY + stride, false, 1.0f);
                c.Other(200, Ground - c.OtherHeight, true, 0.8f);
                break;
            }

            case Scene.SpearUp:
                Trees(span, ink, 60, 5, s);
                BigTree(span, ink, 560, Ground);
                Eyes(span, 560, 180, 1.0 - Math.Clamp(t / 4.0, 0.0, 1.0), c.Paper);
                c.God(godX, godY - 20 + stride, false, 1.3f);
                c.Other(300, Ground - c.OtherHeight + stride, true, 1.3f);
                break;
            case Scene.ColdGarden:
                Spire(span, ink, 200, Ground, 260);
                Spire(span, ink, 280, Ground, 200);
                Garden(span, ink, 420, Ground);
                Snow(span, s, 0.5);
                c.God(godX + 200, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;

            // -- Menphina ------------------------------------------------------------------------------------------------
            case Scene.DarkNight:
                Dark(span, 0.5f);
                Ridge(span, ink, 200);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.5f);
                break;
            case Scene.OschonOverRidge:
                Dark(span, 0.5f);
                Ridge(span, ink, 200);
                c.Other(300 + (int)(t * 60), Ground - c.OtherHeight - (int)(Math.Min(t, 5.0) * 30) + stride, false, 0.3f);
                c.God(godX, godY + stride, false, 0.5f);
                break;
            case Scene.MakeMoon:
            {
                Dark(span, 0.5f - (float)(Math.Clamp(t / 8.0, 0.0, 1.0) * 0.3));
                Ridge(span, ink, 200);
                var r = (int)(Math.Clamp(t / 3.0, 0.0, 1.0) * 60);
                var my = t < 3.0 ? godY + 120 : godY + 120 - (int)((t - 3.0) / 5.0 * 300);
                Canvas.Disc(span, godX + (c.GodWidth / 2), my, r + 20, Canvas.Lerp(MoonWhite, c.Paper, 0.6f));
                Canvas.Disc(span, godX + (c.GodWidth / 2), my, r, MoonWhite);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            }

            case Scene.MoonlitRoads:
                Dark(span, 0.25f);
                Ridge(span, ink, 200);
                Canvas.Disc(span, 640, 120, 60, MoonWhite);
                Roads(span, c.Paper, s);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 160 + (i * 150) + (int)(t * 12), Ground, 0.5f, i == 1);
                c.God(godX + 120, godY + stride, false, 1.2f);
                break;
            case Scene.WolfHowl:
                Dark(span, 0.25f);
                Ridge(span, ink, 200);
                Canvas.Disc(span, 640, 120, 60, MoonWhite);
                Wolf(span, ink, 340, Ground, howl: true, s);
                c.God(godX + 120, godY + stride, false, 1.0f);
                break;
            case Scene.FollowWanderer:
                Dark(span, 0.25f);
                Ridge(span, ink, 200);
                Canvas.Disc(span, 500 + (int)(t * 40), 120, 60, MoonWhite);
                c.Other(200 + (int)(t * 90), Ground - c.OtherHeight - 80 + stride, false, 0.3f);
                c.God(godX - 500 + (int)(t * 70), godY + stride, false, 0.8f);
                Wolf(span, ink, godX - 620 + (int)(t * 70), Ground, howl: false, s);
                break;
            case Scene.WolfAlone:
                Snow(span, s, 0.5);
                Wolf(span, ink, 560, Ground, howl: false, s);
                break;
            case Scene.ApproachWolf:
                Snow(span, s, 0.5);
                Wolf(span, ink, 420 + (int)(Math.Sin(s * 0.8) * 40), Ground, howl: false, s);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.6f);
                break;
            case Scene.WolfLunge:
            {
                Snow(span, s, 0.5);
                var lunge = (int)(Math.Max(0, Math.Sin(t * 2.4)) * 220);
                Wolf(span, ink, 380 + lunge, Ground, howl: false, s);
                c.God(godX, godY, false, 0.8f);
                break;
            }

            case Scene.HandOut:
                Snow(span, s, 0.3);
                Wolf(span, ink, 520, Ground, howl: false, s);
                c.God(godX, godY, false, 1.0f);
                Canvas.Disc(span, godX - 20, godY + (c.GodHeight / 2), 10 + (int)(Math.Sin(s * 3.0) * 3), Canvas.Lerp(MoonWhite, c.Paper, 0.3f));
                break;
            case Scene.HeadUnderHand:
                Snow(span, s, 0.2);
                Wolf(span, ink, godX - 200, Ground, howl: false, s);
                c.God(godX, godY, false, 1.2f);
                break;
            case Scene.WalkTogether:
                Snow(span, s, 0.2);
                Wolf(span, ink, godX - 200 + (int)(t * 60), Ground, howl: false, s);
                c.God(godX + (int)(t * 60), godY + stride, false, 1.0f);
                break;

            // -- Thaliak ------------------------------------------------------------------------------------------------
            case Scene.DryLand:
                Stones(span, ink, s, 0);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.7f);
                break;
            case Scene.AskStones:
                Stones(span, ink, s, 0);
                WindLines(span, ink, s);
                c.God(godX - 200, godY + stride, false, 0.8f);
                break;
            case Scene.CutRiver:
                Stones(span, ink, s, 0);
                River(span, s, Math.Clamp((t - 1.0) / 3.0, 0.0, 1.0));
                c.God(godX - 200, godY + (t is > 0.5 and < 1.5 ? -20 : stride), false, 1.0f);
                break;
            case Scene.TeachWrite:
                River(span, s, 1.0);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 160 + (i * 130), Ground, 0.5f, false);
                Pages(span, c.Paper, ink, 180, Ground - 130, 3, s);
                c.God(godX - 200, godY + stride, false, 1.0f);
                break;
            case Scene.PagesFloat:
                River(span, s, 1.0);
                FloatingPages(span, c.Paper, ink, s);
                Library(span, ink, 200, Ground);
                c.God(godX - 200, godY + stride, false, 1.0f);
                break;
            case Scene.Upstream:
                River(span, s, 1.0);
                Library(span, ink, 200, Ground);
                c.God(godX - 200 + (int)(t * 70), godY - (int)(t * 25) + stride, false, 1.2f);
                break;
            case Scene.Shore:
                Sea(span, s, 0.0);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.7f);
                break;
            case Scene.NavigatorMeets:
                Sea(span, s, 0.0);
                c.God(godX, godY + stride, false, 0.8f);
                c.Other(-c.OtherWidth - 40 + (int)((300 + c.OtherWidth + 40) * ease), Ground - c.OtherHeight + stride, true, 0.8f);
                break;
            case Scene.ShipsTalk:
                Sea(span, s, 0.0);
                Ship(span, ink, 500, 250, 0.5f, s);
                Ship(span, ink, 620, 270, 0.4f, s);
                c.God(godX, godY + stride, false, 0.8f);
                c.Other(300, Ground - c.OtherHeight + stride, true, 0.8f);
                break;
            case Scene.PageOnWater:
                Sea(span, s, 0.0);
                Chart(span, c.Paper, ink, 480, 300, Math.Clamp(t / 6.0, 0.0, 1.0), s);
                c.God(godX, godY + stride, false, 1.0f);
                c.Other(300, Ground - c.OtherHeight + stride, true, 0.8f);
                break;
            case Scene.TakesChart:
                Sea(span, s, 0.0);
                Chart(span, c.Paper, ink, 420, 250, 1.0, s);
                c.God(godX, godY + stride, false, 1.2f);
                c.Other(300, Ground - c.OtherHeight + stride, true, 1.2f);
                break;
            case Scene.ChartDepart:
                Sea(span, s, 0.0);
                Ship(span, ink, 300 + (int)(t * 60), 240, 0.6f, s);
                c.God(godX + (int)(t * 60), godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700) + stride, false, 1.2f);
                break;

            // -- Nymeia ------------------------------------------------------------------------------------------------
            case Scene.KeeperWaits:
                Dark(span, 0.3f);
                Hourglass(span, ink, 300, Ground - 100, 90, s, 0.0);
                c.Other(200, Ground - c.OtherHeight - 200, true, 0.5f);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.6f);
                break;
            case Scene.KeeperMeets:
                Dark(span, 0.3f);
                Hourglass(span, ink, 500, Ground - 100, 90, s, 0.0);
                c.Other(200, Ground - c.OtherHeight + stride, true, 0.7f);
                c.God(godX, godY + stride, false, 0.7f);
                break;
            case Scene.SpinThread:
                Dark(span, 0.3f);
                Threads(span, Water, godX + (c.GodWidth / 2), godY + 60, 1, s, Math.Clamp(t / 4.0, 0.0, 1.0));
                if (t > 4.0)
                    Mortal(span, ink, 400, Ground, 0.4f + (float)Math.Clamp((t - 4.0) / 3.0, 0.0, 0.3), false);
                c.God(godX, godY + stride, false, 1.0f);
                break;
            case Scene.ThreadThroughGlass:
                Dark(span, 0.3f);
                Hourglass(span, ink, 340, Ground - 100, 90, s, 0.3);
                Threads(span, Water, godX + (c.GodWidth / 2), godY + 60, 1, s, 1.0);
                c.Other(180, Ground - c.OtherHeight + stride, true, 0.9f);
                c.God(godX, godY + stride, false, 0.9f);
                break;
            case Scene.Weave:
                Dark(span, 0.3f);
                Threads(span, Water, godX + (c.GodWidth / 2), godY + 60, 8, s, 1.0);
                for (var i = 0; i < 4; i++)
                    Mortal(span, ink, 140 + (i * 120), Ground, 0.45f + (0.1f * (i % 2)), i % 2 == 0);
                c.God(godX, godY + stride, false, 1.0f);
                break;
            case Scene.LoomMoves:
                Dark(span, 0.3f);
                Threads(span, Water, godX + (c.GodWidth / 2), godY + 60 - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), 8, s, 1.0);
                c.God(godX, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.3f);
                break;
            case Scene.KnotOnLoom:
                Dark(span, 0.2f);
                Loom(span, Water, ink, s, knot: true, 0.0);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.6f);
                break;
            case Scene.MarshTown:
                Houses(span, ink, 140, Ground, 3);
                Mortal(span, ink, 240, Ground, 0.5f, true);
                Thief(span, ink, 380, Ground);
                Priest(span, ink, 500, Ground);
                c.God(godX, godY + stride, false, 0.7f);
                break;
            case Scene.TurnKnot:
                Houses(span, ink, 140, Ground, 3);
                Knot(span, Water, ink, godX - 60, godY + (c.GodHeight / 2), s);
                c.God(godX, godY + stride, false, 0.9f);
                break;
            case Scene.BoatChapel:
                Sea(span, s, 0.0);
                Ship(span, ink, 360, 300, 0.4f, s);
                Chapel(span, ink, 200, Ground);
                Thief(span, ink, 420, Ground);
                Priest(span, ink, 260, Ground);
                Mortal(span, ink, 520, Ground, 0.5f, true);
                c.God(godX, godY + stride, false, 0.9f);
                break;
            case Scene.ThreeThreads:
                Dark(span, 0.2f);
                Loom(span, Water, ink, s, knot: true, Math.Clamp(t / 5.0, 0.0, 1.0));
                c.God(godX, godY + stride, false, 1.2f);
                break;
            case Scene.KnotDepart:
                Dark(span, 0.2f);
                Loom(span, Water, ink, s, knot: true, 1.0);
                c.God(godX, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;

            // -- Llymlaen ------------------------------------------------------------------------------------------------
            case Scene.SeaAlone:
                Sea(span, s, 0.0);
                c.God(godX - 200, Ground - c.GodHeight - 40 + (int)(Math.Sin(s * 1.5) * 8), false, 0.7f);
                break;
            case Scene.Fisherfolk:
                Sea(span, s, 0.0);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 160 + (i * 120), Ground, 0.5f, true);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.SplitTree:
            {
                Sea(span, s, 0.0);
                var split = Math.Clamp((t - 2.0) / 1.5, 0.0, 1.0);
                FallenTree(span, ink, 380, Ground - 40, split);
                if (t is > 2.0 and < 2.3)
                    Flash(span, c.Paper);
                Burst(span, Leaf, 480, Ground - 60, s, t > 2.0 ? 20 : 0);
                c.God(godX, godY + (t is > 1.7 and < 2.3 ? -30 : stride), false, 1.0f);
                break;
            }

            case Scene.FirstShip:
            {
                Sea(span, s, 0.0);
                var x = 560 + (int)(t * 40);
                Ship(span, ink, x, 260, 0.8f, s);
                c.God(x - 40, 260 - c.GodHeight + 60 + (int)(Math.Sin(s * 1.5) * 8), false, 0.9f);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 160 + (i * 100), Ground, 0.5f, false);
                break;
            }

            case Scene.GiveShip:
                Sea(span, s, 0.0);
                Ship(span, ink, 420, 300, 0.8f, s);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 160 + (i * 90), Ground, 0.5f, false);
                c.God(godX, godY + stride, false, 1.2f);
                break;
            case Scene.Harbour:
                Sea(span, s, 0.0);
                City(span, ink, 100, Ground, 6);
                Ship(span, ink, 900 - (int)(t * 50), 280, 0.6f, s);
                Ship(span, ink, 1100 - (int)(t * 40), 300, 0.5f, s);
                c.God(godX + 200, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;
            case Scene.FleetOut:
                Sea(span, s, 0.2);
                for (var i = 0; i < 4; i++)
                    Ship(span, ink, 300 + (i * 140) + (int)(t * 30), 240 + ((i % 2) * 30), 0.5f, s);
                c.God(godX + 160, godY + stride, false, 0.6f);
                break;
            case Scene.Headland:
                Sea(span, s, 0.4);
                Headland(span, ink, 900, Ground);
                for (var i = 0; i < 4; i++)
                    Ship(span, ink, 200 + (i * 120), 250 + ((i % 2) * 20), 0.4f, s);
                c.God(godX + 100, Ground - c.GodHeight - 150, false, 0.6f);
                break;
            case Scene.Storm:
                Dark(span, 0.45f);
                Sea(span, s, 1.0);
                Rocks(span, ink, 300, 340);
                Ship(span, ink, 330, 300, 0.5f, s, wrecked: true);
                Ship(span, ink, 700 + (int)(Math.Sin(s * 2.0) * 30), 240, 0.5f, s);
                Rain(span, ink, s);
                if ((int)(s * 3) % 7 == 0)
                    Flash(span, c.Paper);
                c.God(godX + 100, Ground - c.GodHeight - 150, false, 0.4f);
                break;
            case Scene.RaiseArm:
                Dark(span, 0.3f);
                Sea(span, s, 0.8);
                Rocks(span, ink, 300, 340);
                Ship(span, ink, 330, 300, 0.5f, s, wrecked: true);
                for (var i = 0; i < 3; i++)
                    Ship(span, ink, 600 + (i * 130), 250 + ((i % 2) * 30), 0.4f, s);
                Rain(span, ink, s);
                c.God(godX - 240, Ground - c.GodHeight - 60 + (int)(Math.Sin(s * 1.5) * 8), false, 1.4f);
                break;
            case Scene.FleetHome:
                Sea(span, s, 0.3);
                Rocks(span, ink, 300, 340);
                Ship(span, ink, 330, 300, 0.5f, s, wrecked: true);
                City(span, ink, 60, Ground, 5);
                for (var i = 0; i < 3; i++)
                    Ship(span, ink, 900 + (i * 120) - (int)(t * 60), 250 + ((i % 2) * 30), 0.4f, s);
                c.God(godX - 240, Ground - c.GodHeight - 60 + (int)(Math.Sin(s * 1.5) * 8), false, 1.2f);
                break;
            case Scene.SunsetShore:
                Sea(span, s, 0.0);
                City(span, ink, 60, Ground, 5);
                Canvas.Disc(span, 1000, 150, 50, SunGold);
                Mortal(span, ink, 300, Ground, 0.5f, false);
                c.God(godX + 100, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;

            // -- Oschon ------------------------------------------------------------------------------------------------
            case Scene.Between:
                Ridge(span, ink, 260);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.6f);
                break;
            case Scene.WindingRoads:
                Ridge(span, ink, 260);
                Roads(span, c.Paper, s);
                c.God(godX - 100 + (int)(Math.Sin(s * 0.5) * 60), godY + stride, false, 0.8f);
                break;
            case Scene.LooseArrow:
                Ridge(span, ink, 260);
                Arrow(span, ink, godX - 40, godY + 120, Math.Clamp((t - 1.0) / 3.0, 0.0, 1.0), -1);
                c.God(godX, godY + stride, false, 0.9f);
                break;
            case Scene.Traveller:
                Ridge(span, ink, 260);
                Roads(span, c.Paper, s);
                Mortal(span, ink, godX - 160, Ground, 0.55f, true);
                c.God(godX, godY + stride, false, 0.9f);
                break;
            case Scene.Lake:
                Ridge(span, ink, 260);
                LakeWater(span, s, 300, 400, 420, Math.Clamp(t / 3.0, 0.0, 1.0));
                Mortal(span, ink, godX - 160, Ground, 0.55f, true);
                c.God(godX, godY + stride, false, 1.1f);
                break;
            case Scene.OverRidge:
                Ridge(span, ink, 260);
                Arrow(span, ink, godX + 40, godY + 120, Math.Clamp(t / 3.0, 0.0, 1.0), 1);
                c.God(godX + (int)(t * 80), godY - (int)(Math.Min(t, 5.0) * 40) + stride, false, 1.0f);
                break;
            case Scene.RidgeStop:
                Dark(span, 0.4f);
                Ridge(span, ink, 200);
                Clouds(span, c.Paper, s);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY - 100 + stride, false, 0.5f);
                break;
            case Scene.LightBehind:
                Dark(span, 0.4f - (float)Math.Clamp(t / 8.0, 0.0, 0.3));
                Ridge(span, ink, 200);
                Clouds(span, c.Paper, s);
                Canvas.Disc(span, 200, 200 - (int)(Math.Clamp(t / 6.0, 0.0, 1.0) * 100), 50, MoonWhite);
                c.God(godX, godY - 100, false, 0.7f);
                break;
            case Scene.LoverMeets:
                Dark(span, 0.15f);
                Ridge(span, ink, 200);
                Canvas.Disc(span, 200, 100, 50, MoonWhite);
                c.Other(-c.OtherWidth - 40 + (int)((300 + c.OtherWidth + 40) * ease), Ground - c.OtherHeight - 100 + stride, false, 0.8f);
                Wolf(span, ink, -200 + (int)((160 + 200) * ease), Ground - 100, howl: false, s);
                c.God(godX, godY - 100, true, 0.8f);
                break;
            case Scene.LoverTalk:
                Dark(span, 0.15f);
                Ridge(span, ink, 200);
                Canvas.Disc(span, 200, 100, 50, MoonWhite);
                c.Other(300, Ground - c.OtherHeight - 100 + stride, false, 0.9f);
                Wolf(span, ink, 160, Ground - 100, howl: false, s);
                c.God(godX, godY - 100 + stride, true, 0.9f);
                break;
            case Scene.ArrowIntoDark:
                Dark(span, 0.15f);
                Ridge(span, ink, 200);
                Canvas.Disc(span, 200, 100, 50, MoonWhite);
                c.Other(300, Ground - c.OtherHeight - 100 + stride, false, 1.2f);
                Arrow(span, ink, godX + 40, godY + 20, Math.Clamp((t - 1.0) / 3.0, 0.0, 1.0), 1);
                c.God(godX, godY - 100 + stride, false, 1.2f);
                break;
            case Scene.WalkSlow:
                Dark(span, 0.15f);
                Ridge(span, ink, 200);
                Canvas.Disc(span, 200 + (int)(t * 30), 100, 50, MoonWhite);
                c.Other(300 + (int)(t * 30), Ground - c.OtherHeight - 100 + stride, false, 0.9f);
                c.God(godX + (int)(t * 40), godY - 100 + stride, false, 1.0f);
                break;

            // -- Byregot ------------------------------------------------------------------------------------------------
            case Scene.FatherBreaks:
                RubbleField(span, ink, s);
                Comet(span, Spark, ink, 300, 120, 0.0);
                c.Other(200, Ground - c.OtherHeight + stride, false, 0.5f);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.6f);
                break;
            case Scene.BareHill:
                Hill(span, ink, 400, Ground, 220);
                WindLines(span, ink, s);
                c.God(godX - 200, Ground - c.GodHeight - 80 + stride, false, 0.7f);
                break;
            case Scene.StrikeStone:
            {
                Hill(span, ink, 400, Ground, 220);
                if (t is > 2.0 and < 2.3)
                    Flash(span, c.Paper);
                Wall(span, ink, 360, Ground - 80, t > 2.0 ? 1 : 0, 0.0);
                Burst(span, Spark, 420, Ground - 100, s, t > 2.0 ? 20 : 0);
                c.God(godX - 200, Ground - c.GodHeight - 80 + (t is > 1.6 and < 2.3 ? -30 : stride), false, 1.0f);
                break;
            }

            case Scene.WallRises:
                Hill(span, ink, 400, Ground, 220);
                WindLines(span, ink, s);
                Wall(span, ink, 360, Ground - 80, 1 + (int)(t / 1.2), t % 1.2 / 1.2);
                c.God(godX - 200, Ground - c.GodHeight - 80 + stride, false, 1.0f);
                break;
            case Scene.House:
                Hill(span, ink, 400, Ground, 220);
                Wall(span, ink, 360, Ground - 80, 6, 1.0);
                HouseTop(span, ink, 360, Ground - 80 - 96, Math.Clamp(t / 4.0, 0.0, 1.0), s);
                Mortal(span, ink, 300, Ground - 80, 0.4f, false);
                Mortal(span, ink, 500, Ground - 80, 0.35f, false);
                c.God(godX - 200, Ground - c.GodHeight - 80 + stride, false, 1.2f);
                break;
            case Scene.WallDepart:
                Hill(span, ink, 400, Ground, 220);
                Wall(span, ink, 360, Ground - 80, 6, 1.0);
                HouseTop(span, ink, 360, Ground - 80 - 96, 1.0, s);
                WindLines(span, ink, s);
                c.God(godX, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;
            case Scene.Rubble:
                RubbleField(span, ink, s);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.6f);
                break;
            case Scene.DestroyerMeets:
                RubbleField(span, ink, s);
                Hill(span, ink, 300, Ground, 160);
                c.Other(-c.OtherWidth - 40 + (int)((200 + c.OtherWidth + 40) * ease), Ground - c.OtherHeight - 60 + stride, true, 0.8f);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.RubbleTalk:
                RubbleField(span, ink, s);
                Hill(span, ink, 300, Ground, 160);
                c.Other(200, Ground - c.OtherHeight - 60 + stride, true, 0.8f);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.PickHammer:
                RubbleField(span, ink, s);
                Hill(span, ink, 300, Ground, 160);
                c.Other(200, Ground - c.OtherHeight - 60 + stride, true, 0.6f);
                Wall(span, ink, 520, Ground, (int)(t / 1.5), t % 1.5 / 1.5);
                c.God(godX, godY + stride, false, 1.0f);
                break;
            case Scene.NewCity:
                City(span, ink, 380, Ground, 5, Math.Clamp(t / 6.0, 0.0, 1.0));
                Hill(span, ink, 300, Ground, 160);
                c.Other(200, Ground - c.OtherHeight - 60 + stride, true, 0.8f);
                c.God(godX + 100, godY + stride, false, 1.2f);
                break;
            case Scene.TurnsDepart:
                City(span, ink, 380, Ground, 5, 1.0);
                c.Other(200, Ground - c.OtherHeight - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), true, 1.0f);
                c.God(godX + 100, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.0f);
                break;

            // -- Rhalgr ------------------------------------------------------------------------------------------------
            case Scene.Palace:
                PalaceShape(span, ink, 300, Ground, 1.0, s, cracks: true);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 640 + (i * 70), Ground, 0.4f, false);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY - 120 + stride, false, 0.5f);
                break;
            case Scene.HillComet:
                PalaceShape(span, ink, 300, Ground, 1.0, s, cracks: true);
                Hill(span, ink, godX + 100, Ground, 140);
                Comet(span, Spark, ink, godX + 40, godY - 20, 0.0, held: true, s);
                c.God(godX, godY - 60 + stride, false, 0.8f);
                break;
            case Scene.RaiseComet:
                PalaceShape(span, ink, 300, Ground, 1.0, s, cracks: true);
                Hill(span, ink, godX + 100, Ground, 140);
                for (var i = 0; i < 4; i++)
                    Mortal(span, ink, 600 + (i * 60), Ground, 0.4f, false);
                Comet(span, Spark, ink, godX + 40, godY - 80 - (int)(Math.Clamp(t / 3.0, 0.0, 1.0) * 60), 0.0, held: true, s);
                c.God(godX, godY - 60 + stride, false, 1.0f);
                break;
            case Scene.ThrowComet:
            {
                var f = Math.Clamp((t - 1.0) / 2.5, 0.0, 1.0);
                var hit = t > 3.5;
                PalaceShape(span, ink, 300, Ground, hit ? 1.0 - Math.Clamp((t - 3.5) / 2.0, 0.0, 1.0) : 1.0, s, cracks: true);
                Hill(span, ink, godX + 100, Ground, 140);
                if (!hit)
                    Comet(span, Spark, ink, godX + 40 - (int)(f * (godX - 300)), godY - 120 + (int)(Math.Sin(f * Math.PI) * -200) + (int)(f * (Ground - 200 - godY)), f, held: false, s);
                if (t is > 3.5 and < 3.9)
                    Flash(span, c.Paper);
                Burst(span, Spark, 340, Ground - 150, s, hit ? 40 : 0);
                c.God(godX, godY - 60 + (t is > 0.8 and < 1.4 ? -30 : stride), false, 1.2f);
                break;
            }

            case Scene.GreenField:
                Canvas.Fill(span, 200, Ground - 30, 400, 30, Canvas.Lerp(Green, ink, 0.4f));
                for (var i = 0; i < 4; i++)
                    Mortal(span, ink, 240 + (i * 90), Ground, 0.45f, i == 3);
                Hill(span, ink, godX + 100, Ground, 140);
                c.God(godX, godY - 60 + stride, false, 1.0f);
                break;
            case Scene.CometDepart:
                Canvas.Fill(span, 200, Ground - 30, 400, 30, Canvas.Lerp(Green, ink, 0.4f));
                Hill(span, ink, godX + 100, Ground, 140);
                c.God(godX + (int)(t * 20), godY - 60 - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;
            case Scene.Monks:
                Hill(span, ink, 500, Ground, 120);
                for (var i = 0; i < 4; i++)
                    Kneeling(span, ink, 200 + (i * 100), Ground);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.6f);
                break;
            case Scene.Stone:
                Hill(span, ink, 500, Ground, 120);
                Boulder(span, ink, 520, Ground - 120, 1.0);
                for (var i = 0; i < 4; i++)
                    Kneeling(span, ink, 200 + (i * 80), Ground);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.Waiting:
            {
                Hill(span, ink, 500, Ground, 120);
                Boulder(span, ink, 520, Ground - 120, 1.0);
                var day = (float)((Math.Sin(s * 1.2) + 1.0) / 2.0);
                Canvas.Disc(span, 300 + (int)(day * 200), 120, 30, Canvas.Lerp(MoonWhite, SunGold, day));
                for (var i = 0; i < 4; i++)
                    Kneeling(span, ink, 200 + (i * 80) + (int)(Math.Sin(s + i) * 4), Ground);
                c.God(godX, godY + 60, false, 0.7f);
                break;
            }

            case Scene.StoneBreaks:
                Hill(span, ink, 500, Ground, 120);
                Boulder(span, ink, 520, Ground - 120, t > 2.0 ? 0.0 : 1.0);
                if (t is > 2.0 and < 2.3)
                    Flash(span, c.Paper);
                Burst(span, Dust, 540, Ground - 140, s, t > 2.0 ? 24 : 0);
                Mortal(span, ink, 460, Ground - 120, 0.45f, false);
                for (var i = 0; i < 3; i++)
                    Kneeling(span, ink, 160 + (i * 80), Ground);
                c.God(godX, godY + 60, false, 0.8f);
                break;
            case Scene.Nod:
                Hill(span, ink, 500, Ground, 120);
                Mortal(span, ink, 460, Ground - 120, 0.45f, false);
                for (var i = 0; i < 3; i++)
                    Kneeling(span, ink, 160 + (i * 80), Ground);
                c.God(godX, godY + (t < 1.5 ? 60 : (int)(Math.Sin(s * 1.5) * 6)), false, 1.2f);
                break;
            case Scene.StoneDepart:
                Hill(span, ink, 500, Ground, 120);
                Mortal(span, ink, 460, Ground - 120, 0.45f, false);
                c.God(godX + (int)(t * 40), godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.0f);
                break;

            // -- Azeyma ------------------------------------------------------------------------------------------------
            case Scene.SunInHands:
                Dark(span, 0.3f);
                Dunes(span, ink, s);
                Canvas.Disc(span, godX - 30, godY + (c.GodHeight / 2), 34, SunGold);
                c.God(godX, godY + stride, false, 1.0f);
                break;
            case Scene.MarketDark:
                Dark(span, 0.3f);
                Stalls(span, ink, 140, Ground, 4);
                Canvas.Disc(span, 280, 260, 40, SunGold);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.ThrowSun:
            {
                var f = Math.Clamp((t - 1.5) / 3.0, 0.0, 1.0);
                Dark(span, 0.3f - (float)(f * 0.3));
                Dunes(span, ink, s);
                Canvas.Disc(span, godX - 30 - (int)(f * 200), godY + (c.GodHeight / 2) - (int)(f * 380), 34 + (int)(f * 30), SunGold);
                c.God(godX, godY + (t is > 1.0 and < 1.8 ? -40 : stride), false, 1.2f);
                break;
            }

            case Scene.SunHigh:
                Dunes(span, ink, s);
                Canvas.Disc(span, 520, 120, 64, SunGold);
                Stalls(span, ink, 140, Ground, 3);
                Thief(span, ink, 480, Ground);
                Mortal(span, ink, 300, Ground, 0.5f, false);
                c.God(godX, godY + stride, false, 1.0f);
                break;
            case Scene.Noon:
                Dunes(span, ink, s);
                Canvas.Disc(span, 640, 100, 70, SunGold);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 200 + (i * 120), Ground, 0.5f, false);
                c.God(godX, godY + stride, false, 1.2f);
                break;
            case Scene.BehindSun:
                Dunes(span, ink, s);
                Canvas.Disc(span, 640, 100, 70, SunGold);
                c.God(godX + (int)(t * 20), godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;
            case Scene.Gate:
                CityGate(span, ink, 240, Ground);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.7f);
                break;
            case Scene.TradersMeet:
                CityGate(span, ink, 240, Ground);
                c.Other(-c.OtherWidth - 40 + (int)((260 + c.OtherWidth + 40) * ease), Ground - c.OtherHeight + stride, true, 0.8f);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.SunOverMarket:
                Stalls(span, ink, 100, Ground, 4);
                Canvas.Disc(span, godX + 40, godY - 40, 50, SunGold);
                for (var i = 0; i < 4; i++)
                    ScalesSmall(span, ink, 160 + (i * 110), Ground - 90, i == 2 ? -6 : 0);
                c.Other(200, Ground - c.OtherHeight + stride, true, 0.6f);
                c.God(godX, godY + stride, false, 1.0f);
                break;
            case Scene.ScalesLevel:
                Stalls(span, ink, 100, Ground, 4);
                Canvas.Disc(span, godX + 40, godY - 40, 50, SunGold);
                Scales(span, ink, 520, Ground - 60, 0, s);
                c.Other(200, Ground - c.OtherHeight + stride, true, 1.2f);
                c.God(godX, godY + stride, false, 1.2f);
                break;
            case Scene.Ledger:
                Stalls(span, ink, 100, Ground, 4);
                Canvas.Disc(span, godX + 40, godY - 40, 50, SunGold);
                Book(span, c.Paper, ink, 560, Ground - 120, s);
                c.Other(200, Ground - c.OtherHeight + stride, true, 0.9f);
                c.God(godX, godY + stride, false, 0.9f);
                break;
            case Scene.DawnDepart:
                Stalls(span, ink, 100, Ground, 4);
                Canvas.Disc(span, 300, 140, 50, SunGold);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 180 + (i * 130), Ground, 0.5f, false);
                c.God(godX, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;

            // -- Nald'thal ------------------------------------------------------------------------------------------------
            case Scene.TwinsDesert:
                Dunes(span, ink, s);
                c.God(W + 40 - (int)((W + 40 - godX + 100) * ease), godY + stride, false, 0.7f);
                break;
            case Scene.ArgueScales:
                Dunes(span, ink, s);
                Scales(span, ink, 500, Ground - 60, (int)(Math.Sin(s * 2.0) * 14), s);
                c.God(godX - 100, godY + stride, false, 0.8f);
                break;
            case Scene.Deal:
                Dunes(span, ink, s);
                Scales(span, ink, 500, Ground - 60, (int)(Math.Sin(s * 0.8) * 6), s);
                Mortal(span, ink, 260, Ground, 0.5f, false);
                Coffin(span, ink, 200, Ground - 30);
                c.God(godX - 100, godY + stride, false, 0.9f);
                break;
            case Scene.CounterAndTomb:
                Stalls(span, ink, 100, Ground, 2);
                Tomb(span, ink, 440, Ground);
                Scales(span, ink, 660, Ground - 60, 0, s);
                c.God(godX - 60, godY + stride, false, 1.0f);
                break;
            case Scene.Handshake:
            {
                Dunes(span, ink, s);
                var tip = t < 2.0 ? (int)(Math.Sin(t * 3.0) * 20) : 0;
                Scales(span, ink, 500, Ground - 60, tip, s);
                if (t is > 2.0 and < 2.3)
                    Flash(span, c.Paper);
                Burst(span, Ember, 560, Ground - 120, s, t > 2.0 ? 16 : 0);
                c.God(godX - 100, godY + stride, false, 1.3f);
                break;
            }

            case Scene.IntoCity:
                CityGate(span, ink, 240, Ground);
                Stalls(span, ink, 60, Ground, 1);
                Tomb(span, ink, 460, Ground);
                c.God(godX - 100 - (int)(t * 40), godY + stride, true, 1.0f);
                break;
            case Scene.Sultan:
                Dark(span, 0.2f);
                King(span, ink, 300, Ground, s);
                Pages(span, c.Paper, ink, 330, Ground - 110, 2, s);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.6f);
                break;
            case Scene.WeighNothing:
                Dark(span, 0.2f);
                King(span, ink, 260, Ground, s);
                Scales(span, ink, 520, Ground - 60, 0, s);
                Pages(span, c.Paper, ink, 470, Ground - 150, 2, s);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.DebtsHeavy:
                Dark(span, 0.2f);
                King(span, ink, 260, Ground, s);
                Scales(span, ink, 520, Ground - 60, (int)(Math.Clamp(t / 2.0, 0.0, 1.0) * 40), s);
                Canvas.Disc(span, 590, Ground - 100 + (int)(Math.Clamp(t / 2.0, 0.0, 1.0) * 40), 26, ink);
                c.God(godX, godY + stride, false, 0.9f);
                break;
            case Scene.WaterSeller:
                Dark(span, 0.2f);
                King(span, ink, 200, Ground, s);
                Mortal(span, ink, 340, Ground, 0.5f, true);
                Scales(span, ink, 560, Ground - 60, -(int)(Math.Clamp(t / 2.0, 0.0, 1.0) * 30), s);
                c.God(godX, godY + stride, false, 1.1f);
                break;
            case Scene.Exact:
                Dark(span, 0.2f);
                King(span, ink, 200, Ground, s);
                Mortal(span, ink, 340, Ground, 0.5f, true);
                Book(span, c.Paper, ink, 540, Ground - 130, s);
                c.God(godX, godY + stride, false, 1.0f);
                break;
            case Scene.SettleDepart:
                Stalls(span, ink, 100, Ground, 3);
                Canvas.Disc(span, 1000, 130, 50, Canvas.Lerp(MoonWhite, c.Paper, 0.3f));
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 180 + (i * 120), Ground, 0.5f, false);
                c.God(godX, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.1f);
                break;

            // -- Nophica ------------------------------------------------------------------------------------------------
            case Scene.BareLand:
                Stones(span, ink, s, 1);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.7f);
                break;
            case Scene.Kneel:
                Stones(span, ink, s, 1);
                c.God(godX - 200, godY + (int)(Math.Clamp(t / 2.0, 0.0, 1.0) * 70), false, 0.8f);
                break;
            case Scene.SpringTree:
            {
                var grow = Math.Clamp(t / 6.0, 0.0, 1.0);
                Spring(span, Water, ink, 480, Ground, Math.Clamp(t / 2.0, 0.0, 1.0), s);
                GrowingTree(span, ink, Green, 480, Ground, grow);
                c.God(godX - 200, godY + 70 - (int)(Math.Clamp((t - 2.0) / 2.0, 0.0, 1.0) * 70), false, 1.0f);
                break;
            }

            case Scene.FuryAsks:
                Spring(span, Water, ink, 480, Ground, 1.0, s);
                GrowingTree(span, ink, Green, 480, Ground, 1.0);
                Trees(span, ink, 60, 3, s);
                c.Other(-c.OtherWidth - 40 + (int)((160 + c.OtherWidth + 40) * ease), Ground - c.OtherHeight + stride, true, 0.8f);
                c.God(godX - 40, godY + stride, false, 0.9f);
                break;
            case Scene.Bread:
                Spring(span, Water, ink, 480, Ground, 1.0, s);
                GrowingTree(span, ink, Green, 480, Ground, 1.0);
                Table(span, ink, c.Paper, 220, Ground);
                Mortal(span, ink, 140, Ground, 0.5f, false);
                Mortal(span, ink, 300, Ground, 0.4f, false);
                c.God(godX - 40, godY + stride, false, 1.3f);
                break;
            case Scene.SickleDepart:
                Spring(span, Water, ink, 480, Ground, 1.0, s);
                GrowingTree(span, ink, Green, 480, Ground, 1.0);
                Trees(span, ink, 60, 3, s);
                c.God(godX - 40, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;
            case Scene.BrownWood:
                Dry(span, 0.5f);
                Trees(span, ink, 60, 5, s);
                Spring(span, Dust, ink, 480, Ground, 0.3, s);
                c.God(W + 40 - (int)((W + 40 - godX) * ease), godY + stride, false, 0.6f);
                break;
            case Scene.Shrine:
                Dry(span, 0.5f);
                Chapel(span, ink, 200, Ground);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 360 + (i * 90), Ground, 0.5f, false);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.Dig:
                Dry(span, 0.5f);
                Hole(span, ink, c.Paper, 480, Ground, Math.Clamp(t / 6.0, 0.0, 1.0));
                for (var i = 0; i < 3; i++)
                    Kneeling(span, ink, 300 + (i * 70), Ground, dig: true, s);
                c.God(godX - 200, godY + 70, false, 0.9f);
                break;
            case Scene.WaterFound:
                Dry(span, 0.5f - (float)Math.Clamp(t / 8.0, 0.0, 0.4));
                Hole(span, ink, c.Paper, 480, Ground, 1.0);
                Spring(span, Water, ink, 480, Ground, Math.Clamp((t - 1.0) / 2.0, 0.0, 1.0), s);
                if (t is > 1.0 and < 1.3)
                    Flash(span, c.Paper);
                for (var i = 0; i < 3; i++)
                    Kneeling(span, ink, 300 + (i * 70), Ground, dig: false, s);
                c.God(godX - 200, godY + 70, false, 1.2f);
                break;
            case Scene.GreenBack:
                Trees(span, ink, 60, 5, s);
                Spring(span, Water, ink, 480, Ground, 1.0, s);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 280 + (i * 100), Ground, 0.5f, false);
                c.God(godX - 40, godY + stride, false, 1.2f);
                break;
            case Scene.DigDepart:
                Trees(span, ink, 60, 5, s);
                Spring(span, Water, ink, 480, Ground, 1.0, s);
                Mortal(span, ink, 300, Ground, 0.5f, true);
                c.God(godX - 40, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.2f);
                break;

            // -- Althyk ------------------------------------------------------------------------------------------------
            case Scene.NoNow:
                Dark(span, 0.5f);
                c.God(godX - 200, godY + stride, false, 0.5f);
                break;
            case Scene.MakeGlass:
                Dark(span, 0.5f);
                Hourglass(span, ink, godX - 120, Ground - 60, (int)(Math.Clamp(t / 3.0, 0.0, 1.0) * 100), s, 0.0);
                c.God(godX, godY + stride, false, 0.7f);
                break;
            case Scene.TurnGlass:
            {
                Dark(span, 0.5f - (float)Math.Clamp(t / 8.0, 0.0, 0.3));
                var turn = Math.Clamp((t - 1.5) / 1.5, 0.0, 1.0);
                Hourglass(span, ink, godX - 120, Ground - 60, 100, s, t > 3.0 ? Math.Clamp((t - 3.0) / 5.0, 0.0, 1.0) : 0.0, turn);
                if (t is > 3.0 and < 3.3)
                    Flash(span, c.Paper);
                c.God(godX, godY + stride, false, 1.0f);
                break;
            }

            case Scene.Kept:
                Dark(span, 0.2f);
                Hourglass(span, ink, godX - 120, Ground - 60, 100, s, 0.5);
                Mortal(span, ink, 300, Ground, 0.5f, false);
                Mortal(span, ink, 420, Ground, 0.45f, true);
                c.God(godX, godY + stride, false, 1.1f);
                break;
            case Scene.SpinnerComes:
                Dark(span, 0.2f);
                Hourglass(span, ink, godX - 120, Ground - 60, 100, s, 0.6);
                c.Other(-c.OtherWidth - 40 + (int)((260 + c.OtherWidth + 40) * ease), Ground - c.OtherHeight + stride, true, 0.8f);
                Threads(span, Water, 260 + (c.OtherWidth / 2), Ground - c.OtherHeight + 60, 1, s, Math.Clamp((t - 2.5) / 3.0, 0.0, 1.0));
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.YearEnd:
                Dark(span, 0.2f);
                Hourglass(span, ink, godX - 120, Ground - 60, 100, s, 0.95);
                Canvas.Disc(span, 300, 130, 40, MoonWhite);
                for (var i = 0; i < 3; i++)
                    Mortal(span, ink, 200 + (i * 110), Ground, 0.5f, false);
                c.God(godX, godY + stride, false, 1.3f);
                break;
            case Scene.YoungMan:
                Chapel(span, ink, 260, Ground);
                Mortal(span, ink, 480, Ground, 0.4f, false);
                c.God(godX, godY + stride, false, 0.8f);
                break;
            case Scene.KeeperLooks:
                Chapel(span, ink, 260, Ground);
                Mortal(span, ink, 480 + (int)(Math.Sin(s * 4.0) * 3), Ground, 0.4f, false);
                Hourglass(span, ink, godX - 120, Ground - 60, 90, s, 0.3);
                c.God(godX, godY, false, 0.9f);
                break;
            case Scene.TipGlass:
            {
                Chapel(span, ink, 260, Ground);
                var f = Math.Clamp((t - 1.0) / 4.0, 0.0, 1.0);
                Hourglass(span, ink, godX - 120, Ground - 60, 90, s, f, tipped: 1.0);
                Mortal(span, ink, 480, Ground, 0.4f + (float)(f * 0.12), true, stoop: (float)f);
                c.God(godX, godY, false, 1.0f);
                break;
            }

            case Scene.Mirror:
                Chapel(span, ink, 260, Ground);
                MirrorShape(span, ink, c.Paper, 380, Ground);
                Mortal(span, ink, 500, Ground, 0.52f, true, stoop: 1f);
                c.God(godX, godY, false, 0.9f);
                break;
            case Scene.LowerChamber:
                Chapel(span, ink, 260, Ground);
                Hourglass(span, ink, godX - 120, Ground - 60, 110, s, 1.0);
                Mortal(span, ink, 500, Ground, 0.52f, true, stoop: 1f);
                c.God(godX, godY, false, 1.2f);
                break;
            case Scene.Children:
                Chapel(span, ink, 260, Ground);
                Book(span, c.Paper, ink, 420, Ground - 100, s);
                for (var i = 0; i < 4; i++)
                    Mortal(span, ink, 440 + (i * 60) + (int)(Math.Sin(s * 3.0 + i) * 4), Ground, 0.3f, false);
                c.God(godX, godY - (int)(Math.Pow(Math.Clamp((t - 3.0) / 4.0, 0.0, 1.0), 2) * 700), false, 1.1f);
                break;

            // -- the ogre ------------------------------------------------------------------------------------------------
            case Scene.SwampSign:
                Reeds(span, ink, s);
                Mud(span, ink, 300, Ground, s);
                Sign(span, ink, 880, Ground);
                Ogre(span, ink, 600, Ground, s, arms: false);
                break;
            case Scene.Doorstep:
                Reeds(span, ink, s);
                Hut(span, ink, 900, Ground);
                Ogre(span, ink, 760, Ground, s, arms: true);
                for (var i = 0; i < 6; i++)
                    Mortal(span, ink, 160 + (i * 70), Ground, 0.22f + (0.06f * (i % 3)), i % 2 == 0);
                Chocobo(span, ink, 520 + (int)(Math.Sin(s * 5.0) * 6), Ground, s, talking: true);
                break;
            case Scene.ShortLord:
                Castle(span, ink, 760, Ground);
                King(span, ink, 560, Ground, s, small: true);
                Ogre(span, ink, 240, Ground, s, arms: false);
                Chocobo(span, ink, 420, Ground, s, talking: true);
                break;
            case Scene.TowerDragon:
            {
                Dark(span, 0.2f);
                Lava(span, s);
                Tower(span, ink, 900, Ground - 60);
                var swoon = (int)(Math.Sin(s * 2.0) * 10);
                Dragon(span, ink, 700, 130 + swoon, s);
                Heart(span, Canvas.Rgb(0xE0, 0x50, 0x80), 560, 200 - (int)(t * 10 % 60), 10 + (int)(Math.Sin(s * 6.0) * 3));
                Chocobo(span, ink, 420, Ground - 60, s, talking: false);
                Ogre(span, ink, 200, Ground - 60, s, arms: true);
                break;
            }

            case Scene.RoadHome:
            {
                Ridge(span, ink, 90);
                Roads(span, c.Paper, s);
                var walk = (int)(t * 40);
                Ogre(span, ink, 160 + walk, Ground, s, arms: false);
                Chocobo(span, ink, 360 + walk + (int)(Math.Sin(s * 5.0) * 4), Ground, s, talking: true);
                Mortal(span, ink, 500 + walk, Ground, 0.5f, false);
                Canvas.Disc(span, 1000, 160, 34, Canvas.Rgb(0xFF, 0xB0, 0x60));
                break;
            }

            case Scene.Wedding:
            {
                Chapel(span, ink, 200, Ground);
                Canvas.Disc(span, 1000, 140, 34, MoonWhite);
                var turned = t > 4.0;
                if (t is > 4.0 and < 4.3)
                    Flash(span, c.Paper);
                Ogre(span, ink, 520, Ground, s, arms: true);
                if (turned)
                    Ogre(span, ink, 700, Ground, s, arms: true, small: true);
                else
                    Mortal(span, ink, 720, Ground, 0.5f, false);
                Chocobo(span, ink, 900, Ground, s, talking: true);
                for (var i = 0; i < 4; i++)
                    Mortal(span, ink, 1000 + (i * 50), Ground, 0.22f + (0.05f * (i % 2)), false);
                for (var i = 0; i < 6; i++)
                    Heart(span, Canvas.Rgb(0xE0, 0x50, 0x80), 560 + (i * 60), 200 - (int)((t * 30 + (i * 40)) % 120), 6);
                break;
            }
        }
    }

    // -- the ogre's pieces ---------------------------------------------------------------------------------

    private static void Ogre(Span<uint> span, uint ink, int x, int ground, double s, bool arms, bool small = false)
    {
        var sc = small ? 0.8f : 1f;
        var bob = (int)(Math.Sin(s * 1.6) * 3);
        var bodyR = (int)(70 * sc);
        var headR = (int)(44 * sc);
        var by = ground - bodyR - 10 + bob;
        var hy = by - bodyR - headR + (int)(20 * sc);
        Canvas.Disc(span, x, by, bodyR, ink);
        Canvas.Fill(span, x - bodyR, by, bodyR * 2, ground - by, ink);
        Canvas.Disc(span, x, hy, headR, ink);
        // The ears: two little trumpets.
        Canvas.Fill(span, x - headR - (int)(8 * sc), hy - (int)(20 * sc), (int)(10 * sc), (int)(6 * sc), ink);
        Canvas.Disc(span, x - headR - (int)(10 * sc), hy - (int)(18 * sc), (int)(7 * sc), ink);
        Canvas.Fill(span, x + headR - (int)(2 * sc), hy - (int)(20 * sc), (int)(10 * sc), (int)(6 * sc), ink);
        Canvas.Disc(span, x + headR + (int)(10 * sc), hy - (int)(18 * sc), (int)(7 * sc), ink);
        // Eyes, so he reads as a face and not a boulder.
        Canvas.Disc(span, x - (int)(14 * sc), hy - (int)(4 * sc), (int)(4 * sc), Canvas.Rgb(0xF0, 0xE8, 0xD0));
        Canvas.Disc(span, x + (int)(14 * sc), hy - (int)(4 * sc), (int)(4 * sc), Canvas.Rgb(0xF0, 0xE8, 0xD0));
        if (arms)
        {
            Canvas.Line(span, x - bodyR, by, x - bodyR - (int)(40 * sc), by - (int)(30 * sc) + bob, ink);
            for (var i = 1; i < (int)(12 * sc); i++)
                Canvas.Line(span, x - bodyR, by + i, x - bodyR - (int)(40 * sc), by - (int)(30 * sc) + bob + i, ink);
            Canvas.Line(span, x + bodyR, by, x + bodyR + (int)(40 * sc), by - (int)(30 * sc) + bob, ink);
            for (var i = 1; i < (int)(12 * sc); i++)
                Canvas.Line(span, x + bodyR, by + i, x + bodyR + (int)(40 * sc), by - (int)(30 * sc) + bob + i, ink);
        }
    }

    private static void Chocobo(Span<uint> span, uint ink, int x, int ground, double s, bool talking)
    {
        var bob = (int)(Math.Sin(s * 4.0) * 3);
        Canvas.Disc(span, x, ground - 60 + bob, 34, ink);
        Canvas.Fill(span, x - 12, ground - 30, 8, 30, ink);
        Canvas.Fill(span, x + 8, ground - 30, 8, 30, ink);
        // The neck, up and forward; the head with a beak; a tuft.
        for (var i = 0; i < 70; i++)
            Canvas.Fill(span, x + 20 + (i / 3), ground - 70 - i + bob, 16, 1, ink);
        var hx = x + 50;
        var hy = ground - 150 + bob;
        Canvas.Disc(span, hx, hy, 18, ink);
        var beak = talking ? (int)(Math.Abs(Math.Sin(s * 10.0)) * 8) : 0;
        for (var i = 0; i < 26; i++)
            Canvas.Fill(span, hx + 14 + i, hy - 4 + (i / 3) - beak / 2, 1, 6 + beak - (i / 3), ink);
        Canvas.Line(span, hx - 4, hy - 16, hx - 10, hy - 34, ink);
        Canvas.Line(span, hx + 4, hy - 17, hx + 2, hy - 36, ink);
        Canvas.Disc(span, hx + 4, hy - 4, 3, Canvas.Rgb(0xF0, 0xE8, 0xD0));
        // The tail feathers.
        Canvas.Line(span, x - 30, ground - 66 + bob, x - 60, ground - 96 + bob, ink);
        Canvas.Line(span, x - 30, ground - 60 + bob, x - 64, ground - 80 + bob, ink);
    }

    private static void Reeds(Span<uint> span, uint ink, double s)
    {
        for (var i = 0; i < 30; i++)
        {
            var x = 40 + (i * 42) + ((i * 13) % 20);
            var h = 40 + ((i * 29) % 60);
            var sway = (int)(Math.Sin(s * 1.5 + i) * 4);
            Canvas.Line(span, x, Ground, x + sway, Ground - h, ink);
            Canvas.Line(span, x + 1, Ground, x + 1 + sway, Ground - h, ink);
            Canvas.Fill(span, x + sway - 3, Ground - h - 12, 6, 14, ink);
        }
    }

    private static void Mud(Span<uint> span, uint ink, int x, int ground, double s)
    {
        for (var i = 0; i < 30; i++)
        {
            var half = (int)(Math.Sqrt(1.0 - Math.Pow((i - 15) / 15.0, 2)) * 180);
            Canvas.Fill(span, x - half, ground - 30 + i, half * 2, 1, Canvas.Lerp(ink, Canvas.Rgb(0x6A, 0x52, 0x2A), 0.5f));
        }

        for (var b = 0; b < 4; b++)
        {
            var phase = (s * 0.8 + b * 0.7) % 1.0;
            Canvas.Disc(span, x - 100 + (b * 60), ground - 16, (int)(phase * 8), ink);
        }
    }

    private static void Sign(Span<uint> span, uint ink, int x, int ground)
    {
        Canvas.Fill(span, x - 3, ground - 110, 6, 110, ink);
        Canvas.Fill(span, x - 50, ground - 160, 100, 56, ink);
        for (var l = 0; l < 3; l++)
            Canvas.Fill(span, x - 38, ground - 148 + (l * 14), 76 - (l * 20), 4, Canvas.Rgb(0xF0, 0xE8, 0xD0));
    }

    private static void Hut(Span<uint> span, uint ink, int x, int ground)
    {
        Canvas.Fill(span, x, ground - 90, 130, 90, ink);
        for (var k = 0; k < 44; k++)
            Canvas.Fill(span, x - 10 + k, ground - 90 - k, 150 - (k * 2), 1, ink);
        Canvas.Fill(span, x + 50, ground - 60, 30, 60, Canvas.Lerp(ink, Canvas.White, 0.4f));
    }

    private static void Castle(Span<uint> span, uint ink, int x, int ground)
    {
        Canvas.Fill(span, x, ground - 200, 60, 200, ink);
        Canvas.Fill(span, x + 200, ground - 200, 60, 200, ink);
        Canvas.Fill(span, x, ground - 220, 260, 30, ink);
        for (var i = 0; i < 40; i++)
            Canvas.Fill(span, x + 60 + (i / 2), ground - 190 + i, 140 - i, 1, Canvas.Lerp(ink, Canvas.White, 0.35f));
        // Far too tall a keep behind it.
        Canvas.Fill(span, x + 90, ground - 420, 80, 220, ink);
        for (var k = 0; k < 40; k++)
            Canvas.Fill(span, x + 90 + k, ground - 420 - k, 80 - (k * 2), 1, ink);
        for (var m = 0; m < 5; m++)
            Canvas.Fill(span, x + (m * 56), ground - 236, 24, 16, ink);
    }

    private static void Tower(Span<uint> span, uint ink, int x, int ground)
    {
        Canvas.Fill(span, x - 40, ground - 260, 80, 260, ink);
        for (var k = 0; k < 50; k++)
            Canvas.Fill(span, x - 50 + k, ground - 260 - k, 100 - (k * 2), 1, ink);
        Canvas.Fill(span, x - 12, ground - 220, 24, 30, Canvas.Rgb(0xFF, 0xD8, 0x80));
    }

    private static void Lava(Span<uint> span, double s)
    {
        for (var y = Ground - 60; y < Ground + 40; y++)
        {
            var t = (y - (Ground - 60)) / 100f;
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(Canvas.Rgb(0xFF, 0x8A, 0x2E), Canvas.Rgb(0xA0, 0x30, 0x10), t));
        }

        for (var i = 0; i < 12; i++)
        {
            var x = (int)(((s * 30) + (i * 113)) % W);
            Canvas.Disc(span, x, Ground - 40 + (int)(Math.Sin(s * 2 + i) * 6), 6, Canvas.Rgb(0xFF, 0xD0, 0x60));
        }
    }

    private static void Heart(Span<uint> span, uint colour, int x, int y, int r)
    {
        Canvas.Disc(span, x - r / 2, y, r / 2 + 1, colour);
        Canvas.Disc(span, x + r / 2, y, r / 2 + 1, colour);
        for (var i = 0; i < r; i++)
            Canvas.Fill(span, x - r + (i * r / r), y + i, (r * 2) - (i * 2), 1, colour);
    }

    // -- the pieces ----------------------------------------------------------------------------------------------

    private static void Dark(Span<uint> span, float amount)
    {
        for (var y = 60; y < 540; y++)
        {
            var row = span.Slice(y * W, W);
            for (var x = 0; x < W; x++)
                row[x] = Canvas.Lerp(row[x], 0xFF100C10u, amount);
        }
    }

    private static void Dry(Span<uint> span, float amount)
    {
        for (var y = 60; y < 540; y++)
        {
            var row = span.Slice(y * W, W);
            for (var x = 0; x < W; x++)
                row[x] = Canvas.Lerp(row[x], Canvas.Rgb(0xB8, 0x8A, 0x50), amount * 0.5f);
        }
    }

    private static void Flash(Span<uint> span, uint paper)
    {
        for (var y = 60; y < 540; y++)
        {
            var row = span.Slice(y * W, W);
            for (var x = 0; x < W; x++)
                row[x] = Canvas.Lerp(row[x], Canvas.White, 0.5f);
        }
    }

    private static void Mountain(Span<uint> span, uint ink, int cx, int peakY)
    {
        for (var y = peakY; y < Ground; y++)
        {
            var half = (y - peakY) * 5 / 4;
            Canvas.Fill(span, cx - half, y, half * 2, 1, ink);
        }

        // Snow on the peak: a lighter cap in the paper's own colour is drawn by the caller's
        // flakes; here the peak keeps a notch so it reads as rock.
        Canvas.Fill(span, cx - 6, peakY - 8, 12, 8, ink);
    }

    private static void Spear(Span<uint> span, uint ink, int x, int y, int height)
    {
        Canvas.Fill(span, x - 3, y - height, 6, height, ink);
        for (var i = 0; i < 40; i++)
            Canvas.Fill(span, x - (i / 3), y - height - 40 + i, (i / 3 * 2) + 2, 1, ink);
    }

    private static void Snow(Span<uint> span, double s, double amount)
    {
        var count = (int)(amount * 80);
        var rng = 88172645u;
        for (var i = 0; i < count; i++)
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            var x = (int)((rng % (uint)W) + (Math.Sin((s * 0.7) + i) * 20));
            var y = 60 + (int)(((rng >> 8) % 480 + (s * 40) + (i * 7)) % 480);
            Canvas.Disc(span, x, y, 2 + (int)((rng >> 16) % 2), Canvas.Rgb(0xF4, 0xFA, 0xFF));
        }
    }

    private static void Dragon(Span<uint> span, uint ink, int x, int y, double s)
    {
        // A long neck, a horned head, a body with a wing, a tail: all cut paper.
        var flap = (int)(Math.Sin(s * 3.0) * 30);
        Canvas.Disc(span, x, y + 160, 70, ink);
        Canvas.Line(span, x - 40, y + 120, x - 140, y + 30, ink);
        for (var i = 1; i < 22; i++)
            Canvas.Line(span, x - 40 + (i / 2), y + 120 + i, x - 140 + (i / 2), y + 30 + i, ink);
        Canvas.Disc(span, x - 150, y + 20, 26, ink);
        Canvas.Line(span, x - 165, y, x - 190, y - 40, ink);
        Canvas.Line(span, x - 140, y - 2, x - 150, y - 45, ink);
        Canvas.Line(span, x - 176, y + 30, x - 230, y + 40, ink);
        for (var i = 0; i < 90; i++)
            Canvas.Fill(span, x - 10 + i, y + 110 - flap - (i * 2), 1, 60 + (i / 2), ink);
        Canvas.Line(span, x + 60, y + 180, x + 200, y + 240 + (int)(Math.Sin(s * 2.0) * 20), ink);
        for (var i = 1; i < 14; i++)
            Canvas.Line(span, x + 60, y + 180 + i, x + 200, y + 240 + i + (int)(Math.Sin(s * 2.0) * 20), ink);
        Canvas.Disc(span, x - 156, y + 14, 4, Canvas.Rgb(0xFF, 0xD0, 0x40));
    }

    private static void Burst(Span<uint> span, uint colour, int cx, int cy, double s, int count)
    {
        var rng = 1234567u;
        for (var i = 0; i < count; i++)
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            var phase = ((s * 1.4) + (i * 0.11)) % 1.0;
            var angle = (rng % 360) * Math.PI / 180.0;
            var reach = 220 * phase;
            Canvas.Disc(span, cx + (int)(Math.Cos(angle) * reach), cy + (int)(Math.Sin(angle) * reach * 0.7), Math.Max(1, (int)((1.0 - phase) * 7)), colour);
        }
    }

    private static void Housework(Span<uint> span, uint ink, int x, int ground, double done)
    {
        var h = (int)(done * 70);
        Canvas.Fill(span, x, ground - h, 90, h, ink);
        if (done >= 1.0)
        {
            for (var i = 0; i < 30; i++)
                Canvas.Fill(span, x - 6 + i, ground - 70 - i, 102 - (i * 2), 1, ink);
        }
    }

    private static void Spire(Span<uint> span, uint ink, int x, int ground, int height)
    {
        Canvas.Fill(span, x - 14, ground - (height * 2 / 3), 28, height * 2 / 3, ink);
        for (var i = 0; i < height / 3; i++)
            Canvas.Fill(span, x - (i * 14 / (height / 3)), ground - height + i, (i * 28 / (height / 3)) + 1, 1, ink);
    }

    private static void Trees(Span<uint> span, uint ink, int x0, int count, double s)
    {
        for (var i = 0; i < count; i++)
        {
            var x = x0 + (i * 90) + (int)(Math.Sin(s * 0.5 + i) * 2);
            var sc = 2 + (i % 2);
            Canvas.Fill(span, x - (2 * sc), Ground - (18 * sc), 4 * sc, 18 * sc, ink);
            Canvas.Disc(span, x, Ground - (22 * sc), 10 * sc, ink);
            Canvas.Disc(span, x - (4 * sc), Ground - (18 * sc), 7 * sc, ink);
        }
    }

    private static void BigTree(Span<uint> span, uint ink, int x, int ground)
    {
        Canvas.Fill(span, x - 18, ground - 220, 36, 220, ink);
        Canvas.Disc(span, x, ground - 260, 90, ink);
        Canvas.Disc(span, x - 60, ground - 220, 60, ink);
        Canvas.Disc(span, x + 60, ground - 230, 65, ink);
    }

    private static void Eyes(Span<uint> span, int x, int y, double open, uint paper)
    {
        if (open <= 0)
            return;
        var r = (int)(open * 12);
        Canvas.Disc(span, x - 40, y, r, Canvas.Rgb(0xFF, 0xD0, 0x40));
        Canvas.Disc(span, x + 40, y, r, Canvas.Rgb(0xFF, 0xD0, 0x40));
    }

    private static void Garden(Span<uint> span, uint ink, int x, int ground)
    {
        for (var i = 0; i < 5; i++)
            Canvas.Disc(span, x + (i * 40), ground - 14, 14 + ((i * 7) % 8), ink);
    }

    private static void Ridge(Span<uint> span, uint ink, int height)
    {
        for (var x = 0; x < W; x++)
        {
            var h = (int)((Math.Sin(x * 0.004) * 0.5 + Math.Sin(x * 0.011) * 0.3 + 0.8) * height / 1.6);
            Canvas.Fill(span, x, Ground - h, 1, h, ink);
        }
    }

    private static void Roads(Span<uint> span, uint paper, double s)
    {
        for (var x = 0; x < W; x++)
        {
            var y = Ground - 40 + (int)(Math.Sin((x * 0.01) + (s * 0.2)) * 16);
            Canvas.Fill(span, x, y, 1, 4, Canvas.Lerp(paper, Canvas.White, 0.4f));
            var y2 = Ground - 110 + (int)(Math.Sin((x * 0.007) + 2.0) * 12);
            Canvas.Fill(span, x, y2, 1, 3, Canvas.Lerp(paper, Canvas.White, 0.25f));
        }
    }

    private static void Wolf(Span<uint> span, uint ink, int x, int ground, bool howl, double s)
    {
        Canvas.Fill(span, x - 70, ground - 80, 140, 60, ink);
        Canvas.Disc(span, x - 60, ground - 60, 34, ink);
        Canvas.Disc(span, x + 60, ground - 60, 36, ink);
        for (var i = 0; i < 4; i++)
            Canvas.Fill(span, x - 60 + (i * 38), ground - 30, 14, 30, ink);
        var hx = x - 96;
        var hy = howl ? ground - 130 - (int)(Math.Abs(Math.Sin(s * 2.0)) * 10) : ground - 96;
        Canvas.Disc(span, hx, hy, 24, ink);
        Canvas.Line(span, hx - 6, hy - 16, hx - 2, hy - 40, ink);
        Canvas.Line(span, hx + 10, hy - 16, hx + 16, hy - 40, ink);
        for (var i = 0; i < 12; i++)
            Canvas.Fill(span, hx - 40 - (howl ? 0 : i), hy + (howl ? -i : i / 2) - 4, 30, 1, ink);
        Canvas.Disc(span, hx - 8, hy - 4, 3, Canvas.Rgb(0xF0, 0xF0, 0xFF));
        Canvas.Line(span, x + 80, ground - 70, x + 130, ground - 110 + (int)(Math.Sin(s * 3.0) * 8), ink);
        Canvas.Line(span, x + 80, ground - 66, x + 130, ground - 106 + (int)(Math.Sin(s * 3.0) * 8), ink);
    }

    private static void Stones(Span<uint> span, uint ink, double s, int seed)
    {
        for (var i = 0; i < 7; i++)
        {
            var x = 120 + (i * 150) + ((i * 37 + seed * 19) % 60);
            Canvas.Disc(span, x, Ground - 12, 14 + ((i * 11 + seed) % 12), ink);
        }
    }

    private static void WindLines(Span<uint> span, uint ink, double s)
    {
        for (var i = 0; i < 6; i++)
        {
            var y = 140 + (i * 55);
            var x0 = (int)(((s * 180) + (i * 200)) % (W + 200)) - 200;
            Canvas.Line(span, x0, y, x0 + 120, y - 6, ink);
            Canvas.Line(span, x0 + 10, y + 12, x0 + 90, y + 6, ink);
        }
    }

    private static void River(Span<uint> span, double s, double length)
    {
        var w = (int)(length * W);
        for (var x = 0; x < w; x++)
        {
            var y = Ground - 60 + (int)(Math.Sin(x * 0.01) * 10);
            Canvas.Fill(span, x, y, 1, 40, Water);
            if ((x + (int)(s * 40)) % 40 < 12)
                Canvas.Fill(span, x, y + 8 + (int)(Math.Sin((x * 0.05) + s) * 4), 1, 2, Canvas.Rgb(0xC0, 0xE0, 0xFF));
        }
    }

    private static void Pages(Span<uint> span, uint paper, uint ink, int x, int y, int count, double s)
    {
        for (var i = 0; i < count; i++)
        {
            var px = x + (i * 30);
            var py = y - (int)(Math.Sin(s * 2.0 + i) * 4);
            Canvas.Fill(span, px, py, 22, 30, Canvas.Lerp(paper, Canvas.White, 0.6f));
            Canvas.Rect(span, px, py, 22, 30, ink, 1);
            for (var l = 0; l < 4; l++)
                Canvas.Fill(span, px + 4, py + 6 + (l * 6), 14, 1, ink);
        }
    }

    private static void FloatingPages(Span<uint> span, uint paper, uint ink, double s)
    {
        for (var i = 0; i < 6; i++)
        {
            var x = (int)(((s * 60) + (i * 210)) % (W + 40)) - 20;
            var y = Ground - 52 + (int)(Math.Sin((x * 0.01)) * 10) + (int)(Math.Sin(s * 3.0 + i) * 3);
            Canvas.Fill(span, x, y, 22, 16, Canvas.Lerp(paper, Canvas.White, 0.6f));
            Canvas.Rect(span, x, y, 22, 16, ink, 1);
        }
    }

    private static void Library(Span<uint> span, uint ink, int x, int ground)
    {
        Canvas.Fill(span, x, ground - 150, 200, 150, ink);
        for (var i = 0; i < 5; i++)
            Canvas.Fill(span, x + 16 + (i * 36), ground - 130, 14, 110, Canvas.Lerp(ink, Canvas.White, 0.12f));
        for (var i = 0; i < 20; i++)
            Canvas.Fill(span, x - 10 + i, ground - 150 - i, 220 - (i * 2), 1, ink);
    }

    private static void Sea(Span<uint> span, double s, double rough)
    {
        for (var y = 200; y < Ground + 40; y++)
        {
            var t = (y - 200) / 310f;
            Canvas.Fill(span, 0, y, W, 1, Canvas.Lerp(Canvas.Rgb(0x7A, 0xB0, 0xE0), Canvas.Rgb(0x2E, 0x5C, 0xA0), t));
        }

        for (var i = 0; i < 24; i++)
        {
            var y = 220 + (i * 13);
            var x = (int)(((s * (40 + (rough * 120))) + (i * 137)) % (W + 100)) - 50;
            var amp = 2 + (int)(rough * 10);
            Canvas.Fill(span, x, y + (int)(Math.Sin(s * 2 + i) * amp), 40 + (int)(rough * 40), 2, Canvas.Rgb(0xC8, 0xE4, 0xFF));
        }
    }

    private static void Ship(Span<uint> span, uint ink, int x, int y, float scale, double s, bool wrecked = false)
    {
        var w = (int)(120 * scale);
        var h = (int)(30 * scale);
        var roll = wrecked ? 20 : (int)(Math.Sin(s * 1.5 + x) * 3);
        for (var i = 0; i < h; i++)
            Canvas.Fill(span, x - (w / 2) + (i / 2), y + i + (roll * i / h), w - i, 1, ink);
        var mast = (int)(90 * scale);
        Canvas.Fill(span, x - 2, y - mast + (wrecked ? 30 : 0), 4, mast, ink);
        if (!wrecked)
        {
            for (var i = 0; i < mast * 2 / 3; i++)
                Canvas.Fill(span, x + 4, y - mast + 6 + i, (i * (int)(50 * scale)) / (mast * 2 / 3), 1, ink);
        }
        else
        {
            Canvas.Line(span, x, y - mast + 30, x + 40, y - 10, ink);
        }
    }

    private static void Chart(Span<uint> span, uint paper, uint ink, int x, int y, double drawn, double s)
    {
        Canvas.Fill(span, x, y, 200, 130, Canvas.Lerp(paper, Canvas.White, 0.6f));
        Canvas.Rect(span, x, y, 200, 130, ink, 2);
        var n = (int)(drawn * 60);
        for (var i = 0; i < n; i++)
        {
            var px = x + 10 + (i * 3);
            var py = y + 70 + (int)(Math.Sin(i * 0.2) * 20);
            Canvas.Fill(span, px, py, 3, 3, ink);
        }

        if (drawn > 0.5)
        {
            Canvas.Disc(span, x + 150, y + 30, 4, ink);
            Canvas.Disc(span, x + 40, y + 110, 4, ink);
        }
    }

    private static void Hourglass(Span<uint> span, uint ink, int x, int y, int size, double s, double sand, double tipped = 0.0)
    {
        if (size <= 0)
            return;
        var h = size;
        for (var i = 0; i < h; i++)
        {
            var w = Math.Abs(i - (h / 2)) * size / h + 4;
            Canvas.Fill(span, x - w, y - h + i, w * 2, 1, Canvas.Lerp(ink, Canvas.White, 0.75f));
            Canvas.Fill(span, x - w, y - h + i, 2, 1, ink);
            Canvas.Fill(span, x + w - 2, y - h + i, 2, 1, ink);
        }

        Canvas.Fill(span, x - (size / 2) - 6, y - h - 6, size + 12, 6, ink);
        Canvas.Fill(span, x - (size / 2) - 6, y, size + 12, 6, ink);
        // Sand: the top drains as the bottom fills.
        var top = (int)((1.0 - sand) * (h / 2 - 8));
        var bottom = (int)(sand * (h / 2 - 8));
        for (var i = 0; i < top; i++)
        {
            var yy = y - (h / 2) - 4 - i;
            var w = Math.Abs(yy - (y - h + h / 2)) * size / h;
            Canvas.Fill(span, x - w + 2, yy, (w * 2) - 4, 1, Canvas.Rgb(0xE0, 0xC0, 0x70));
        }

        for (var i = 0; i < bottom; i++)
        {
            var yy = y - 2 - i;
            var w = Math.Abs(yy - (y - h + h / 2)) * size / h;
            Canvas.Fill(span, x - w + 2, yy, (w * 2) - 4, 1, Canvas.Rgb(0xE0, 0xC0, 0x70));
        }

        if (sand is > 0.0 and < 1.0)
            Canvas.Fill(span, x - 1, y - (h / 2), 2, h / 2 - 2 - bottom, Canvas.Rgb(0xE0, 0xC0, 0x70));
    }

    private static void Threads(Span<uint> span, uint colour, int fromX, int fromY, int count, double s, double length)
    {
        for (var i = 0; i < count; i++)
        {
            var toX = 200 + (i * 90);
            var toY = 150 + ((i * 37) % 120);
            var n = (int)(length * 100);
            for (var k = 0; k < n; k++)
            {
                var t = k / 100.0;
                var x = fromX + (int)((toX - fromX) * t);
                var y = fromY + (int)((toY - fromY) * t) + (int)(Math.Sin((t * 10) + s * 2 + i) * 8);
                Canvas.Fill(span, x, y, 3, 2, colour);
            }
        }
    }

    private static void Loom(Span<uint> span, uint colour, uint ink, double s, bool knot, double mended)
    {
        Canvas.Fill(span, 160, 120, 8, 320, ink);
        Canvas.Fill(span, 1120, 120, 8, 320, ink);
        Canvas.Fill(span, 160, 120, 968, 8, ink);
        for (var i = 0; i < 12; i++)
        {
            var y = 150 + (i * 24);
            for (var x = 170; x < 1120; x += 3)
            {
                var wob = (int)(Math.Sin((x * 0.02) + s + i) * 2);
                var near = knot && Math.Abs(x - 640) < 60 && i is >= 4 and <= 6;
                Canvas.Fill(span, x, y + wob + (near ? (int)(Math.Sin(x * 0.3 + i) * 12) : 0), 3, 2, colour);
            }
        }

        if (knot)
        {
            Knot(span, colour, ink, 640, 270, s);
            var extra = (int)(mended * 3);
            for (var i = 0; i < extra; i++)
            {
                var y = 240 + (i * 30);
                for (var x = 170; x < 1120; x += 3)
                    Canvas.Fill(span, x, y + (int)(Math.Sin((x * 0.03) + s) * 3), 3, 3, Canvas.Lerp(colour, Canvas.White, 0.3f));
            }
        }
    }

    private static void Knot(Span<uint> span, uint colour, uint ink, int x, int y, double s)
    {
        for (var a = 0; a < 720; a += 3)
        {
            var rad = a * Math.PI / 180.0;
            var r = 18 + (a / 40);
            Canvas.Disc(span, x + (int)(Math.Cos(rad + s * 0.3) * r), y + (int)(Math.Sin(rad * 1.3 + s * 0.3) * r * 0.7), 3, colour);
        }
    }

    private static void Houses(Span<uint> span, uint ink, int x0, int ground, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var x = x0 + (i * 120);
            Canvas.Fill(span, x, ground - 60, 80, 60, ink);
            for (var k = 0; k < 24; k++)
                Canvas.Fill(span, x - 6 + k, ground - 60 - k, 92 - (k * 2), 1, ink);
        }
    }

    private static void Thief(Span<uint> span, uint ink, int x, int ground)
    {
        Mortal(span, ink, x, ground, 0.5f, false);
        Canvas.Fill(span, x + 34, ground - 60, 22, 22, ink);
    }

    private static void Priest(Span<uint> span, uint ink, int x, int ground)
    {
        Mortal(span, ink, x, ground, 0.55f, false);
        Canvas.Fill(span, x + 12, ground - 150, 20, 30, ink);
    }

    private static void King(Span<uint> span, uint ink, int x, int ground, double s, bool small = false)
    {
        var sc = small ? 0.28f : 0.6f;
        Mortal(span, ink, x, ground, sc, false);
        var top = ground - (int)(200 * sc) + (int)(6 * sc);
        var cx = x + (int)(40 * sc);
        for (var i = 0; i < 4; i++)
            Canvas.Fill(span, cx - 18 + (i * 10), top - 22, 5, 22, Canvas.Rgb(0xE0, 0xB8, 0x4A));
        Canvas.Fill(span, cx - 20, top - 4, 40, 6, Canvas.Rgb(0xE0, 0xB8, 0x4A));
    }

    private static void Chapel(Span<uint> span, uint ink, int x, int ground)
    {
        Canvas.Fill(span, x, ground - 110, 120, 110, ink);
        for (var k = 0; k < 40; k++)
            Canvas.Fill(span, x - 8 + k, ground - 110 - k, 136 - (k * 2), 1, ink);
        Canvas.Fill(span, x + 52, ground - 190, 16, 50, ink);
        Canvas.Fill(span, x + 44, ground - 176, 32, 6, ink);
        Canvas.Fill(span, x + 48, ground - 60, 24, 60, Canvas.Lerp(ink, Canvas.White, 0.5f));
    }

    private static void FallenTree(Span<uint> span, uint ink, int x, int y, double split)
    {
        var gap = (int)(split * 40);
        Canvas.Fill(span, x, y - 30 - gap, 300, 24, ink);
        Canvas.Fill(span, x, y - 4 + gap, 300, 24, ink);
        Canvas.Disc(span, x, y, 22, ink);
    }

    private static void City(Span<uint> span, uint ink, int x0, int ground, int count, double built = 1.0)
    {
        for (var i = 0; i < count; i++)
        {
            var h = (int)((90 + ((i * 53) % 110)) * Math.Clamp((built * count) - i, 0.0, 1.0));
            Canvas.Fill(span, x0 + (i * 70), ground - h, 56, h, ink);
        }
    }

    private static void Headland(Span<uint> span, uint ink, int x, int ground)
    {
        for (var y = 200; y < ground; y++)
            Canvas.Fill(span, x - ((y - 200) * 3 / 4), y, W, 1, ink);
    }

    private static void Rocks(Span<uint> span, uint ink, int x, int y)
    {
        Canvas.Disc(span, x, y + 30, 40, ink);
        Canvas.Disc(span, x + 60, y + 40, 30, ink);
        Canvas.Disc(span, x - 50, y + 44, 26, ink);
    }

    private static void Rain(Span<uint> span, uint ink, double s)
    {
        var rng = 424242u;
        for (var i = 0; i < 160; i++)
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            var x = (int)(rng % (uint)W);
            var y = 60 + (int)(((rng >> 8) % 480 + (s * 600)) % 480);
            Canvas.Line(span, x, y, x - 6, y + 18, Canvas.Lerp(ink, Canvas.White, 0.5f));
        }
    }

    private static void Arrow(Span<uint> span, uint ink, int x, int y, double flight, int dir)
    {
        if (flight <= 0)
            return;
        var ax = x + (int)(dir * flight * 500);
        var ay = y - (int)(Math.Sin(flight * Math.PI) * 220) + (int)(flight * 60);
        Canvas.Line(span, ax, ay, ax - (dir * 40), ay + 10, ink);
        Canvas.Line(span, ax, ay + 1, ax - (dir * 40), ay + 11, ink);
        Canvas.Line(span, ax, ay, ax - (dir * 10), ay - 8, ink);
        Canvas.Line(span, ax, ay, ax - (dir * 12), ay + 8, ink);
    }

    private static void LakeWater(Span<uint> span, double s, int x, int y, int w, double reveal)
    {
        var rw = (int)(w * reveal);
        for (var i = 0; i < 40; i++)
        {
            var half = (int)(Math.Sqrt(1.0 - Math.Pow((i - 20) / 20.0, 2)) * rw / 2);
            Canvas.Fill(span, x + (w / 2) - half, y + i, half * 2, 1, Canvas.Lerp(Water, Canvas.White, 0.2f));
        }
    }

    private static void Clouds(Span<uint> span, uint paper, double s)
    {
        for (var i = 0; i < 5; i++)
        {
            var x = (int)(((s * 15) + (i * 260)) % (W + 300)) - 150;
            var y = 380 + ((i * 23) % 40);
            Canvas.Disc(span, x, y, 40, Canvas.Lerp(paper, Canvas.White, 0.5f));
            Canvas.Disc(span, x + 50, y - 10, 50, Canvas.Lerp(paper, Canvas.White, 0.5f));
            Canvas.Disc(span, x + 110, y, 38, Canvas.Lerp(paper, Canvas.White, 0.5f));
        }
    }

    private static void RubbleField(Span<uint> span, uint ink, double s)
    {
        for (var i = 0; i < 12; i++)
        {
            var x = 100 + (i * 95) + ((i * 31) % 40);
            var h = 20 + ((i * 17) % 50);
            for (var k = 0; k < h; k++)
                Canvas.Fill(span, x - (k / 2), Ground - h + k, k + ((i % 3) * 8), 1, ink);
        }
    }

    private static void Comet(Span<uint> span, uint colour, uint ink, int x, int y, double flight, bool held = false, double s = 0)
    {
        Canvas.Disc(span, x, y, held ? 22 : 18, colour);
        Canvas.Disc(span, x, y, held ? 12 : 9, Canvas.White);
        for (var i = 1; i < 12; i++)
            Canvas.Disc(span, x + (held ? -i * 6 : i * 10), y - (held ? i * 8 : i * 9), Math.Max(1, 12 - i), Canvas.Lerp(colour, ink, 0.3f));
    }

    private static void Hill(Span<uint> span, uint ink, int cx, int ground, int height)
    {
        for (var i = 0; i < height; i++)
        {
            var half = (int)(Math.Sqrt(1.0 - Math.Pow(1.0 - (i / (double)height), 2)) * height * 1.6);
            Canvas.Fill(span, cx - half, ground - height + i, half * 2, 1, ink);
        }
    }

    private static void Wall(Span<uint> span, uint ink, int x, int ground, int courses, double partial)
    {
        for (var c = 0; c < courses; c++)
        {
            var y = ground - ((c + 1) * 16);
            var w = c == courses - 1 && partial < 1.0 ? (int)(180 * partial) : 180;
            Canvas.Fill(span, x, y, w, 14, ink);
        }
    }

    private static void HouseTop(Span<uint> span, uint ink, int x, int y, double done, double s)
    {
        var roof = (int)(done * 40);
        for (var i = 0; i < roof; i++)
            Canvas.Fill(span, x - 10 + (i * 100 / 40), y - i, 200 - (i * 200 / 40), 1, ink);
        if (done > 0.6)
            Canvas.Fill(span, x + 76, y + 40, 28, 56, Canvas.Lerp(ink, Canvas.White, 0.5f));
        if (done >= 1.0)
            Canvas.Disc(span, x + 90, y + 20, 6 + (int)(Math.Sin(s * 6.0) * 2), Ember);
    }

    private static void PalaceShape(Span<uint> span, uint ink, int x, int ground, double standing, double s, bool cracks)
    {
        var h = (int)(200 * standing);
        Canvas.Fill(span, x, ground - h, 280, h, ink);
        if (standing > 0.9)
        {
            Canvas.Fill(span, x + 100, ground - 260, 80, 60, ink);
            Canvas.Disc(span, x + 140, ground - 270, 40, ink);
            Canvas.Fill(span, x + 20, ground - 240, 30, 40, ink);
            Canvas.Fill(span, x + 230, ground - 240, 30, 40, ink);
        }

        if (cracks && standing > 0.9)
        {
            Canvas.Line(span, x + 60, ground, x + 90, ground - 60, Canvas.Lerp(ink, Canvas.White, 0.4f));
            Canvas.Line(span, x + 90, ground - 60, x + 70, ground - 110, Canvas.Lerp(ink, Canvas.White, 0.4f));
        }
    }

    private static void Kneeling(Span<uint> span, uint ink, int x, int ground, bool dig = false, double s = 0)
    {
        var dip = dig ? (int)(Math.Abs(Math.Sin(s * 4.0 + x)) * 8) : 0;
        Canvas.Disc(span, x, ground - 60 + dip, 14, ink);
        Canvas.Fill(span, x - 18, ground - 48 + dip, 36, 48 - dip, ink);
        Canvas.Fill(span, x - 30, ground - 14, 60, 14, ink);
    }

    private static void Boulder(Span<uint> span, uint ink, int x, int y, double whole)
    {
        if (whole >= 1.0)
        {
            Canvas.Disc(span, x, y - 30, 36, ink);
            return;
        }

        Canvas.Disc(span, x - 30, y - 12, 16, ink);
        Canvas.Disc(span, x + 26, y - 14, 18, ink);
        Canvas.Disc(span, x + 2, y - 8, 10, ink);
    }

    private static void Dunes(Span<uint> span, uint ink, double s)
    {
        for (var x = 0; x < W; x++)
        {
            var h = (int)((Math.Sin(x * 0.006) * 0.5 + 0.5) * 60) + 10;
            Canvas.Fill(span, x, Ground - h, 1, h, ink);
        }
    }

    private static void Stalls(Span<uint> span, uint ink, int x0, int ground, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var x = x0 + (i * 130);
            Canvas.Fill(span, x, ground - 50, 100, 50, ink);
            Canvas.Fill(span, x - 4, ground - 110, 4, 60, ink);
            Canvas.Fill(span, x + 100, ground - 110, 4, 60, ink);
            for (var k = 0; k < 16; k++)
                Canvas.Fill(span, x - 10 + k, ground - 110 - k, 120 - (k * 2), 1, ink);
        }
    }

    private static void ScalesSmall(Span<uint> span, uint ink, int x, int y, int tip)
    {
        Canvas.Fill(span, x - 2, y - 40, 4, 40, ink);
        Canvas.Line(span, x - 30, y - 40 - tip, x + 30, y - 40 + tip, ink);
        Canvas.Fill(span, x - 40, y - 26 - tip, 20, 4, ink);
        Canvas.Fill(span, x + 20, y - 26 + tip, 20, 4, ink);
    }

    private static void Scales(Span<uint> span, uint ink, int x, int y, int tip, double s)
    {
        Canvas.Fill(span, x - 4, y - 140, 8, 140, ink);
        Canvas.Fill(span, x - 40, y, 80, 8, ink);
        for (var i = -3; i <= 3; i++)
            Canvas.Line(span, x - 90, y - 140 - tip + i, x + 90, y - 140 + tip + i, ink);
        Canvas.Line(span, x - 90, y - 140 - tip, x - 90, y - 90 - tip, ink);
        Canvas.Line(span, x + 90, y - 140 + tip, x + 90, y - 90 + tip, ink);
        Canvas.Fill(span, x - 120, y - 90 - tip, 60, 8, ink);
        Canvas.Fill(span, x + 60, y - 90 + tip, 60, 8, ink);
    }

    private static void Book(Span<uint> span, uint paper, uint ink, int x, int y, double s)
    {
        Canvas.Fill(span, x, y, 120, 80, Canvas.Lerp(paper, Canvas.White, 0.6f));
        Canvas.Rect(span, x, y, 120, 80, ink, 2);
        Canvas.Fill(span, x + 59, y, 2, 80, ink);
        var lines = 4 + (int)(s % 3);
        for (var l = 0; l < lines; l++)
        {
            Canvas.Fill(span, x + 8, y + 10 + (l * 12), 44, 2, ink);
            Canvas.Fill(span, x + 68, y + 10 + (l * 12), 44, 2, ink);
        }
    }

    private static void CityGate(Span<uint> span, uint ink, int x, int ground)
    {
        Canvas.Fill(span, x, ground - 200, 60, 200, ink);
        Canvas.Fill(span, x + 200, ground - 200, 60, 200, ink);
        Canvas.Fill(span, x, ground - 220, 260, 30, ink);
        for (var i = 0; i < 40; i++)
            Canvas.Fill(span, x + 60 + (i / 2), ground - 190 + i, 140 - i, 1, Canvas.Lerp(ink, Canvas.White, 0.35f));
    }

    private static void Coffin(Span<uint> span, uint ink, int x, int y)
    {
        Canvas.Fill(span, x, y, 70, 30, ink);
    }

    private static void Tomb(Span<uint> span, uint ink, int x, int ground)
    {
        Canvas.Fill(span, x, ground - 100, 120, 100, ink);
        for (var k = 0; k < 30; k++)
            Canvas.Fill(span, x - 8 + k, ground - 100 - k, 136 - (k * 2), 1, ink);
        Canvas.Fill(span, x + 48, ground - 70, 24, 70, Canvas.Lerp(ink, Canvas.White, 0.2f));
    }

    private static void Spring(Span<uint> span, uint water, uint ink, int x, int ground, double up, double s)
    {
        if (up <= 0)
            return;
        var r = (int)(up * 40);
        Canvas.Disc(span, x, ground - 6, r + 10, ink);
        Canvas.Disc(span, x, ground - 8, r, water);
        for (var i = 0; i < 3; i++)
        {
            var h = (int)(up * (30 + (i * 14)) * (0.7 + (0.3 * Math.Abs(Math.Sin(s * 3.0 + i)))));
            Canvas.Fill(span, x - 14 + (i * 12), ground - 8 - h, 4, h, water);
        }
    }

    private static void GrowingTree(Span<uint> span, uint ink, uint green, int x, int ground, double grow)
    {
        if (grow <= 0)
            return;
        var h = (int)(grow * 220);
        Canvas.Fill(span, x - 12, ground - h, 24, h, ink);
        if (grow > 0.4)
        {
            var r = (int)((grow - 0.4) / 0.6 * 80);
            Canvas.Disc(span, x, ground - h, r, ink);
            Canvas.Disc(span, x - 40, ground - h + 30, r * 2 / 3, ink);
            Canvas.Disc(span, x + 44, ground - h + 24, r * 2 / 3, ink);
            Canvas.Disc(span, x + 10, ground - h - 10, r / 2, Canvas.Lerp(ink, green, 0.5f));
        }
    }

    private static void Table(Span<uint> span, uint ink, uint paper, int x, int ground)
    {
        Canvas.Fill(span, x, ground - 60, 160, 8, ink);
        Canvas.Fill(span, x + 10, ground - 52, 8, 52, ink);
        Canvas.Fill(span, x + 142, ground - 52, 8, 52, ink);
        for (var i = 0; i < 3; i++)
            Canvas.Disc(span, x + 40 + (i * 40), ground - 68, 12, Canvas.Lerp(paper, Canvas.Rgb(0xC8, 0x8A, 0x4A), 0.6f));
    }

    private static void Hole(Span<uint> span, uint ink, uint paper, int x, int ground, double depth)
    {
        var d = (int)(depth * 60);
        for (var i = 0; i < d; i++)
            Canvas.Fill(span, x - 60 + (i / 2), ground - 40 + i, 120 - i, 1, Canvas.Lerp(paper, ink, 0.6f));
        Canvas.Disc(span, x - 90, ground - 12, 16 + (d / 4), ink);
        Canvas.Disc(span, x + 90, ground - 12, 16 + (d / 4), ink);
    }

    private static void MirrorShape(Span<uint> span, uint ink, uint paper, int x, int ground)
    {
        Canvas.Fill(span, x, ground - 200, 90, 200, ink);
        Canvas.Fill(span, x + 8, ground - 192, 74, 170, Canvas.Lerp(paper, Canvas.White, 0.7f));
    }

    /// <summary>A mortal as a cutout: a hooded head, a cloak to the ground, a staff or a raised hand; stooped when old.</summary>
    public static void Mortal(Span<uint> span, uint ink, int x, int ground, float scale, bool staff, float stoop = 0f)
    {
        var h = (int)(200 * scale);
        var top = ground - h + (int)(stoop * 30 * scale);
        var cx = x + (int)(40 * scale) + (int)(stoop * 16 * scale);
        var headR = (int)(22 * scale);
        var headY = top + headR + (int)(10 * scale);
        Canvas.Disc(span, cx, headY, headR, ink);

        var shoulderY = headY + headR - (int)(4 * scale);
        for (var y = shoulderY; y < ground; y++)
        {
            var t = (float)(y - shoulderY) / Math.Max(1, ground - shoulderY);
            var half = (int)((26 + (t * 22)) * scale);
            Canvas.Fill(span, cx - half - (int)(stoop * t * 10 * scale), y, half * 2, 1, ink);
        }

        if (staff)
        {
            var sx = cx - (int)(40 * scale);
            Canvas.Fill(span, sx, top - (int)(10 * scale), (int)(5 * scale), ground - top + (int)(10 * scale), ink);
            Canvas.Disc(span, sx + (int)(2 * scale), top - (int)(10 * scale), (int)(6 * scale), ink);
        }
        else
        {
            Canvas.Line(span, cx + (int)(26 * scale), shoulderY + (int)(30 * scale), cx + (int)(56 * scale), shoulderY - (int)(20 * scale), ink);
            Canvas.Line(span, cx + (int)(27 * scale), shoulderY + (int)(31 * scale), cx + (int)(57 * scale), shoulderY - (int)(19 * scale), ink);
            Canvas.Disc(span, cx + (int)(56 * scale), shoulderY - (int)(20 * scale), (int)(5 * scale), ink);
        }
    }
}
