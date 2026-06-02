using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IProjectile"/>: each frame it advances the model, mirrors
/// the position onto the node, and frees itself once the projectile dies (hits a wall).</summary>
public partial class ProjectileView : Node2D
{
    private IProjectile? _projectile;

    public void Bind(IProjectile projectile)
    {
        _projectile = projectile;
        Position = new Vector2(projectile.Position.X, projectile.Position.Y);
    }

    public override void _Process(double delta) => Advance((float)delta);

    /// <summary>Advances the bound projectile and mirrors it onto the node, despawning the
    /// view when the projectile dies. Public so tests can drive a deterministic step.</summary>
    public void Advance(float deltaSeconds)
    {
        if (_projectile is null)
        {
            return;
        }

        _projectile.Step(deltaSeconds);
        Position = new Vector2(_projectile.Position.X, _projectile.Position.Y);

        if (!_projectile.IsAlive)
        {
            QueueFree();
        }
    }
}
