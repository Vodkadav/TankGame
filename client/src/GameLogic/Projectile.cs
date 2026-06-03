using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A fired shot. Pure C#: travels in a straight line at constant speed and dies
/// when the arena reports a hit within the step's travel distance, snapping to the contact
/// point. No Godot — the Presentation layer renders this state.</summary>
public sealed class Projectile : IProjectile
{
    private readonly IArena _arena;
    private readonly Vector2 _direction;
    private readonly float _speed;
    private readonly int _damage;

    /// <param name="arena">Collision query source.</param>
    /// <param name="spawn">World-space spawn position (the turret muzzle).</param>
    /// <param name="direction">Travel direction; normalised internally.</param>
    /// <param name="speed">Travel speed in units per second.</param>
    /// <param name="damage">Damage dealt to a destructible wall or tank on impact.</param>
    /// <param name="team">The firing tank's team; the combat pass spares the same team.</param>
    public Projectile(IArena arena, Vector2 spawn, Vector2 direction, float speed, int damage = 1, int team = 0)
    {
        Id = Guid.NewGuid();
        Team = team;
        _arena = arena;
        Position = spawn;
        _direction = Vector2.Normalize(direction);
        _speed = speed;
        _damage = damage;
        IsAlive = true;
    }

    public int Team { get; }
    public Guid Id { get; }
    public Vector2 Position { get; private set; }
    public bool IsAlive { get; private set; }

    /// <summary>Damage this shot deals to a tank on impact.</summary>
    public int Damage => _damage;

    /// <summary>Marks the shot spent so the world reaps it — used when the combat pass lands
    /// it on a tank.</summary>
    public void Expire() => IsAlive = false;

    public void Step(float deltaSeconds)
    {
        if (!IsAlive)
        {
            return;
        }

        var distance = _speed * deltaSeconds;

        if (_arena.RaycastFirstHit(Position, _direction, distance) is { } hit)
        {
            Position = hit.Point;
            _arena.DamageAt(hit.Point, _direction, _damage);
            IsAlive = false;
            return;
        }

        Position += _direction * distance;
    }
}
