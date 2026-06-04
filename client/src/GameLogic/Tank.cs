using System;
using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A locally-driven tank — a world <see cref="IEntity"/>. Pure C#: reads an
/// <see cref="IInputSource"/> each <see cref="Step"/>, moves at a constant speed, faces its
/// movement direction, aims its turret at the input, and (rate-limited) spawns a
/// <see cref="Projectile"/> into the <see cref="IWorld"/> when fire is held. No Godot — the
/// Presentation layer renders this state.</summary>
public sealed class Tank : ITank
{
    private readonly IInputSource _input;
    private readonly IWorld _world;
    private readonly IArena _arena;
    private readonly Vector2 _spawnPosition;
    private readonly Stats _stats;
    private readonly float _projectileSpeed;
    private float _fireCooldown;
    private float _pushTimer;
    private int _livesRemaining;
    private float _respawnTimer;
    private readonly IWeapon _defaultWeapon = new BehaviourWeapon(() => StraightBehaviour.Instance);
    private IWeapon? _specialWeapon;
    private int _specialShots;

    /// <param name="input">Per-frame intent source.</param>
    /// <param name="world">The world the tank spawns its shots into.</param>
    /// <param name="arena">Collision source handed to spawned projectiles.</param>
    /// <param name="startPosition">Initial world-space position.</param>
    /// <param name="speed">Movement speed in units per second.</param>
    /// <param name="fireInterval">Minimum seconds between shots.</param>
    /// <param name="projectileSpeed">Speed of spawned projectiles, units per second.</param>
    /// <param name="maxHp">Hit points at full health.</param>
    /// <param name="team">The side this tank fights for (its shots spare the same team).</param>
    /// <param name="lives">Total lives, including the current one: a tank revives at its spawn
    /// after each death until its lives run out, then the world reaps it. Default 1 = no respawn.</param>
    public Tank(
        IInputSource input,
        IWorld world,
        IArena arena,
        Vector2 startPosition,
        float speed,
        float fireInterval,
        float projectileSpeed,
        int maxHp = 3,
        int team = 0,
        int lives = 1)
    {
        Id = Guid.NewGuid();
        MaxHp = maxHp;
        Hp = maxHp;
        Team = team;
        _livesRemaining = lives;
        _input = input;
        _world = world;
        _arena = arena;
        Position = startPosition;
        _spawnPosition = startPosition;
        _stats = new Stats(new Dictionary<StatKind, float>
        {
            [StatKind.Speed] = speed,
            [StatKind.FireInterval] = fireInterval,
        });
        _projectileSpeed = projectileSpeed;
    }

    /// <summary>Applies a timed <see cref="StatusEffect"/> to this tank (a powerup or trap):
    /// it modifies the matching stat until it expires. See
    /// <c>docs/adr/0012-stats-and-status-effects.md</c>.</summary>
    public void ApplyEffect(StatusEffect effect) => _stats.Apply(effect);

    /// <summary>Loads a special weapon for the next <paramref name="shots"/> shots (an ammo
    /// crate); once spent, the tank reverts to its default straight shot. See
    /// <c>docs/adr/0013-weapon-behaviour-strategy.md</c>.</summary>
    public void LoadAmmo(IWeapon weapon, int shots)
    {
        _specialWeapon = weapon;
        _specialShots = shots;
    }

    /// <summary>Half-extent of the tank used for wall collision: its leading edge, not its
    /// centre, is what must clear a wall.</summary>
    public const float CollisionRadius = 24f;

    /// <summary>Seconds of sustained pushing between each point of demolition damage. Three
    /// hits (a full-hp brick) break in <c>3 ×</c> this — about 1.2 s of shoving.</summary>
    public const float PushInterval = 0.4f;
    private const int PushDamage = 1;

    /// <summary>Seconds a tank stays down after a death before it revives at its spawn point
    /// (only while it still has lives left).</summary>
    public const float RespawnDelay = 2f;

    public Guid Id { get; }
    public Vector2 Position { get; private set; }
    public float Rotation { get; private set; }
    public float TurretRotation { get; private set; }

    public int Hp { get; private set; }
    public int MaxHp { get; }
    public int Team { get; }
    public int Shield { get; private set; }

    // "In the match": a tank with positive hit points is fighting; one at zero hit points is
    // down but still in the match while it has a life left to respawn. Only once both are gone
    // does the world reap it. Combat, collision, and the view instead key off Hp > 0 (whether
    // it is a tangible, fightable tank right now).
    public bool IsAlive => Hp > 0 || _livesRemaining > 0;

    /// <summary>Restores hit points up to <see cref="MaxHp"/> (a repair pickup). A no-op on a
    /// downed tank — repair cannot revive; only the respawn timer does.</summary>
    public void Heal(int amount)
    {
        if (amount <= 0 || Hp <= 0)
        {
            return;
        }

        Hp = Math.Min(MaxHp, Hp + amount);
    }

    /// <summary>Adds over-shield points (a shield pickup): a buffer that incoming damage spends
    /// before hit points. Stacks; not capped by <see cref="MaxHp"/>.</summary>
    public void AddShield(int amount)
    {
        if (amount > 0)
        {
            Shield += amount;
        }
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || Hp <= 0)
        {
            return; // no damage, or already down (the kill was already credited)
        }

        if (Shield > 0)
        {
            var absorbed = Math.Min(Shield, amount);
            Shield -= absorbed;
            amount -= absorbed;
            if (amount == 0)
            {
                return; // the shield soaked the whole hit
            }
        }

        Hp = Math.Max(0, Hp - amount);
        if (Hp == 0)
        {
            _livesRemaining--; // spend the life just lost; respawn if any remain
            if (_livesRemaining > 0)
            {
                _respawnTimer = RespawnDelay;
            }
        }
    }

    public void Step(float deltaSeconds)
    {
        _stats.Step(deltaSeconds); // age timed effects in real time, even while down

        if (Hp <= 0)
        {
            // Down: inert (no movement, aim, or fire) until the respawn timer elapses, then
            // revive at the spawn point. A tank out of lives has no timer and simply waits to
            // be reaped.
            if (_respawnTimer > 0f)
            {
                _respawnTimer -= deltaSeconds;
                if (_respawnTimer <= 0f)
                {
                    Hp = MaxHp;
                    Position = _spawnPosition;
                    _fireCooldown = 0f;
                    _pushTimer = 0f;
                }
            }

            return;
        }

        var input = _input.Read();

        var move = input.Move;
        if (move.LengthSquared() > 1f)
        {
            move = Vector2.Normalize(move); // a (1,1) keyboard diagonal must not be faster
        }

        var desired = Position + (move * _stats.Current(StatKind.Speed) * deltaSeconds);

        // Resolve each axis independently so the tank slides along a wall instead of sticking.
        // A move is allowed only if the leading edge (centre + radius) clears walls and the new
        // centre does not overlap another tank. Only wall blocks feed push-to-demolish — bumping
        // a tank must never chip a wall behind it, so wall and tank blocks are tracked apart.
        var wallX = false;
        var nextX = Position.X;
        if (move.X != 0f)
        {
            var hitWall = _arena.IsBlocked(new Vector2(desired.X + (MathF.Sign(move.X) * CollisionRadius), Position.Y));
            if (!hitWall && !OverlapsAnotherTank(new Vector2(desired.X, Position.Y)))
            {
                nextX = desired.X;
            }
            else
            {
                wallX = hitWall;
            }
        }

        var wallY = false;
        var nextY = Position.Y;
        if (move.Y != 0f)
        {
            var hitWall = _arena.IsBlocked(new Vector2(nextX, desired.Y + (MathF.Sign(move.Y) * CollisionRadius)));
            if (!hitWall && !OverlapsAnotherTank(new Vector2(nextX, desired.Y)))
            {
                nextY = desired.Y;
            }
            else
            {
                wallY = hitWall;
            }
        }

        Position = new Vector2(nextX, nextY);

        PushAgainstWall(wallX, wallY, move, deltaSeconds);

        if (move != Vector2.Zero)
        {
            Rotation = MathF.Atan2(move.Y, move.X); // chassis keeps its last facing when idle
        }

        TurretRotation = input.Aim;

        _fireCooldown -= deltaSeconds;
        if (_fireCooldown <= 0f && input.Fire)
        {
            _fireCooldown = _stats.Current(StatKind.FireInterval);
            var direction = new Vector2(MathF.Cos(TurretRotation), MathF.Sin(TurretRotation));

            // Fire the loaded special weapon while ammo lasts, then fall back to the default shot.
            var weapon = _specialShots > 0 ? _specialWeapon! : _defaultWeapon;
            weapon.Fire(_world, _arena, Position, direction, _projectileSpeed, Team);
            if (_specialShots > 0 && --_specialShots == 0)
            {
                _specialWeapon = null;
            }
        }
    }

    // Driving into a destructible wall demolishes it: while the tank is jammed against a wall
    // it is pushing into, accumulate time and chip that wall once per PushInterval. DamageAt
    // only affects brick, so steel and the border shrug it off. Releasing the stick or
    // breaking through resets the timer, so demolition needs sustained pressure, not a tap.
    private void PushAgainstWall(bool wallX, bool wallY, Vector2 move, float deltaSeconds)
    {
        if (!wallX && !wallY)
        {
            _pushTimer = 0f;
            return;
        }

        _pushTimer += deltaSeconds;
        if (_pushTimer < PushInterval)
        {
            return;
        }

        _pushTimer -= PushInterval;

        var (point, direction) = wallX
            ? (new Vector2(Position.X + (MathF.Sign(move.X) * CollisionRadius), Position.Y),
                new Vector2(MathF.Sign(move.X), 0f))
            : (new Vector2(Position.X, Position.Y + (MathF.Sign(move.Y) * CollisionRadius)),
                new Vector2(0f, MathF.Sign(move.Y)));

        _arena.DamageAt(point, direction, PushDamage);
    }

    // A tank is a circle of CollisionRadius; two tanks may not overlap. Scans the world for any
    // other live tank whose centre is within a tank-diameter of this candidate centre. Order is
    // tick-order-dependent (the tank that steps first claims the ground), which is fine for a
    // body block — no momentum is transferred, the mover simply stops.
    private bool OverlapsAnotherTank(Vector2 candidateCentre)
    {
        var minDistance = CollisionRadius * 2f;
        foreach (var entity in _world.Entities)
        {
            if (entity is ITank other && !ReferenceEquals(other, this) && other.Hp > 0 &&
                Vector2.DistanceSquared(candidateCentre, other.Position) < minDistance * minDistance)
            {
                return true;
            }
        }

        return false;
    }
}
