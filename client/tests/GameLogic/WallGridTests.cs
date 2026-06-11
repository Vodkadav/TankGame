using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class WallGridTests
{
    private sealed class ScriptedInput(TankInput value) : IInputSource
    {
        public TankInput Read() => value;
    }

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
    public void LayerAt_DefaultsToGround_WhenNoLayerMapIsSupplied()
    {
        var grid = Grid();

        Assert.Equal(0, grid.LayerAt(0, 0));
        Assert.Equal(0, grid.LayerAt(1, 1));
        Assert.Equal(0, grid.LayerAt(-5, 99)); // out of bounds reads as ground too
    }

    [Fact]
    public void LayerAt_ReportsTheSuppliedLayer_ForARaisedCell()
    {
        var grid = WallGrid.FromMaterials(
            new[,]
            {
                { CellMaterial.Floor, CellMaterial.Floor },
                { CellMaterial.Floor, CellMaterial.Floor },
            },
            layers: new[,] { { 0, 0 }, { 0, 2 } });

        Assert.Equal(0, grid.LayerAt(0, 0));
        Assert.Equal(2, grid.LayerAt(1, 1));
    }

    [Fact]
    public void IsRamp_IsFalseEverywhere_WhenNoRampMapIsSupplied()
    {
        var grid = Grid();

        Assert.False(grid.IsRamp(0, 0));
        Assert.False(grid.IsRamp(1, 1));
        Assert.False(grid.IsRamp(-1, 9)); // out of bounds is never a ramp
    }

    [Fact]
    public void IsRamp_ReportsTheSuppliedRampCells()
    {
        var grid = WallGrid.FromMaterials(
            new[,]
            {
                { CellMaterial.Floor, CellMaterial.Floor },
                { CellMaterial.Floor, CellMaterial.Floor },
            },
            ramps: new[,] { { false, true }, { false, false } });

        Assert.True(grid.IsRamp(0, 1));
        Assert.False(grid.IsRamp(0, 0));
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
    public void FromMaterials_FillsACrate_WithItsHitPoints()
    {
        var grid = WallGrid.FromMaterials(new[,] { { CellMaterial.Crate } });

        Assert.Equal(WallGrid.DefaultCrateHp, grid.GetCell(0, 0).Hp);
    }

    [Fact]
    public void DamageCell_BreaksACrateToFloor_AfterTwoHits()
    {
        var grid = WallGrid.FromMaterials(new[,] { { CellMaterial.Floor, CellMaterial.Crate } });

        grid.DamageCell(0, 1, 1);
        Assert.True(grid.IsBlocked(0, 1)); // a crate survives the first hit (2 hp)
        grid.DamageCell(0, 1, 1);
        Assert.False(grid.IsBlocked(0, 1)); // and breaks to floor on the second
        Assert.Equal(CellMaterial.Floor, grid.GetCell(0, 1).Material);
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

    // ── Scaled footprints (owner feedback 2026-06-11 evening): collision follows the gizmo scale ──

    // A 5x5 floor field with one solid cell at the centre (2,2), posed at the given scale.
    private static WallGrid ScaledCentre(CellMaterial material, float scale)
    {
        var materials = new CellMaterial[5, 5];
        materials[2, 2] = material;
        return WallGrid.FromMaterials(materials, transforms: new Dictionary<(int X, int Y), PropTransform>
        {
            [(2, 2)] = new(YawDeg: 45f, PitchDeg: 0f, RollDeg: 0f, Scale: scale),
        });
    }

    [Fact]
    public void IsBlocked_SteelAtScaleTwo_BlocksItsEightNeighbours_AndShots()
    {
        var grid = ScaledCentre(CellMaterial.Steel, scale: 2f);

        for (var x = 1; x <= 3; x++)
        {
            for (var y = 1; y <= 3; y++)
            {
                Assert.True(grid.IsBlocked(x, y), $"({x},{y}) lies under the 2x footprint");
                Assert.True(grid.BlocksShots(x, y), $"({x},{y}) blocks shots under the 2x footprint");
            }
        }

        Assert.False(grid.IsBlocked(0, 2)); // two cells out — beyond the footprint
        Assert.False(grid.IsBlocked(4, 2));
        Assert.False(grid.BlocksShots(2, 0));
        Assert.False(grid.BlocksShots(2, 4));
    }

    [Fact]
    public void IsBlocked_SteelAtScaleOne_BlocksOnlyItsOwnCell()
    {
        var grid = ScaledCentre(CellMaterial.Steel, scale: 1f); // rotated but unscaled

        Assert.True(grid.IsBlocked(2, 2));
        Assert.False(grid.IsBlocked(1, 2));
        Assert.False(grid.IsBlocked(2, 1));
        Assert.False(grid.BlocksShots(3, 3));
    }

    [Fact]
    public void IsBlocked_FloorWithATransform_NeverBlocks()
    {
        var grid = ScaledCentre(CellMaterial.Floor, scale: 3f);

        Assert.False(grid.IsBlocked(2, 2));
        Assert.False(grid.IsBlocked(1, 2));
        Assert.False(grid.BlocksShots(2, 2));
        Assert.False(grid.BlocksShots(3, 2));
    }

    [Fact]
    public void DamageCell_OnAnOverlappedFloorCell_StaysANoOp()
    {
        var grid = ScaledCentre(CellMaterial.Steel, scale: 2f);
        var raised = false;
        grid.CellChanged += _ => raised = true;

        grid.DamageCell(1, 2, 99); // overlapped by the footprint, but the cell itself is floor

        Assert.Equal(CellMaterial.Floor, grid.GetCell(1, 2).Material);
        Assert.False(raised);
    }

    [Fact]
    public void IsBlocked_FootprintVanishes_WhenItsScaledCrateIsBroken()
    {
        var grid = ScaledCentre(CellMaterial.Crate, scale: 2f);
        Assert.True(grid.IsBlocked(1, 2)); // the enlarged crate spills onto its neighbour

        grid.DamageCell(2, 2, WallGrid.DefaultCrateHp); // break the crate itself

        Assert.False(grid.IsBlocked(2, 2));
        Assert.False(grid.IsBlocked(1, 2)); // the prop is gone, so its footprint is too
    }

    [Fact]
    public void Tank_DrivingTowardAnEnlargedBlock_StopsBeforeTheOverlappedCell()
    {
        // A single row: open floor (cols 0-2), a steel block at col 3 scaled 2x so its footprint
        // spills onto col 2, then floor (col 4). Drive a real Tank through a GridArena like
        // TankTests.Step_DrivingUpARamp does — the arena resolves movement per cell, so it
        // inherits the footprint for free.
        var grid = WallGrid.FromMaterials(
            new[,]
            {
                { CellMaterial.Floor }, { CellMaterial.Floor }, { CellMaterial.Floor },
                { CellMaterial.Steel }, { CellMaterial.Floor },
            },
            transforms: new Dictionary<(int X, int Y), PropTransform>
            {
                [(3, 0)] = new(YawDeg: 0f, PitchDeg: 0f, RollDeg: 0f, Scale: 2f),
            });
        var arena = new GridArena(grid, tileSize: 100f, Vector2.Zero);
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        var tank = new Tank(input, new World(), arena, new Vector2(50f, 50f), speed: 100f,
            fireInterval: 0.3f, projectileSpeed: 600f);

        for (var i = 0; i < 30; i++)
        {
            tank.Step(0.1f); // drive +X into the enlarged block
        }

        // The overlapped cell (col 2) starts at x=200: the tank's leading edge never enters it.
        Assert.True(tank.Position.X + Tank.CollisionRadius <= 200f,
            $"the tank stopped at the footprint, not the real cell (X={tank.Position.X})");
        Assert.True(tank.Position.X > 150f, "the tank drove right up to the footprint before stopping");
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
