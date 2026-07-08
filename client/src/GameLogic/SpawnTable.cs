using System;
using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>Up to eight spawn cells for a room on a level that only declares one or two: the level's
/// own spawn, the classic second spawn, and their reflections across the field's centre and both
/// axes — eight symmetric points, each nudged to the nearest un-blocked cell not already taken, so
/// eight players never collide and nobody materialises inside a wall. Pure C#: the caller supplies
/// the blocked predicate.</summary>
public static class SpawnTable
{
    /// <summary>The most spawns yielded — one per player in an eight-tank match.</summary>
    public const int MaxSpawns = 8;

    public static IReadOnlyList<(int X, int Y)> For(
        int width, int height, (int X, int Y) primary, (int X, int Y) secondary,
        Func<int, int, bool> isBlocked)
    {
        // Each seed plus its three reflections (across the centre, the horizontal axis, the vertical
        // axis) spreads eight starts symmetrically over the field. The two declared spawns lead so
        // they never move; the reflections fill the rest of the ring.
        var candidates = new[]
        {
            primary,
            secondary,
            Mirror(primary, width, height),
            Mirror(secondary, width, height),
            (X: primary.X, Y: height - 1 - primary.Y),
            (X: width - 1 - primary.X, Y: primary.Y),
            (X: secondary.X, Y: height - 1 - secondary.Y),
            (X: width - 1 - secondary.X, Y: secondary.Y),
        };

        var taken = new HashSet<(int X, int Y)>();
        var spawns = new List<(int X, int Y)>(MaxSpawns);
        foreach (var candidate in candidates)
        {
            var cell = NearestOpen(candidate, width, height, isBlocked, taken);
            taken.Add(cell);
            spawns.Add(cell);
        }

        return spawns;
    }

    private static (int X, int Y) Mirror((int X, int Y) c, int width, int height) =>
        (width - 1 - c.X, height - 1 - c.Y);

    // Ring search outward from the candidate until an un-blocked, in-bounds, not-yet-taken cell turns
    // up. The candidate itself wins when open and free (ring 0), so an unobstructed declared spawn is
    // never moved; the taken set keeps the eight distinct.
    private static (int X, int Y) NearestOpen(
        (int X, int Y) from, int width, int height, Func<int, int, bool> isBlocked,
        HashSet<(int X, int Y)> taken)
    {
        for (var ring = 0; ring < Math.Max(width, height); ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring)
                    {
                        continue; // only the ring's shell — inner cells were checked already
                    }

                    var x = from.X + dx;
                    var y = from.Y + dy;
                    if (x >= 0 && x < width && y >= 0 && y < height
                        && !isBlocked(x, y) && !taken.Contains((x, y)))
                    {
                        return (x, y);
                    }
                }
            }
        }

        return from; // a fully-blocked level: give the candidate back rather than loop forever
    }
}
