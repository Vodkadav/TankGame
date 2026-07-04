using System;
using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>Grows a randomized organic blob of grid cells — an Eden growth / randomized flood. Seed one
/// cell, then repeatedly annex a random cell from the frontier (the in-bounds cells edge-adjacent to the
/// blob) until the blob reaches its target size or the frontier runs dry. Picking the frontier cell at
/// random is what makes each blob a different shape from play to play; the cells are returned in growth
/// order (seed first, outward) so a caller sweeping them expands outward from the seed.</summary>
public static class AirstrikeSpread
{
    private static readonly (int Dx, int Dy)[] Neighbours = { (0, -1), (1, 0), (0, 1), (-1, 0) };

    /// <param name="startX">Seed column (clamped into <c>[0, cols)</c>).</param>
    /// <param name="startY">Seed row (clamped into <c>[0, rows)</c>).</param>
    /// <param name="cols">Grid width in cells.</param>
    /// <param name="rows">Grid height in cells.</param>
    /// <param name="targetCells">Desired blob size; the result is capped by how many cells the grid holds.</param>
    /// <param name="rng">Randomness source — different sequences yield different blob shapes.</param>
    /// <returns>The blob's cells in growth order (seed first), each in-bounds, unique, and edge-adjacent to
    /// at least one earlier cell.</returns>
    public static IReadOnlyList<(int X, int Y)> Grow(int startX, int startY, int cols, int rows,
        int targetCells, Random rng)
    {
        var blob = new List<(int X, int Y)>();
        if (cols <= 0 || rows <= 0 || targetCells <= 0)
        {
            return blob;
        }

        var sx = Math.Clamp(startX, 0, cols - 1);
        var sy = Math.Clamp(startY, 0, rows - 1);

        var seen = new HashSet<(int, int)> { (sx, sy) };
        blob.Add((sx, sy));

        var frontier = new List<(int X, int Y)>();
        PushNeighbours(sx, sy, cols, rows, seen, frontier);

        while (blob.Count < targetCells && frontier.Count > 0)
        {
            // Random frontier pick = random growth direction. Swap-remove keeps it O(1); order does not
            // matter since the next pick is random anyway.
            var pick = rng.Next(frontier.Count);
            var cell = frontier[pick];
            frontier[pick] = frontier[^1];
            frontier.RemoveAt(frontier.Count - 1);

            blob.Add(cell);
            PushNeighbours(cell.X, cell.Y, cols, rows, seen, frontier);
        }

        return blob;
    }

    // Queue each in-bounds neighbour that has not already been blobbed or frontiered (the seen set keeps a
    // cell out of the frontier list at most once).
    private static void PushNeighbours(int x, int y, int cols, int rows, HashSet<(int, int)> seen,
        List<(int X, int Y)> frontier)
    {
        foreach (var (dx, dy) in Neighbours)
        {
            var nx = x + dx;
            var ny = y + dy;
            if (nx >= 0 && ny >= 0 && nx < cols && ny < rows && seen.Add((nx, ny)))
            {
                frontier.Add((nx, ny));
            }
        }
    }
}
