using System;
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

        Environment.SetEnvironmentVariable(EnvVarName, null);
    }

    [Test]
    public void SentrySdk_IsEnabled_WhenDsnEnvVarSet()
    {
        Environment.SetEnvironmentVariable(EnvVarName, StubDsn);

        _scene = GD.Load<PackedScene>("res://Main.tscn").Instantiate();
        TestScene.AddChild(_scene);

        if (!SentrySdk.IsEnabled)
        {
            throw new Exception(
                "SentrySdk.IsEnabled must be true after MainScene._Ready when "
                + $"{EnvVarName} is set.");
        }
    }

    [Test]
    public void SentrySdk_StaysDisabled_WhenDsnEnvVarMissing()
    {
        Environment.SetEnvironmentVariable(EnvVarName, null);

        _scene = GD.Load<PackedScene>("res://Main.tscn").Instantiate();
        TestScene.AddChild(_scene);

        if (SentrySdk.IsEnabled)
        {
            throw new Exception(
                "SentrySdk.IsEnabled must be false after MainScene._Ready when "
                + $"{EnvVarName} is absent.");
        }
    }
}
