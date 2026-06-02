using System;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class RectArenaTests
{
    // 100x100 arena with the origin corner at (0,0).
    private static RectArena Arena() => new(new Vector2(0f, 0f), new Vector2(100f, 100f));

    [Fact]
    public void RaycastFirstHit_FromCentre_ExitsTheRightWall_GoingPositiveX()
    {
        var hit = Arena().RaycastFirstHit(new Vector2(50f, 50f), new Vector2(1f, 0f), maxDistance: 1000f);

        Assert.NotNull(hit);
        Assert.Equal(new Vector2(100f, 50f), hit!.Value.Point);
        Assert.Equal(50f, hit.Value.Distance, precision: 3);
    }

    [Fact]
    public void RaycastFirstHit_FromCentre_ExitsTheTopWall_GoingNegativeY()
    {
        var hit = Arena().RaycastFirstHit(new Vector2(50f, 40f), new Vector2(0f, -1f), maxDistance: 1000f);

        Assert.NotNull(hit);
        Assert.Equal(new Vector2(50f, 0f), hit!.Value.Point);
        Assert.Equal(40f, hit.Value.Distance, precision: 3);
    }

    [Fact]
    public void RaycastFirstHit_TakesTheNearestWall_OnADiagonal()
    {
        // From (90,50) on a 45° ray, the right wall (10 units of X away) is nearest;
        // the diagonal travel distance is 10·√2 and the exit point is (100,60).
        var hit = Arena().RaycastFirstHit(new Vector2(90f, 50f), new Vector2(1f, 1f), maxDistance: 1000f);

        Assert.NotNull(hit);
        Assert.Equal(10f * MathF.Sqrt(2f), hit!.Value.Distance, precision: 3);
        Assert.Equal(new Vector2(100f, 60f), hit.Value.Point);
    }

    [Fact]
    public void RaycastFirstHit_ReturnsNull_WhenTheWallIsBeyondMaxDistance()
    {
        var hit = Arena().RaycastFirstHit(new Vector2(50f, 50f), new Vector2(1f, 0f), maxDistance: 10f);

        Assert.Null(hit);
    }

    [Fact]
    public void RaycastFirstHit_NormalisesDirection()
    {
        var hit = Arena().RaycastFirstHit(new Vector2(50f, 50f), new Vector2(5f, 0f), maxDistance: 1000f);

        Assert.NotNull(hit);
        Assert.Equal(50f, hit!.Value.Distance, precision: 3);
    }
}
