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

/// <summary>An ammo crate: applies an <see cref="AmmoModifier"/> to the tank's loadout, which then
/// fires that special shot for every shot until the tank dies (unlimited while alive). Because the
/// modifier sets only its own axis, crates stack (spread + bouncing).</summary>
public sealed class AmmoPickup : IPickupEffect
{
    private readonly AmmoModifier _modifier;

    public AmmoPickup(AmmoModifier modifier) => _modifier = modifier;

    public void ApplyTo(Tank tank, IWorld world) => tank.LoadAmmo(_modifier);
}

/// <summary>A telephone: on pickup it calls in a carpet-bombing <see cref="Airstrike"/> that drops a small,
/// randomized organic blob of blast zones (about <see cref="TargetCells"/> of them) — a clump that lights up
/// one zone after another and then detonates in the same order. The blob grows outward from the collector's
/// nearest foe (or the field centre) in random directions, so it lands as a different shape each call.
/// Spawns the strike into the world the pickup hands it.</summary>
public sealed class AirstrikePickup : IPickupEffect
{
    /// <summary>Roughly how many blast cells the strike drops (about 6; jittered a little per call for variety).</summary>
    public const int TargetCells = 6;

    private readonly Vector2 _min;
    private readonly Vector2 _max;
    private readonly float _zoneRadius;
    private readonly float _armWindow;
    private readonly float _delay;
    private readonly int _damage;

    /// <param name="min">Field minimum corner (world units).</param>
    /// <param name="max">Field maximum corner.</param>
    /// <param name="zoneRadius">Blast radius of each zone.</param>
    /// <param name="armWindow">Seconds over which all zones light up (the expanding telegraph).</param>
    /// <param name="delay">Seconds between a zone lighting up and that zone detonating.</param>
    /// <param name="damage">Damage each zone deals to a caught tank.</param>
    public AirstrikePickup(Vector2 min, Vector2 max, float zoneRadius, float armWindow, float delay, int damage)
    {
        _min = min;
        _max = max;
        _zoneRadius = zoneRadius;
        _armWindow = armWindow;
        _delay = delay;
        _damage = damage;
    }

    public void ApplyTo(Tank tank, IWorld world)
    {
        // A fresh, time-seeded rng each call so every strike is a different blob. NOT Guid.NewGuid(): on the
        // web/WASM runtime that returns a constant, which would make every strike land identically.
        var rng = new System.Random();
        var zones = BuildSwathe(NearestFoe(tank, world)?.Position ?? Centre(), rng);
        if (zones.Count > 0)
        {
            world.Spawn(new Airstrike(world, zones, tank.Team, _zoneRadius, _armWindow, _delay, _damage));
        }
    }

    private Vector2 Centre() => (_min + _max) * 0.5f;

    // Grow a small randomized blob of zones (a grid spaced by the zone radius) out from the cell nearest the
    // start point, in Eden-growth order, until it holds about TargetCells cells. The growth order is the
    // order the zones light up and then detonate, so the strike still sweeps outward from the seed.
    private List<Vector2> BuildSwathe(Vector2 start, System.Random rng)
    {
        var spacing = _zoneRadius;
        var cols = System.Math.Max(1, (int)((_max.X - _min.X) / spacing));
        var rows = System.Math.Max(1, (int)((_max.Y - _min.Y) / spacing));

        var sx = System.Math.Clamp((int)((start.X - _min.X) / spacing), 0, cols - 1);
        var sy = System.Math.Clamp((int)((start.Y - _min.Y) / spacing), 0, rows - 1);

        var target = TargetCells + rng.Next(-1, 2); // ~6 (5–7): a slightly different size each call
        var cells = AirstrikeSpread.Grow(sx, sy, cols, rows, target, rng);

        var order = new List<Vector2>(cells.Count);
        foreach (var (cx, cy) in cells)
        {
            order.Add(new Vector2(_min.X + ((cx + 0.5f) * spacing), _min.Y + ((cy + 0.5f) * spacing)));
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
