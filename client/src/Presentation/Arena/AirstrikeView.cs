using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Draws the warning marker for an incoming <see cref="IAirstrike"/>: a translucent red blast
/// circle that pulses while the strike telegraphs, so tanks can scramble clear. A pure mirror built in
/// code — the world owns the strike (it detonates and expires); the scene frees this view on despawn.
/// The strike does not move, so the node is placed once on <see cref="Bind"/>.</summary>
public partial class AirstrikeView : Node2D
{
    private const int Segments = 28;
    private double _elapsed;

    public void Bind(IAirstrike strike)
    {
        var screen = IsoProjection.WorldToScreen(strike.Position);
        Position = new Vector2(screen.X, screen.Y);
        ZIndex = IsoProjection.DepthOf(strike.Position);
        AddChild(BuildRing(strike.Radius));
    }

    // Pulse the marker so it reads as an urgent, incoming strike rather than static decoration.
    public override void _Process(double delta)
    {
        _elapsed += delta;
        Modulate = new Color(1f, 1f, 1f, 0.45f + (0.35f * Mathf.Sin((float)_elapsed * 9f)));
    }

    private static Polygon2D BuildRing(float radius)
    {
        var points = new Vector2[Segments];
        for (var i = 0; i < Segments; i++)
        {
            var angle = Mathf.Tau * i / Segments;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return new Polygon2D
        {
            Name = "BlastMarker",
            Color = new Color(0.95f, 0.2f, 0.15f, 0.4f),
            Polygon = points,
        };
    }
}
