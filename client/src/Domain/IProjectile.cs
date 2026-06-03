namespace TankGame.Domain;

/// <summary>A fired projectile — a world <see cref="IEntity"/>. The implementation
/// (GameLogic) is constructed with a spawn position/direction and an <see cref="IArena"/>;
/// it advances on <see cref="IEntity.Step"/> and dies (<see cref="IEntity.IsAlive"/> goes
/// false) when it hits something. The entity surface (Id/Position/IsAlive/Step) is the
/// whole contract — a projectile adds no members of its own today.</summary>
public interface IProjectile : IEntity
{
    /// <summary>The team of the tank that fired this shot. The combat pass spares tanks on
    /// the same team, so a tank cannot shoot itself or its allies.</summary>
    int Team { get; }
}
