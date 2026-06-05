using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IPowerup"/> as a glowing disc — the neutral
/// <c>AssetCatalogue.PickupDisc</c> texture tinted per <see cref="PowerupKind"/> via Modulate, so one
/// disc reads as every pickup by colour. A pure mirror: the world owns the entity (a tank collecting it
/// expires it); the scene frees this view on the despawn event. Powerups do not move, so the node's
/// position is set once on <see cref="Bind"/>.</summary>
public partial class PowerupView : Node2D
{
    // The disc texture is 128 px; this renders it at roughly one-and-a-bit tile-thirds across.
    private const float DiscScale = 0.42f;

    private IPowerup? _powerup;

    /// <summary>Builds the tinted disc and places the node at the powerup's position.</summary>
    public void Bind(IPowerup powerup)
    {
        _powerup = powerup;
        Position = new Vector2(powerup.Position.X, powerup.Position.Y);
        AddChild(BuildDisc(powerup.Kind));
        UpdateFromModel();
    }

    public override void _Process(double delta) => UpdateFromModel();

    /// <summary>Mirrors the powerup's availability: a respawning pickup hides while dormant and
    /// reappears when it returns. Public so a test can assert the mirror without frame timing.</summary>
    public void UpdateFromModel()
    {
        if (_powerup is not null)
        {
            Visible = _powerup.IsAvailable;
            Position = new Vector2(_powerup.Position.X, _powerup.Position.Y); // it moves when it drops
        }
    }

    private static Sprite2D BuildDisc(PowerupKind kind) => new()
    {
        Name = "Disc",
        Texture = GD.Load<Texture2D>(AssetCatalogue.Active.PickupDisc),
        Modulate = ColourFor(kind), // white core → the kind's colour; dark coin edge stays dark
        Scale = new Vector2(DiscScale, DiscScale),
    };

    /// <summary>The on-screen colour for each kind — public so a test can assert the mapping.</summary>
    public static Color ColourFor(PowerupKind kind) => kind switch
    {
        PowerupKind.SpeedBoost => new Color(0.3f, 0.7f, 1f),    // blue
        PowerupKind.RapidFire => new Color(1f, 0.7f, 0.2f),     // orange
        PowerupKind.BouncingAmmo => new Color(0.7f, 0.3f, 1f),  // purple
        PowerupKind.SpreadAmmo => new Color(0.9f, 0.3f, 0.5f),  // pink
        PowerupKind.PiercingAmmo => new Color(1f, 0.95f, 0.4f), // yellow
        PowerupKind.Repair => new Color(0.2f, 0.9f, 0.3f),      // green
        PowerupKind.Shield => new Color(0.3f, 0.9f, 0.95f),     // cyan
        _ => Colors.White,
    };
}
