using System;
using System.Linq;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A collectible on the field. Pure C#: each <see cref="Step"/> it scans the world for
/// a live tank within <c>pickupRadius</c>; the first one to overlap receives its
/// <see cref="StatusEffect"/> (via <see cref="Tank.ApplyEffect"/>) and the powerup expires so
/// the world reaps it. No Godot — the Presentation layer draws it. Powerups and traps are just
/// "apply an effect" (see <c>docs/adr/0012-stats-and-status-effects.md</c>).</summary>
public sealed class Powerup : IPowerup
{
    private readonly IWorld _world;
    private readonly StatusEffect _effect;
    private readonly float _pickupRadius;

    /// <param name="world">The world scanned for a collecting tank.</param>
    /// <param name="position">Where it sits on the field.</param>
    /// <param name="kind">Selects its colour (Presentation) and matches its effect.</param>
    /// <param name="effect">The timed effect granted to the tank that collects it.</param>
    /// <param name="pickupRadius">World distance within which a tank collects it.</param>
    public Powerup(IWorld world, Vector2 position, PowerupKind kind, StatusEffect effect, float pickupRadius)
    {
        Id = Guid.NewGuid();
        _world = world;
        Position = position;
        Kind = kind;
        _effect = effect;
        _pickupRadius = pickupRadius;
        IsAlive = true;
    }

    public Guid Id { get; }
    public Vector2 Position { get; }
    public PowerupKind Kind { get; }
    public bool IsAlive { get; private set; }

    public void Step(float deltaSeconds)
    {
        if (!IsAlive)
        {
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
                tank.ApplyEffect(_effect);
                IsAlive = false; // collected → the world reaps it this step
                return;
            }
        }
    }
}
