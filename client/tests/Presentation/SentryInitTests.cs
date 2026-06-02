using Godot;
using Chickensoft.GoDotTest;
using Sentry;

namespace TankGame.Tests.Presentation;

public class SentryInitTests : TestClass
{
    private const string EnvVarName = "SENTRY_DSN_CLIENT";
    private const string StubDsn = "https://public@example.invalid/1";

    private Node? _scene;

    public SentryInitTests(Node testScene) : base(testScene) { }

    [Cleanup]
    public void Cleanup()
    {
        if (_scene != null)
        {
            _scene.QueueFree();
            _scene = null;
        }

        if (SentrySdk.IsEnabled)
        {
            SentrySdk.Close();
        }

        OS.UnsetEnvironment(EnvVarName);
    }

    [Test]
    public void SentrySdk_IsEnabled_WhenDsnEnvVarSet()
    {
        // Godot's OS env API, not System.Environment: MainScene reads via
        // OS.GetEnvironment, and the two stores are not shared on Linux.
        OS.SetEnvironment(EnvVarName, StubDsn);

        _scene = GD.Load<PackedScene>("res://Main.tscn").Instantiate();
        TestScene.AddChild(_scene);

        if (!SentrySdk.IsEnabled)
        {
            throw new System.Exception(
                "SentrySdk.IsEnabled must be true after MainScene._Ready when "
                + $"{EnvVarName} is set.");
        }
    }

    [Test]
    public void SentrySdk_StaysDisabled_WhenDsnEnvVarMissing()
    {
        OS.UnsetEnvironment(EnvVarName);

        _scene = GD.Load<PackedScene>("res://Main.tscn").Instantiate();
        TestScene.AddChild(_scene);

        if (SentrySdk.IsEnabled)
        {
            throw new System.Exception(
                "SentrySdk.IsEnabled must be false after MainScene._Ready when "
                + $"{EnvVarName} is absent.");
        }
    }
}
