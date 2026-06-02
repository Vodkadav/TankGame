using Godot;
#if DEBUG
using System.Reflection;
using Chickensoft.GoDotTest;
#endif

namespace TankGame.Presentation;

// Composition-root entry point. In a test run (`--run-tests`) it hands the
// executing assembly to GoDotTest and quits with the result's exit code;
// otherwise it boots the real game scene. Kept separate from MainScene so the
// scene-under-test (Main.tscn) can be instantiated by tests without re-entering
// the runner. Test wiring is DEBUG-only, so ExportRelease builds exclude it.
public partial class Bootstrap : Node
{
#if DEBUG
    private TestEnvironment _environment = default!;
#endif

    public override void _Ready()
    {
#if DEBUG
        _environment = TestEnvironment.From(OS.GetCmdlineArgs());
        if (_environment.ShouldRunTests)
        {
            Callable.From(RunTests).CallDeferred();
            return;
        }
#endif
        // Deferred: the scene tree is mid-add during _Ready and rejects an
        // immediate scene swap ("parent node is busy adding/removing children").
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Main.tscn");
    }

#if DEBUG
    private void RunTests()
        => _ = GoTest.RunTests(Assembly.GetExecutingAssembly(), this, _environment);
#endif
}
