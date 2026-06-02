using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="ITank"/>: the node's position follows the model, the
/// Body sprite rotates with the chassis, the Turret sprite with the aim. The model is
/// injected via <see cref="Bind"/> (composition root) so this view holds no game rules.</summary>
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

    public override void _Process(double delta)
    {
        if (_tank is not null)
        {
            UpdateFromModel((float)delta);
        }
    }

    /// <summary>Advances the bound tank and mirrors its state onto the sprites. Public so
    /// tests can drive a deterministic step without relying on frame timing.</summary>
    public void UpdateFromModel(float deltaSeconds)
    {
        _tank!.Step(deltaSeconds);
        Position = new Vector2(_tank.Position.X, _tank.Position.Y);
        _body.Rotation = _tank.Rotation;
        _turret.Rotation = _tank.TurretRotation;
    }
}
