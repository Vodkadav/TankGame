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

    // The AI's circle of vision (≈10 tiles): it is unaware of tanks outside this radius — it does not
    // see across the whole map. Line-of-sight gating means walls and bushes break the view within it,
    // too. Roughly matches the player's lit vision circle so both sides see about as far. A firing tank
    // can still draw the AI from beyond this (handled separately). A tunable balance knob.
    private const float VisionRange = 640f;

    // A tank hiding in a bush is only spotted from within about a tile and a half — close
    // enough to brush the foliage. Beyond that the AI cannot pick it out of the cover.
    private const float BushRevealRange = 96f;

    // How far the AI will break off to grab a pickup it can see (≈11 tiles). Smaller than the
    // vision range so it collects nearby crates without abandoning the fight to chase a far one.
    private const float PowerupSeekRange = 700f;

    // How far an ambusher will travel to reach a patch of grass to lie in wait (≈9 tiles).
    private const float AmbushSeekRange = 600f;

    // Roughly how many ticks the AI holds a wander heading before picking a fresh one, so an idle
    // tank roams the field looking for a fight instead of standing still.
    private const int WanderTicks = 75;

    private readonly IWorld _world;
    private readonly IArena _arena;
    private readonly IConcealment? _concealment;
    private readonly bool _ambusher;
    private ITank? _self;
    private Random _rng = new();
    private Vector2 _wanderDirection;
    private int _wanderCooldown;

    /// <param name="ambusher">When true (and concealment exists), this tank prefers to slip into grass
    /// and snipe from cover rather than charge — so some enemies lie in wait. Bind it after construction.</param>
    public AiInputSource(IWorld world, IArena arena, IConcealment? concealment = null, bool ambusher = false)
    {
        _world = world;
        _arena = arena;
        _concealment = concealment;
        _ambusher = ambusher;
    }

    /// <summary>Links this controller to the tank it drives (resolves the construction cycle:
    /// the tank needs the input source, the input source needs the tank's position).</summary>
    public void Bind(ITank self)
    {
        _self = self;
        _rng = new Random(self.Id.GetHashCode()); // varied per tank, stable for one tank
    }

    public TankInput Read()
    {
        if (_self is null || !_self.IsAlive)
        {
            return Hold();
        }

        if (_ambusher && _concealment is not null && Ambush() is { } ambushIntent)
        {
            return ambushIntent;
        }

        var target = NearestVisibleEnemy();
        if (target is null)
        {
            // No-one to fight: grab a pickup if one is in view, otherwise roam to find a fight.
            var loose = NearestReachablePowerup();
            return loose is null ? Wander() : DriveToward(loose.Position);
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

    // Ambusher mode: slip into the nearest grass and snipe from cover. While hidden it holds position
    // and fires at any enemy in range; otherwise it moves to the grass. Returns null when no grass is
    // within reach, so the AI falls back to fighting normally.
    private TankInput? Ambush()
    {
        var enemy = NearestVisibleEnemy();
        if (enemy is null)
        {
            return null; // nothing in sight to ambush — fall through to roam/hunt, don't just sit
        }

        var aim = AimAt(enemy.Position);
        var fire = Vector2.Distance(enemy.Position, _self!.Position) <= FireRange;

        if (_concealment!.ConcealsAt(_self.Position))
        {
            return new TankInput(Vector2.Zero, aim, fire); // hidden with prey in sight — lie in wait
        }

        var spot = _concealment.NearestConcealment(_self.Position, AmbushSeekRange);
        return spot is null ? null : new TankInput(DirectionTo(spot.Value), aim, fire);
    }

    // Roam: hold a heading for a while, then pick a fresh one, so an idle tank keeps moving and
    // bumps into fights instead of standing still.
    private TankInput Wander()
    {
        if (_wanderCooldown <= 0 || _wanderDirection == Vector2.Zero)
        {
            var angle = _rng.NextDouble() * Math.Tau;
            _wanderDirection = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            _wanderCooldown = WanderTicks;
        }

        _wanderCooldown--;
        return new TankInput(_wanderDirection, MathF.Atan2(_wanderDirection.Y, _wanderDirection.X), Fire: false);
    }

    private float AimAt(Vector2 point)
    {
        var delta = point - _self!.Position;
        return MathF.Atan2(delta.Y, delta.X);
    }

    private TankInput Hold() => new(Vector2.Zero, _self?.TurretRotation ?? 0f, Fire: false);

    // The nearest tank the AI can see — ANY tank but itself (it attacks other AI as well as the
    // player), within VisionRange and with no wall between. Tanks it cannot see are invisible to its
    // decision-making entirely.
    private ITank? NearestVisibleEnemy()
    {
        ITank? nearest = null;
        var nearestDistance = float.PositiveInfinity;

        foreach (var entity in _world.Entities)
        {
            if (entity is not ITank tank || tank.Hp <= 0 || tank.Id == _self!.Id)
            {
                continue; // skip itself and downed tanks (a respawning tank is no target)
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
