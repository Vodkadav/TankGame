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
        // Setup() loads the scene in the default mode; the catalogue places nine pickups (speed,
        // rapid-fire, bouncing-ammo, spread-ammo, repair, shield, piercing-ammo, missile, telephone).
        var powerups = 0;
        foreach (var child in _arena.GetChildren())
        {
            if (child is PowerupView)
            {
                powerups++;
            }
        }

        if (powerups != 9)
        {
            throw new System.Exception($"Arena must spawn the nine field pickups; saw {powerups} PowerupViews.");
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

    [Test]
    public void Arena_LaysTheIsometricGround_TintedToTheTheme()
    {
        var ground = _arena.GetNodeOrNull<TileMapLayer>("Ground")
            ?? throw new System.Exception("Arena must lay a 'Ground' iso TileMapLayer beneath the field.");

        if (ground.TileSet?.TileShape != TileSet.TileShapeEnum.Isometric)
        {
            throw new System.Exception("The ground must be an isometric TileMapLayer.");
        }

        if (ground.Modulate != GameSetup.Theme.Ground)
        {
            throw new System.Exception($"Ground tint should match the theme; was {ground.Modulate}.");
        }

        if (ground.ZIndex >= 0)
        {
            throw new System.Exception("Ground must sit behind the walls and tanks (negative ZIndex).");
        }

        // The corner cell must carry a ground tile (the whole field is laid).
        if (ground.GetCellSourceId(new Vector2I(0, 0)) == -1)
        {
            throw new System.Exception("The ground should lay a tile in every cell, including (0,0).");
        }
    }

    [Test]
    public void Ground_AlignsWithTheEntityProjection()
    {
        var ground = _arena.GetNodeOrNull<TileMapLayer>("Ground")
            ?? throw new System.Exception("Arena must lay a 'Ground' iso TileMapLayer.");

        // A cell's on-screen centre (tilemap local + layer offset) must equal where the entity
        // projection puts that cell's world centre — so tanks stand on their tiles.
        const float tile = 64f;
        var cell = new Vector2I(3, 2);
        var tilemapCentre = ground.MapToLocal(cell) + ground.Position;
        var world = new System.Numerics.Vector2((cell.X + 0.5f) * tile, (cell.Y + 0.5f) * tile);
        var projected = IsoProjection.WorldToScreen(world);

        if (Mathf.Abs(tilemapCentre.X - projected.X) > 0.5f || Mathf.Abs(tilemapCentre.Y - projected.Y) > 0.5f)
        {
            throw new System.Exception($"Ground cell {cell} centre {tilemapCentre} must match the projection {projected}.");
        }
    }

    [Test]
    public void SteppedZoom_ZoomsInAndOut_WithinClamps()
    {
        var inOnce = ArenaScene.SteppedZoom(new Vector2(1f, 1f), zoomIn: true);
        if (inOnce.X <= 1f || Mathf.Abs(inOnce.X - inOnce.Y) > 0.0001f)
        {
            throw new System.Exception($"Zooming in should raise a uniform zoom; was {inOnce}.");
        }

        var farIn = new Vector2(1f, 1f);
        for (var i = 0; i < 50; i++)
        {
            farIn = ArenaScene.SteppedZoom(farIn, zoomIn: true);
        }
        if (farIn.X > 4f + 0.0001f)
        {
            throw new System.Exception($"Zoom in must clamp at the max; was {farIn.X}.");
        }

        var farOut = new Vector2(1f, 1f);
        for (var i = 0; i < 50; i++)
        {
            farOut = ArenaScene.SteppedZoom(farOut, zoomIn: false);
        }
        if (farOut.X < 0.25f - 0.0001f)
        {
            throw new System.Exception($"Zoom out must clamp at the min; was {farOut.X}.");
        }
    }

    [Test]
    public void Arena_TintsTheWalls_WithTheTheme()
    {
        WallGridView? walls = null;
        foreach (var child in _arena.GetChildren())
        {
            if (child is WallGridView view)
            {
                walls = view;
            }
        }

        if (walls!.Modulate != GameSetup.Theme.WallTint)
        {
            throw new System.Exception($"Walls should carry the theme tint; was {walls.Modulate}.");
        }
    }
}
