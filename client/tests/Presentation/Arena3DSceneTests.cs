using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Smoke test for the 3D arena (ADR-0017): the scene must instantiate, load the tank GLB, and wire the
// player + AI as 3D views under a 3D camera without crashing headless.
public class Arena3DSceneTests : TestClass
{
    private Node _arena = default!;

    public Arena3DSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _arena = GD.Load<PackedScene>("res://src/Presentation/Arena/Arena3D.tscn").Instantiate();
        TestScene.AddChild(_arena); // runs Arena3DScene._Ready
    }

    [Cleanup]
    public void Cleanup() => _arena.QueueFree();

    [Test]
    public void Arena3D_WiresTanksAndGround_UnderA3DCamera()
    {
        var tankViews = 0;
        Camera3D? camera = null;
        MeshInstance3D? ground = null;
        Terrain3DView? terrain = null;
        foreach (var child in _arena.GetChildren())
        {
            switch (child)
            {
                case Tank3DView:
                    tankViews++;
                    break;
                case Camera3D cam:
                    camera = cam;
                    break;
                case Terrain3DView t:
                    terrain = t;
                    break;
                case MeshInstance3D g when g.Name.ToString() == "Ground":
                    ground = g;
                    break;
            }
        }

        if (tankViews < 2)
        {
            throw new System.Exception($"3D arena must instance the player plus AI; saw {tankViews} tanks.");
        }

        if (camera is null || camera.Projection != Camera3D.ProjectionType.Orthogonal)
        {
            throw new System.Exception("3D arena must have an orthographic Camera3D.");
        }

        if (ground is null)
        {
            throw new System.Exception("3D arena must lay a ground mesh.");
        }

        // The generated arena has a steel border, so the terrain view must hold wall meshes.
        if (terrain is null || terrain.GetChildCount() == 0)
        {
            throw new System.Exception("3D arena must render terrain (walls) via a Terrain3DView.");
        }
    }
}
