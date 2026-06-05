using Godot;

namespace TankGame.Presentation;

/// <summary>Shared styling and layout for the screen-space HUD readouts, so the corner overlays line
/// up instead of stacking on the same spot and stay readable over the textured ground. White text
/// with a dark outline reads on any background; <see cref="LineY"/> gives each top-corner overlay its
/// own row.</summary>
public static class Hud
{
    /// <summary>Margin from the screen edge, pixels.</summary>
    public const float Margin = 8f;

    /// <summary>Vertical spacing between stacked HUD rows, pixels.</summary>
    public const float LineHeight = 26f;

    /// <summary>The top Y for the HUD row at <paramref name="slot"/> (0 = topmost).</summary>
    public static float LineY(int slot) => Margin + (slot * LineHeight);

    /// <summary>Gives a HUD label white text with a dark outline so it reads over any ground.</summary>
    public static void Style(Label label)
    {
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        label.AddThemeConstantOverride("outline_size", 6);
    }
}
