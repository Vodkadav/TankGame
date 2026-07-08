using System.Numerics;

namespace TankGame.Domain;

/// <summary>Where a ray first met an obstacle.</summary>
/// <param name="Point">World-space contact point.</param>
/// <param name="Distance">Distance from the ray origin to <paramref name="Point"/>.</param>
/// <param name="Normal">Unit surface normal of the struck face, pointing back toward the side
/// the ray came from — so a reflection is <c>dir - 2·(dir·Normal)·Normal</c> (the seam a
/// bouncing/ricochet shell needs, per <c>docs/research/feature-roadmap.md</c> S2).</param>
/// <param name="Destructible">Whether the struck obstacle can be destroyed (a brick wall) versus
/// permanent (steel or an arena boundary). A piercing shell passes through a destructible hit but
/// is stopped by a permanent one. Defaults to false so existing call sites are unaffected.</param>
public readonly record struct RaycastHit(Vector2 Point, float Distance, Vector2 Normal, bool Destructible = false);

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

    /// <summary>The material of the cell at <paramref name="point"/> — used to resolve terrain
    /// underfoot (e.g. a tank standing on lethal lava). Defaults to <see cref="CellMaterial.Floor"/>
    /// so an open box and every existing fake keep working; a grid-backed arena overrides it.</summary>
    CellMaterial MaterialAt(Vector2 point) => CellMaterial.Floor;

    // ── Layer-aware overloads (ADR-0018 step 2) ──
    // Walls, the boundary, and destructible cells are resolved for the querying entity's elevation
    // layer: the edge of a plateau is a wall to a tank on it and empty space to a tank below. These are
    // default members that ignore the layer and fall back to the flat query, so a flat arena and every
    // existing IArena fake keep working untouched; a layered GridArena overrides them to use per-cell
    // layers.

    /// <summary>First obstacle hit on the querying entity's <paramref name="layer"/>.</summary>
    RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance, int layer) =>
        RaycastFirstHit(origin, direction, maxDistance);

    /// <summary>Damages whatever a shot on <paramref name="layer"/> struck at <paramref name="point"/>.</summary>
    void DamageAt(Vector2 point, Vector2 direction, int amount, int layer) =>
        DamageAt(point, direction, amount);

    /// <summary>Whether <paramref name="point"/> is blocked for an entity on <paramref name="layer"/>.</summary>
    bool IsBlocked(Vector2 point, int layer) => IsBlocked(point);

    /// <summary>The elevation layer a tank ends up on after sliding from <paramref name="from"/> to
    /// <paramref name="to"/> while currently on <paramref name="currentLayer"/> (ADR-0018). A tank
    /// changes layer only by driving onto a ramp, which connects two adjacent layers; on a flat arena
    /// (the default) the layer never changes.</summary>
    int LayerAfterMove(Vector2 from, Vector2 to, int currentLayer) => currentLayer;

    /// <summary>The lower layer a grounded tank at <paramref name="point"/> on
    /// <paramref name="currentLayer"/> would drop onto by driving off the ledge there (ADR-0020
    /// Wave B), or <c>null</c> when the ground carries the tank instead — its own layer, a ramp
    /// joining its layer, higher ground (a cliff face, still a wall), or a cell it cannot land on
    /// (a wall, water, out of bounds). On a flat arena (the default) there is never a drop.</summary>
    int? DropTargetAt(Vector2 point, int currentLayer) => null;
}
