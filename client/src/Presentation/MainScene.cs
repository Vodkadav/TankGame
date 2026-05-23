using Godot;
using TankGame.Infrastructure;

namespace TankGame.Presentation;

public partial class MainScene : Node2D
{
    public override void _Ready()
    {
        TranslationLoader.EnsureLoaded();

        var version = System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version;

        GetNode<CanvasLayer>("CanvasLayer")
            .GetNode<Label>("BootLabel")
            .Text = $"{Tr("m0.boot_label")} — build {version}";
    }
}
