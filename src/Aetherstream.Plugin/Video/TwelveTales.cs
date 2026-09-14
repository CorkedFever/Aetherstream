namespace Aetherstream.Plugin.Video;

/// <summary>What the god does on a page, which is what the plate animates.</summary>
internal enum Beat
{
    /// <summary>The god comes in from the right and takes their place.</summary>
    Arrive,

    /// <summary>The god works their element: it streams from them over the country.</summary>
    Work,

    /// <summary>The god strikes: a lunge, a flash, and the ground shakes.</summary>
    Strike,

    /// <summary>Another of the Twelve comes in from the left, and the two face each other.</summary>
    Meet,

    /// <summary>The god blesses: the halo swells and the mortals bow.</summary>
    Bless,

    /// <summary>The god goes: up and away into the sky.</summary>
    Depart,
}

/// <summary>
/// The tonberry's tales of the Twelve: stories in which the gods are the ones doing things,
/// made up in the spirit of the setting's own lore. Each stays on what the game says of its god
/// — domain, element, symbol, the city that keeps the shrine, the moon each rules, and the few
/// kinships everyone knows — and invents the rest the way a fireside teller would. Nothing is
/// quoted from the game's text.
/// </summary>
internal static class TwelveTales
{
    /// <summary>A tale: its god (1 Halone … 12 Althyk), title, six pages with a beat each, where it is set, and who else appears.</summary>
    public sealed record Tale(int Deity, string Title, Biome Where, int Other, (string Text, Beat Beat)[] Pages);

    public static readonly Tale[] All =
    [
        // -- Halone, the Fury: ice, the spear, the First Astral Moon, Ishgard's ---------------------------------------
        new(1, "HALONE AND THE FIRST WINTER", Biome.Snow, 0,
        [
            ("The mountain had no winter until Halone climbed it. She came up out of the lowlands with her spear on her shoulder, looking for somewhere hard enough to be worth standing on.", Beat.Arrive),
            ("At the top she drove the spear into the rock, and the cold came out of the wound: not a cruel cold, the tellers say, but a testing one, the kind that asks what you are made of.", Beat.Work),
            ("A dragon came to argue the point. It had held the high places since before the gods had names, and it did not care for a goddess putting her spear in its mountain.", Beat.Strike),
            ("They fought for a full moon's turn, and the snow that fell on that fight is still falling. When it ended the dragon had learned the mountain was no longer only its own.", Beat.Work),
            ("Then the people came, half-frozen, and Halone did not warm them. She showed them how to build against the wind, and told them the cold would make them the kind who could.", Beat.Bless),
            ("She left the spear in the rock. The Ishgardians say the spires of the Holy See are copies of it, raised so the Fury can find her people from the sky.", Beat.Depart),
        ]),
        new(1, "THE FURY AT THE MATRON'S GATE", Biome.Forest, 11,
        [
            ("Halone came down out of the snow to the Twelveswood, because she had heard the Matron kept a garden with no wall, and she did not believe it.", Beat.Arrive),
            ("Nophica met her at the edge of the trees, unarmed, with soil on her hands, and asked the Fury what she wanted with a garden.", Beat.Meet),
            ("I want to know who guards it, said Halone. Nophica said: the Wood guards it. Halone said: the Wood is not a wall. Nophica said: no, it is better, it grows back.", Beat.Meet),
            ("To prove her point the Fury struck the nearest tree with the butt of her spear, and the tree shook, and the Wood shook with it, and something very old woke up and looked at her.", Beat.Strike),
            ("Halone, who fears nothing, did not fear it either, but she understood it, and she put up her spear, and the Matron laughed for the first time in the telling.", Beat.Bless),
            ("They did not part friends. But the Fury went back to her mountain thinking, and the Ishgardians say that is why their cathedral has a garden, small and cold and stubborn.", Beat.Depart),
        ]),

        // -- Menphina, the Lover: ice, the moon, the First Umbral Moon ------------------------------------------------
        new(2, "MENPHINA LIGHTS THE LAMP", Biome.Highland, 6,
        [
            ("Before there was a moon the nights were black, and Menphina, who is the Lover, walked out into one of them because someone she loved had walked into it first.", Beat.Arrive),
            ("Oschon the Wanderer had gone over the ridge at dusk, as he does, and had not come back, as he does not, and the Lover could not see which way he had gone.", Beat.Meet),
            ("So she took the cold of her element and pressed it into a disc between her hands, and breathed on it, and it shone, and she hung it up in the sky to see by.", Beat.Work),
            ("The light fell over every road at once. It found the Wanderer on the far side of the ridge, and it found a hundred travellers who had also been lost, and it found their way home too.", Beat.Bless),
            ("Dalamud, the great wolf who walks at her side, put back his head and howled at it, and the tellers say that is why wolves have howled at the moon since.", Beat.Work),
            ("Oschon walked on. He always does. And the Lover took the lamp with her and followed, and she is following still, over every road he has ever taken.", Beat.Depart),
        ]),
        new(2, "THE LOVER AND THE WOLF", Biome.Snow, 0,
        [
            ("Dalamud was not always Menphina's. In the telling he was a wolf of the high snow, wild and alone, and he had never let anything near him that he could not eat.", Beat.Arrive),
            ("The Lover came up the snowfield with her lamp, and the wolf circled her, and she did not run, because she had come for him and she had all night.", Beat.Work),
            ("He lunged. She did not move. He lunged again and she did not move, and on the third he stopped short, because there is no sport in a thing that will not be afraid of you.", Beat.Strike),
            ("She held out a hand. The tellers disagree about what she said. Most agree it was not much, and that she meant every word, which is the Lover's way.", Beat.Bless),
            ("He put his head under her hand. That was the whole of it. He has walked at her side since, and he lets no one near her that she has not chosen, and she chooses nearly everyone.", Beat.Work),
            ("So when the Ishgardian says a hard heart can be won, she is thinking of the wolf. And when she says it takes all night, she is thinking of the Lover's patience.", Beat.Depart),
        ]),

        // -- Thaliak, the Scholar: water, the scroll, the Second Astral Moon, Sharlayan's ------------------------------
        new(3, "THALIAK CUTS THE RIVER", Biome.Marsh, 0,
        [
            ("There was no river through Sharlayan until Thaliak came looking for a place to keep things. He came with a scroll under one arm and a question on his face.", Beat.Arrive),
            ("The question was: how does a thing that is known stay known after the knower dies? He asked the stones, who did not answer, and the wind, who could not remember.", Beat.Work),
            ("So the Scholar drew a line in the ground with the edge of the scroll, and water followed the line, because water is his and goes where he says, and the river was cut.", Beat.Strike),
            ("Pour it in here, he told the first Sharlayans, and he showed them what pouring was: marks on a page, one after another, so the page could say them back.", Beat.Bless),
            ("The river carried the pages, and the pages carried the knowing, and the great library that stands beside the water is only the place the river left them.", Beat.Work),
            ("Thaliak went upstream. He is up there still, the tellers say, at the source, reading what comes down to him and making notes in the margin.", Beat.Depart),
        ]),
        new(3, "THE SCHOLAR AND THE NAVIGATOR", Biome.Coast, 5,
        [
            ("Thaliak came down to the sea, which is not his, because he had heard that Llymlaen's sailors were finding roads on it and not writing them down.", Beat.Arrive),
            ("The Navigator met him on the shore with salt in her hair and asked what a river god wanted with the ocean. The Scholar said: your charts. She said: what charts.", Beat.Meet),
            ("That was the trouble exactly. Her people found the way by star and swell and memory, and when a captain drowned the way drowned with her.", Beat.Meet),
            ("So the Scholar took a page from his scroll and laid it flat on the water, and it did not sink, and he showed her how a coast could be drawn on it, and a reef, and a star.", Beat.Work),
            ("Llymlaen was not a woman to be taught by a river. But she looked at the page for a long moment, and took it, and the tellers say that was the first chart in Eorzea.", Beat.Bless),
            ("They argue still about whose gift it was. The Lominsans say the Navigator's, the Sharlayans the Scholar's, and the chart does not care, and gets you home.", Beat.Depart),
        ]),

        // -- Nymeia, the Spinner: water, the spindle, the Second Umbral Moon -------------------------------------------
        new(4, "NYMEIA SPINS THE FIRST THREAD", Biome.Night, 12,
        [
            ("At the beginning there was the Keeper turning his glass, and nothing to live in the hours he made, and Nymeia came to him with a spindle and said: let me fix that.", Beat.Arrive),
            ("Althyk asked what a spindle was for. The Spinner said: for making one thing out of many. He said the hours were already one thing. She said: they are, and it is boring.", Beat.Meet),
            ("So she drew out a thread from nothing, twisting as she went, and where the thread ran there was a someone, and the someone had a beginning and would have an end.", Beat.Work),
            ("The Keeper watched the thread run through his hours and saw that it made them matter, and he did not say so, because he is the Keeper, but he turned the glass more gently.", Beat.Bless),
            ("She spun another, and another, and wove them as she went, and a cloth grew that no one thread could see the whole of, and that cloth is everyone.", Beat.Work),
            ("She has not stopped. The tellers say if you sit very still at the turn of the Second Umbral Moon you can feel the loom move, which is Nymeia, adding you in.", Beat.Depart),
        ]),
        new(4, "THE SPINNER'S KNOT", Biome.Marsh, 0,
        [
            ("A thread on Nymeia's loom went wrong. That is rare and it is not the Spinner's doing; a thread will sometimes twist against its neighbours out of nothing but stubbornness.", Beat.Arrive),
            ("It knotted three lives together that should have run apart: a fisher, a thief, and a priest who did not believe in the Spinner, in a marsh town by the water.", Beat.Work),
            ("The Spinner came down to look at it. She could have cut it. Cutting is easy. She turned the knot over in her fingers instead, and found it was not a mistake at all.", Beat.Work),
            ("The thief had stolen the fisher's boat to reach the priest's chapel to return a thing he had taken years before, and the fisher had followed to get the boat back, and had stayed.", Beat.Strike),
            ("Nymeia left the knot where it was. The tellers say she wove three more threads through it before she went, to hold it, because a good knot deserves company.", Beat.Bless),
            ("So when your life tangles with someone's it had no business tangling with, the Spinner's priests say: before you cut it, turn it over. She did.", Beat.Depart),
        ]),

        // -- Llymlaen, the Navigator: wind, the ship, the Third Astral Moon, Limsa's ------------------------------------
        new(5, "LLYMLAEN AND THE FIRST SHIP", Biome.Coast, 0,
        [
            ("The sea was Llymlaen's before anyone sailed it, and she was lonely on it, the tellers say, with no one to shout at.", Beat.Arrive),
            ("So she came ashore to where the fisherfolk stood knee-deep with their nets and looked at the horizon the way you look at a door you cannot open.", Beat.Work),
            ("The Navigator took a fallen tree and split it with a word, which is her element, the wind, put to a sharp use. She hollowed it and set a cloth on a pole above it.", Beat.Strike),
            ("Then she pushed it out past the breakers and stood in it, and the wind filled the cloth, and the fisherfolk saw a thing float that should not, and go where it was pointed.", Beat.Work),
            ("She came back and gave it to them. Point it, she said. Read the wind, watch the stars, and shout at the sea when it needs shouting at. It respects that.", Beat.Bless),
            ("Limsa Lominsa is what they built when they had enough ships to need a harbour. The Navigator's shrine there faces the water, so she can see them come home.", Beat.Depart),
        ]),
        new(5, "THE NAVIGATOR'S STORM", Biome.Coast, 0,
        [
            ("A fleet went out against Llymlaen's advice, which she gives in the colour of the sunset and the set of the swell, and which the admiral had decided not to read.", Beat.Arrive),
            ("The Navigator watched them go from the headland with her arms folded, and the tellers say she did not send the storm. She simply did not stand in its way.", Beat.Work),
            ("It hit them at the mouth of the strait. The admiral's ship broke on the rocks she had been warned of, and the rest turned, and ran, and could not find the harbour.", Beat.Strike),
            ("Then Llymlaen went down to the water. Not for the admiral. For the crews, who had only done as they were told, and she stood on the swell and raised her arm.", Beat.Bless),
            ("Every mast in the fleet turned toward her as a needle turns north, and the wind came round behind them, and they came in one by one past the wreck of the flagship.", Beat.Work),
            ("The Lominsans tell it to every new captain: the Navigator will bring your people home. She will not bring you, if you did not listen. Learn to read the sunset.", Beat.Depart),
        ]),

        // -- Oschon, the Wanderer: wind, the bow, the Third Umbral Moon --------------------------------------------------
        new(6, "OSCHON LOOSES THE ARROW", Biome.Steppe, 0,
        [
            ("When the gods divided the world Oschon took none of it. He said he would keep the space between the pieces, and the others let him, thinking it was nothing.", Beat.Arrive),
            ("It was the roads. It was every pass and ford and ferry crossing, every place a traveller sleeps that is no one's home. The Wanderer had taken the whole world's between.", Beat.Work),
            ("He walked it with a bow, and when he came to a fork he loosed an arrow over the ridge and went where it fell, and that, the tellers say, is why the roads wind.", Beat.Strike),
            ("A traveller once asked him which way was right. Oschon said he did not know and did not want to. She asked what use a god was who did not know the way. He said: company.", Beat.Bless),
            ("She walked with him a while. The road took the long way round a mountain and came out at a lake she would never have found, and she stopped asking.", Beat.Work),
            ("He went on, over the next ridge, after the next arrow. The steppe folk say if a road ever surprises you, that is not the road. That is the Wanderer, wanting you to see something.", Beat.Depart),
        ]),
        new(6, "THE WANDERER AND THE LOVER'S LAMP", Biome.Highland, 2,
        [
            ("Oschon does not stop. That is the whole of him. But there was one night in the telling when he did, on a ridge above the clouds, because the sky had changed.", Beat.Arrive),
            ("A light had come up behind him that had not been there before, and it lay on the road ahead as well as the road behind, and he had never in his life seen his own road lit.", Beat.Work),
            ("Menphina came over the ridge with the lamp in her hands and the wolf at her heel, and she did not say she had made it for him, and he did not say he knew.", Beat.Meet),
            ("They stood there a while, which for the Wanderer is a long time. The tellers say he asked if she meant to follow him everywhere. She said: only where there is a road.", Beat.Meet),
            ("He said there was always a road. She said: then yes. And he laughed, which he does not often, and loosed an arrow into the dark to see where it would take them both.", Beat.Bless),
            ("He walked on, as he must. But he walks slower on moonlit nights, the highland folk say, and looks back more than a wanderer should, and that is her doing.", Beat.Depart),
        ]),

        // -- Byregot, the Builder: lightning, the hammer, the Fourth Astral Moon ----------------------------------------
        new(7, "BYREGOT RAISES THE FIRST WALL", Biome.Highland, 0,
        [
            ("Byregot grew up in the telling watching his father Rhalgr break things, and by the time he could lift a hammer he had decided he would rather find out whether a thing could be made to stay.", Beat.Arrive),
            ("He came to a bare hill where the wind had knocked down everything the people put up, and he set his hammer down and looked at the ground for a long time before he touched it.", Beat.Work),
            ("Then he struck. The Builder's element is lightning, and the hammer came down with it in the head, and the stone it hit did not break; it fused, and stood, and the next stone fused to it.", Beat.Strike),
            ("Course on course he raised it, checking each with the flat of his hand, and when the wind came in the evening it hit the wall and went round, and the wall stayed.", Beat.Work),
            ("He built a roof on it and a door in it and lit a fire under the roof and put a family in it, and stood outside in the wind to make sure, and that was the first house.", Beat.Bless),
            ("The crafters' guilds keep his shrine and his creed both: make it twice as strong as it needs to be, because the wind is Rhalgr's cousin and it will come to check.", Beat.Depart),
        ]),
        new(7, "THE BUILDER AND THE DESTROYER", Biome.Steppe, 8,
        [
            ("Byregot went to find his father on the day the comet fell on the city he had spent a hundred years building, and he did not go quietly.", Beat.Arrive),
            ("Rhalgr was waiting on the hill above the rubble, as if he had expected it, which he had, because the Destroyer knows exactly what he has done and does not pretend otherwise.", Beat.Meet),
            ("The Builder asked why. The Destroyer said: it was finished. Byregot said: that is the point of building. Rhalgr said: no, that is the end of it. What comes next is the point.", Beat.Meet),
            ("They did not fight. The tellers are clear on this and it disappoints the children every time. The Builder looked at the rubble for a long moment and picked up his hammer.", Beat.Work),
            ("What he built on the broken ground was better than what had stood there, because he had learned something in the hundred years and the old city could not have held it.", Beat.Bless),
            ("Father and son do not agree, and never will, and take turns. Ala Mhigo was broken, and is being rebuilt, and the Builder's priests say: watch, this is the tale, in stone.", Beat.Depart),
        ]),

        // -- Rhalgr, the Destroyer: lightning, the comet, the Fourth Umbral Moon, Ala Mhigo's --------------------------
        new(8, "RHALGR'S COMET", Biome.Steppe, 0,
        [
            ("There was a king in the telling whose throne had grown too heavy for the land beneath it, and the land had begun to crack, and the king did not notice because he did not look down.", Beat.Arrive),
            ("Rhalgr came to the hill above the city with the comet in his hand. He did not hurry. The Destroyer never hurries; the ending is coming either way, and he is only the messenger.", Beat.Work),
            ("He raised it over his head, and the people in the streets saw the star with the tail and knew what it meant, because their grandmothers had painted it on the temple ceiling.", Beat.Bless),
            ("He threw it. The throne went, and the palace around it, and the wall that had grown too proud, and the crack in the land closed over because there was nothing heavy on it any more.", Beat.Strike),
            ("The field where the palace had stood was green by spring. The Mhigans went out and stood on it, and some wept and some did not, and all of them had been told.", Beat.Work),
            ("The Destroyer went back up the hill. His people keep his shrine not because he is kind but because he is honest: nothing stands forever, and he says so, and then he shows you.", Beat.Depart),
        ]),
        new(8, "RHALGR AND THE STONE", Biome.Highland, 0,
        [
            ("The monks who kept Rhalgr's temple came to him asking to be made strong, and the Destroyer looked at them for a while and then went and found a stone.", Beat.Arrive),
            ("Break it, he said. With the hand. They tried, and could not, and looked at him, and he said: in the morning, then. And he sat down on the hill to wait.", Beat.Work),
            ("He waited years. The tellers say he did not move and did not speak and did not once show them how it was done, because showing them was not what they had asked for.", Beat.Work),
            ("On the morning it broke, it broke under the hand of the youngest of them, who had never been the strongest and had only been the one who kept coming back.", Beat.Strike),
            ("Rhalgr stood up for the first time in years, and looked at the pieces, and looked at the monk, and nodded once, which from the Destroyer is a ceremony.", Beat.Bless),
            ("Then he left, and the Fist tells its novices the rest: the stone was never the point. What you became to break it was the point. He knew that on the first day.", Beat.Depart),
        ]),

        // -- Azeyma, the Warden: fire, the sun, the Fifth Astral Moon --------------------------------------------------
        new(9, "AZEYMA RAISES THE SUN", Biome.Desert, 0,
        [
            ("In the first days Azeyma carried the sun in her hands and shone it where she chose: on the honest stall, on the true word, and never on the thief.", Beat.Arrive),
            ("The thieves complained, which she expected. Then the honest complained, which she had not: for in a light that fell only on the good, no one could tell the good from the dark.", Beat.Work),
            ("So the Warden climbed the highest dune in the telling and threw the sun up over her head, and it did not come down, and it has fallen on everyone equally since.", Beat.Strike),
            ("Now it falls on the thief and the merchant alike, she told them, and I will not say which is which. Look. That is what I have given you: the looking.", Beat.Bless),
            ("The desert folk say that is why nothing hides at noon, when the Warden is straight overhead, and why the honest keep her shrine and the rest keep out of her light.", Beat.Work),
            ("She walks the sky behind it now, hand raised to shade her eyes, watching what her light finds. She does not judge it. She only makes sure it can be seen.", Beat.Depart),
        ]),
        new(9, "THE WARDEN AND THE TRADERS' LEDGER", Biome.Desert, 10,
        [
            ("Azeyma came down to Ul'dah, which keeps the Traders' shrine and not hers, because she had heard the Traders' scales were weighing things in the dark.", Beat.Arrive),
            ("Nald'thal met her at the gate, both of them, which they only do for another god. The Warden asked to see the scales. Nald said: they are true. Thal said: in any light.", Beat.Meet),
            ("So she held her sun up over the market, and the tellers say every scale in Ul'dah threw a shadow at once, and in the shadows you could see which had a thumb on them.", Beat.Work),
            ("Not the Traders'. Theirs stood level, and the Warden saw it, and lowered her hand, and said she had not doubted them, only the hands that borrowed their name.", Beat.Bless),
            ("Nald laughed. Thal did not. The tellers say Thal never does, and that he wrote the Warden's visit in his ledger under debts, because now the Traders owed the sun a look.", Beat.Meet),
            ("She left the light a little longer over Ul'dah's market than over anyone else's, the Ul'dahns say, and that is why the bazaar opens at dawn and no one there trusts the dusk.", Beat.Depart),
        ]),

        // -- Nald'thal, the Traders: fire, the scales, the Fifth Umbral Moon, Ul'dah's ---------------------------------
        new(10, "THE TRADERS DIVIDE THE WORLD", Biome.Desert, 0,
        [
            ("Nald'thal were born twins in the telling, and they could not agree on anything, and the gods, tired of it, gave them one thing between them to keep and told them to divide it.", Beat.Arrive),
            ("The thing was trade: every bargain that would ever be struck. Nald wanted all of it. Thal wanted all of it. They stood in the desert with the scales between them and argued for a moon.", Beat.Meet),
            ("Then Thal said: take the living. Nald said: what does that leave you. Thal said: what they carry when they are done. And Nald, who is quick, saw that was the larger half, and took the deal anyway.", Beat.Work),
            ("So Nald keeps the counter and the coin and the deal struck at noon, and Thal keeps the ledger of what it all cost, and they meet at the scales when a soul comes to be weighed.", Beat.Bless),
            ("They struck the bargain by shaking on it, and the scales tipped once each way and came level, and that, the Ul'dahns say, is the only time the twins have agreed on anything.", Beat.Strike),
            ("They went back into the city together, one to the market and one to the tombs, and Ul'dah keeps both shrines and prays to both, morning and night, and is careful.", Beat.Depart),
        ]),
        new(10, "THAL WEIGHS A KING", Biome.Desert, 0,
        [
            ("A sultan of Ul'dah died rich, in the telling, and came to Thal's scales sure of his place, with the deeds to half the city folded in his hand.", Beat.Arrive),
            ("Thal took the deeds and set them on the pan and they weighed nothing, and the sultan asked why, and the Trader said: I do not weigh what you owned. I weigh what you settled.", Beat.Work),
            ("The other pan held every debt the sultan had let stand, every wage he had let go unpaid, every bargain struck with a thumb on the scale. It was heavy, and it did not move.", Beat.Strike),
            ("A water-seller came after him with nothing in her hands, and Thal set her on the scale, and the pan went up, because she had owed little and paid all of it and a little over.", Beat.Bless),
            ("The sultan said that was not fair. Thal said: no. It is exact. Fair is my brother's business, at the counter, where you had a hundred years to visit him.", Beat.Work),
            ("So the Ul'dahn settles his debts before the Fifth Umbral Moon, and not because Thal is cruel. The Trader is only the most careful bookkeeper the world has, and the book is long.", Beat.Depart),
        ]),

        // -- Nophica, the Matron: earth, the spring, the Sixth Astral Moon, Gridania's ----------------------------------
        new(11, "NOPHICA OPENS THE SPRING", Biome.Forest, 0,
        [
            ("When the land was made and lay bare, the gods looked at it and said it was finished, and Nophica, who is the Matron, put down her sickle and said it was not.", Beat.Arrive),
            ("She walked out into the middle of it, where nothing grew, and knelt, and pressed her hand flat to the ground, and waited, the way you wait for something you have planted.", Beat.Work),
            ("The spring came up where she pressed. And from the spring a green, and from the green a tree, and from the tree a wood, and the Twelveswood is what grew from that one hand.", Beat.Bless),
            ("The other gods asked her what it was for. The Matron said: for whoever comes. Halone said no one was coming. Nophica said: then it will be ready when they do.", Beat.Work),
            ("Someone came. They were hungry, as people are, and there was already bread, because the Matron plants early, for a mouth she has not met, every time.", Beat.Bless),
            ("She went back to her sickle. Gridania keeps her shrine in the trees she grew, and the Gridanian says grace to the Matron before eating, because the bread was hers first.", Beat.Depart),
        ]),
        new(11, "THE MATRON AND THE DROUGHT", Biome.Forest, 0,
        [
            ("A dry year came to the Wood in the telling, the kind the Matron does not send but does not always prevent, and the spring sank and the green went brown.", Beat.Arrive),
            ("The Gridanians came to her shrine and asked her to make it rain. Nophica came out among them, and looked at the sky, and said the rain was not hers to make.", Beat.Work),
            ("But the earth was. She went down on her knees in the dry bed of the spring and dug with her hands, and the people watched, and one by one they knelt and dug too.", Beat.Work),
            ("They found water. Not the spring; a deeper one, colder, that the Matron had known was there and had not told them of, because they had never needed to look.", Beat.Strike),
            ("She showed them how to bring it up and how to keep it and how to share it, and the Wood came back, thinner that year, but back, and the people had done it.", Beat.Bless),
            ("So when the Gridanian is asked why they garden in a drought, they say the Matron does not stop the dry years. She teaches you to dig. That is the greater gift.", Beat.Depart),
        ]),

        // -- Althyk, the Keeper: earth, the hourglass, the Sixth Umbral Moon ---------------------------------------------
        new(12, "ALTHYK TURNS THE GLASS", Biome.Night, 0,
        [
            ("In the beginning there was a now that went nowhere, and Althyk stood in it, and found it unbearable, and the tellers say that is the first thing any god ever felt.", Beat.Arrive),
            ("He made a glass with two chambers and he filled one with sand, and he held it and looked at it a long while, because once he turned it there would be no untorning it.", Beat.Work),
            ("Then he turned it. The sand ran, and a grain that had fallen could not un-fall, and for the first time a thing that had happened stayed happened, and there was a before.", Beat.Strike),
            ("It was the hardest gift. Nothing could go back. But nothing was lost, either: the past was kept, in the lower chamber, and the Keeper is called that because he keeps it.", Beat.Bless),
            ("Nymeia came and spun a thread through his hours, and the hours mattered, and he did not say so, because he is the Keeper, but he has turned the glass gently since.", Beat.Work),
            ("He stands at the year's end with the glass in his hands, and the Sixth Umbral Moon is his, and when it turns the Sharlayan says thank you, because the year counted.", Beat.Depart),
        ]),
        new(12, "THE KEEPER AND THE IMPATIENT", Biome.Night, 0,
        [
            ("A young man came to Althyk's shrine and asked the Keeper to hurry the sand, because he wanted to be old and wise and had no patience for the years between.", Beat.Arrive),
            ("The Keeper looked at him for a long time, which the young man found unbearable, and then he did as he was asked, because a lesson refused is a lesson wasted.", Beat.Work),
            ("He tipped the glass. The sand ran like water. The young man was grey by morning and stooped by noon, and he went to a mirror and looked, and was not wise.", Beat.Strike),
            ("The wisdom was in the days, the Keeper told him, the ones inside the years, the ones you asked me to run past you. I told you. You were not listening.", Beat.Work),
            ("The old man asked to have them back. Althyk said: no one has them back. That is what a day is. But I kept them. They are in the lower chamber. You may look.", Beat.Bless),
            ("So the almanac counts the days one at a time and marks each and lets none run past, and the Sharlayan reads the Keeper's tale to children who will not sit still.", Beat.Depart),
        ]),
    ];

    /// <summary>The god's name, epithet, symbol and element by row id.</summary>
    public static (string Name, string Epithet, string Symbol, string Element) Of(int deity) => deity switch
    {
        1 => ("Halone", "the Fury", "spear", "ice"),
        2 => ("Menphina", "the Lover", "moon", "ice"),
        3 => ("Thaliak", "the Scholar", "scroll", "water"),
        4 => ("Nymeia", "the Spinner", "spindle", "water"),
        5 => ("Llymlaen", "the Navigator", "ship", "wind"),
        6 => ("Oschon", "the Wanderer", "bow", "wind"),
        7 => ("Byregot", "the Builder", "hammer", "lightning"),
        8 => ("Rhalgr", "the Destroyer", "comet", "lightning"),
        9 => ("Azeyma", "the Warden", "sun", "fire"),
        10 => ("Nald'thal", "the Traders", "scales", "fire"),
        11 => ("Nophica", "the Matron", "spring", "earth"),
        _ => ("Althyk", "the Keeper", "hourglass", "earth"),
    };
}
