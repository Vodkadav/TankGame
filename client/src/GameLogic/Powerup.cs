using System;
using System.Linq;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A collectible on the field. Pure C#: while available, each <see cref="Step"/> it scans
/// the world for a live tank within <c>pickupRadius</c>; the first one to overlap receives its effect
/// (via <see cref="IPickupEffect"/>). It then behaves in one of three ways:
/// <list type="bullet">
/// <item>one-shot (default) — expires so the world reaps it;</item>
/// <item><c>dropOnCarrierDeath</c> — goes dormant in the carrier's hands and reappears where that tank
/// dies, so a powerup changes the balance of a fight until its holder is destroyed, then drops back
/// onto the field where it fell;</item>
/// <item><c>respawnCooldown</c> — a fixed station: it stays at its spot, goes dormant on collection, and
/// becomes available there again after the cooldown (the airstrike refills this way).</item>
/// </list>
/// No Godot — the Presentation layer draws it. Powerups and traps are just "apply an effect" (see
/// <c>docs/adr/0012-stats-and-status-effects.md</c>).</summary>
public sealed class Powerup : IPowerup
{
    private readonly IWorld _world;
    private readonly IPickupEffect _effect;
    private readonly float _pickupRadius;
    private readonly bool _dropOnCarrierDeath;
    private readonly float _respawnCooldown;
    private Tank? _carrier;
    private float _cooldownRemaining;

    /// <param name="world">The world scanned for a collecting tank.</param>
    /// <param name="position">Where it sits on the field.</param>
    /// <param name="kind">Selects its colour (Presentation) and matches its effect.</param>
    /// <param name="effect">What it does to the tank that collects it (a stat effect or ammo).</param>
    /// <param name="pickupRadius">World distance within which a tank collects it.</param>
    /// <param name="dropOnCarrierDeath">When true the pickup is not reaped on collection: it goes
    /// dormant with its collector and reappears where that tank dies. False (default) = one-shot.</param>
    /// <param name="respawnCooldown">When greater than zero the pickup is a fixed station: it stays put,
    /// goes dormant on collection, and becomes available again at the same spot after this many seconds.
    /// Mutually exclusive with <paramref name="dropOnCarrierDeath"/>.</param>
    public Powerup(IWorld world, Vector2 position, PowerupKind kind, IPickupEffect effect,
        float pickupRadius, bool dropOnCarrierDeath = false, float respawnCooldown = 0f)
    {
        Id = EntityId.Next();
        _world = world;
        Position = position;
        Kind = kind;
        _effect = effect;
        _pickupRadius = pickupRadius;
        _dropOnCarrierDeath = dropOnCarrierDeath;
        _respawnCooldown = respawnCooldown;
        IsAlive = true;
        IsAvailable = true;
    }

    public Guid Id { get; }
    public Vector2 Position { get; private set; }
    public PowerupKind Kind { get; }
    public bool IsAlive { get; private set; }
    public bool IsAvailable { get; private set; }

    public event Action<PowerupKind>? Collected;

    public void Step(float deltaSeconds)
    {
        if (!IsAlive)
        {
            return;
        }

        if (!IsAvailable)
        {
            if (_dropOnCarrierDeath)
            {
                // Carried and dormant: drop back onto the field, where the carrier died, once it falls.
                if (_carrier is { Hp: <= 0 })
                {
                    Position = _carrier.Position;
                    _carrier = null;
                    IsAvailable = true;
                }
            }
            else if (_respawnCooldown > 0f)
            {
                // A fixed station refilling: become collectable again at its spot once the cooldown ends.
                _cooldownRemaining -= deltaSeconds;
                if (_cooldownRemaining <= 0f)
                {
                    IsAvailable = true;
                }
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
                _effect.ApplyTo(tank, _world);
                Collected?.Invoke(Kind);
                if (_dropOnCarrierDeath)
                {
                    _carrier = tank;     // dormant until this tank dies, then it drops where it fell
                    IsAvailable = false;
                }
                else if (_respawnCooldown > 0f)
                {
                    _cooldownRemaining = _respawnCooldown; // dormant at its spot until the cooldown ends
                    IsAvailable = false;
                }
                else
                {
                    IsAlive = false;     // one-shot → the world reaps it this step
                }

                return;
            }
        }
    }
}
