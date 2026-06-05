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
        var screen = IsoProjection.WorldToScreen(projectile.Position);
        Position = new Vector2(screen.X, screen.Y);
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

        var screen = IsoProjection.WorldToScreen(_projectile.Position);
        Position = new Vector2(screen.X, screen.Y);
        ZIndex = IsoProjection.DepthOf(_projectile.Position);
        // The bullet art faces east at rotation 0; point it the way the shot is travelling.
        Rotation = Mathf.Atan2(_projectile.Direction.Y, _projectile.Direction.X);
    }
}
