using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class ProjectileTests
{
    // Never hits anything.
    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
    }

    // A wall on the +X axis at x = WallX.
    private sealed class WallAtX(float wallX) : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance)
        {
            var remaining = wallX - origin.X;
            return remaining >= 0f && maxDistance >= remaining
                ? new RaycastHit(new Vector2(wallX, origin.Y), remaining)
                : null;
        }
    }

    private const float Speed = 200f;

    [Fact]
    public void Step_MovesAtSpeed_WhenNothingIsHit()
    {
        var shot = new Projectile(new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed);

        shot.Step(0.1f);

        Assert.True(shot.IsAlive);
        Assert.Equal(new Vector2(20f, 0f), shot.Position);
    }

    [Fact]
    public void Step_NormalisesDirection_SoNonUnitInputStillTravelsAtSpeed()
    {
        var shot = new Projectile(new OpenArena(), Vector2.Zero, new Vector2(5f, 0f), Speed);

        shot.Step(0.1f);

        Assert.Equal(20f, shot.Position.Length(), precision: 3);
    }

    [Fact]
    public void Step_DiesAndSnapsToHitPoint_WhenItReachesAWall()
    {
        var shot = new Projectile(new WallAtX(10f), Vector2.Zero, new Vector2(1f, 0f), Speed);

        shot.Step(0.1f); // would travel 20 units, but the wall is at 10

        Assert.False(shot.IsAlive);
        Assert.Equal(new Vector2(10f, 0f), shot.Position);
    }

    [Fact]
    public void Step_IsNoOp_OnceDead()
    {
        var shot = new Projectile(new WallAtX(10f), Vector2.Zero, new Vector2(1f, 0f), Speed);
        shot.Step(0.1f);
        var deadPosition = shot.Position;

        shot.Step(0.1f);

        Assert.False(shot.IsAlive);
        Assert.Equal(deadPosition, shot.Position);
    }
}
