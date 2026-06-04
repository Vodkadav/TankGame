using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class ArenaSceneTests : TestClass
{
    private Node _arena = default!;

    public ArenaSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _arena = GD.Load<PackedScene>("res://src/Presentation/Arena/Arena.tscn").Instantiate();
        TestScene.AddChild(_arena); // runs ArenaScene._Ready, which wires the tank
    }

    [Cleanup]
    public void Cleanup() => _arena.QueueFree();

    [Test]
    public void Arena_WiresUpThePlayerAndAdversaries_WithAFollowingCamera()
    {
        var tankViews = 0;
        Camera2D? camera = null;
        foreach (var child in _arena.GetChildren())
        {
            if (child is TankView)
            {
                tankViews++;
            }
            else if (child is Camera2D cam)
            {
                camera = cam; // the game camera lives on the scene, so it survives a death
            }
        }

        if (tankViews < 2)
        {
            throw new System.Exception($"Arena must instance the player plus AI adversaries; saw {tankViews} tanks.");
        }

        if (camera is null)
        {
            throw new System.Exception("Arena must have a Camera2D that follows the player.");
        }
    }

    [Test]
    public void VersusMode_SpawnsTwoPlayerTanks_AndNoAi()
    {
        var original = GameSetup.Mode;
        try
        {
            GameSetup.Mode = GameMode.TwoPlayerVersus;
            var arena = GD.Load<PackedScene>("res://src/Presentation/Arena/Arena.tscn").Instantiate();
            TestScene.AddChild(arena); // _Ready reads the mode and spawns

            var tankViews = 0;
            foreach (var child in arena.GetChildren())
            {
                if (child is TankView)
                {
                    tankViews++;
                }
            }

            arena.QueueFree();

            if (tankViews != 2)
            {
                throw new System.Exception($"Versus must spawn exactly two tanks (P1, P2); saw {tankViews}.");
            }
        }
        finally
        {
            GameSetup.Mode = original;
        }
    }

    [Test]
    public void OnePlayer_FogsTheField_WithALightAroundThePlayer()
    {
        // Setup() loads the scene in the default OnePlayer mode.
        var hasModulate = false;
        var lights = 0;
        foreach (var child in _arena.GetChildren())
        {
            if (child is CanvasModulate)
            {
                hasModulate = true;
            }
            else if (child is PointLight2D)
            {
                lights++;
            }
        }

        if (!hasModulate)
        {
            throw new System.Exception("Fog of war must darken the field with a CanvasModulate.");
        }

        if (lights != 1)
        {
            throw new System.Exception($"One-player fog needs exactly one player light; saw {lights}.");
        }
    }

    [Test]
    public void VersusMode_HasNoFog()
    {
        var original = GameSetup.Mode;
        try
        {
            GameSetup.Mode = GameMode.TwoPlayerVersus;
            var arena = GD.Load<PackedScene>("res://src/Presentation/Arena/Arena.tscn").Instantiate();
            TestScene.AddChild(arena);

            var hasFog = false;
            foreach (var child in arena.GetChildren())
            {
                if (child is CanvasModulate or PointLight2D)
                {
                    hasFog = true;
                }
            }

            arena.QueueFree();

            if (hasFog)
            {
                throw new System.Exception("Versus shares one screen, so it must not fog the field.");
            }
        }
        finally
        {
            GameSetup.Mode = original;
        }
    }

    [Test]
    public void Arena_SpawnsThePickupsOnTheField()
    {
        // Setup() loads the scene in the default mode; the catalogue places two pickups.
        var powerups = 0;
        foreach (var child in _arena.GetChildren())
        {
            if (child is PowerupView)
            {
                powerups++;
            }
        }

        if (powerups != 2)
        {
            throw new System.Exception($"Arena must spawn the two field pickups; saw {powerups} PowerupViews.");
        }
    }

    [Test]
    public void Arena_RendersTheMazeWallGrid()
    {
        WallGridView? walls = null;
        foreach (var child in _arena.GetChildren())
        {
            if (child is WallGridView view)
            {
                walls = view;
            }
        }

        if (walls is null)
        {
            throw new System.Exception("Arena must render the maze via a WallGridView.");
        }

        // The hand-authored maze has a steel border, so the corner cell must carry a tile.
        if (walls.GetCellSourceId(new Vector2I(0, 0)) == -1)
        {
            throw new System.Exception("The maze's steel border should be drawn at cell (0,0).");
        }
    }
}
