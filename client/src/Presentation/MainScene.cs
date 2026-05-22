using System.Reflection;
using Godot;

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
        ShowBootLabel();
    }

    private void ShowBootLabel()
    {
        var label = GetNode<Label>("BootLabel");
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.1";
        label.Text = "TankGame M0 — build " + version;
    }

#if DEBUG
    private void RunTests()
        => _ = Chickensoft.GoDotTest.GoTest.RunTests(
            Assembly.GetExecutingAssembly(), this, Env);
#endif
}
