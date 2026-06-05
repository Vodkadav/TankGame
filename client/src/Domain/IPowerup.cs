using System;

namespace TankGame.Domain;

/// <summary>What a <see cref="IPowerup"/> grants — drives both its colour on screen and the
/// effect it applies on pickup. The effect data itself lives in GameLogic (a
/// <c>StatusEffect</c>); this enum is the cross-layer handle the view keys off.</summary>
public enum PowerupKind
{
    /// <summary>A temporary movement-speed multiplier.</summary>
    SpeedBoost,

    /// <summary>A temporary fire-rate boost (shorter fire interval).</summary>
    RapidFire,

    /// <summary>An ammo crate: the next shots ricochet off walls.</summary>
    BouncingAmmo,

    /// <summary>An ammo crate: the next shots fire as a fanned spread.</summary>
    SpreadAmmo,

    /// <summary>An ammo crate: the next shots punch through one target (tank or brick wall).</summary>
    PiercingAmmo,

    /// <summary>Restores hit points on pickup.</summary>
    Repair,

    /// <summary>Grants over-shield points that absorb damage before hit points.</summary>
    Shield,
}

/// <summary>A collectible sitting on the field — a world <see cref="IEntity"/>. The
/// implementation (GameLogic) applies its timed effect to the first live tank that overlaps it,
/// then expires. The entity surface (Id/Position/IsAlive/Step) plus <see cref="Kind"/> is the
/// whole contract; the Presentation layer draws it from <see cref="Kind"/>.</summary>
public interface IPowerup : IEntity
{
    /// <summary>Which powerup this is — selects its colour and the effect it grants.</summary>
    PowerupKind Kind { get; }

    /// <summary>Whether the pickup is currently on the field to be collected. A respawning pickup
    /// goes unavailable (dormant) for its respawn delay after being collected, then returns; a
    /// one-shot pickup is simply reaped. The view shows the shape only while available.</summary>
    bool IsAvailable { get; }

    /// <summary>Raised when a tank collects this pickup — each time, for a respawning one. Carries
    /// the kind so Presentation can pop a floating label of what was grabbed.</summary>
    event Action<PowerupKind>? Collected;
}
