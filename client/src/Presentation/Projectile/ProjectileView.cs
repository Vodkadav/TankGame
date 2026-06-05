using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IProjectile"/>: a pure mirror that each frame copies the
/// model's position onto the node. The world owns the tick (advancing and reaping the
/// projectile); the scene frees this view in response to the world's despawn event.</summary>
public partial class ProjectileView : Node2D
{
    private IProjectile? _projectile;

    public override void _Ready() =>
        GetNode<Sprite2D>("Bullet").Texture = GD.Load<Texture2D>(AssetCatalogue.Active.Bullet);

    public void Bind(IProjectile projectile)
    {
        _projectile = projectile;
        Position = new Vector2(projectile.Position.X, projectile.Position.Y);
    }

    public override void _Process(double delta) => UpdateFromModel();

    /// <summary>Mirrors the bound projectile's position onto the node. Public so tests can
    /// assert the mirror without relying on frame timing.</summary>
    public void UpdateFromModel()
    {
        if (_projectile is null)
        {
            return;
        }

        Position = new Vector2(_projectile.Position.X, _projectile.Position.Y);
    }
}
