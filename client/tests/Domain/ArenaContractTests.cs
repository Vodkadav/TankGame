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
            return new RaycastHit(origin + (dir * wallDistance), wallDistance, -dir);
        }

        public void DamageAt(Vector2 point, Vector2 direction, int amount)
        {
        }

        public bool IsBlocked(Vector2 point) => point.X >= wallDistance;
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

    // StubArena implements only the flat queries; the layer-aware overloads are inherited (default
    // interface members) and delegate to the flat ones — so a flat arena and every existing fake behave
    // identically per layer with no edit (ADR-0018 step 2; a layered GridArena overrides them later).
    [Fact]
    public void IsBlocked_LayerAwareOverload_DelegatesToTheFlatQuery_ByDefault()
    {
        IArena arena = new StubArena(wallDistance: 5f);

        Assert.True(arena.IsBlocked(new Vector2(6f, 0f), layer: 2));
        Assert.False(arena.IsBlocked(new Vector2(1f, 0f), layer: 2));
    }

    [Fact]
    public void RaycastFirstHit_LayerAwareOverload_DelegatesToTheFlatQuery_ByDefault()
    {
        IArena arena = new StubArena(wallDistance: 5f);

        var flat = arena.RaycastFirstHit(Vector2.Zero, new Vector2(1f, 0f), 10f);
        var layered = arena.RaycastFirstHit(Vector2.Zero, new Vector2(1f, 0f), 10f, layer: 1);

        Assert.NotNull(layered);
        Assert.Equal(flat!.Value.Distance, layered!.Value.Distance);
    }

    [Fact]
    public void LayerAfterMove_KeepsTheCurrentLayer_ByDefault()
    {
        // StubArena declares no ramp logic, so a move never changes the tank's layer — a flat arena
        // is single-layer (ADR-0018 step 2). A layered GridArena overrides this for ramps.
        IArena arena = new StubArena(wallDistance: 5f);

        Assert.Equal(3, arena.LayerAfterMove(Vector2.Zero, new Vector2(10f, 0f), currentLayer: 3));
    }
}
