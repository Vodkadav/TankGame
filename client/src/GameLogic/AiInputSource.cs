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
    // sightlines and the game stays lively — while line-of-sight gating means walls and bushes
    // still break the AI's view and enable ambush. A tunable balance knob.
    private const float VisionRange = 1200f;

    // A tank hiding in a bush is only spotted from within about a tile and a half — close
    // enough to brush the foliage. Beyond that the AI cannot pick it out of the cover.
    private const float BushRevealRange = 96f;

    // How far the AI will break off to grab a pickup it can see (≈11 tiles). Smaller than the
    // vision range so it collects nearby crates without abandoning the fight to chase a far one.
    private const float PowerupSeekRange = 700f;

    private readonly IWorld _world;
    private readonly IArena _arena;
    private readonly IConcealment? _concealment;
    private ITank? _self;

    public AiInputSource(IWorld world, IArena arena, IConcealment? concealment = null)
    {
        _world = world;
        _arena = arena;
        _concealment = concealment;
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
            // No-one to fight: grab a pickup if one is in view, otherwise sit tight.
            var loose = NearestReachablePowerup();
            return loose is null ? Hold() : DriveToward(loose.Position);
        }

        var delta = target.Position - _self.Position;
        var distance = delta.Length();
        var aim = MathF.Atan2(delta.Y, delta.X);

        if (distance <= FireRange)
        {
            // Engaging: hold at stand-off and shoot — don't wander off for a pickup mid-fight.
            var holdMove = distance > StandoffDistance ? Vector2.Normalize(delta) : Vector2.Zero;
            return new TankInput(holdMove, aim, Fire: true);
        }

        // The enemy is seen but out of range: close in, detouring through a nearby pickup if there
        // is one on the way, while keeping the gun trained on the threat.
        var detour = NearestReachablePowerup();
        var move = detour is not null
            ? DirectionTo(detour.Position)
            : Vector2.Normalize(delta);
        return new TankInput(move, aim, Fire: false);
    }

    // Steer straight at a point (aim along the travel direction); used when chasing a pickup with
    // no enemy to keep the gun on.
    private TankInput DriveToward(Vector2 point)
    {
        var delta = point - _self!.Position;
        return new TankInput(DirectionTo(point), MathF.Atan2(delta.Y, delta.X), Fire: false);
    }

    // Unit vector toward a point, or zero if we are already on it (avoids a NaN from normalising
    // a zero vector when the AI is sitting on the pickup the instant before it is collected).
    private Vector2 DirectionTo(Vector2 point)
    {
        var delta = point - _self!.Position;
        return delta == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(delta);
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
            if (entity is not ITank tank || tank.Hp <= 0 || tank.Team == _self!.Team)
            {
                continue; // skip allies and downed enemies (a respawning tank is no target)
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

            // A target lurking in a bush is hidden unless the AI is right on top of it.
            if (distance > BushRevealRange && _concealment?.ConcealsAt(tank.Position) == true)
            {
                continue;
            }

            nearestDistance = distance;
            nearest = tank;
        }

        return nearest;
    }

    // The nearest pickup the AI can see and reach: available (not collected/dormant), within
    // PowerupSeekRange, and with a clear line of sight (it won't chase a crate walled off behind
    // steel it can't see past).
    private IPowerup? NearestReachablePowerup()
    {
        IPowerup? nearest = null;
        var nearestDistance = float.PositiveInfinity;

        foreach (var entity in _world.Entities)
        {
            if (entity is not IPowerup powerup || !powerup.IsAvailable)
            {
                continue;
            }

            var distance = Vector2.Distance(powerup.Position, _self!.Position);
            if (distance > PowerupSeekRange || distance >= nearestDistance)
            {
                continue;
            }

            if (!HasLineOfSight(_self.Position, powerup.Position, distance))
            {
                continue;
            }

            nearestDistance = distance;
            nearest = powerup;
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
