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
}
