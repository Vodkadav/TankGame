using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>A short-lived floating label that pops up where a pickup was collected and names what it
/// was — so the player learns what each powerup does. It rises a little and fades, then frees itself.
/// The label text is the kind's translation key; Godot auto-translates it to the active locale.</summary>
public partial class PickupFloater : Node2D
{
    private const float LifeSeconds = 1.1f;
    private const float RiseSpeed = 42f; // pixels per second, upward (screen up is -Y)
    private const float LabelHalfWidth = 70f;

    private float _elapsed;

    /// <summary>Places the floater at <paramref name="worldPosition"/> and labels it with the pickup
    /// kind's translation key.</summary>
    public void Show(Vector2 worldPosition, string textKey)
    {
        Position = worldPosition;
        AddChild(new Label
        {
            Name = "Text",
            Text = textKey, // a translation key — Godot auto-translates Control text to the locale
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-LabelHalfWidth, -44f),
            CustomMinimumSize = new Vector2(LabelHalfWidth * 2f, 0f),
        });
    }

    public override void _Process(double delta) => Advance((float)delta);

    /// <summary>Advances the rise/fade; frees the floater once its lifetime elapses. Public so a test
    /// can drive it without relying on frame timing.</summary>
    public void Advance(float delta)
    {
        _elapsed += delta;
        Position -= new Vector2(0f, RiseSpeed * delta);
        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(1f - (_elapsed / LifeSeconds), 0f, 1f));

        if (_elapsed >= LifeSeconds)
        {
            QueueFree();
        }
    }

    /// <summary>The translation key naming each pickup kind — public so a test can assert the mapping.</summary>
    public static string LabelKeyFor(PowerupKind kind) => kind switch
    {
        PowerupKind.SpeedBoost => "pickup.speed_boost",
        PowerupKind.RapidFire => "pickup.rapid_fire",
        PowerupKind.BouncingAmmo => "pickup.bouncing_ammo",
        PowerupKind.SpreadAmmo => "pickup.spread_ammo",
        PowerupKind.PiercingAmmo => "pickup.piercing_ammo",
        PowerupKind.Repair => "pickup.repair",
        PowerupKind.Shield => "pickup.shield",
        PowerupKind.Missile => "pickup.missile",
        _ => "pickup.unknown",
    };
}
