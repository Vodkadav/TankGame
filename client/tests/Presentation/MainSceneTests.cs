using Godot;
using Chickensoft.GoDotTest;

namespace TankGame.Tests.Presentation;

public class MainSceneTests : TestClass
{
    private Node _scene = default!;

    public MainSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _scene = GD.Load<PackedScene>("res://Main.tscn").Instantiate();
        TestScene.AddChild(_scene);
    }

    [Cleanup]
    public void Cleanup()
    {
        _scene.QueueFree();
    }

    [Test]
    public void BootLabel_TextStartsWithTankGame()
    {
        var canvas = _scene.GetNode<CanvasLayer>("CanvasLayer");
        var label = canvas.GetNode<Label>("BootLabel");

        if (!label.Text.StartsWith("TankGame"))
        {
            throw new System.Exception(
                $"BootLabel must start with 'TankGame'; was '{label.Text}'");
        }
    }
}
