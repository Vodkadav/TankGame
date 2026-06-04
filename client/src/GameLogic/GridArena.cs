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

    public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance)
    {
        if (direction == Vector2.Zero)
        {
            return null;
        }

        var dir = Vector2.Normalize(direction);
        var local = origin - _origin;

        var cellX = FloorDiv(local.X);
        var cellY = FloorDiv(local.Y);

        if (_grid.IsBlocked(cellX, cellY))
        {
            // already inside a wall — face back along the ray
            return new RaycastHit(origin, 0f, -dir, IsDestructible(cellX, cellY));
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

            if (_grid.IsBlocked(cellX, cellY))
            {
                return new RaycastHit(origin + (dir * t), t, normal, IsDestructible(cellX, cellY));
            }
        }
    }

    // Brick is the only destructible material; steel and the implicit out-of-bounds border are
    // permanent. A piercing shot uses this to decide whether it can punch through.
    private bool IsDestructible(int cellX, int cellY) => _grid.GetCell(cellX, cellY).Material == CellMaterial.Brick;

    public void DamageAt(Vector2 point, Vector2 direction, int amount)
    {
        if (direction == Vector2.Zero)
        {
            return;
        }

        // The contact point sits on the struck cell's near face; nudge half a tile along the
        // ray to land squarely inside that cell, then damage it.
        var inside = point + (Vector2.Normalize(direction) * (_tileSize * 0.5f));
        var local = inside - _origin;
        _grid.DamageCell(FloorDiv(local.X), FloorDiv(local.Y), amount);
    }

    public bool IsBlocked(Vector2 point)
    {
        var local = point - _origin;
        return _grid.IsBlocked(FloorDiv(local.X), FloorDiv(local.Y));
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
