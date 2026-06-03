using System.Numerics;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class BushFieldTests
{
    // tiles of 64, origin 0; cell (1,0) is a bush, everything else clear.
    private static BushField OneBushAtCellOneZero()
    {
        var bushes = new bool[3, 2];
        bushes[1, 0] = true;
        return new BushField(bushes, tileSize: 64f, origin: Vector2.Zero);
    }

    [Fact]
    public void ConcealsAt_IsTrue_InsideABushCell()
    {
        var field = OneBushAtCellOneZero();

        Assert.True(field.ConcealsAt(new Vector2(80f, 32f))); // world point in cell (1,0)
    }

    [Fact]
    public void ConcealsAt_IsFalse_OnPlainFloor()
    {
        var field = OneBushAtCellOneZero();

        Assert.False(field.ConcealsAt(new Vector2(32f, 32f))); // cell (0,0), not a bush
    }

    [Fact]
    public void ConcealsAt_IsFalse_OutsideTheGrid()
    {
        var field = OneBushAtCellOneZero();

        Assert.False(field.ConcealsAt(new Vector2(-10f, -10f)));
        Assert.False(field.ConcealsAt(new Vector2(10000f, 10000f)));
    }
}
