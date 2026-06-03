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

    /// <param name="input">Per-frame intent source.</param>
    /// <param name="world">The world the tank spawns its shots into.</param>
    /// <param name="arena">Collision source handed to spawned projectiles.</param>
    /// <param name="startPosition">Initial world-space position.</param>
    /// <param name="speed">Movement speed in units per second.</param>
    /// <param name="fireInterval">Minimum seconds between shots.</param>
    /// <param name="projectileSpeed">Speed of spawned projectiles, units per second.</param>
    public Tank(
        IInputSource input,
        IWorld world,
        IArena arena,
        Vector2 startPosition,
        float speed,
        float fireInterval,
        float projectileSpeed)
    {
        Id = Guid.NewGuid();
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

    public Guid Id { get; }
    public Vector2 Position { get; private set; }
    public float Rotation { get; private set; }
    public float TurretRotation { get; private set; }

    // No health yet — a tank is always alive until S3 introduces damage.
    public bool IsAlive => true;

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
        var nextX = Position.X;
        if (move.X != 0f &&
            !_arena.IsBlocked(new Vector2(desired.X + (MathF.Sign(move.X) * CollisionRadius), Position.Y)))
        {
            nextX = desired.X;
        }

        var nextY = Position.Y;
        if (move.Y != 0f &&
            !_arena.IsBlocked(new Vector2(nextX, desired.Y + (MathF.Sign(move.Y) * CollisionRadius))))
        {
            nextY = desired.Y;
        }

        Position = new Vector2(nextX, nextY);

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
}
