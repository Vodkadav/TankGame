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
    public void Arena_WiresUpATankViewWithAFollowingCamera()
    {
        TankView? view = null;
        foreach (var child in _arena.GetChildren())
        {
            if (child is TankView tankView)
            {
                view = tankView;
            }
        }

        if (view is null)
        {
            throw new System.Exception("Arena must instance a TankView at runtime.");
        }

        var hasCamera = false;
        foreach (var child in view.GetChildren())
        {
            if (child is Camera2D)
            {
                hasCamera = true;
            }
        }

        if (!hasCamera)
        {
            throw new System.Exception("The TankView must have a Camera2D so the tank stays centred.");
        }
    }
}
