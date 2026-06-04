using System;
using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A level layout: cell materials, bush flags, and the player spawn. Built either from a
/// hand-authored text map (<see cref="Parse"/>: <c>#</c> steel, <c>x</c> brick, <c>.</c> floor,
/// <c>b</c> bush, <c>@</c> spawn) or, for S8, directly from generated cell data
/// (<see cref="FromCells"/> — see <see cref="ArenaGenerator"/>). Pure C# — produces a
/// <see cref="WallGrid"/> model the arena and view consume.</summary>
public sealed class LevelMap
{
    private LevelMap(CellMaterial[,] materials, bool[,] bushes, int spawnX, int spawnY)
    {
        Materials = materials;
        Bushes = bushes;
        Width = materials.GetLength(0);
        Height = materials.GetLength(1);
        SpawnX = spawnX;
        SpawnY = spawnY;
    }

    /// <summary>Builds a level directly from cell data — the producer seam a procedural generator
    /// (S8) uses instead of authoring a text map. <paramref name="bushes"/> must match the
    /// materials' dimensions; the spawn must be an in-bounds floor cell.</summary>
    public static LevelMap FromCells(CellMaterial[,] materials, bool[,] bushes, int spawnX, int spawnY)
    {
        var width = materials.GetLength(0);
        var height = materials.GetLength(1);
        if (bushes.GetLength(0) != width || bushes.GetLength(1) != height)
        {
            throw new ArgumentException("bushes must match the materials' dimensions");
        }

        if (spawnX < 0 || spawnY < 0 || spawnX >= width || spawnY >= height)
        {
            throw new ArgumentOutOfRangeException(nameof(spawnX), "spawn is out of bounds");
        }

        if (materials[spawnX, spawnY] != CellMaterial.Floor)
        {
            throw new ArgumentException("spawn must be a floor cell");
        }

        return new LevelMap(materials, bushes, spawnX, spawnY);
    }

    /// <summary>Cell materials indexed <c>[x, y]</c>.</summary>
    public CellMaterial[,] Materials { get; }

    /// <summary>Which cells are bushes (passable floor that conceals a tank standing on it),
    /// indexed <c>[x, y]</c>. A bush cell is <see cref="CellMaterial.Floor"/> for collision.</summary>
    public bool[,] Bushes { get; }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Column of the player spawn.</summary>
    public int SpawnX { get; }

    /// <summary>Row of the player spawn.</summary>
    public int SpawnY { get; }

    /// <summary>Parses a text map. Throws <see cref="FormatException"/> on ragged rows, an
    /// unknown character, or anything other than exactly one spawn.</summary>
    public static LevelMap Parse(string text)
    {
        var rows = SplitRows(text);
        if (rows.Count == 0)
        {
            throw new FormatException("level map is empty");
        }

        var width = rows[0].Length;
        var height = rows.Count;
        var materials = new CellMaterial[width, height];
        var bushes = new bool[width, height];
        int? spawnX = null;
        var spawnY = 0;

        for (var y = 0; y < height; y++)
        {
            var row = rows[y];
            if (row.Length != width)
            {
                throw new FormatException($"row {y} has length {row.Length}, expected {width}");
            }

            for (var x = 0; x < width; x++)
            {
                var c = row[x];
                if (c == '@')
                {
                    if (spawnX.HasValue)
                    {
                        throw new FormatException("level map has more than one spawn");
                    }

                    spawnX = x;
                    spawnY = y;
                    materials[x, y] = CellMaterial.Floor;
                    continue;
                }

                if (c == 'b')
                {
                    materials[x, y] = CellMaterial.Floor; // a bush is passable floor…
                    bushes[x, y] = true;                  // …that conceals whoever stands on it
                    continue;
                }

                materials[x, y] = c switch
                {
                    '#' => CellMaterial.Steel,
                    'x' => CellMaterial.Brick,
                    '.' => CellMaterial.Floor,
                    _ => throw new FormatException($"unknown level character '{c}' at ({x},{y})"),
                };
            }
        }

        if (!spawnX.HasValue)
        {
            throw new FormatException("level map has no spawn ('@')");
        }

        return new LevelMap(materials, bushes, spawnX.Value, spawnY);
    }

    /// <summary>Builds the <see cref="WallGrid"/> for this level (brick starts at full hp).</summary>
    public WallGrid BuildGrid() => WallGrid.FromMaterials(Materials);

    private static List<string> SplitRows(string text)
    {
        var rows = new List<string>(text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'));
        while (rows.Count > 0 && rows[^1].Length == 0)
        {
            rows.RemoveAt(rows.Count - 1); // ignore trailing blank lines from the literal
        }

        return rows;
    }
}
