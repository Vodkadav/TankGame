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
        var teleportPads = 0;
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
                case TeleportPad3DView:
                    teleportPads++;
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

        // Teleport pads ship as a linked pair of glowing ground rings (teleport pads T1).
        if (teleportPads != 2)
        {
            throw new System.Exception($"3D arena must place a linked pair of teleport pad rings; saw {teleportPads}.");
        }
    }

    [Test]
    public void Arena3D_EscapeMenu_PausesAndFreezesTheMatch()
    {
        var scene = (Arena3DScene)_arena;
        if (scene.IsPaused)
        {
            throw new System.Exception("The match should start unpaused.");
        }

        scene.TogglePause();
        if (!scene.IsPaused)
        {
            throw new System.Exception("Opening the Escape menu should pause the match.");
        }

        var pauseLayer = _arena.FindChild("PauseMenu", recursive: true, owned: false) as CanvasLayer
            ?? throw new System.Exception("3D arena must build a PauseMenu overlay.");
        if (!pauseLayer.Visible)
        {
            throw new System.Exception("The pause overlay should be visible while paused.");
        }

        foreach (var name in new[] { "Resume", "MainMenu", "ExitGame" })
        {
            if (pauseLayer.FindChild(name, recursive: true, owned: false) is not Button)
            {
                throw new System.Exception($"The pause menu must offer a '{name}' button.");
            }
        }

        scene.TogglePause();
        if (scene.IsPaused || pauseLayer.Visible)
        {
            throw new System.Exception("Resuming should unpause and hide the overlay.");
        }
    }

    [Test]
    public void Arena3D_FogsTheField_WithALightAroundThePlayer()
    {
        // Setup() loads the single-player 3D arena. Fog darkens the world (dark ambient) and lights only
        // a circle around the player via a SpotLight3D that follows the player on the ground plane.
        var scene = (Arena3DScene)_arena;

        var fogLight = _arena.FindChild("FogLight", recursive: true, owned: false) as SpotLight3D
            ?? throw new System.Exception("3D fog must add a SpotLight3D 'FogLight' around the player.");

        var player = scene.PlayerWorldPosition;
        var lit = fogLight.Position;
        if (Mathf.Abs(lit.X - player.X) > 1f || Mathf.Abs(lit.Z - player.Z) > 1f)
        {
            throw new System.Exception(
                $"The fog light must be centred over the player (light {lit} vs player {player}).");
        }

        if (lit.Y <= player.Y)
        {
            throw new System.Exception("The fog light must hang above the player to light a ground circle.");
        }

        WorldEnvironment? worldEnv = null;
        foreach (var child in _arena.GetChildren())
        {
            if (child is WorldEnvironment we)
            {
                worldEnv = we;
            }
        }

        if (worldEnv?.Environment is null || worldEnv.Environment.AmbientLightColor.Luminance > 0.3f)
        {
            throw new System.Exception("Fog must darken the world's ambient so only the lit circle reads as visible.");
        }
    }

    [Test]
    public void Arena3D_SpawnsThePickups_AsPowerupViews()
    {
        var powerups = 0;
        foreach (var child in _arena.GetChildren())
        {
            if (child is Powerup3DView)
            {
                powerups++;
            }
        }

        if (powerups != 9)
        {
            throw new System.Exception($"3D arena must spawn the nine field pickups; saw {powerups}.");
        }
    }
}
