using Godot;
using GoDotTest;

namespace TankGame.Tests.Presentation;

/// <summary>
/// Verifies that Main.tscn loads successfully and BootLabel text is set.
/// </summary>
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

        Assert.That(label.Text, Does.StartWith("TankGame"),
            "BootLabel must start with 'TankGame'");
    }
}
