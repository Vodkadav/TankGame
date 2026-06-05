using Godot;

namespace TankGame.Presentation;

/// <summary>A screen-edge arrow that flashes toward a tank that just fired, so the player can tell
/// which direction shots are coming from. It blinks a few times over its lifetime then frees itself.
/// Lives on a screen-space CanvasLayer; the scene computes its edge position and heading. Several can
/// be on screen at once (one per shot).</summary>
public partial class FireArrow : Node2D
{
    private const float Lifetime = 1.5f;
    private const float BlinkInterval = 0.3f; // on/off every 0.3 s → ~3 blinks across the 1.5 s life

    private float _elapsed;

    /// <summary>Places the arrow at <paramref name="screenPosition"/> and rotates it to
    /// <paramref name="angle"/> (the arrow art points +X at angle 0).</summary>
    public void Show(Vector2 screenPosition, float angle)
    {
        Position = screenPosition;
        Rotation = angle;
        AddChild(BuildArrow());
    }

    public override void _Process(double delta) => Advance((float)delta);

    /// <summary>Advances the blink and frees the arrow at the end of its life. Public so a test can
    /// drive it without frame timing.</summary>
    public void Advance(float delta)
    {
        _elapsed += delta;
        Visible = (int)(_elapsed / BlinkInterval) % 2 == 0; // blink on, off, on, …
        if (_elapsed >= Lifetime)
        {
            QueueFree();
        }
    }

    private static Polygon2D BuildArrow() => new()
    {
        Name = "Arrow",
        Color = new Color(1f, 0.3f, 0.2f, 0.95f),
        Polygon = new[]
        {
            new Vector2(20f, 0f),    // tip
            new Vector2(-12f, -14f),
            new Vector2(-4f, 0f),
            new Vector2(-12f, 14f),
        },
    };
}
