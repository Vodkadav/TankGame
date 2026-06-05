using System;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A tile grid of walls and floor, backed by a 2D array of <see cref="WallCell"/>
/// indexed <c>[x, y]</c>. Pure C# — no Godot, no world coordinates. Brick cells take damage
/// and turn to floor when destroyed; steel is indestructible; out-of-bounds reads as a steel
/// border so the maze is implicitly enclosed. <see cref="CellChanged"/> fires whenever a
/// cell's state actually changes, so the view re-renders only the affected tile.</summary>
public sealed class WallGrid : IWallGrid
{
    /// <summary>Hit points a brick cell starts with — three hits to break.</summary>
    public const int DefaultBrickHp = 3;

    /// <summary>Hit points a crate starts with — two hits to break (flimsier than brick).</summary>
    public const int DefaultCrateHp = 2;

    private readonly WallCell[,] _cells;

    public WallGrid(WallCell[,] cells)
    {
        _cells = cells;
        Width = cells.GetLength(0);
        Height = cells.GetLength(1);
    }

    /// <summary>Builds a grid from a material map, filling each brick cell with
    /// <see cref="DefaultBrickHp"/> and leaving floor/steel at 0 hp.</summary>
    public static WallGrid FromMaterials(CellMaterial[,] materials)
    {
        var width = materials.GetLength(0);
        var height = materials.GetLength(1);
        var cells = new WallCell[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var material = materials[x, y];
                var hp = material switch
                {
                    CellMaterial.Brick => DefaultBrickHp,
                    CellMaterial.Crate => DefaultCrateHp,
                    _ => 0,
                };
                cells[x, y] = new WallCell(material, hp);
            }
        }

        return new WallGrid(cells);
    }

    public int Width { get; }
    public int Height { get; }

    public event Action<WallCellChanged>? CellChanged;

    public WallCell GetCell(int x, int y) =>
        InBounds(x, y) ? _cells[x, y] : new WallCell(CellMaterial.Steel, 0);

    public bool IsBlocked(int x, int y) => GetCell(x, y).Material != CellMaterial.Floor;

    /// <summary>Overwrites a cell with an authoritative state and raises <see cref="CellChanged"/>
    /// if it differs. Unlike <see cref="DamageCell"/> (relative, local damage) this is absolute —
    /// used to sync the grid to a server snapshot's <c>WallDelta</c> in networked play.</summary>
    public void SetCell(int x, int y, WallCell cell)
    {
        if (!InBounds(x, y) || _cells[x, y] == cell)
        {
            return;
        }

        _cells[x, y] = cell;
        CellChanged?.Invoke(new WallCellChanged(x, y, cell));
    }

    public void DamageCell(int x, int y, int amount)
    {
        if (!InBounds(x, y))
        {
            return;
        }

        var cell = _cells[x, y];
        if (cell.Material is not (CellMaterial.Brick or CellMaterial.Crate))
        {
            return; // steel and floor are immune; brick and crates take damage
        }

        var hp = Math.Max(0, cell.Hp - amount);
        var updated = hp == 0 ? new WallCell(CellMaterial.Floor, 0) : cell with { Hp = hp };
        if (updated == cell)
        {
            return; // amount <= 0 changed nothing
        }

        _cells[x, y] = updated;
        CellChanged?.Invoke(new WallCellChanged(x, y, updated));
    }

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
}
