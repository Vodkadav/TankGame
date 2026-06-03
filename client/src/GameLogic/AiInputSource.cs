using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Drives an enemy tank by emitting the same <see cref="TankInput"/> intent a human
/// would — the multiplayer-safe seam (see <c>docs/adr/PROPOSAL-local-first-combat.md</c>).
/// Each tick it finds the nearest tank on another team, aims the turret at it, drives toward
/// it until within stand-off range, and fires when the target is in range with a clear line
/// of sight. Pure C#: no Godot, no pathfinding yet (it bulldozes brick walls in the way and
/// is stopped by steel). Bind it to the tank it drives after construction.</summary>
public sealed class AiInputSource : IInputSource
{
    private const float FireRange = 420f;
    private const float StandoffDistance = 160f;

    private readonly IWorld _world;
    private readonly IArena _arena;
    private ITank? _self;

    public AiInputSource(IWorld world, IArena arena)
    {
        _world = world;
        _arena = arena;
    }

    /// <summary>Links this controller to the tank it drives (resolves the construction cycle:
    /// the tank needs the input source, the input source needs the tank's position).</summary>
    public void Bind(ITank self) => _self = self;

    public TankInput Read()
    {
        if (_self is null || !_self.IsAlive)
        {
            return Hold();
        }

        var target = NearestEnemy();
        if (target is null)
        {
            return Hold();
        }

        var delta = target.Position - _self.Position;
        var distance = delta.Length();
        var aim = MathF.Atan2(delta.Y, delta.X);
        var move = distance > StandoffDistance ? Vector2.Normalize(delta) : Vector2.Zero;
        var fire = distance <= FireRange && HasLineOfSight(_self.Position, target.Position, distance);

        return new TankInput(move, aim, fire);
    }

    private TankInput Hold() => new(Vector2.Zero, _self?.TurretRotation ?? 0f, Fire: false);

    private ITank? NearestEnemy()
    {
        ITank? nearest = null;
        var nearestDistance = float.PositiveInfinity;

        foreach (var entity in _world.Entities)
        {
            if (entity is not ITank tank || !tank.IsAlive || tank.Team == _self!.Team)
            {
                continue;
            }

            var distance = Vector2.Distance(tank.Position, _self.Position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = tank;
            }
        }

        return nearest;
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to, float distance)
    {
        var direction = to - from;
        if (direction == Vector2.Zero)
        {
            return true;
        }

        var hit = _arena.RaycastFirstHit(from, direction, distance);
        return hit is null || hit.Value.Distance >= distance; // no wall before the target
    }
}
