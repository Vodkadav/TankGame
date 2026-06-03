using System;
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
    private readonly float _speed;
    private readonly float _fireInterval;
    private readonly float _projectileSpeed;
    private float _fireCooldown;
    private float _pushTimer;

    /// <param name="input">Per-frame intent source.</param>
    /// <param name="world">The world the tank spawns its shots into.</param>
    /// <param name="arena">Collision source handed to spawned projectiles.</param>
    /// <param name="startPosition">Initial world-space position.</param>
    /// <param name="speed">Movement speed in units per second.</param>
    /// <param name="fireInterval">Minimum seconds between shots.</param>
    /// <param name="projectileSpeed">Speed of spawned projectiles, units per second.</param>
    /// <param name="maxHp">Hit points at full health.</param>
    public Tank(
        IInputSource input,
        IWorld world,
        IArena arena,
        Vector2 startPosition,
        float speed,
        float fireInterval,
        float projectileSpeed,
        int maxHp = 3)
    {
        Id = Guid.NewGuid();
        MaxHp = maxHp;
        Hp = maxHp;
        _input = input;
        _world = world;
        _arena = arena;
        Position = startPosition;
        _speed = speed;
        _fireInterval = fireInterval;
        _projectileSpeed = projectileSpeed;
    }

    /// <summary>Half-extent of the tank used for wall collision: its leading edge, not its
    /// centre, is what must clear a wall.</summary>
    public const float CollisionRadius = 24f;

    /// <summary>Seconds of sustained pushing between each point of demolition damage. Three
    /// hits (a full-hp brick) break in <c>3 ×</c> this — about 1.2 s of shoving.</summary>
    public const float PushInterval = 0.4f;
    private const int PushDamage = 1;

    public Guid Id { get; }
    public Vector2 Position { get; private set; }
    public float Rotation { get; private set; }
    public float TurretRotation { get; private set; }

    public int Hp { get; private set; }
    public int MaxHp { get; }

    // Reaped by the world once its hit points are gone.
    public bool IsAlive => Hp > 0;

    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Hp = Math.Max(0, Hp - amount);
    }

    public void Step(float deltaSeconds)
    {
        var input = _input.Read();

        var move = input.Move;
        if (move.LengthSquared() > 1f)
        {
            move = Vector2.Normalize(move); // a (1,1) keyboard diagonal must not be faster
        }

        var desired = Position + (move * _speed * deltaSeconds);

        // Resolve each axis independently so the tank slides along a wall instead of
        // sticking. A move is allowed only if the leading edge (centre + radius) stays clear.
        var blockedX = false;
        var nextX = Position.X;
        if (move.X != 0f)
        {
            if (!_arena.IsBlocked(new Vector2(desired.X + (MathF.Sign(move.X) * CollisionRadius), Position.Y)))
            {
                nextX = desired.X;
            }
            else
            {
                blockedX = true;
            }
        }

        var blockedY = false;
        var nextY = Position.Y;
        if (move.Y != 0f)
        {
            if (!_arena.IsBlocked(new Vector2(nextX, desired.Y + (MathF.Sign(move.Y) * CollisionRadius))))
            {
                nextY = desired.Y;
            }
            else
            {
                blockedY = true;
            }
        }

        Position = new Vector2(nextX, nextY);

        PushAgainstWall(blockedX, blockedY, move, deltaSeconds);

        if (move != Vector2.Zero)
        {
            Rotation = MathF.Atan2(move.Y, move.X); // chassis keeps its last facing when idle
        }

        TurretRotation = input.Aim;

        _fireCooldown -= deltaSeconds;
        if (_fireCooldown <= 0f && input.Fire)
        {
            _fireCooldown = _fireInterval;
            var direction = new Vector2(MathF.Cos(TurretRotation), MathF.Sin(TurretRotation));
            _world.Spawn(new Projectile(_arena, Position, direction, _projectileSpeed));
        }
    }

    // Driving into a destructible wall demolishes it: while the tank is jammed against a wall
    // it is pushing into, accumulate time and chip that wall once per PushInterval. DamageAt
    // only affects brick, so steel and the border shrug it off. Releasing the stick or
    // breaking through resets the timer, so demolition needs sustained pressure, not a tap.
    private void PushAgainstWall(bool blockedX, bool blockedY, Vector2 move, float deltaSeconds)
    {
        if (!blockedX && !blockedY)
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

        var (point, direction) = blockedX
            ? (new Vector2(Position.X + (MathF.Sign(move.X) * CollisionRadius), Position.Y),
                new Vector2(MathF.Sign(move.X), 0f))
            : (new Vector2(Position.X, Position.Y + (MathF.Sign(move.Y) * CollisionRadius)),
                new Vector2(0f, MathF.Sign(move.Y)));

        _arena.DamageAt(point, direction, PushDamage);
    }
}
