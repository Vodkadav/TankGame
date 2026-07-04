using System;
using System.Collections.Generic;
using Godot;
using Chickensoft.GoDotTest;
using TankGame.Infrastructure;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class SoloMapSelectionTests : TestClass
{
    private static readonly ArenaId[] BuiltIns = { ArenaId.DesertWar, ArenaId.CliffsAndValleys };

    public SoloMapSelectionTests(Node testScene) : base(testScene) { }

    [Test]
    public void Pick_WithNoCreatedMaps_AlwaysPicksABuiltIn_SoTheBuiltInArenaStaysTheFallback()
    {
        var rng = new Random(1);
        var noMaps = new List<StoredMap>();

        for (var i = 0; i < 100; i++)
        {
            if (SoloMapSelection.Pick(BuiltIns, noMaps, rng) is not SoloMapSelection.BuiltIn)
            {
                throw new Exception("With no created maps, every Solo pick must be a built-in arena.");
            }
        }
    }

    [Test]
    public void Pick_DrawsFromTheWholePool_BothBuiltInsAndCreatedMaps()
    {
        var created = new[] { new StoredMap("my_map", "My Map") };
        var rng = new Random(42);
        var sawBuiltIn = false;
        var sawCreated = false;

        for (var i = 0; i < 200; i++)
        {
            switch (SoloMapSelection.Pick(BuiltIns, created, rng))
            {
                case SoloMapSelection.BuiltIn:
                    sawBuiltIn = true;
                    break;
                case SoloMapSelection.Created c:
                    sawCreated = true;
                    if (c.MapId != "my_map")
                    {
                        throw new Exception($"A created pick must carry the map's id; was '{c.MapId}'.");
                    }

                    break;
            }
        }

        if (!sawCreated)
        {
            throw new Exception("Created maps must be in the random pool — a created map was never picked.");
        }
        if (!sawBuiltIn)
        {
            throw new Exception("Built-in arenas must stay in the pool — a built-in was never picked.");
        }
    }
}
