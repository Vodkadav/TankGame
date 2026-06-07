using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IProjectile"/> as a small glowing 3D sphere at its world position
/// (ADR-0017). A pure mirror — the world ticks the shot; this copies its position each frame.</summary>
public partial class Projectile3DView : Node3D
{
    private const float Radius = 6f;
    private const float Height = 18f; // ride a little above the ground so it reads against the field

    private IProjectile? _projectile;

    public override void _Ready()
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = Radius, Height = Radius * 2f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.85f, 0.3f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.7f, 0.2f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        AddChild(mesh);
    }

    public void Bind(IProjectile projectile)
    {
        _projectile = projectile;
        UpdateFromModel();
    }

    public override void _Process(double delta) => UpdateFromModel();

    public void UpdateFromModel()
    {
        if (_projectile is not null)
        {
            Position = GroundProjection.ToWorld(_projectile.Position, Height);
        }
    }
}
