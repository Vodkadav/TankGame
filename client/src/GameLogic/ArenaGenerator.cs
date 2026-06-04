using System;
using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Knobs for one procedural arena (S8). All densities are fractions of the interior cells.
/// <paramref name="Seed"/> makes generation deterministic — the same parameters always yield the
/// same arena (so a match is reproducible and, later, server/client can agree from a seed).
/// <paramref name="ReservedFloor"/> are cells the layout must keep as open floor (the spawns and
/// pickup spots the scene relies on).</summary>
public sealed record ArenaGenParams(
    int Width,
    int Height,
    int Seed,
    int SpawnX,
    int SpawnY,
    IReadOnlyCollection<(int X, int Y)> ReservedFloor,
    double BrickDensity = 0.12,
    double SteelDensity = 0.04,
    double BushDensity = 0.05,
    int SpawnSafeRadius = 1,
    double MinOpenFloorFraction = 0.8);

/// <summary>Generates an open battlefield in the spirit of the hand-authored one (a steel border
/// around a mostly-open interior of scattered brick, a little steel, and bush hide-spots) — but
/// procedurally and to any size (S8, <c>docs/adr/0014-procedural-arena-generation.md</c>). Pure C#:
/// deterministic from a seed, no Godot. Every arena it returns is valid by construction — the steel
/// border encloses it, the spawn and every reserved cell are open floor, no floor cell is walled off
/// from the spawn, and the interior stays mostly open.</summary>
public sealed class ArenaGenerator
{
    private const int MaxAttempts = 40;

    /// <summary>Returns a valid <see cref="LevelMap"/> for the given parameters. Tries scattered
    /// layouts until one satisfies the invariants; falls back to a bare border-only arena (trivially
    /// valid) only if every attempt is rejected — which density kept low makes vanishingly rare.</summary>
    public LevelMap Generate(ArenaGenParams p)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // A distinct but deterministic layout per retry, still a pure function of the seed.
            if (TryGenerate(p, new Random(p.Seed + attempt)) is { } map)
            {
                return map;
            }
        }

        return BorderOnly(p);
    }

    private static LevelMap? TryGenerate(ArenaGenParams p, Random rng)
    {
        var materials = new CellMaterial[p.Width, p.Height];
        var bushes = new bool[p.Width, p.Height];
        var locked = ReservedMask(p);

        for (var x = 0; x < p.Width; x++)
        {
            for (var y = 0; y < p.Height; y++)
            {
                if (IsBorder(p, x, y))
                {
                    materials[x, y] = CellMaterial.Steel;
                    continue;
                }

                if (locked[x, y])
                {
                    materials[x, y] = CellMaterial.Floor;
                    continue;
                }

                var roll = rng.NextDouble();
                if (roll < p.BrickDensity)
                {
                    materials[x, y] = CellMaterial.Brick;
                }
                else if (roll < p.BrickDensity + p.SteelDensity)
                {
                    materials[x, y] = CellMaterial.Steel;
                }
                else
                {
                    materials[x, y] = CellMaterial.Floor;
                    if (rng.NextDouble() < p.BushDensity)
                    {
                        bushes[x, y] = true;
                    }
                }
            }
        }

        // Wall off any floor the spawn can't reach so no tank is stranded; a reserved cell that
        // ends up enclosed instead rejects this attempt (it must stay reachable floor).
        var reached = FloodFillFloor(materials, p.SpawnX, p.SpawnY);
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
                    return null; // a reserved cell got walled off — try another layout
                }

                materials[x, y] = CellMaterial.Steel; // fill the unreachable pocket
                bushes[x, y] = false;
            }
        }

        return OpenFloorFraction(materials) >= p.MinOpenFloorFraction
            ? LevelMap.FromCells(materials, bushes, p.SpawnX, p.SpawnY)
            : null;
    }

    private static bool[,] ReservedMask(ArenaGenParams p)
    {
        var locked = new bool[p.Width, p.Height];
        Lock(locked, p.SpawnX, p.SpawnY);

        // A small clearing around the spawn so the player is not boxed in on the first frame.
        for (var dx = -p.SpawnSafeRadius; dx <= p.SpawnSafeRadius; dx++)
        {
            for (var dy = -p.SpawnSafeRadius; dy <= p.SpawnSafeRadius; dy++)
            {
                Lock(locked, p.SpawnX + dx, p.SpawnY + dy);
            }
        }

        foreach (var (x, y) in p.ReservedFloor)
        {
            Lock(locked, x, y);
        }

        return locked;

        void Lock(bool[,] mask, int x, int y)
        {
            if (!IsBorder(p, x, y) && x >= 0 && y >= 0 && x < p.Width && y < p.Height)
            {
                mask[x, y] = true; // never a border cell — the enclosing steel ring stays intact
            }
        }
    }

    private static bool IsBorder(ArenaGenParams p, int x, int y) =>
        x <= 0 || y <= 0 || x >= p.Width - 1 || y >= p.Height - 1;

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

    private static LevelMap BorderOnly(ArenaGenParams p)
    {
        var materials = new CellMaterial[p.Width, p.Height];
        for (var x = 0; x < p.Width; x++)
        {
            for (var y = 0; y < p.Height; y++)
            {
                materials[x, y] = IsBorder(p, x, y) ? CellMaterial.Steel : CellMaterial.Floor;
            }
        }

        return LevelMap.FromCells(materials, new bool[p.Width, p.Height], p.SpawnX, p.SpawnY);
    }

    private static readonly (int, int)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1) };
}
