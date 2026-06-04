using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class BouncingBehaviourTests
{
    // A single vertical wall plane at x = wallX that only a +X ray meets (west face, normal -X).
    private sealed class WallAtX(float wallX) : IArena
    {
        public int DamageApplied { get; private set; }

        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance)
        {
            if (direction.X <= 0f)
            {
                return null; // the wall only faces an approach from the left
            }

            var t = (wallX - origin.X) / direction.X;
            return t >= 0f && t <= maxDistance
                ? new RaycastHit(new Vector2(wallX, origin.Y), t, new Vector2(-1f, 0f))
                : null;
        }

        public void DamageAt(Vector2 point, Vector2 direction, int amount) => DamageApplied += amount;
        public bool IsBlocked(Vector2 point) => point.X >= wallX;
    }

    private static ProjectileState ShotAt(Vector2 pos, Vector2 dir, float speed = 100f) =>
        new() { Position = pos, Direction = dir, Speed = speed, Damage = 1 };

    [Fact]
    public void ReflectsOffTheWall_AndKeepsGoing_WhenBouncesRemain()
    {
        var state = ShotAt(Vector2.Zero, new Vector2(1f, 0f));

        new BouncingBehaviour(bounces: 1).Step(state, new WallAtX(40f), deltaSeconds: 1f); // 100 units

        Assert.True(state.IsAlive);                       // bounced, not spent
        Assert.Equal(-1f, state.Direction.X, precision: 3); // now heading back -X
        Assert.True(state.Position.X < 40f);              // travelled past the wall back toward origin
    }

    [Fact]
    public void ExpiresAndDamages_WhenItHitsWithNoBouncesLeft()
    {
        var arena = new WallAtX(40f);
        var state = ShotAt(Vector2.Zero, new Vector2(1f, 0f));

        new BouncingBehaviour(bounces: 0).Step(state, arena, deltaSeconds: 1f);

        Assert.False(state.IsAlive);
        Assert.Equal(40f, state.Position.X, precision: 3);
        Assert.Equal(1, arena.DamageApplied);
    }

    [Fact]
    public void TravelsStraight_WhenNothingIsHit()
    {
        var state = ShotAt(Vector2.Zero, new Vector2(0f, 1f)); // +Y never meets the +X-facing wall

        new BouncingBehaviour(bounces: 3).Step(state, new WallAtX(40f), deltaSeconds: 0.5f);

        Assert.True(state.IsAlive);
        Assert.Equal(50f, state.Position.Y, precision: 3); // 100 u/s * 0.5s
    }
}
