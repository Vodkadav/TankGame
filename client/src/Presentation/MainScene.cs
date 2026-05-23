using Godot;
using Sentry;
using TankGame.Infrastructure;

namespace TankGame.Presentation;

public partial class MainScene : Node2D
{
    public override void _Ready()
    {
        InitSentry();
        TranslationLoader.EnsureLoaded();

        var version = System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version;

        GetNode<CanvasLayer>("CanvasLayer")
            .GetNode<Label>("BootLabel")
            .Text = $"{Tr("m0.boot_label")} — build {version}";
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
