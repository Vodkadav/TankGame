using Godot;

namespace TankGame.Infrastructure;

/// <summary>Text entry for the touch web export. Godot's HTML5 canvas cannot raise the iOS
/// Safari soft keyboard when a LineEdit grabs focus (engine limitation), so on a touchscreen web
/// build every in-game text field is un-typeable. This seam routes text entry through the
/// browser's native prompt dialog — which always brings its own keyboard — and is meant to be
/// reused by every text field that must work in the arcade (the title's name prompt today, the
/// multiplayer lobby's fields next).</summary>
public static class WebTextEntry
{
    /// <summary>Only the touch web export needs the native prompt: desktop web has a hardware
    /// keyboard for the in-game panel, and the desktop/Android builds are not in a browser.</summary>
    public static bool NeedsNativePrompt(bool isWebExport, bool hasTouchscreen) =>
        isWebExport && hasTouchscreen;

    /// <summary>The JS to run: a native prompt pre-filled with the current value. Message and
    /// value are escaped into single-quoted JS literals so no player-typed text can break out of
    /// the script.</summary>
    public static string PromptScript(string message, string currentValue) =>
        $"window.prompt('{EscapeJs(message)}', '{EscapeJs(currentValue)}')";

    /// <summary>Shows the native browser prompt and blocks until it closes. Returns the entered
    /// text, or null when the player cancelled. Call only on the web export (see
    /// <see cref="NeedsNativePrompt"/>).</summary>
    public static string? Prompt(string message, string currentValue)
    {
        var result = JavaScriptBridge.Eval(PromptScript(message, currentValue));
        return result.VariantType == Variant.Type.String ? (string)result : null;
    }

    private static string EscapeJs(string value) => value
        .Replace("\\", "\\\\")
        .Replace("'", "\\'")
        .Replace("\r", "")
        .Replace("\n", "\\n");
}
