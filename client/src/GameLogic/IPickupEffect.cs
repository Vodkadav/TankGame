namespace TankGame.GameLogic;

/// <summary>What a <see cref="Powerup"/> does to the tank that collects it. Keeps the pickup
/// entity generic: a stat powerup applies a <see cref="StatusEffect"/>, an ammo crate loads a
/// special weapon — same field entity, same collect-and-expire flow, different effect.</summary>
public interface IPickupEffect
{
    void ApplyTo(Tank tank);
}

/// <summary>A pickup that grants a timed <see cref="StatusEffect"/> (speed boost, rapid fire).</summary>
public sealed class StatusEffectPickup : IPickupEffect
{
    private readonly StatusEffect _effect;

    public StatusEffectPickup(StatusEffect effect) => _effect = effect;

    public void ApplyTo(Tank tank) => tank.ApplyEffect(_effect);
}

/// <summary>A pickup that repairs the collecting tank by <c>amount</c> hit points (clamped at
/// its max). Part of the health-modifier path deferred from ADR-0012.</summary>
public sealed class RepairPickup : IPickupEffect
{
    private readonly int _amount;

    public RepairPickup(int amount) => _amount = amount;

    public void ApplyTo(Tank tank) => tank.Heal(_amount);
}

/// <summary>A pickup that grants the collecting tank <c>amount</c> over-shield points — a buffer
/// absorbed before its hit points. Part of the health-modifier path deferred from ADR-0012.</summary>
public sealed class ShieldPickup : IPickupEffect
{
    private readonly int _amount;

    public ShieldPickup(int amount) => _amount = amount;

    public void ApplyTo(Tank tank) => tank.AddShield(_amount);
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

    public void ApplyTo(Tank tank) => tank.LoadAmmo(_modifier, _shots);
}
