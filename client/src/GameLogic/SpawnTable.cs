using System;
using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>The four spawn cells for a networked room on a level that only declares one: the
/// level's own spawn, the classic second spawn, and their mirrors across the field — each nudged
/// to the nearest un-blocked cell so nobody materialises inside a wall. Pure C#: the caller
/// supplies the blocked predicate.</summary>
public static class SpawnTable
{
    public static IReadOnlyList<(int X, int Y)> For(
        int width, int height, (int X, int Y) primary, (int X, int Y) secondary,
        Func<int, int, bool> isBlocked)
    {
        var candidates = new[]
        {
            primary,
            secondary,
            (X: width - 1 - primary.X, Y: height - 1 - primary.Y),
            (X: width - 1 - secondary.X, Y: height - 1 - secondary.Y),
        };

        var spawns = new List<(int X, int Y)>(candidates.Length);
        foreach (var candidate in candidates)
        {
            spawns.Add(NearestOpen(candidate, width, height, isBlocked));
        }

        return spawns;
    }

    // Ring search outward from the candidate until an un-blocked in-bounds cell turns up. The
    // candidate itself wins when open (ring 0), so declared spawns are never moved.
    private static (int X, int Y) NearestOpen(
        (int X, int Y) from, int width, int height, Func<int, int, bool> isBlocked)
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
                    if (x >= 0 && x < width && y >= 0 && y < height && !isBlocked(x, y))
                    {
                        return (x, y);
                    }
                }
            }
        }

        return from; // a fully-blocked level: give the candidate back rather than loop forever
    }
}
