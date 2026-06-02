using System.Numerics;
using TankGame.Domain;
using Xunit;

namespace TankGame.Tests.Domain;

public class ArenaContractTests
{
    // A one-wall arena: the ray hits if it travels at least `wallDistance`.
    private sealed class StubArena(float wallDistance) : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance)
        {
            if (maxDistance < wallDistance)
            {
                return null;
            }

            var dir = Vector2.Normalize(direction);
            return new RaycastHit(origin + (dir * wallDistance), wallDistance);
        }
    }

    [Fact]
    public void RaycastFirstHit_ReturnsHit_WhenObstacleWithinRange()
    {
        IArena arena = new StubArena(wallDistance: 5f);

        var hit = arena.RaycastFirstHit(Vector2.Zero, new Vector2(1f, 0f), maxDistance: 10f);

        Assert.NotNull(hit);
        Assert.Equal(5f, hit!.Value.Distance);
        Assert.Equal(new Vector2(5f, 0f), hit.Value.Point);
    }

    [Fact]
    public void RaycastFirstHit_ReturnsNull_WhenNothingWithinRange()
    {
        IArena arena = new StubArena(wallDistance: 5f);

        var hit = arena.RaycastFirstHit(Vector2.Zero, new Vector2(1f, 0f), maxDistance: 3f);

        Assert.Null(hit);
    }
}
