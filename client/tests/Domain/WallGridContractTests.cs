using System;
using System.Collections.Generic;
using TankGame.Domain;
using Xunit;

namespace TankGame.Tests.Domain;

public class WallGridContractTests
{
    // Reference semantics the real WallGrid (M2-T3) must honour: a backing array of cells;
    // out-of-bounds reads as blocking steel; DamageCell chips brick and breaks it to floor
    // at 0 hp, raising CellChanged; steel and floor are immune. Pinned here on a fake so the
    // contract is exercised before the impl exists.
    private sealed class StubWallGrid : IWallGrid
    {
        private readonly WallCell[,] _cells;

        public StubWallGrid(WallCell[,] cells)
        {
            _cells = cells;
            Width = cells.GetLength(0);
            Height = cells.GetLength(1);
        }

        public int Width { get; }
        public int Height { get; }

        public event Action<WallCellChanged>? CellChanged;

        private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public WallCell GetCell(int x, int y) =>
            InBounds(x, y) ? _cells[x, y] : new WallCell(CellMaterial.Steel, 0);

        public bool IsBlocked(int x, int y) => CellMaterials.BlocksMovement(GetCell(x, y).Material);

        public bool BlocksShots(int x, int y) => CellMaterials.BlocksShots(GetCell(x, y).Material);

        public void DamageCell(int x, int y, int amount)
        {
            if (!InBounds(x, y))
            {
                return;
            }

            var cell = _cells[x, y];
            if (cell.Material != CellMaterial.Brick)
            {
                return;
            }

            var hp = Math.Max(0, cell.Hp - amount);
            var updated = hp == 0 ? new WallCell(CellMaterial.Floor, 0) : cell with { Hp = hp };
            if (updated == cell)
            {
                return;
            }

            _cells[x, y] = updated;
            CellChanged?.Invoke(new WallCellChanged(x, y, updated));
        }
    }

    private const int BrickHp = 3;

    private static StubWallGrid Grid() => new(new[,]
    {
        { new WallCell(CellMaterial.Floor, 0), new WallCell(CellMaterial.Brick, BrickHp) },
        { new WallCell(CellMaterial.Steel, 0), new WallCell(CellMaterial.Brick, BrickHp) },
    });

    [Fact]
    public void IsBlocked_IsTrueForWalls_FalseForFloor()
    {
        var grid = Grid();

        Assert.False(grid.IsBlocked(0, 0)); // floor
        Assert.True(grid.IsBlocked(0, 1)); // brick
        Assert.True(grid.IsBlocked(1, 0)); // steel
    }

    [Fact]
    public void GetCell_OutOfBounds_ReadsAsBlockingSteel()
    {
        var grid = Grid();

        Assert.Equal(CellMaterial.Steel, grid.GetCell(-1, 0).Material);
        Assert.True(grid.IsBlocked(99, 99));
    }

    [Fact]
    public void DamageCell_ChipsBrickHp_AndRaisesCellChanged()
    {
        var grid = Grid();
        var changes = new List<WallCellChanged>();
        grid.CellChanged += changes.Add;

        grid.DamageCell(0, 1, 1);

        Assert.Equal(BrickHp - 1, grid.GetCell(0, 1).Hp);
        Assert.Equal(new WallCellChanged(0, 1, new WallCell(CellMaterial.Brick, BrickHp - 1)), Assert.Single(changes));
    }

    [Fact]
    public void DamageCell_BreaksBrickToFloor_AtZeroHp()
    {
        var grid = Grid();

        grid.DamageCell(0, 1, BrickHp);

        Assert.Equal(CellMaterial.Floor, grid.GetCell(0, 1).Material);
        Assert.False(grid.IsBlocked(0, 1));
    }

    [Fact]
    public void DamageCell_LeavesSteelUntouched_AndRaisesNoEvent()
    {
        var grid = Grid();
        var raised = false;
        grid.CellChanged += _ => raised = true;

        grid.DamageCell(1, 0, 99);

        Assert.Equal(CellMaterial.Steel, grid.GetCell(1, 0).Material);
        Assert.False(raised);
    }
}
