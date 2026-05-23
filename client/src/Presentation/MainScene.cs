using Godot;

namespace TankGame.Presentation;

public partial class MainScene : Node2D
{
    public override void _Ready()
    {
        var version = System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version;

        GetNode<CanvasLayer>("CanvasLayer")
            .GetNode<Label>("BootLabel")
            .Text = $"TankGame M0 — build {version}";
    }
}
