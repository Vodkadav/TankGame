using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IProjectile"/> in 3D (ADR-0017): an ordinary shot is a small glowing
/// sphere; a missile shot (from the Missile pickup) is a larger elongated bolt pointed along its travel
/// direction. A pure mirror — the world ticks the shot; this copies its position (and a missile's heading)
/// each frame.</summary>
public partial class Projectile3DView : Node3D
{
    private const float Height = 18f; // ride above the ground so it reads against the field

    private IProjectile? _projectile;
    private bool _missile;

    public void Bind(IProjectile projectile) => _projectile = projectile;

    public override void _Ready()
    {
        _missile = _projectile?.Style == ProjectileStyle.Missile;
        AddChild(_missile ? MissileMesh() : BulletMesh());
        UpdateFromModel();
    }

    public override void _Process(double delta) => UpdateFromModel();

    public void UpdateFromModel()
    {
        if (_projectile is null)
        {
            return;
        }

        Position = GroundProjection.ToWorld(_projectile.Position, Height);
        if (_missile)
        {
            var dir = _projectile.Direction; // game (x,y) → world (x,z); point the bolt along travel
            Rotation = new Vector3(0f, Mathf.Atan2(dir.X, dir.Y), 0f);
        }
    }

    private static MeshInstance3D BulletMesh() => new()
    {
        Name = "Bullet",
        Mesh = new SphereMesh { Radius = 6f, Height = 12f },
        MaterialOverride = Glow(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.7f, 0.2f)),
    };

    // A capsule lying along +Z (rotated from its default +Y), so the view's yaw aims it along travel.
    private static MeshInstance3D MissileMesh() => new()
    {
        Name = "Missile",
        Mesh = new CapsuleMesh { Radius = 5f, Height = 30f },
        RotationDegrees = new Vector3(90f, 0f, 0f),
        MaterialOverride = Glow(new Color(0.95f, 0.35f, 0.18f), new Color(1f, 0.45f, 0.15f)),
    };

    private static StandardMaterial3D Glow(Color albedo, Color emission) => new()
    {
        AlbedoColor = albedo,
        EmissionEnabled = true,
        Emission = emission,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };
}
