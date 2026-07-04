using System;
using System.Collections.Generic;
using System.Linq;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class AirstrikeSpreadTests
{
    [Fact]
    public void Grow_ReturnsExactlyTargetCells_WhenTheFieldIsLargeEnough()
    {
        var cells = AirstrikeSpread.Grow(10, 8, cols: 28, rows: 16, targetCells: 30, new Random(1));

        Assert.Equal(30, cells.Count);
    }

    [Fact]
    public void Grow_KeepsEveryCellInBounds_WithNoDuplicates()
    {
        var cells = AirstrikeSpread.Grow(5, 5, cols: 28, rows: 16, targetCells: 30, new Random(7));

        Assert.Equal(cells.Count, cells.Distinct().Count());
        foreach (var (x, y) in cells)
        {
            Assert.InRange(x, 0, 27);
            Assert.InRange(y, 0, 15);
        }
    }

    // Eden growth annexes a cell only from the frontier (cells edge-adjacent to the blob), so every cell
    // after the seed touches an earlier one — the blob is a single connected clump.
    [Fact]
    public void Grow_ProducesAConnectedBlob_EachCellAdjacentToAnEarlierOne()
    {
        var cells = AirstrikeSpread.Grow(14, 8, cols: 28, rows: 16, targetCells: 30, new Random(3));

        for (var i = 1; i < cells.Count; i++)
        {
            var adjacent = false;
            for (var j = 0; j < i; j++)
            {
                if (Manhattan(cells[i], cells[j]) == 1)
                {
                    adjacent = true;
                    break;
                }
            }

            Assert.True(adjacent, $"cell {cells[i]} at index {i} is not edge-adjacent to any earlier cell");
        }
    }

    [Fact]
    public void Grow_SeedsAtTheClampedStart()
    {
        var cells = AirstrikeSpread.Grow(999, -5, cols: 28, rows: 16, targetCells: 30, new Random(2));

        Assert.Equal((27, 0), cells[0]);
    }

    [Fact]
    public void Grow_IsDeterministic_ForAGivenSeed()
    {
        var a = AirstrikeSpread.Grow(14, 8, cols: 28, rows: 16, targetCells: 30, new Random(42));
        var b = AirstrikeSpread.Grow(14, 8, cols: 28, rows: 16, targetCells: 30, new Random(42));

        Assert.Equal(a, b);
    }

    [Fact]
    public void Grow_ProducesDifferentShapes_ForDifferentSeeds()
    {
        var a = AirstrikeSpread.Grow(14, 8, cols: 28, rows: 16, targetCells: 30, new Random(1));
        var b = AirstrikeSpread.Grow(14, 8, cols: 28, rows: 16, targetCells: 30, new Random(2));
        var c = AirstrikeSpread.Grow(14, 8, cols: 28, rows: 16, targetCells: 30, new Random(3));

        // The randomness is real: different seeds do not collapse to one blob.
        Assert.True(!a.SequenceEqual(b) || !a.SequenceEqual(c), "different seeds should yield different blobs");
    }

    [Fact]
    public void Grow_OnATinyField_IsCappedByTheGrid_AndDoesNotLoopForever()
    {
        var cells = AirstrikeSpread.Grow(0, 0, cols: 2, rows: 2, targetCells: 30, new Random(9));

        Assert.True(cells.Count <= 4, $"a 2x2 grid holds at most 4 cells; got {cells.Count}");
        Assert.Equal(cells.Count, cells.Distinct().Count());
    }

    private static int Manhattan((int X, int Y) a, (int X, int Y) b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}
