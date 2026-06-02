using Godot;
using Chickensoft.GoDotTest;
using Sentry;
using TankGame.Infrastructure;

namespace TankGame.Tests.Presentation;

public class SentryInitTests : TestClass
{
    private const string EnvVarName = "SENTRY_DSN_CLIENT";
    private const string StubDsn = "https://public@example.invalid/1";

    public SentryInitTests(Node testScene) : base(testScene) { }

    [Cleanup]
    public void Cleanup()
    {
        if (SentrySdk.IsEnabled)
        {
            SentrySdk.Close();
        }

        OS.UnsetEnvironment(EnvVarName);
    }

    [Test]
    public void Init_EnablesSentry_WhenDsnEnvVarSet()
    {
        OS.SetEnvironment(EnvVarName, StubDsn);

        SentryBootstrap.Init();

        if (!SentrySdk.IsEnabled)
        {
            throw new System.Exception(
                $"SentrySdk.IsEnabled must be true after SentryBootstrap.Init when {EnvVarName} is set.");
        }
    }

    [Test]
    public void Init_LeavesSentryDisabled_WhenDsnEnvVarMissing()
    {
        OS.UnsetEnvironment(EnvVarName);

        SentryBootstrap.Init();

        if (SentrySdk.IsEnabled)
        {
            throw new System.Exception(
                $"SentrySdk.IsEnabled must be false after SentryBootstrap.Init when {EnvVarName} is absent.");
        }
    }
}
