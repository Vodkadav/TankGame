namespace TankGame.Domain;

/// <summary>A controllable tank — a world <see cref="IEntity"/>. The implementation
/// (GameLogic) is constructed with an <see cref="IInputSource"/> and advances itself on
/// <see cref="IEntity.Step"/> — pure C#, no engine. Position, identity, liveness, and the
/// tick come from <see cref="IEntity"/>; a tank adds only its chassis and turret facing.</summary>
public interface ITank : IEntity
{
    /// <summary>Chassis facing, in radians (follows movement direction).</summary>
    float Rotation { get; }

    /// <summary>Turret facing, in radians (follows the aim input).</summary>
    float TurretRotation { get; }
}
