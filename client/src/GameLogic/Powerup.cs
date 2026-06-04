using System;
using System.Linq;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A collectible on the field. Pure C#: while available, each <see cref="Step"/> it scans
/// the world for a live tank within <c>pickupRadius</c>; the first one to overlap receives its
/// effect (via <see cref="IPickupEffect"/>). A one-shot pickup (<c>respawnDelay</c> 0) then expires
/// so the world reaps it; a respawning pickup goes dormant for the delay and becomes available
/// again at the same spot — it stays in the world the whole time, the view just hides while it is
/// unavailable. No Godot — the Presentation layer draws it. Powerups and traps are just "apply an
/// effect" (see <c>docs/adr/0012-stats-and-status-effects.md</c>).</summary>
public sealed class Powerup : IPowerup
{
    private readonly IWorld _world;
    private readonly IPickupEffect _effect;
    private readonly float _pickupRadius;
    private readonly float _respawnDelay;
    private float _cooldown;

    /// <param name="world">The world scanned for a collecting tank.</param>
    /// <param name="position">Where it sits on the field.</param>
    /// <param name="kind">Selects its colour (Presentation) and matches its effect.</param>
    /// <param name="effect">What it does to the tank that collects it (a stat effect or ammo).</param>
    /// <param name="pickupRadius">World distance within which a tank collects it.</param>
    /// <param name="respawnDelay">Seconds before a collected pickup returns. 0 (default) = one-shot:
    /// it is reaped on collection and does not respawn.</param>
    public Powerup(IWorld world, Vector2 position, PowerupKind kind, IPickupEffect effect,
        float pickupRadius, float respawnDelay = 0f)
    {
        Id = Guid.NewGuid();
        _world = world;
        Position = position;
        Kind = kind;
        _effect = effect;
        _pickupRadius = pickupRadius;
        _respawnDelay = respawnDelay;
        IsAlive = true;
        IsAvailable = true;
    }

    public Guid Id { get; }
    public Vector2 Position { get; }
    public PowerupKind Kind { get; }
    public bool IsAlive { get; private set; }
    public bool IsAvailable { get; private set; }

    public void Step(float deltaSeconds)
    {
        if (!IsAlive)
        {
            return;
        }

        if (!IsAvailable)
        {
            // Dormant after a collection: count down, then return to the field.
            _cooldown -= deltaSeconds;
            if (_cooldown <= 0f)
            {
                IsAvailable = true;
            }

            return;
        }

        foreach (var tank in _world.Entities.OfType<Tank>())
        {
            if (tank.Hp <= 0)
            {
                continue; // only a live, tangible tank collects (not one awaiting respawn)
            }

            if (Vector2.DistanceSquared(Position, tank.Position) <= _pickupRadius * _pickupRadius)
            {
                _effect.ApplyTo(tank);
                if (_respawnDelay > 0f)
                {
                    IsAvailable = false; // dormant until it respawns at the same spot
                    _cooldown = _respawnDelay;
                }
                else
                {
                    IsAlive = false; // one-shot → the world reaps it this step
                }

                return;
            }
        }
    }
}
