using System;
using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class WorldTests
{
    // Advances on Step, records how many times, and can be killed on demand.
    private sealed class FakeEntity : IEntity
    {
        public FakeEntity() => Id = Guid.NewGuid();

        public Guid Id { get; }
        public Vector2 Position { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public int StepCount { get; private set; }

        public void Kill() => IsAlive = false;

        public void Step(float deltaSeconds)
        {
            StepCount++;
            Position += new Vector2(deltaSeconds, 0f);
        }
    }

    // Spawns one child into the world the first time it is stepped — exercises
    // the "spawn during a step must not corrupt iteration" guarantee.
    private sealed class SpawningEntity : IEntity
    {
        private readonly IWorld _world;
        private bool _spawned;

        public SpawningEntity(IWorld world)
        {
            _world = world;
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }
        public Vector2 Position => Vector2.Zero;
        public bool IsAlive => true;
        public IEntity? Child { get; private set; }

        public void Step(float deltaSeconds)
        {
            if (_spawned)
            {
                return;
            }

            _spawned = true;
            Child = new FakeEntity();
            _world.Spawn(Child);
        }
    }

    [Fact]
    public void Spawn_AddsTheEntityAndRaisesEntitySpawned()
    {
        var world = new World();
        var entity = new FakeEntity();
        IEntity? announced = null;
        world.EntitySpawned += e => announced = e;

        world.Spawn(entity);

        Assert.Contains(entity, world.Entities);
        Assert.Same(entity, announced);
    }

    [Fact]
    public void Step_AdvancesEveryLivingEntity()
    {
        var world = new World();
        var a = new FakeEntity();
        var b = new FakeEntity();
        world.Spawn(a);
        world.Spawn(b);

        world.Step(0.25f);

        Assert.Equal(1, a.StepCount);
        Assert.Equal(1, b.StepCount);
        Assert.Equal(new Vector2(0.25f, 0f), a.Position);
    }

    [Fact]
    public void Step_ReapsDeadEntitiesAndRaisesEntityDespawned()
    {
        var world = new World();
        var survivor = new FakeEntity();
        var doomed = new FakeEntity();
        world.Spawn(survivor);
        world.Spawn(doomed);
        var despawned = new List<IEntity>();
        world.EntityDespawned += e => despawned.Add(e);

        doomed.Kill();
        world.Step(1f);

        Assert.DoesNotContain(doomed, world.Entities);
        Assert.Contains(survivor, world.Entities);
        Assert.Equal(new[] { (IEntity)doomed }, despawned);
    }

    [Fact]
    public void Step_LeavesLivingEntitiesInTheWorld()
    {
        var world = new World();
        var entity = new FakeEntity();
        world.Spawn(entity);

        world.Step(1f);

        Assert.Contains(entity, world.Entities);
    }

    [Fact]
    public void Entities_PreservesInsertionOrder()
    {
        var world = new World();
        var first = new FakeEntity();
        var second = new FakeEntity();
        var third = new FakeEntity();
        world.Spawn(first);
        world.Spawn(second);
        world.Spawn(third);

        Assert.Equal(new IEntity[] { first, second, third }, world.Entities);
    }

    [Fact]
    public void Step_SpawningDuringAStep_DoesNotCorruptIteration_AndDefersTheNewEntity()
    {
        var world = new World();
        var spawner = new SpawningEntity(world);
        world.Spawn(spawner);

        world.Step(0.1f);

        Assert.NotNull(spawner.Child);
        Assert.Contains(spawner.Child!, world.Entities);
        // The child spawned mid-step is not itself stepped until the next tick.
        Assert.Equal(0, ((FakeEntity)spawner.Child!).StepCount);
    }

    [Fact]
    public void Spawn_DuringStep_StillRaisesEntitySpawned()
    {
        var world = new World();
        var spawner = new SpawningEntity(world);
        world.Spawn(spawner);
        var announced = new List<IEntity>();
        world.EntitySpawned += announced.Add;

        world.Step(0.1f);

        Assert.Contains(spawner.Child!, announced);
    }
}
