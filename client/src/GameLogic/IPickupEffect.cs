using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>What a <see cref="Powerup"/> does to the tank that collects it. Keeps the pickup entity
/// generic: a stat powerup applies a <see cref="StatusEffect"/>, an ammo crate loads a weapon, a
/// telephone spawns an airstrike — same field entity, same collect-and-expire flow, different effect.
/// Receives the <see cref="IWorld"/> so an effect can spawn another entity (the airstrike).</summary>
public interface IPickupEffect
{
    void ApplyTo(Tank tank, IWorld world);
}

/// <summary>A pickup that grants a timed <see cref="StatusEffect"/> (speed boost, rapid fire).</summary>
public sealed class StatusEffectPickup : IPickupEffect
{
    private readonly StatusEffect _effect;

    public StatusEffectPickup(StatusEffect effect) => _effect = effect;

    public void ApplyTo(Tank tank, IWorld world) => tank.ApplyEffect(_effect);
}

/// <summary>A pickup that repairs the collecting tank by <c>amount</c> hit points (clamped at
/// its max). Part of the health-modifier path deferred from ADR-0012.</summary>
public sealed class RepairPickup : IPickupEffect
{
    private readonly int _amount;

    public RepairPickup(int amount) => _amount = amount;

    public void ApplyTo(Tank tank, IWorld world) => tank.Heal(_amount);
}

/// <summary>A pickup that grants the collecting tank <c>amount</c> over-shield points — a buffer
/// absorbed before its hit points. Part of the health-modifier path deferred from ADR-0012.</summary>
public sealed class ShieldPickup : IPickupEffect
{
    private readonly int _amount;

    public ShieldPickup(int amount) => _amount = amount;

    public void ApplyTo(Tank tank, IWorld world) => tank.AddShield(_amount);
}

/// <summary>An ammo crate: applies an <see cref="AmmoModifier"/> to the tank's loadout for its next
/// <c>shots</c> shots. Because the modifier sets only its own axis, crates stack (spread + bouncing).</summary>
public sealed class AmmoPickup : IPickupEffect
{
    private readonly AmmoModifier _modifier;
    private readonly int _shots;

    public AmmoPickup(AmmoModifier modifier, int shots)
    {
        _modifier = modifier;
        _shots = shots;
    }

    public void ApplyTo(Tank tank, IWorld world) => tank.LoadAmmo(_modifier, _shots);
}

/// <summary>A telephone: on pickup it calls in a carpet-bombing <see cref="Airstrike"/> that covers a
/// connected swathe of the field (about <see cref="CoverFraction"/> of it) — a run of blast zones that
/// light up one after another and then detonate in the same order. The swathe grows out from the
/// collector's nearest foe (or the field centre), so it sweeps toward the action. Spawns the strike into
/// the world the pickup hands it.</summary>
public sealed class AirstrikePickup : IPickupEffect
{
    /// <summary>Fraction of the field the carpet bomb blankets.</summary>
    public const float CoverFraction = 0.4f;

    private readonly Vector2 _min;
    private readonly Vector2 _max;
    private readonly float _zoneRadius;
    private readonly float _step;
    private readonly int _damage;

    /// <param name="min">Field minimum corner (world units).</param>
    /// <param name="max">Field maximum corner.</param>
    /// <param name="zoneRadius">Blast radius of each zone.</param>
    /// <param name="step">Seconds between each zone lighting up (and each detonation).</param>
    /// <param name="damage">Damage each zone deals to a caught tank.</param>
    public AirstrikePickup(Vector2 min, Vector2 max, float zoneRadius, float step, int damage)
    {
        _min = min;
        _max = max;
        _zoneRadius = zoneRadius;
        _step = step;
        _damage = damage;
    }

    public void ApplyTo(Tank tank, IWorld world)
    {
        var zones = BuildSwathe(NearestFoe(tank, world)?.Position ?? Centre());
        if (zones.Count > 0)
        {
            world.Spawn(new Airstrike(world, zones, tank.Team, _zoneRadius, _step, _damage));
        }
    }

    private Vector2 Centre() => (_min + _max) * 0.5f;

    // Grow a connected blob of zones (a grid spaced by the zone radius) out from the cell nearest the
    // start point, in breadth-first order, until it blankets CoverFraction of the field. The BFS order is
    // the order the zones light up and then detonate, so the carpet sweeps outward.
    private List<Vector2> BuildSwathe(Vector2 start)
    {
        var spacing = _zoneRadius;
        var cols = System.Math.Max(1, (int)((_max.X - _min.X) / spacing));
        var rows = System.Math.Max(1, (int)((_max.Y - _min.Y) / spacing));
        var target = System.Math.Max(1, (int)(CoverFraction * cols * rows));

        var sx = System.Math.Clamp((int)((start.X - _min.X) / spacing), 0, cols - 1);
        var sy = System.Math.Clamp((int)((start.Y - _min.Y) / spacing), 0, rows - 1);

        var visited = new HashSet<(int, int)>();
        var order = new List<Vector2>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((sx, sy));
        visited.Add((sx, sy));
        while (queue.Count > 0 && order.Count < target)
        {
            var (cx, cy) = queue.Dequeue();
            order.Add(new Vector2(_min.X + ((cx + 0.5f) * spacing), _min.Y + ((cy + 0.5f) * spacing)));
            foreach (var (nx, ny) in new[] { (cx, cy - 1), (cx + 1, cy), (cx, cy + 1), (cx - 1, cy) })
            {
                if (nx >= 0 && ny >= 0 && nx < cols && ny < rows && visited.Add((nx, ny)))
                {
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return order;
    }

    private static Tank? NearestFoe(Tank caller, IWorld world)
    {
        Tank? nearest = null;
        var nearestDistance = float.PositiveInfinity;
        foreach (var tank in world.Entities.OfType<Tank>())
        {
            if (tank.Hp <= 0 || tank.Team == caller.Team)
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(caller.Position, tank.Position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = tank;
            }
        }

        return nearest;
    }
}
