using System;
using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A hand-authored maze parsed from a text map: <c>#</c> steel, <c>x</c> brick,
/// <c>.</c> floor, <c>@</c> the tank spawn (a floor cell). Pure C# — produces a
/// <see cref="WallGrid"/> model the arena and view consume. The map is the single source of
/// the level layout; no procedural generation.</summary>
public sealed class MazeDefinition
{
    private MazeDefinition(CellMaterial[,] materials, int spawnX, int spawnY)
    {
        Materials = materials;
        Width = materials.GetLength(0);
        Height = materials.GetLength(1);
        SpawnX = spawnX;
        SpawnY = spawnY;
    }

    /// <summary>Cell materials indexed <c>[x, y]</c>.</summary>
    public CellMaterial[,] Materials { get; }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Column of the tank spawn.</summary>
    public int SpawnX { get; }

    /// <summary>Row of the tank spawn.</summary>
    public int SpawnY { get; }

    /// <summary>Parses a text map. Throws <see cref="FormatException"/> on ragged rows, an
    /// unknown character, or anything other than exactly one spawn.</summary>
    public static MazeDefinition Parse(string text)
    {
        var rows = SplitRows(text);
        if (rows.Count == 0)
        {
            throw new FormatException("maze is empty");
        }

        var width = rows[0].Length;
        var height = rows.Count;
        var materials = new CellMaterial[width, height];
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
                        throw new FormatException("maze has more than one spawn");
                    }

                    spawnX = x;
                    spawnY = y;
                    materials[x, y] = CellMaterial.Floor;
                    continue;
                }

                materials[x, y] = c switch
                {
                    '#' => CellMaterial.Steel,
                    'x' => CellMaterial.Brick,
                    '.' => CellMaterial.Floor,
                    _ => throw new FormatException($"unknown maze character '{c}' at ({x},{y})"),
                };
            }
        }

        if (!spawnX.HasValue)
        {
            throw new FormatException("maze has no spawn ('@')");
        }

        return new MazeDefinition(materials, spawnX.Value, spawnY);
    }

    /// <summary>Builds the <see cref="WallGrid"/> for this maze (brick starts at full hp).</summary>
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
