using System.Numerics;

namespace TankGame.Domain;

/// <summary>Where a ray first met an obstacle.</summary>
/// <param name="Point">World-space contact point.</param>
/// <param name="Distance">Distance from the ray origin to <paramref name="Point"/>.</param>
public readonly record struct RaycastHit(Vector2 Point, float Distance);

/// <summary>The playable space. Resolves collisions for projectiles (and later tanks)
/// without exposing how the space is represented (empty box now, wall grid in M2).</summary>
public interface IArena
{
    /// <summary>First obstacle hit along <paramref name="direction"/> from
    /// <paramref name="origin"/>, or <c>null</c> if nothing is hit within
    /// <paramref name="maxDistance"/>.</summary>
    RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance);
}
