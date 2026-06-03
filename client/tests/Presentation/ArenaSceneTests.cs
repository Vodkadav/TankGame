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
    public void Arena_WiresUpThePlayerAndAdversaries_WithACameraOnThePlayerOnly()
    {
        var tankViews = 0;
        var cameras = 0;
        foreach (var child in _arena.GetChildren())
        {
            if (child is not TankView tankView)
            {
                continue;
            }

            tankViews++;
            foreach (var grandchild in tankView.GetChildren())
            {
                if (grandchild is Camera2D)
                {
                    cameras++;
                }
            }
        }

        if (tankViews < 2)
        {
            throw new System.Exception($"Arena must instance the player plus AI adversaries; saw {tankViews} tanks.");
        }

        if (cameras != 1)
        {
            throw new System.Exception($"Exactly one tank (the player) must carry the Camera2D; saw {cameras}.");
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
