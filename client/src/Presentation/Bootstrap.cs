using Godot;
using TankGame.Infrastructure;
#if DEBUG
using System;
using System.Linq;
using System.Reflection;
using Chickensoft.GoDotTest;
using TankGame.Domain;
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
        if (OS.GetCmdlineArgs().Contains("--audit-assets"))
        {
            Callable.From(AuditAssets).CallDeferred();
            return;
        }

        _environment = TestEnvironment.From(OS.GetCmdlineArgs());
        if (_environment.ShouldRunTests)
        {
            Callable.From(RunTests).CallDeferred();
            return;
        }
#endif
        // App init happens once here (the composition root), then the play scene loads.
        SentryBootstrap.Init();
        TranslationLoader.EnsureLoaded();

        // Deferred: the scene tree is mid-add during _Ready and rejects an
        // immediate scene swap ("parent node is busy adding/removing children").
        GetTree().CallDeferred(
            SceneTree.MethodName.ChangeSceneToFile, "res://src/Presentation/Title.tscn");
    }

#if DEBUG
    private void RunTests()
        => _ = GoTest.RunTests(Assembly.GetExecutingAssembly(), this, _environment);

    private void AuditAssets()
    {
        var catalogue = DecorationAssets.Catalogue();
        GD.Print($"ASSET-TINT-AUDIT START total={catalogue.Count}");

        var whiteCount = 0;
        var emptyCount = 0;

        foreach (var entry in catalogue)
        {
            var view = new DecorationView();
            view.Configure(entry.Id, PropTransform.Identity, 64f);
            AddChild(view);

            var meshCount = CountMeshes(view);
            if (meshCount == 0)
            {
                GD.Print($"EMPTY {entry.Id}");
                emptyCount++;
                view.Free();
                continue;
            }

            var whites = ModelFit.WhiteRenderingSurfaces(view);
            if (whites.Count > 0)
            {
                GD.Print($"WHITE {entry.Id} [{string.Join(", ", whites)}]");
                whiteCount++;
            }

            view.Free();
        }

        GD.Print($"ASSET-TINT-AUDIT DONE white={whiteCount} empty={emptyCount} total={catalogue.Count}");
        GetTree().Quit(0);
    }

    // Recurse over Node, not Node3D — a GLTF scene also holds non-spatial nodes (AnimationPlayer,
    // etc.) that would throw on a Node3D cast.
    private static int CountMeshes(Node node)
    {
        var count = node is MeshInstance3D { Mesh: not null } ? 1 : 0;
        foreach (var child in node.GetChildren())
        {
            count += CountMeshes(child);
        }

        return count;
    }
#endif
}
