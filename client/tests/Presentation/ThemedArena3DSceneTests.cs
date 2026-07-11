using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Smoke test for the five themed built-in arenas (Forest/Volcano/City/Frozen/Canyon): selecting each
// makes Arena3DScene build the layout and Terrain3DView render its materials (lava pools, bridge decks,
// building blocks, mountains, water) headless without crashing — and every map seats eight tanks.
public class ThemedArena3DSceneTests : TestClass
{
    private static readonly ArenaId[] ThemedArenas =
    {
        ArenaId.Forest, ArenaId.Volcano, ArenaId.City, ArenaId.Frozen, ArenaId.Canyon,
        ArenaId.Donut, ArenaId.Cross, ArenaId.Archipelago,
    };

    private ArenaId _previousArena;

    public ThemedArena3DSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _previousArena = GameSetup.Arena;
        GameSetup.CustomMap = null;
    }

    [Cleanup]
    public void Cleanup()
    {
        GameSetup.Arena = _previousArena;
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [Test]
    public void EveryThemedArena_BuildsAndRendersWithEightTanks()
    {
        foreach (var id in ThemedArenas)
        {
            GameSetup.Arena = id;
            var arena = GD.Load<PackedScene>("res://src/Presentation/Arena/Arena3D.tscn").Instantiate();
            TestScene.AddChild(arena); // runs Arena3DScene._Ready — must not throw on any material

            try
            {
                Terrain3DView? terrain = null;
                var tankViews = 0;
                foreach (var child in arena.GetChildren())
                {
                    switch (child)
                    {
                        case Terrain3DView t:
                            terrain = t;
                            break;
                        case Tank3DView:
                            tankViews++;
                            break;
                    }
                }

                if (terrain is null)
                {
                    throw new System.Exception($"{id} arena must render terrain via a Terrain3DView.");
                }

                if (tankViews < 8)
                {
                    throw new System.Exception(
                        $"{id} arena must seat eight tanks (player + seven AI); saw {tankViews}.");
                }
            }
            finally
            {
                arena.Free(); // free immediately (the Arena3D teardown-leak pattern) before the next map
                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
            }
        }
    }
}
