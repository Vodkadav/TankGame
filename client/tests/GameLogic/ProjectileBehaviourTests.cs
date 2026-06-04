using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class ProjectileBehaviourTests
{
    // A wall whose face sits at x = wallX; a +X ray hits its west face (normal -X).
    private sealed class WallAtX(float wallX) : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance)
        {
            var remaining = wallX - origin.X;
            return remaining >= 0f && maxDistance >= remaining
                ? new RaycastHit(new Vector2(wallX, origin.Y), remaining, new Vector2(-1f, 0f))
                : null;
        }

        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => point.X >= wallX;
    }

    // Advances the shot in a straight line and never collides — so a projectile using it sails
    // through a wall that StraightBehaviour would have stopped at, proving delegation.
    private sealed class GhostBehaviour : IProjectileBehaviour
    {
        public void Step(ProjectileState state, IArena arena, float deltaSeconds) =>
            state.Position += state.Direction * state.Speed * deltaSeconds;
    }

    [Fact]
    public void Projectile_DelegatesItsStep_ToTheInjectedBehaviour()
    {
        var projectile = new Projectile(new WallAtX(50f), Vector2.Zero, new Vector2(1f, 0f),
            speed: 600f, behaviour: new GhostBehaviour());

        projectile.Step(0.2f); // 120 units of travel, past the wall at x=50

        Assert.True(projectile.Position.X > 50f, "the ghost behaviour ignores walls");
        Assert.True(projectile.IsAlive);
    }

    [Fact]
    public void StraightBehaviour_StopsAndDamages_AtTheFirstHit()
    {
        var state = new ProjectileState
        {
            Position = Vector2.Zero,
            Direction = new Vector2(1f, 0f),
            Speed = 600f,
            Damage = 1,
        };

        StraightBehaviour.Instance.Step(state, new WallAtX(30f), 0.2f);

        Assert.Equal(30f, state.Position.X, precision: 3); // snapped to the wall face
        Assert.False(state.IsAlive);                       // spent on impact
    }
}
