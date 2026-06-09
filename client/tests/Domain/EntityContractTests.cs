using System;
using System.Numerics;
using TankGame.Domain;
using Xunit;

namespace TankGame.Tests.Domain;

public class EntityContractTests
{
    // Moves at a constant velocity each Step and can be killed on command —
    // the minimal thing the world owns, advances, and reaps.
    private sealed class StubEntity : IEntity
    {
        private readonly Vector2 _velocity;

        public StubEntity(Vector2 start, Vector2 velocity)
        {
            Id = Guid.NewGuid();
            Position = start;
            _velocity = velocity;
            IsAlive = true;
        }

        public Guid Id { get; }
        public Vector2 Position { get; private set; }
        public bool IsAlive { get; private set; }

        public void Kill() => IsAlive = false;

        public void Step(float deltaSeconds) => Position += _velocity * deltaSeconds;
    }

    [Fact]
    public void Entity_HasANonEmptyIdentityThatSurvivesStepping()
    {
        IEntity entity = new StubEntity(Vector2.Zero, new Vector2(1f, 0f));
        var id = entity.Id;

        entity.Step(1f);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(id, entity.Id);
    }

    [Fact]
    public void DistinctEntities_HaveDistinctIds()
    {
        IEntity a = new StubEntity(Vector2.Zero, Vector2.Zero);
        IEntity b = new StubEntity(Vector2.Zero, Vector2.Zero);

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Step_AdvancesPositionByTheElapsedDelta()
    {
        IEntity entity = new StubEntity(Vector2.Zero, new Vector2(10f, 0f));

        entity.Step(0.5f);

        Assert.Equal(new Vector2(5f, 0f), entity.Position);
    }

    [Fact]
    public void Entity_StartsAliveAndStaysDeadOnceKilled()
    {
        var entity = new StubEntity(Vector2.Zero, Vector2.Zero);
        Assert.True(entity.IsAlive);

        entity.Kill();

        Assert.False(entity.IsAlive);
    }

    [Fact]
    public void Layer_DefaultsToGround_ForAnEntityThatDoesNotDeclareOne()
    {
        // StubEntity declares no Layer — it inherits the IEntity default (ground = 0), so a flat arena
        // and every existing fake stay layer-0 with no edit (ADR-0018 step 2).
        IEntity entity = new StubEntity(Vector2.Zero, Vector2.Zero);

        Assert.Equal(0, entity.Layer);
    }

    [Fact]
    public void Layer_ReflectsAnEntityThatSitsOnARaisedLayer()
    {
        IEntity entity = new LayeredEntity(layer: 3);

        Assert.Equal(3, entity.Layer);
    }

    private sealed class LayeredEntity(int layer) : IEntity
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Vector2 Position => Vector2.Zero;
        public bool IsAlive => true;
        public int Layer { get; } = layer;
        public void Step(float deltaSeconds) { }
    }
}
