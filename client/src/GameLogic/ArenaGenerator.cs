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

        // Carve a river with bridge crossings first and claim its cells, so anchors, walls, and
        // terrain never land on the water or the bridges (the "claim a cell" rule).
        var (riverCells, claimed, approaches) = CarveRiver(p, rng);

        // Tank spawns first, via SpawnTable's seeded min-distance placement (target 10, floor 3), so
        // no two tanks start packed together; then pickups spread across the field. All land on
        // unclaimed interior floor so walls scatter around them, never on them or the river.
        var spawnCells = SpawnTable.For(p.Width, p.Height, 2 + p.EnemyCount, rng,
            (x, y) => IsBorder(p, x, y) || claimed[x, y]);
        chosen.AddRange(spawnCells);
        var playerSpawn = spawnCells[0];
        var player2Spawn = spawnCells[1];
        var enemySpawns = new List<(int X, int Y)>();
        for (var i = 2; i < spawnCells.Count; i++)
        {
            enemySpawns.Add(spawnCells[i]);
        }

        var pickupCells = new List<(int X, int Y)>();
        for (var i = 0; i < p.PickupCount; i++)
        {
            pickupCells.Add(PickInRegion(p, rng, chosen, claimed, 1, 1, p.Width - 2, p.Height - 2));
        }

        var locked = LockedMask(p, chosen, playerSpawn);
        foreach (var (ax, ay) in approaches)
        {
            Lock(p, locked, ax, ay); // keep the cells leading onto each bridge clear so it is crossable
        }

        // Mountains: a couple of clumps of impassable rock; buildings: a few solid rectangles. Both
        // are claimed so nothing else lands on them and kept off the anchors, river, and approaches.
        var mountainCells = PlaceMountains(p, rng, claimed, locked);
        var buildingCells = PlaceBuildings(p, rng, claimed, locked);

        var (materials, bushes, sandbags) = Scatter(p, rng, locked, claimed);

        // Lay the river, mountains, and buildings over the scattered land (still floor here).
        foreach (var (rx, ry, material) in riverCells)
        {
            materials[rx, ry] = material;
        }

        foreach (var (mx, my) in mountainCells)
        {
            materials[mx, my] = CellMaterial.Mountain;
        }

        foreach (var (bx, by) in buildingCells)
        {
            materials[bx, by] = CellMaterial.Building;
        }

        // Wall off any passable land the spawn cannot reach (across the river only via bridges); a
        // locked anchor that ends up cut off instead rejects this attempt.
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
        ArenaGenParams p, Random rng, List<(int X, int Y)> chosen, bool[,] claimed,
        int xMin, int yMin, int xMax, int yMax)
    {
        xMin = Math.Max(1, xMin);
        yMin = Math.Max(1, yMin);
        xMax = Math.Min(p.Width - 2, Math.Max(xMin, xMax));
        yMax = Math.Min(p.Height - 2, Math.Max(yMin, yMax));

        (int X, int Y) fallback = (xMin, yMin);
        for (var attempt = 0; attempt < PlacementTries; attempt++)
        {
            var cell = (X: rng.Next(xMin, xMax + 1), Y: rng.Next(yMin, yMax + 1));
            if (chosen.Contains(cell) || claimed[cell.X, cell.Y])
            {
                continue; // never an already-taken anchor or a river cell
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
        ArenaGenParams p, Random rng, bool[,] locked, bool[,] claimed)
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

                if (locked[x, y] || claimed[x, y])
                {
                    materials[x, y] = CellMaterial.Floor; // a spawn clearing or a river cell — left for later
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

                if (!seen[nx, ny] && !CellMaterials.BlocksMovement(materials[nx, ny]))
                {
                    seen[nx, ny] = true; // floor and bridges are passable; water and walls are not
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
                // The river (water + bridges) and mountains are deliberate terrain, not clutter —
                // measure the open-floor fraction over the land only, so they do not trip the invariant.
                if (materials[x, y] is CellMaterial.Water or CellMaterial.Bridge
                    or CellMaterial.Mountain or CellMaterial.Building)
                {
                    continue;
                }

                interior++;
                if (materials[x, y] == CellMaterial.Floor)
                {
                    floor++;
                }
            }
        }

        return interior == 0 ? 1d : floor / (double)interior;
    }

    // Grow one or two mountain clumps of 10-15 cells each on free interior cells, claiming them so
    // nothing else generates on top. Each clump grows by random flood from a seed, only onto cells that
    // are unclaimed, unlocked (so never on an anchor, the river, or a bridge approach), and interior.
    private static System.Collections.Generic.List<(int X, int Y)> PlaceMountains(
        ArenaGenParams p, Random rng, bool[,] claimed, bool[,] locked)
    {
        var cells = new System.Collections.Generic.List<(int X, int Y)>();
        var clumps = rng.Next(1, 3); // one or two ranges
        for (var c = 0; c < clumps; c++)
        {
            var size = rng.Next(10, 16); // 10-15 blocks at a time
            var seed = FreeInteriorCell(p, rng, claimed, locked);
            if (seed is not { } start)
            {
                continue;
            }

            var frontier = new System.Collections.Generic.List<(int X, int Y)> { start };
            claimed[start.X, start.Y] = true;
            cells.Add(start);
            var grownInClump = 1;

            while (grownInClump < size && frontier.Count > 0)
            {
                var from = frontier[rng.Next(frontier.Count)];
                var grew = false;
                foreach (var (dx, dy) in Neighbours)
                {
                    var nx = from.X + dx;
                    var ny = from.Y + dy;
                    if (!IsBorder(p, nx, ny) && nx >= 0 && ny >= 0 && nx < p.Width && ny < p.Height
                        && !claimed[nx, ny] && !locked[nx, ny])
                    {
                        claimed[nx, ny] = true;
                        cells.Add((nx, ny));
                        frontier.Add((nx, ny));
                        grownInClump++;
                        grew = true;
                        break;
                    }
                }

                if (!grew)
                {
                    frontier.Remove(from); // this cell has no room left to grow
                }
            }
        }

        return cells;
    }

    // Place one to three solid building rectangles (2-3 cells each side) on free interior ground,
    // claiming them so nothing else generates on top. A building only lands where its whole footprint
    // is unclaimed and unlocked, so it never sits on an anchor, the river, or a bridge approach.
    private static System.Collections.Generic.List<(int X, int Y)> PlaceBuildings(
        ArenaGenParams p, Random rng, bool[,] claimed, bool[,] locked)
    {
        var cells = new System.Collections.Generic.List<(int X, int Y)>();
        var count = rng.Next(1, 4);
        for (var b = 0; b < count; b++)
        {
            var bw = rng.Next(2, 4);
            var bh = rng.Next(2, 4);
            for (var attempt = 0; attempt < PlacementTries; attempt++)
            {
                var x0 = rng.Next(1, Math.Max(2, p.Width - 1 - bw));
                var y0 = rng.Next(1, Math.Max(2, p.Height - 1 - bh));
                if (!FootprintFree(claimed, locked, p, x0, y0, bw, bh))
                {
                    continue;
                }

                for (var x = x0; x < x0 + bw; x++)
                {
                    for (var y = y0; y < y0 + bh; y++)
                    {
                        claimed[x, y] = true;
                        cells.Add((x, y));
                    }
                }

                break;
            }
        }

        return cells;
    }

    private static bool FootprintFree(bool[,] claimed, bool[,] locked, ArenaGenParams p, int x0, int y0, int bw, int bh)
    {
        for (var x = x0; x < x0 + bw; x++)
        {
            for (var y = y0; y < y0 + bh; y++)
            {
                if (IsBorder(p, x, y) || x >= p.Width || y >= p.Height || claimed[x, y] || locked[x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static (int X, int Y)? FreeInteriorCell(ArenaGenParams p, Random rng, bool[,] claimed, bool[,] locked)
    {
        for (var attempt = 0; attempt < PlacementTries; attempt++)
        {
            var x = rng.Next(1, p.Width - 1);
            var y = rng.Next(1, p.Height - 1);
            if (!claimed[x, y] && !locked[x, y])
            {
                return (x, y);
            }
        }

        return null;
    }

    // Carve a single river across the field with bridge crossings, claiming every cell so nothing else
    // is generated on top of it. A vertical river gets 2 bridges; a horizontal one gets 3. Each bridge
    // also returns its two perpendicular approach cells, which the caller keeps clear so it is crossable.
    private static (System.Collections.Generic.List<(int X, int Y, CellMaterial Material)> Cells,
        bool[,] Claimed, System.Collections.Generic.List<(int X, int Y)> Approaches) CarveRiver(
        ArenaGenParams p, Random rng)
    {
        var cells = new System.Collections.Generic.List<(int, int, CellMaterial)>();
        var claimed = new bool[p.Width, p.Height];
        var approaches = new System.Collections.Generic.List<(int, int)>();

        if (rng.Next(2) == 0) // vertical river
        {
            var rx = rng.Next(Third(p.Width), p.Width - Third(p.Width));
            var bridges = new[] { p.Height / 3, 2 * p.Height / 3 };
            for (var y = 1; y < p.Height - 1; y++)
            {
                var bridge = System.Array.IndexOf(bridges, y) >= 0;
                cells.Add((rx, y, bridge ? CellMaterial.Bridge : CellMaterial.Water));
                claimed[rx, y] = true;
                if (bridge)
                {
                    approaches.Add((rx - 1, y));
                    approaches.Add((rx + 1, y));
                }
            }
        }
        else // horizontal river
        {
            var ry = rng.Next(Third(p.Height), p.Height - Third(p.Height));
            var bridges = new[] { p.Width / 4, p.Width / 2, 3 * p.Width / 4 };
            for (var x = 1; x < p.Width - 1; x++)
            {
                var bridge = System.Array.IndexOf(bridges, x) >= 0;
                cells.Add((x, ry, bridge ? CellMaterial.Bridge : CellMaterial.Water));
                claimed[x, ry] = true;
                if (bridge)
                {
                    approaches.Add((x, ry - 1));
                    approaches.Add((x, ry + 1));
                }
            }
        }

        return (cells, claimed, approaches);
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
