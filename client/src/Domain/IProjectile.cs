using System.Numerics;

namespace TankGame.Domain;

/// <summary>A fired projectile. The implementation (GameLogic) is constructed with a spawn
/// position/direction and an <see cref="IArena"/>; it advances on <see cref="Step"/> and dies
/// when it hits something.</summary>
public interface IProjectile
{
    /// <summary>Current world-space position.</summary>
    Vector2 Position { get; }

    /// <summary>False once the projectile has hit an obstacle (or otherwise expired).</summary>
    bool IsAlive { get; }

    /// <summary>Advances the projectile by <paramref name="deltaSeconds"/>, resolving any hit.</summary>
    void Step(float deltaSeconds);
}
