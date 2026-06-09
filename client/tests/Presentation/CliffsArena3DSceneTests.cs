using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Scene test for the Cliffs & Valleys multi-level map (ADR-0018 step 3): selecting it on the map browser
// makes Arena3DScene build the elevated arena, whose Terrain3DView must render the raised plateau (and so
// hold the elevation meshes) without crashing headless.
public class CliffsArena3DSceneTests : TestClass
{
    private Node _arena = default!;
    private ArenaId _previousArena;

    public CliffsArena3DSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _previousArena = GameSetup.Arena;
        GameSetup.CustomMap = null;
        GameSetup.Arena = ArenaId.CliffsAndValleys; // the seam MapSelectScene sets when Cliffs is chosen
        _arena = GD.Load<PackedScene>("res://src/Presentation/Arena/Arena3D.tscn").Instantiate();
        TestScene.AddChild(_arena); // runs Arena3DScene._Ready
    }

    // Free immediately (not deferred) and force a GC so no managed wrapper to a freed Godot resource
    // lingers to engine shutdown — the leak that trips the fatal teardown crash. Restore the seam.
    [Cleanup]
    public void Cleanup()
    {
        if (GodotObject.IsInstanceValid(_arena))
        {
            _arena.Free();
        }

        GameSetup.Arena = _previousArena;
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [Test]
    public void Cliffs_BuildsTheArena_WithRaisedPlateauMeshes()
    {
        Terrain3DView? terrain = null;
        var tankViews = 0;
        foreach (var child in _arena.GetChildren())
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

        if (tankViews < 2)
        {
            throw new System.Exception($"Cliffs arena must instance the player plus AI; saw {tankViews} tanks.");
        }

        if (terrain is null)
        {
            throw new System.Exception("Cliffs arena must render terrain via a Terrain3DView.");
        }

        var plateaus = 0;
        var ramps = 0;
        foreach (var node in terrain.GetChildren())
        {
            var name = node.Name.ToString();
            if (name.StartsWith("Plateau_"))
            {
                plateaus++;
            }
            else if (name.StartsWith("Ramp_"))
            {
                ramps++;
            }
        }

        if (plateaus == 0)
        {
            throw new System.Exception("Cliffs arena must raise plateau blocks for its high ground.");
        }

        if (ramps == 0)
        {
            throw new System.Exception("Cliffs arena must render ramps connecting the valley to the plateau.");
        }
    }
}
