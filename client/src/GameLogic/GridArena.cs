using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>An <see cref="IArena"/> backed by an <see cref="IWallGrid"/>. Maps world-space
/// rays onto the tile grid and walks them cell by cell (Amanatides–Woo DDA) to find the
/// first blocked cell — a brick/steel wall, or the implicit steel border at the grid edge.
/// Damage from an impact is routed to the struck cell. Pure C# — no Godot; the tile size and
/// origin fix the world↔tile mapping.</summary>
public sealed class GridArena : IArena
{
    private readonly IWallGrid _grid;
    private readonly float _tileSize;
    private readonly Vector2 _origin;

    /// <param name="grid">The wall grid that defines blocking cells.</param>
    /// <param name="tileSize">World-space size of one square tile.</param>
    /// <param name="origin">World position of cell (0,0)'s minimum corner.</param>
    public GridArena(IWallGrid grid, float tileSize, Vector2 origin)
    {
        _grid = grid;
        _tileSize = tileSize;
        _origin = origin;
    }

    public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) =>
        Raycast(origin, direction, maxDistance, OnAnyLayer);

    public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance, int layer) =>
        Raycast(origin, direction, maxDistance, layer);

    // Walks the ray cell by cell (Amanatides–Woo DDA). A cell stops the shot if it blocks shots OR
    // sits on a different elevation layer than the shooter (its cliff face); a flat shooter
    // (OnAnyLayer) is stopped only by shot-blocking walls, reproducing the pre-elevation behaviour.
    // The struck cell is destructible only when it is breakable AND on the shooter's own layer — a
    // cliff is permanent regardless of what material happens to sit on it.
    private const int OnAnyLayer = int.MinValue;

    private RaycastHit? Raycast(Vector2 origin, Vector2 direction, float maxDistance, int layer)
    {
        if (direction == Vector2.Zero)
        {
            return null;
        }

        var dir = Vector2.Normalize(direction);
        var local = origin - _origin;

        var cellX = FloorDiv(local.X);
        var cellY = FloorDiv(local.Y);

        if (StopsShot(cellX, cellY, layer))
        {
            // already inside a shot-blocking wall — face back along the ray
            return new RaycastHit(origin, 0f, -dir, IsDestructible(cellX, cellY, layer));
        }

        var (stepX, tMaxX, tDeltaX) = SetupAxis(local.X, dir.X, cellX);
        var (stepY, tMaxY, tDeltaY) = SetupAxis(local.Y, dir.Y, cellY);

        while (true)
        {
            float t;
            Vector2 normal;
            if (tMaxX < tMaxY)
            {
                t = tMaxX;
                cellX += stepX;
                tMaxX += tDeltaX;
                normal = new Vector2(-stepX, 0f); // crossed a vertical face
            }
            else
            {
                t = tMaxY;
                cellY += stepY;
                tMaxY += tDeltaY;
                normal = new Vector2(0f, -stepY); // crossed a horizontal face
            }

            if (t > maxDistance)
            {
                return null;
            }

            if (StopsShot(cellX, cellY, layer))
            {
                return new RaycastHit(origin + (dir * t), t, normal, IsDestructible(cellX, cellY, layer));
            }
        }
    }

    private bool StopsShot(int cellX, int cellY, int layer) =>
        _grid.BlocksShots(cellX, cellY) || OnAnotherLayer(cellX, cellY, layer);

    // A cell is on "another layer" only for a layer-aware query (layer != OnAnyLayer) whose layer the
    // cell does not connect. A flat query ignores layers entirely.
    private bool OnAnotherLayer(int cellX, int cellY, int layer) =>
        layer != OnAnyLayer && !ConnectsLayer(cellX, cellY, layer);

    // A cell connects a layer when it sits on it — or, for a ramp, when it is either of the two
    // adjacent layers the ramp joins (LayerAt and LayerAt+1), so a tank or shot on either level
    // crosses the ramp freely.
    private bool ConnectsLayer(int cellX, int cellY, int layer)
    {
        var cellLayer = _grid.LayerAt(cellX, cellY);
        if (layer == cellLayer)
        {
            return true;
        }

        return _grid.IsRamp(cellX, cellY) && layer == cellLayer + 1;
    }

    // Brick and crates are destructible; steel and the implicit out-of-bounds border are permanent.
    // A piercing shot uses this to decide whether it can punch through. (Water never blocks a shot,
    // so it is never the struck cell.) A breakable cell on another layer reads as permanent — the
    // shot meets its cliff face, not the wall itself.
    private bool IsDestructible(int cellX, int cellY, int layer) =>
        !OnAnotherLayer(cellX, cellY, layer) &&
        _grid.GetCell(cellX, cellY).Material is CellMaterial.Brick or CellMaterial.Crate;

    public void DamageAt(Vector2 point, Vector2 direction, int amount) =>
        DamageAt(point, direction, amount, OnAnyLayer);

    public void DamageAt(Vector2 point, Vector2 direction, int amount, int layer)
    {
        if (direction == Vector2.Zero)
        {
            return;
        }

        // The contact point sits on the struck cell's near face; nudge half a tile along the
        // ray to land squarely inside that cell, then damage it — unless that cell is on another
        // layer (a cliff the shot merely grazed), which is permanent.
        var inside = point + (Vector2.Normalize(direction) * (_tileSize * 0.5f));
        var local = inside - _origin;
        var cellX = FloorDiv(local.X);
        var cellY = FloorDiv(local.Y);
        if (OnAnotherLayer(cellX, cellY, layer))
        {
            return;
        }

        _grid.DamageCell(cellX, cellY, amount);
    }

    public bool IsBlocked(Vector2 point) => IsBlocked(point, OnAnyLayer);

    public bool IsBlocked(Vector2 point, int layer)
    {
        var local = point - _origin;
        var cellX = FloorDiv(local.X);
        var cellY = FloorDiv(local.Y);
        return _grid.IsBlocked(cellX, cellY) || OnAnotherLayer(cellX, cellY, layer);
    }

    public int LayerAfterMove(Vector2 from, Vector2 to, int currentLayer)
    {
        var toLocal = to - _origin;
        var toX = FloorDiv(toLocal.X);
        var toY = FloorDiv(toLocal.Y);

        if (!_grid.IsRamp(toX, toY))
        {
            return currentLayer; // only a ramp changes a tank's layer
        }

        var fromLocal = from - _origin;
        if (FloorDiv(fromLocal.X) == toX && FloorDiv(fromLocal.Y) == toY)
        {
            return currentLayer; // already parked on this ramp — don't toggle every tick
        }

        // Just drove onto the ramp: cross to the connected layer it leads to — up from the low side,
        // down from the high side.
        var rampLow = _grid.LayerAt(toX, toY);
        return currentLayer == rampLow ? rampLow + 1 : rampLow;
    }

    private int FloorDiv(float worldComponent) => (int)MathF.Floor(worldComponent / _tileSize);

    // Per-axis DDA setup: which way the ray steps in this axis, the ray parameter at the
    // first cell boundary crossing, and the parameter span of one full cell.
    private (int step, float tMax, float tDelta) SetupAxis(float localComponent, float dirComponent, int cell)
    {
        if (dirComponent == 0f)
        {
            return (0, float.PositiveInfinity, float.PositiveInfinity);
        }

        var step = dirComponent > 0f ? 1 : -1;
        var boundary = (cell + (dirComponent > 0f ? 1 : 0)) * _tileSize;
        var tMax = (boundary - localComponent) / dirComponent;
        var tDelta = _tileSize / MathF.Abs(dirComponent);
        return (step, tMax, tDelta);
    }
}
