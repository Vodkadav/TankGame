using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Draws the warning marker for an incoming <see cref="IAirstrike"/> (ADR-0017, the 3D
/// replacement for <c>AirstrikeView</c>): a translucent red disc on the ground at the target, the size of
/// the blast radius, pulsing while the strike telegraphs so tanks can scramble clear. A pure mirror — the
/// scene frees it when the strike detonates and despawns.</summary>
public partial class Airstrike3DView : Node3D
{
    private const float DiscY = 2f;

    private StandardMaterial3D _material = null!;
    private double _elapsed;

    public void Bind(IAirstrike strike)
    {
        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.2f, 0.15f, 0.4f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        AddChild(new MeshInstance3D
        {
            Name = "BlastMarker",
            Mesh = new CylinderMesh { TopRadius = strike.Radius, BottomRadius = strike.Radius, Height = 1f },
            Position = GroundProjection.ToWorld(strike.Position, DiscY),
            MaterialOverride = _material,
        });
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        var alpha = 0.30f + (0.30f * Mathf.Sin((float)_elapsed * 9f));
        _material.AlbedoColor = new Color(0.95f, 0.2f, 0.15f, alpha);
    }
}
