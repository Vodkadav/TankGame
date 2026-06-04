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

/// <summary>A pickup that loads a special weapon for the tank's next <c>shots</c> shots
/// (a bouncing / spread ammo crate).</summary>
public sealed class AmmoPickup : IPickupEffect
{
    private readonly IWeapon _weapon;
    private readonly int _shots;

    public AmmoPickup(IWeapon weapon, int shots)
    {
        _weapon = weapon;
        _shots = shots;
    }

    public void ApplyTo(Tank tank) => tank.LoadAmmo(_weapon, _shots);
}
