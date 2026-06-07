using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IPowerup"/> as a glowing, slowly spinning orb hovering over its cell
/// (ADR-0017, the 3D replacement for <c>PowerupView</c>), coloured per <see cref="PowerupKind"/> from the
/// shared <see cref="PowerupView.ColourFor"/> mapping. A pure mirror: hidden while the pickup is dormant
/// (respawning), shown while available; the scene frees it on despawn.</summary>
public partial class Powerup3DView : Node3D
{
    private const float HoverHeight = 26f;
    private const float OrbRadius = 15f;
    private const float SpinSpeed = 1.8f;

    private IPowerup? _powerup;
    private MeshInstance3D _orb = null!;

    public void Bind(IPowerup powerup)
    {
        _powerup = powerup;
        var colour = PowerupView.ColourFor(powerup.Kind);
        _orb = new MeshInstance3D
        {
            Name = "Orb",
            Mesh = new SphereMesh { Radius = OrbRadius, Height = OrbRadius * 2f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = colour,
                EmissionEnabled = true,
                Emission = colour,
                EmissionEnergyMultiplier = 0.6f,
            },
        };
        AddChild(_orb);
        UpdateFromModel();
    }

    public override void _Process(double delta)
    {
        _orb.RotateY((float)delta * SpinSpeed);
        UpdateFromModel();
    }

    public void UpdateFromModel()
    {
        if (_powerup is not null)
        {
            Visible = _powerup.IsAvailable;
            Position = GroundProjection.ToWorld(_powerup.Position, HoverHeight); // it moves if it drops
        }
    }
}
