using System.Collections.Generic;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class WallGridTests
{
    // A 2x2 grid laid out [x, y]: (0,0) floor, (0,1) brick, (1,0) steel, (1,1) brick.
    private static WallGrid Grid() => new(new[,]
    {
        { new WallCell(CellMaterial.Floor, 0), new WallCell(CellMaterial.Brick, WallGrid.DefaultBrickHp) },
        { new WallCell(CellMaterial.Steel, 0), new WallCell(CellMaterial.Brick, WallGrid.DefaultBrickHp) },
    });

    [Fact]
    public void Dimensions_MatchTheBackingArray()
    {
        var grid = Grid();

        Assert.Equal(2, grid.Width);
        Assert.Equal(2, grid.Height);
    }

    [Fact]
    public void IsBlocked_TrueForWalls_FalseForFloor()
    {
        var grid = Grid();

        Assert.False(grid.IsBlocked(0, 0)); // floor
        Assert.True(grid.IsBlocked(0, 1));  // brick
        Assert.True(grid.IsBlocked(1, 0));  // steel
    }

    [Fact]
    public void GetCell_OutOfBounds_ReadsAsBlockingSteel()
    {
        var grid = Grid();

        Assert.Equal(CellMaterial.Steel, grid.GetCell(-1, 0).Material);
        Assert.Equal(CellMaterial.Steel, grid.GetCell(0, 5).Material);
        Assert.True(grid.IsBlocked(99, 99));
    }

    [Fact]
    public void SetCell_OverwritesAbsolutely_AndRaisesCellChanged()
    {
        var grid = Grid();
        var changes = new List<WallCellChanged>();
        grid.CellChanged += changes.Add;

        grid.SetCell(0, 1, new WallCell(CellMaterial.Floor, 0)); // authoritative break-through

        Assert.Equal(CellMaterial.Floor, grid.GetCell(0, 1).Material);
        Assert.Equal(new WallCellChanged(0, 1, new WallCell(CellMaterial.Floor, 0)), Assert.Single(changes));
    }

    [Fact]
    public void SetCell_ToTheSameState_RaisesNothing()
    {
        var grid = Grid();
        var changes = new List<WallCellChanged>();
        grid.CellChanged += changes.Add;

        grid.SetCell(0, 1, new WallCell(CellMaterial.Brick, WallGrid.DefaultBrickHp)); // unchanged
        grid.SetCell(5, 5, new WallCell(CellMaterial.Floor, 0)); // out of bounds

        Assert.Empty(changes);
    }

    [Fact]
    public void DamageCell_ChipsBrickHp_AndRaisesCellChanged()
    {
        var grid = Grid();
        var changes = new List<WallCellChanged>();
        grid.CellChanged += changes.Add;

        grid.DamageCell(0, 1, 1);

        Assert.Equal(WallGrid.DefaultBrickHp - 1, grid.GetCell(0, 1).Hp);
        Assert.Equal(
            new WallCellChanged(0, 1, new WallCell(CellMaterial.Brick, WallGrid.DefaultBrickHp - 1)),
            Assert.Single(changes));
    }

    [Fact]
    public void DamageCell_BreaksBrickToFloor_AtZeroHp()
    {
        var grid = Grid();
        var changes = new List<WallCellChanged>();
        grid.CellChanged += changes.Add;

        grid.DamageCell(0, 1, WallGrid.DefaultBrickHp);

        Assert.Equal(CellMaterial.Floor, grid.GetCell(0, 1).Material);
        Assert.False(grid.IsBlocked(0, 1));
        Assert.Equal(new WallCell(CellMaterial.Floor, 0), changes[^1].Cell);
    }

    [Fact]
    public void DamageCell_TakesThreeHitsToBreakBrick()
    {
        var grid = Grid();

        grid.DamageCell(0, 1, 1);
        Assert.True(grid.IsBlocked(0, 1));
        grid.DamageCell(0, 1, 1);
        Assert.True(grid.IsBlocked(0, 1));
        grid.DamageCell(0, 1, 1);
        Assert.False(grid.IsBlocked(0, 1)); // broken after the third hit
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

    [Fact]
    public void DamageCell_OnFloorOrOutOfBounds_IsANoOp()
    {
        var grid = Grid();
        var raised = false;
        grid.CellChanged += _ => raised = true;

        grid.DamageCell(0, 0, 1);   // floor
        grid.DamageCell(-1, -1, 1); // out of bounds

        Assert.False(raised);
    }

    [Fact]
    public void FromMaterials_FillsBrickWithDefaultHp_AndLeavesOthersAtZero()
    {
        var grid = WallGrid.FromMaterials(new[,]
        {
            { CellMaterial.Brick, CellMaterial.Steel },
            { CellMaterial.Floor, CellMaterial.Brick },
        });

        Assert.Equal(WallGrid.DefaultBrickHp, grid.GetCell(0, 0).Hp);
        Assert.Equal(0, grid.GetCell(0, 1).Hp); // steel
        Assert.Equal(0, grid.GetCell(1, 0).Hp); // floor
        Assert.Equal(WallGrid.DefaultBrickHp, grid.GetCell(1, 1).Hp);
    }
}
