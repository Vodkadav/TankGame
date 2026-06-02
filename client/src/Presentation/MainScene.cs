using System.Reflection;
using Godot;
using Sentry;

#if DEBUG
using Chickensoft.GoDotTest;
#endif

namespace TankGame.Presentation;

public partial class MainScene : CanvasLayer
{
#if DEBUG
    // Prevent re-entry when GoDotTest's LoadAndAddScene instantiates this scene
    // as a child node during test execution.
    private static bool _testRunnerStarted;

    public TestEnvironment Env = default!;
#endif

    public override void _Ready()
    {
#if DEBUG
        Env = TestEnvironment.From(OS.GetCmdlineArgs());
        if (Env.ShouldRunTests && !_testRunnerStarted)
        {
            _testRunnerStarted = true;
            CallDeferred(MethodName.RunTests);
            return;
        }
#endif
        InitializeCrashReporting(OS.GetEnvironment("SENTRY_DSN_CLIENT"));
        ShowBootLabel();
    }

    // Initialises Sentry crash reporting. The DSN is injected via the
    // SENTRY_DSN_CLIENT environment variable (set at build time in CI), so it is
    // never committed. Returns true when a DSN was supplied and Sentry enabled.
    public static bool InitializeCrashReporting(string? dsn)
    {
        if (string.IsNullOrWhiteSpace(dsn))
        {
            return false;
        }

        SentrySdk.Init(options =>
        {
            options.Dsn = dsn;
            options.AutoSessionTracking = false;
            options.TracesSampleRate = 0.0;
        });
        return SentrySdk.IsEnabled;
    }

    private void ShowBootLabel()
    {
        var label = GetNode<Label>("BootLabel");
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.1";
        label.Text = Tr("m0.boot_label") + " — build " + version;
    }

#if DEBUG
    private void RunTests()
        => _ = Chickensoft.GoDotTest.GoTest.RunTests(
            Assembly.GetExecutingAssembly(), this, Env);
#endif
}
