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

/// <summary>A telephone: on pickup it calls in an <see cref="Airstrike"/> on the collector's nearest
/// foe (a different team), which detonates after a short telegraph. Does nothing if no enemy is on the
/// field. Spawns the strike into the world the pickup hands it.</summary>
public sealed class AirstrikePickup : IPickupEffect
{
    private readonly float _delay;
    private readonly float _radius;
    private readonly int _damage;

    public AirstrikePickup(float delay, float radius, int damage)
    {
        _delay = delay;
        _radius = radius;
        _damage = damage;
    }

    public void ApplyTo(Tank tank, IWorld world)
    {
        var target = NearestFoe(tank, world);
        if (target is null)
        {
            return; // no-one to strike
        }

        world.Spawn(new Airstrike(world, target.Position, tank.Team, _delay, _radius, _damage));
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
