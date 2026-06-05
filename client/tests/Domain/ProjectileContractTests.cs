using System;
using System.Numerics;
using TankGame.Domain;
using Xunit;

namespace TankGame.Tests.Domain;

public class ProjectileContractTests
{
    // Travels in a straight line until it reaches the arena's first hit, then dies.
    private sealed class StubProjectile : IProjectile
    {
        private readonly IArena _arena;
        private readonly Vector2 _direction;
        private readonly float _speed;
        private float _travelled;

        public StubProjectile(IArena arena, Vector2 origin, Vector2 direction, float speed)
        {
            Id = Guid.NewGuid();
            _arena = arena;
            Position = origin;
            _direction = Vector2.Normalize(direction);
            _speed = speed;
            IsAlive = true;
        }

        public Guid Id { get; }
        public int Team => 0;
        public Vector2 Position { get; private set; }
        public Vector2 Direction => _direction;
        public bool IsAlive { get; private set; }

        public void Step(float deltaSeconds)
        {
            if (!IsAlive)
            {
                return;
            }

            var move = _speed * deltaSeconds;
            if (_arena.RaycastFirstHit(Position, _direction, move) is { } hit)
            {
                Position = hit.Point;
                IsAlive = false;
                return;
            }

            Position += _direction * move;
            _travelled += move;
        }
    }

    private sealed class WallAtArena(float distance) : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance)
        {
            var remaining = distance - origin.X;
            return maxDistance >= remaining
                ? new RaycastHit(new Vector2(distance, origin.Y), remaining, new Vector2(-1f, 0f))
                : null;
        }

        public void DamageAt(Vector2 point, Vector2 direction, int amount)
        {
        }

        public bool IsBlocked(Vector2 point) => false;
    }

    [Fact]
    public void Step_MovesWhileNothingIsHit()
    {
        IProjectile shot = new StubProjectile(new WallAtArena(1000f), Vector2.Zero, new Vector2(1f, 0f), speed: 200f);

        shot.Step(0.1f);

        Assert.True(shot.IsAlive);
        Assert.Equal(new Vector2(20f, 0f), shot.Position);
    }

    [Fact]
    public void Step_DiesWhenItHitsTheArena()
    {
        IProjectile shot = new StubProjectile(new WallAtArena(10f), Vector2.Zero, new Vector2(1f, 0f), speed: 200f);

        shot.Step(0.1f);

        Assert.False(shot.IsAlive);
        Assert.Equal(new Vector2(10f, 0f), shot.Position);
    }
}
