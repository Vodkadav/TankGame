using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="ITank"/>: a pure mirror that copies the model's position
/// onto the node, the chassis facing onto the Body sprite, and the aim onto the Turret
/// sprite. The world owns the tick (advancing the tank); this view holds no game rules.</summary>
public partial class TankView : Node2D
{
    private ITank? _tank;
    private Sprite2D _body = null!;
    private Sprite2D _turret = null!;

    public override void _Ready()
    {
        _body = GetNode<Sprite2D>("Body");
        _turret = GetNode<Sprite2D>("Turret");
    }

    public void Bind(ITank tank) => _tank = tank;

    public override void _Process(double delta) => UpdateFromModel();

    /// <summary>Mirrors the bound tank's state onto the node and sprites. Public so tests
    /// can assert the mirror without relying on frame timing.</summary>
    public void UpdateFromModel()
    {
        if (_tank is null)
        {
            return;
        }

        Position = new Vector2(_tank.Position.X, _tank.Position.Y);
        _body.Rotation = _tank.Rotation;
        _turret.Rotation = _tank.TurretRotation;
    }
}
