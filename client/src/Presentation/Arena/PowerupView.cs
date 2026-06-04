using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IPowerup"/> as a code-built coloured diamond (no art asset),
/// its colour chosen from the powerup's <see cref="PowerupKind"/>. A pure mirror: the world owns
/// the entity (a tank collecting it expires it); the scene frees this view on the despawn
/// event. Powerups do not move, so the node's position is set once on <see cref="Bind"/>.</summary>
public partial class PowerupView : Node2D
{
    private const float HalfSize = 14f;

    /// <summary>Builds the coloured shape and places the node at the powerup's position.</summary>
    public void Bind(IPowerup powerup)
    {
        Position = new Vector2(powerup.Position.X, powerup.Position.Y);
        AddChild(BuildShape(powerup.Kind));
    }

    private static Polygon2D BuildShape(PowerupKind kind) => new()
    {
        Name = "Shape",
        Color = ColourFor(kind),
        Polygon = new[]
        {
            new Vector2(0f, -HalfSize),
            new Vector2(HalfSize, 0f),
            new Vector2(0f, HalfSize),
            new Vector2(-HalfSize, 0f),
        },
    };

    /// <summary>The on-screen colour for each kind — public so a test can assert the mapping.</summary>
    public static Color ColourFor(PowerupKind kind) => kind switch
    {
        PowerupKind.SpeedBoost => new Color(0.3f, 0.7f, 1f),    // blue
        PowerupKind.RapidFire => new Color(1f, 0.7f, 0.2f),     // orange
        PowerupKind.BouncingAmmo => new Color(0.7f, 0.3f, 1f),  // purple
        PowerupKind.SpreadAmmo => new Color(0.9f, 0.3f, 0.5f),  // pink
        _ => Colors.White,
    };
}
