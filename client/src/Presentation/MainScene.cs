using Godot;
using Sentry;

namespace TankGame.Presentation;

public partial class MainScene : Node2D
{
    public override void _Ready()
    {
        InitSentry();

        var version = System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version;

        GetNode<CanvasLayer>("CanvasLayer")
            .GetNode<Label>("BootLabel")
            .Text = $"TankGame M0 — build {version}";
    }

    private static void InitSentry()
    {
        var dsn = OS.GetEnvironment("SENTRY_DSN_CLIENT");
        if (string.IsNullOrEmpty(dsn))
        {
            return;
        }

        SentrySdk.Init(o =>
        {
            o.Dsn = dsn;
            o.AutoSessionTracking = false;
            o.SendDefaultPii = false;
        });
    }
}
