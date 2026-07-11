using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The one balance catalogue mapping every <see cref="PowerupKind"/> to the effect it grants —
/// shared by the solo and networked 3D scenes so a crate is worth the same everywhere. Stat boosts
/// run 15 s and REFRESH (not stack) on re-pickup — the source tag makes a second grab of the same
/// kind replace the live effect, so boosts can't multiply into runaway speed/fire rate. The
/// airstrike telephone needs the field bounds for its swathe, hence the parameters.</summary>
public static class PowerupEffects
{
    private const int RepairAmount = 2;
    private const int ShieldAmount = 3;
    private const float AirstrikeZoneRadius = 70f;
    private const float AirstrikeArmWindow = 5f; // all zones telegraph red within ~5s, expanding outward
    private const float AirstrikeDelay = 0.5f;   // brief hold once fully telegraphed, then zones detonate in order
    private const int AirstrikeDamage = 3;

    /// <summary>Every kind's effect for a field spanning <paramref name="gridOrigin"/> to
    /// <paramref name="fieldMax"/> with <paramref name="tileSize"/>-unit cells.</summary>
    public static IReadOnlyDictionary<PowerupKind, IPickupEffect> Catalogue(
        Vector2 gridOrigin, Vector2 fieldMax, float tileSize) => new Dictionary<PowerupKind, IPickupEffect>
    {
        [PowerupKind.SpeedBoost] = new StatusEffectPickup(new StatusEffect(StatKind.Speed, Mult: 1.6f, AddFlat: 0f, Seconds: 15f, Source: nameof(PowerupKind.SpeedBoost))),
        [PowerupKind.RapidFire] = new StatusEffectPickup(new StatusEffect(StatKind.FireInterval, Mult: 0.5f, AddFlat: 0f, Seconds: 15f, Source: nameof(PowerupKind.RapidFire))),
        [PowerupKind.BouncingAmmo] = new AmmoPickup(new BouncingAmmo(bounces: 3)),
        [PowerupKind.SpreadAmmo] = new AmmoPickup(new SpreadAmmo(count: 3, radians: 0.18f)),
        [PowerupKind.Repair] = new RepairPickup(RepairAmount),
        [PowerupKind.Shield] = new ShieldPickup(ShieldAmount),
        [PowerupKind.PiercingAmmo] = new AmmoPickup(new PiercingAmmo(pierces: 1, tileSize)),
        [PowerupKind.Missile] = new AmmoPickup(new MissileAmmo(tileSize)),
        [PowerupKind.Telephone] = new AirstrikePickup(gridOrigin, fieldMax, AirstrikeZoneRadius, AirstrikeArmWindow, AirstrikeDelay, AirstrikeDamage),
    };
}
