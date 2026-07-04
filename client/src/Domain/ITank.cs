namespace TankGame.Domain;

/// <summary>A controllable tank — a world <see cref="IEntity"/> with hit points
/// (<see cref="IDamageable"/>). The implementation (GameLogic) is constructed with an
/// <see cref="IInputSource"/> and advances itself on <see cref="IEntity.Step"/> — pure C#, no
/// engine. Position, identity, liveness, and the tick come from <see cref="IEntity"/>; health
/// from <see cref="IDamageable"/>; a tank adds only its chassis and turret facing.</summary>
public interface ITank : IEntity, IDamageable
{
    /// <summary>Chassis facing, in radians (follows movement direction).</summary>
    float Rotation { get; }

    /// <summary>Turret facing, in radians (follows the aim input).</summary>
    float TurretRotation { get; }

    /// <summary>The side this tank fights for. Shots from the same team pass through it; a
    /// different team's shots damage it. Player 1, Player 2, and the AI each get their own.</summary>
    int Team { get; }

    /// <summary>Over-shield points: a buffer absorbed by incoming damage before hit points are
    /// touched (granted by a shield pickup). 0 when unshielded; not capped by <see cref="IDamageable.MaxHp"/>.
    /// The view renders it above the health bar.</summary>
    int Shield { get; }

    /// <summary>Lives left including the current one (respawns remaining = this minus one). Default 1 =
    /// a tank with no respawns; the HUD reads it to show how many revives the player has left.</summary>
    int LivesRemaining => 1;

    /// <summary>The name shown above the tank in battle — the player's chosen name, or an AI's
    /// generated one. Blank (the default) means no name tag.</summary>
    string DisplayName => "";

    /// <summary>Continuous elevation in layer units (ADR-0020 Wave B): a grounded tank sits exactly
    /// on its <see cref="IEntity.Layer"/>; while airborne it sweeps down toward the lower layer. The
    /// 3D view renders the tank at this height × the world layer height. Default: grounded.</summary>
    float Altitude => Layer;

    /// <summary>True while the tank is falling off a ledge (ADR-0020 Wave B). A falling tank keeps
    /// its source <see cref="IEntity.Layer"/> for combat until it lands, cannot fire, and ignores
    /// ramps and teleport pads. Default: grounded, never airborne.</summary>
    bool IsAirborne => false;
}
