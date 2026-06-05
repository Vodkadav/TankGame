using System;
using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Knobs for one procedural arena (S8). All densities are fractions of the interior cells.
/// <paramref name="Seed"/> makes generation deterministic — the same parameters always yield the
/// same arena. The generator places the spawns and pickup spots itself
/// (<paramref name="EnemyCount"/> enemies, <paramref name="PickupCount"/> pickups), spread out and
/// on reachable floor, so the scene no longer hard-codes any cell — works at any
/// <paramref name="Width"/>×<paramref name="Height"/>.</summary>
public sealed record ArenaGenParams(
    int Width,
    int Height,
    int Seed,
    int EnemyCount,
    int PickupCount,
    double BrickDensity = 0.08,
    double SteelDensity = 0.04,
    double CrateDensity = 0.06,
    double BushDensity = 0.05,
    double SandbagDensity = 0.04,
    int SpawnSafeRadius = 1,
    double MinOpenFloorFraction = 0.8,
    int MinAnchorSeparation = 4);

/// <summary>The result of generating an arena: the <see cref="LevelMap"/> plus where the generator
/// chose to put the spawns and pickups, all on reachable open floor.</summary>
public sealed record GeneratedArena(
    LevelMap Map,
    (int X, int Y) PlayerSpawn,
    (int X, int Y) Player2Spawn,
    IReadOnlyList<(int X, int Y)> EnemySpawns,
    IReadOnlyList<(int X, int Y)> PickupCells,
    bool[,] Sandbags);

/// <summary>Generates an open battlefield in the spirit of the hand-authored one (a steel border
/// around a mostly-open interior of scattered brick, a little steel, and bush hide-spots) — but
/// procedurally, to any size, choosing its own spawn and pickup cells (S8,
/// <c>docs/adr/0014-procedural-arena-generation.md</c>). Pure C#: deterministic from a seed, no
/// Godot. Every arena it returns is valid by construction — enclosed by steel, every spawn and
/// pickup on reachable open floor, no floor cell walled off from the player spawn, interior mostly
/// open.</summary>
public sealed class ArenaGenerator
{
    private const int MaxAttempts = 60;
    private const int PlacementTries = 200;

    /// <summary>Returns a valid <see cref="GeneratedArena"/>. Tries scattered layouts until one
    /// satisfies the invariants; falls back to a bare arena with spread anchors (trivially valid)
    /// only if every attempt is rejected — which low density makes vanishingly rare.</summary>
    public GeneratedArena Generate(ArenaGenParams p)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (TryGenerate(p, new Random(p.Seed + attempt)) is { } arena)
            {
                return arena;
            }
        }

        return Fallback(p);
    }

    private static GeneratedArena? TryGenerate(ArenaGenParams p, Random rng)
    {
        var chosen = new List<(int X, int Y)>();

        // Spawns first, in opposite corners; then enemies and pickups spread across the field. All
        // are picked on the (still empty) interior so walls scatter around them, never on them.
        var playerSpawn = PickInRegion(p, rng, chosen, 1, 1, Third(p.Width), Third(p.Height));
        var player2Spawn = PickInRegion(p, rng, chosen,
            p.Width - 1 - Third(p.Width), p.Height - 1 - Third(p.Height), p.Width - 2, p.Height - 2);

        var enemySpawns = new List<(int X, int Y)>();
        for (var i = 0; i < p.EnemyCount; i++)
        {
            enemySpawns.Add(PickInRegion(p, rng, chosen, 1, 1, p.Width - 2, p.Height - 2));
        }

        var pickupCells = new List<(int X, int Y)>();
        for (var i = 0; i < p.PickupCount; i++)
        {
            pickupCells.Add(PickInRegion(p, rng, chosen, 1, 1, p.Width - 2, p.Height - 2));
        }

        var locked = LockedMask(p, chosen, playerSpawn);
        var (materials, bushes, sandbags) = Scatter(p, rng, locked);

        // Wall off any floor the spawn can't reach; a locked anchor that ends up enclosed instead
        // rejects this attempt (every anchor must stay reachable open floor).
        var reached = FloodFillFloor(materials, playerSpawn.X, playerSpawn.Y);
        for (var x = 1; x < p.Width - 1; x++)
        {
            for (var y = 1; y < p.Height - 1; y++)
            {
                if (materials[x, y] != CellMaterial.Floor || reached[x, y])
                {
                    continue;
                }

                if (locked[x, y])
                {
                    return null;
                }

                materials[x, y] = CellMaterial.Steel;
                bushes[x, y] = false;
                sandbags[x, y] = false;
            }
        }

        if (OpenFloorFraction(materials) < p.MinOpenFloorFraction)
        {
            return null;
        }

        var map = LevelMap.FromCells(materials, bushes, playerSpawn.X, playerSpawn.Y);
        return new GeneratedArena(map, playerSpawn, player2Spawn, enemySpawns, pickupCells, sandbags);
    }

    // Picks a cell within the region, preferring one at least MinAnchorSeparation (Chebyshev) from
    // every already-chosen anchor; relaxes to any free cell if separation cannot be met, so
    // placement always terminates even on a crowded small map. The pick is recorded in `chosen`.
    private static (int X, int Y) PickInRegion(
        ArenaGenParams p, Random rng, List<(int X, int Y)> chosen, int xMin, int yMin, int xMax, int yMax)
    {
        xMin = Math.Max(1, xMin);
        yMin = Math.Max(1, yMin);
        xMax = Math.Min(p.Width - 2, Math.Max(xMin, xMax));
        yMax = Math.Min(p.Height - 2, Math.Max(yMin, yMax));

        (int X, int Y) fallback = (xMin, yMin);
        for (var attempt = 0; attempt < PlacementTries; attempt++)
        {
            var cell = (X: rng.Next(xMin, xMax + 1), Y: rng.Next(yMin, yMax + 1));
            if (chosen.Contains(cell))
            {
                continue;
            }

            fallback = cell; // a free cell, kept in case nothing meets the separation preference
            if (FarEnough(cell, chosen, p.MinAnchorSeparation))
            {
                chosen.Add(cell);
                return cell;
            }
        }

        chosen.Add(fallback);
        return fallback;
    }

    private static bool FarEnough((int X, int Y) cell, List<(int X, int Y)> chosen, int separation)
    {
        foreach (var other in chosen)
        {
            if (Math.Max(Math.Abs(cell.X - other.X), Math.Abs(cell.Y - other.Y)) < separation)
            {
                return false;
            }
        }

        return true;
    }

    private static bool[,] LockedMask(ArenaGenParams p, List<(int X, int Y)> anchors, (int X, int Y) playerSpawn)
    {
        var locked = new bool[p.Width, p.Height];
        foreach (var (x, y) in anchors)
        {
            Lock(p, locked, x, y);
        }

        // A small clearing around the player spawn so the tank is not boxed in on the first frame.
        for (var dx = -p.SpawnSafeRadius; dx <= p.SpawnSafeRadius; dx++)
        {
            for (var dy = -p.SpawnSafeRadius; dy <= p.SpawnSafeRadius; dy++)
            {
                Lock(p, locked, playerSpawn.X + dx, playerSpawn.Y + dy);
            }
        }

        return locked;
    }

    private static void Lock(ArenaGenParams p, bool[,] mask, int x, int y)
    {
        if (!IsBorder(p, x, y) && x >= 0 && y >= 0 && x < p.Width && y < p.Height)
        {
            mask[x, y] = true; // never a border cell — the enclosing steel ring stays intact
        }
    }

    // The six things a non-border interior cell can be; Bush and Sandbag are passable floor variants.
    private enum Kind { Floor, Brick, Steel, Crate, Bush, Sandbag }

    // Clustering: every cell gets a bonus toward each interior neighbour's kind, so obstacles and grass
    // form clumps rather than salt-and-pepper — but once a kind already runs RunCap cells into this one
    // the bonus stops, so it competes at base odds (the owner's "cap at 5, then equal chance").
    private const int RunCap = 5;
    private const double ClusterBonus = 0.45;

    private static (CellMaterial[,] Materials, bool[,] Bushes, bool[,] Sandbags) Scatter(
        ArenaGenParams p, Random rng, bool[,] locked)
    {
        var materials = new CellMaterial[p.Width, p.Height];
        var bushes = new bool[p.Width, p.Height];
        var sandbags = new bool[p.Width, p.Height];
        var kinds = new Kind[p.Width, p.Height]; // Floor by default

        for (var x = 0; x < p.Width; x++)
        {
            for (var y = 0; y < p.Height; y++)
            {
                if (IsBorder(p, x, y))
                {
                    materials[x, y] = CellMaterial.Steel;
                    kinds[x, y] = Kind.Steel;
                    continue;
                }

                if (locked[x, y])
                {
                    materials[x, y] = CellMaterial.Floor; // kinds stays Floor
                    continue;
                }

                var kind = PickKind(p, rng, kinds, x, y);
                kinds[x, y] = kind;
                Apply(kind, materials, bushes, sandbags, x, y);
            }
        }

        return (materials, bushes, sandbags);
    }

    // A weighted roll over the six kinds, biased toward each already-placed interior neighbour (capped).
    private static Kind PickKind(ArenaGenParams p, Random rng, Kind[,] kinds, int x, int y)
    {
        var weights = BaseWeights(p);
        BiasFromNeighbour(weights, kinds, x, y, -1, 0); // left
        BiasFromNeighbour(weights, kinds, x, y, 0, -1); // up
        return WeightedPick(weights, rng);
    }

    private static void BiasFromNeighbour(double[] weights, Kind[,] kinds, int x, int y, int dx, int dy)
    {
        var nx = x + dx;
        var ny = y + dy;
        // Only interior neighbours cluster — the steel border must not pull interior cells toward steel.
        if (nx < 1 || ny < 1 || nx >= kinds.GetLength(0) - 1 || ny >= kinds.GetLength(1) - 1)
        {
            return;
        }

        var nk = kinds[nx, ny];
        if (RunLength(kinds, nx, ny, dx, dy, nk) < RunCap)
        {
            weights[(int)nk] += ClusterBonus;
        }
    }

    // Consecutive cells of `kind` from (x,y) heading (dx,dy) — the run leading into the cell being placed.
    private static int RunLength(Kind[,] kinds, int x, int y, int dx, int dy, Kind kind)
    {
        var count = 0;
        int cx = x, cy = y;
        while (cx >= 0 && cy >= 0 && cx < kinds.GetLength(0) && cy < kinds.GetLength(1) && kinds[cx, cy] == kind)
        {
            count++;
            cx += dx;
            cy += dy;
        }

        return count;
    }

    private static double[] BaseWeights(ArenaGenParams p)
    {
        var w = new double[6];
        w[(int)Kind.Brick] = p.BrickDensity;
        w[(int)Kind.Steel] = p.SteelDensity;
        w[(int)Kind.Crate] = p.CrateDensity;
        w[(int)Kind.Bush] = p.BushDensity;
        w[(int)Kind.Sandbag] = p.SandbagDensity;
        w[(int)Kind.Floor] = Math.Max(0d,
            1d - (p.BrickDensity + p.SteelDensity + p.CrateDensity + p.BushDensity + p.SandbagDensity));
        return w;
    }

    private static Kind WeightedPick(double[] weights, Random rng)
    {
        var total = 0d;
        foreach (var w in weights)
        {
            total += w;
        }

        var roll = rng.NextDouble() * total;
        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0)
            {
                return (Kind)i;
            }
        }

        return Kind.Floor;
    }

    private static void Apply(Kind kind, CellMaterial[,] materials, bool[,] bushes, bool[,] sandbags, int x, int y)
    {
        switch (kind)
        {
            case Kind.Brick: materials[x, y] = CellMaterial.Brick; break;
            case Kind.Steel: materials[x, y] = CellMaterial.Steel; break;
            case Kind.Crate: materials[x, y] = CellMaterial.Crate; break;
            case Kind.Bush: materials[x, y] = CellMaterial.Floor; bushes[x, y] = true; break;
            case Kind.Sandbag: materials[x, y] = CellMaterial.Floor; sandbags[x, y] = true; break;
            default: materials[x, y] = CellMaterial.Floor; break;
        }
    }

    private static bool IsBorder(ArenaGenParams p, int x, int y) =>
        x <= 0 || y <= 0 || x >= p.Width - 1 || y >= p.Height - 1;

    private static int Third(int extent) => Math.Max(2, extent / 3);

    private static bool[,] FloodFillFloor(CellMaterial[,] materials, int spawnX, int spawnY)
    {
        var width = materials.GetLength(0);
        var height = materials.GetLength(1);
        var seen = new bool[width, height];
        var queue = new Queue<(int X, int Y)>();
        seen[spawnX, spawnY] = true;
        queue.Enqueue((spawnX, spawnY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in Neighbours)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                {
                    continue;
                }

                if (!seen[nx, ny] && materials[nx, ny] == CellMaterial.Floor)
                {
                    seen[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return seen;
    }

    private static double OpenFloorFraction(CellMaterial[,] materials)
    {
        var width = materials.GetLength(0);
        var height = materials.GetLength(1);
        var interior = 0;
        var floor = 0;
        for (var x = 1; x < width - 1; x++)
        {
            for (var y = 1; y < height - 1; y++)
            {
                interior++;
                if (materials[x, y] == CellMaterial.Floor)
                {
                    floor++;
                }
            }
        }

        return interior == 0 ? 1d : floor / (double)interior;
    }

    // Trivially-valid fallback: a bare arena (steel border, open interior) with anchors spread on a
    // coarse grid. Only reached if every scattered attempt was rejected.
    private static GeneratedArena Fallback(ArenaGenParams p)
    {
        var materials = new CellMaterial[p.Width, p.Height];
        for (var x = 0; x < p.Width; x++)
        {
            for (var y = 0; y < p.Height; y++)
            {
                materials[x, y] = IsBorder(p, x, y) ? CellMaterial.Steel : CellMaterial.Floor;
            }
        }

        var playerSpawn = (X: 1, Y: 1);
        var player2Spawn = (X: p.Width - 2, Y: p.Height - 2);
        var enemySpawns = SpreadCells(p, p.EnemyCount, offset: 0);
        var pickupCells = SpreadCells(p, p.PickupCount, offset: 1);

        var map = LevelMap.FromCells(materials, new bool[p.Width, p.Height], playerSpawn.X, playerSpawn.Y);
        return new GeneratedArena(map, playerSpawn, player2Spawn, enemySpawns, pickupCells,
            new bool[p.Width, p.Height]);
    }

    private static List<(int X, int Y)> SpreadCells(ArenaGenParams p, int count, int offset)
    {
        var cells = new List<(int X, int Y)>();
        for (var i = 0; i < count; i++)
        {
            var x = 1 + (((i * 3) + offset + 1) % Math.Max(1, p.Width - 2));
            var y = 1 + (((i * 2) + offset + 1) % Math.Max(1, p.Height - 2));
            cells.Add((x, y));
        }

        return cells;
    }

    private static readonly (int, int)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1) };
}
