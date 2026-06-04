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
}

/// <summary>A collectible sitting on the field — a world <see cref="IEntity"/>. The
/// implementation (GameLogic) applies its timed effect to the first live tank that overlaps it,
/// then expires. The entity surface (Id/Position/IsAlive/Step) plus <see cref="Kind"/> is the
/// whole contract; the Presentation layer draws it from <see cref="Kind"/>.</summary>
public interface IPowerup : IEntity
{
    /// <summary>Which powerup this is — selects its colour and the effect it grants.</summary>
    PowerupKind Kind { get; }
}
