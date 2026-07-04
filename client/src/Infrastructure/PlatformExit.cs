using Godot;

namespace TankGame.Infrastructure;

/// <summary>Where an "Exit" button leads. In the Lundrea Arcade web build a browser tab can't close
/// itself, so <c>GetTree().Quit()</c> is inert there — <see cref="Run"/> instead navigates the hosting
/// page back to the arcade root; on desktop it quits the process. The web-vs-desktop choice is the whole
/// decision, kept pure in <see cref="Resolve"/> so it is unit-tested without a Godot runtime.</summary>
public static class PlatformExit
{
    public enum Destination { QuitApp, ReturnToArcade }

    // The arcade landing page is the site root, served same-origin with the /tank/ WASM bundle
    // (ProjectX convention: every game's "back" navigates to '/').
    private const string ArcadeUrl = "/";

    public static Destination Resolve(bool isWebExport) =>
        isWebExport ? Destination.ReturnToArcade : Destination.QuitApp;

    /// <summary>Exit the game: back to the arcade on the web export, quit the process on desktop.</summary>
    public static void Run(SceneTree tree)
    {
        if (Resolve(OS.HasFeature("web")) == Destination.ReturnToArcade)
        {
            JavaScriptBridge.Eval($"window.location.href = '{ArcadeUrl}';");
        }
        else
        {
            tree.Quit();
        }
    }
}
