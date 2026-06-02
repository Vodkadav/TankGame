using Chickensoft.GoDotTest;
using Godot;
using Sentry;
using Shouldly;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class CrashReportingTests : TestClass
{
    public CrashReportingTests(Node testScene) : base(testScene) { }

    [Test]
    public void Initialize_with_a_dsn_enables_Sentry()
    {
        try
        {
            MainScene.InitializeCrashReporting("https://test@test.ingest.sentry.io/1")
                .ShouldBeTrue();
            SentrySdk.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            SentrySdk.Close();
        }
    }

    [Test]
    public void Initialize_without_a_dsn_stays_disabled() =>
        MainScene.InitializeCrashReporting("").ShouldBeFalse();
}
