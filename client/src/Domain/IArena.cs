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
    /// <paramref name="maxDistance"/>. A pure query — it never mutates the arena.</summary>
    RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance);

    /// <summary>Applies <paramref name="amount"/> damage to whatever a shot struck at
    /// <paramref name="point"/> while travelling along <paramref name="direction"/> (the
    /// direction resolves which cell took the hit). A no-op where nothing is destructible —
    /// an open box or an indestructible boundary.</summary>
    void DamageAt(Vector2 point, Vector2 direction, int amount);

    /// <summary>Whether <paramref name="point"/> lies inside a solid wall (or outside the
    /// playable space). Used to stop a tank from driving through walls.</summary>
    bool IsBlocked(Vector2 point);
}
