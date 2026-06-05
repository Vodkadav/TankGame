using System.Numerics;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class SandbagFieldTests
{
    [Fact]
    public void SlowsATank_OnlyOnASandbagCell()
    {
        var sandbags = new bool[3, 3];
        sandbags[1, 1] = true;
        var field = new SandbagField(sandbags, tileSize: 64f, origin: Vector2.Zero);

        Assert.Equal(SandbagField.SlowFactor, field.SpeedFactorAt(new Vector2(96f, 96f))); // centre of (1,1)
        Assert.Equal(1f, field.SpeedFactorAt(new Vector2(32f, 32f)));                       // floor cell (0,0)
    }

    [Fact]
    public void OffTheGrid_IsNormalGround()
    {
        var field = new SandbagField(new bool[2, 2], tileSize: 64f, origin: Vector2.Zero);

        Assert.Equal(1f, field.SpeedFactorAt(new Vector2(-10f, -10f)));
        Assert.Equal(1f, field.SpeedFactorAt(new Vector2(1000f, 1000f)));
    }
}
