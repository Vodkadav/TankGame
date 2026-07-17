using System;
using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>Seeded random spawn placement with a pairwise minimum distance. The old fixed/mirrored
/// scheme collapsed centre-ish spawns onto each other and nudged the duplicates to ADJACENT cells,
/// packing eight tanks together; this one scatters them anywhere eligible instead. Placement aims
/// for <see cref="TargetSeparation"/> cells between any two spawns, relaxing one cell at a time down
/// to <see cref="FloorSeparation"/> when the map is too cramped, and below that best-effort maximises
/// whatever separation the field can still give — two tanks share a cell only when the field itself
/// is smaller than the spawn count. Pure C#: the caller supplies the eligibility predicate, which
/// must cover walls AND deadly terrain (lava/water), so no tank materialises inside a wall or on a
/// cell that kills it. Deterministic per seed, so net peers and replays derive identical spawns.</summary>
public static class SpawnTable
{
    /// <summary>The most spawns requested — one per player in an eight-tank match.</summary>
    public const int MaxSpawns = 8;

    /// <summary>The pairwise separation (Chebyshev cells) placement aims for.</summary>
    public const int TargetSeparation = 10;

    /// <summary>The smallest separation relaxation may accept before going best-effort.</summary>
    public const int FloorSeparation = 3;

    // Re-shuffles per separation level: a greedy pass over one shuffle can miss a seating that
    // exists, so a few fresh orders are tried before conceding the level and relaxing.
    private const int TriesPerSeparation = 4;

    public static IReadOnlyList<(int X, int Y)> For(
        int width, int height, int count, int seed, Func<int, int, bool> isBlocked) =>
        For(width, height, count, new Random(seed), isBlocked);

    /// <summary>As <see cref="For(int, int, int, int, Func{int, int, bool})"/>, but drawing from a
    /// caller-owned <see cref="Random"/> mid-stream (the arena generator's per-attempt rng).</summary>
    public static IReadOnlyList<(int X, int Y)> For(
        int width, int height, int count, Random rng, Func<int, int, bool> isBlocked)
    {
        var open = new List<(int X, int Y)>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!isBlocked(x, y))
                {
                    open.Add((x, y));
                }
            }
        }

        for (var separation = TargetSeparation; separation >= FloorSeparation; separation--)
        {
            for (var attempt = 0; attempt < TriesPerSeparation; attempt++)
            {
                if (TryPlace(open, count, separation, rng) is { } placed)
                {
                    return placed;
                }
            }
        }

        return BestEffort(open, width, height, count, rng);
    }

    // One greedy pass over a fresh shuffle: take each cell that keeps the separation to everything
    // already placed. Null when the pass cannot seat everyone at this separation.
    private static List<(int X, int Y)>? TryPlace(
        List<(int X, int Y)> open, int count, int separation, Random rng)
    {
        if (open.Count < count)
        {
            return null;
        }

        var placed = new List<(int X, int Y)>(count);
        foreach (var cell in Shuffled(open, rng))
        {
            if (FarEnough(cell, placed, separation))
            {
                placed.Add(cell);
                if (placed.Count == count)
                {
                    return placed;
                }
            }
        }

        return null;
    }

    // Below the floor: farthest-point placement squeezes out whatever separation the field still
    // gives. Blocked cells join the pool only when the open ones cannot seat everyone (distinctness
    // beats openness); cells repeat only when the whole field is smaller than the spawn count.
    private static List<(int X, int Y)> BestEffort(
        List<(int X, int Y)> open, int width, int height, int count, Random rng)
    {
        var pool = open;
        if (pool.Count < count)
        {
            var openSet = new HashSet<(int X, int Y)>(open);
            pool = new List<(int X, int Y)>(open);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!openSet.Contains((x, y)))
                    {
                        pool.Add((x, y));
                    }
                }
            }
        }

        var placed = new List<(int X, int Y)>(count);
        if (pool.Count == 0)
        {
            return placed; // a zero-area field — nothing to place on
        }

        placed.Add(pool[rng.Next(pool.Count)]);
        while (placed.Count < count)
        {
            var best = default((int X, int Y));
            var bestDistance = -1;
            foreach (var cell in pool)
            {
                if (placed.Contains(cell))
                {
                    continue;
                }

                var distance = int.MaxValue;
                foreach (var other in placed)
                {
                    distance = Math.Min(distance, Chebyshev(cell, other));
                }

                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = cell;
                }
            }

            if (bestDistance < 0)
            {
                break; // every distinct cell is taken — the field is smaller than the spawn count
            }

            placed.Add(best);
        }

        for (var i = 0; placed.Count < count; i = (i + 1) % placed.Count)
        {
            placed.Add(placed[i]); // reuse cells round-robin — only reachable on a too-small field
        }

        return placed;
    }

    private static List<(int X, int Y)> Shuffled(List<(int X, int Y)> cells, Random rng)
    {
        var copy = new List<(int X, int Y)>(cells);
        for (var i = copy.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }

    private static bool FarEnough((int X, int Y) cell, List<(int X, int Y)> placed, int separation)
    {
        foreach (var other in placed)
        {
            if (Chebyshev(cell, other) < separation)
            {
                return false;
            }
        }

        return true;
    }

    private static int Chebyshev((int X, int Y) a, (int X, int Y) b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
