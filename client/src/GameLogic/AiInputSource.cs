using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Drives an enemy tank by emitting the same <see cref="TankInput"/> intent a human
/// would — the multiplayer-safe seam (see <c>docs/adr/PROPOSAL-local-first-combat.md</c>).
/// Each tick it finds the nearest tank on another team that it can actually <em>see</em> —
/// within <see cref="VisionRange"/> and with a clear line of sight — aims the turret at it,
/// drives toward it until within stand-off range, and fires when in firing range. An enemy
/// hidden behind a wall (or too far off) is not hunted: the AI holds until something comes
/// into view, so cover and concealment matter. Pure C#: no Godot, no pathfinding yet (it
/// bulldozes brick walls in the way and is stopped by steel). Bind it to the tank it drives
/// after construction.</summary>
public sealed class AiInputSource : IInputSource
{
    private const float FireRange = 420f;
    private const float StandoffDistance = 160f;

    // Generous sight (≈18 tiles) so enemies engage across the open battlefield's clear
    // sightlines and the game stays lively — while line-of-sight gating means walls (and,
    // later, bushes) still break the AI's view and enable ambush. A tunable balance knob.
    private const float VisionRange = 1200f;

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

        var target = NearestVisibleEnemy();
        if (target is null)
        {
            return Hold();
        }

        var delta = target.Position - _self.Position;
        var distance = delta.Length();
        var aim = MathF.Atan2(delta.Y, delta.X);
        var move = distance > StandoffDistance ? Vector2.Normalize(delta) : Vector2.Zero;
        var fire = distance <= FireRange; // the target is already known to be in sight

        return new TankInput(move, aim, fire);
    }

    private TankInput Hold() => new(Vector2.Zero, _self?.TurretRotation ?? 0f, Fire: false);

    // The nearest enemy the AI can see: on another team, within VisionRange, and with no wall
    // between. Enemies it cannot see are invisible to its decision-making entirely.
    private ITank? NearestVisibleEnemy()
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
            if (distance > VisionRange || distance >= nearestDistance)
            {
                continue;
            }

            if (!HasLineOfSight(_self.Position, tank.Position, distance))
            {
                continue;
            }

            nearestDistance = distance;
            nearest = tank;
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
