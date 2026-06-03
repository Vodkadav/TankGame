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
    /// <param name="damage">Damage dealt to a destructible wall on impact.</param>
    public Projectile(IArena arena, Vector2 spawn, Vector2 direction, float speed, int damage = 1)
    {
        Id = Guid.NewGuid();
        _arena = arena;
        Position = spawn;
        _direction = Vector2.Normalize(direction);
        _speed = speed;
        _damage = damage;
        IsAlive = true;
    }

    public Guid Id { get; }
    public Vector2 Position { get; private set; }
    public bool IsAlive { get; private set; }

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
