using Godot;

namespace TankGame.Presentation;

/// <summary>A swappable visual palette for the arena (S8 theming): the ground colour painted
/// beneath the field and a multiply tint applied to the wall sprites. Source-agnostic — it
/// recolours whatever wall art is loaded, so it does not depend on the art pass. A title
/// control can later let the player pick one; for now the scene reads <see cref="GameSetup.Theme"/>,
/// which defaults to <see cref="Default"/>.</summary>
public sealed record ArenaTheme(Color Ground, Color WallTint)
{
    /// <summary>Warm sandy ground, walls untinted — the look the owner's reference image asks for.</summary>
    public static readonly ArenaTheme Sandy = new(
        Ground: new Color(0.85f, 0.74f, 0.52f),
        WallTint: Colors.White);

    /// <summary>A cool grey-stone alternative that also tints the walls bluish, proving the seam swaps.</summary>
    public static readonly ArenaTheme Slate = new(
        Ground: new Color(0.30f, 0.33f, 0.40f),
        WallTint: new Color(0.78f, 0.82f, 0.95f));

    public static ArenaTheme Default => Sandy;
}
