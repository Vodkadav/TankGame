using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TankGame.Domain;
using Xunit;

namespace TankGame.Tests.Domain;

public class WorldContractTests
{
    // An entity the test can advance and kill on demand, so the contract can
    // exercise stepping and reaping without depending on any real entity.
    private sealed class TestEntity : IEntity
    {
        public TestEntity() => Id = Guid.NewGuid();

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

    // Reference semantics the real World (S1-T2) must honour: owns entities,
    // raises EntitySpawned on Spawn, advances every entity on Step, then reaps
    // the dead and raises EntityDespawned for each. The contract tests pin this
    // behaviour; World is verified against the same expectations in S1-T2.
    private sealed class StubWorld : IWorld
    {
        private readonly List<IEntity> _entities = new();

        public IReadOnlyCollection<IEntity> Entities => _entities;

        public event Action<IEntity>? EntitySpawned;
        public event Action<IEntity>? EntityDespawned;

        public void Spawn(IEntity entity)
        {
            _entities.Add(entity);
            EntitySpawned?.Invoke(entity);
        }

        public void Step(float deltaSeconds)
        {
            foreach (var entity in _entities.ToArray())
            {
                entity.Step(deltaSeconds);
            }

            foreach (var dead in _entities.Where(e => !e.IsAlive).ToArray())
            {
                _entities.Remove(dead);
                EntityDespawned?.Invoke(dead);
            }
        }
    }

    [Fact]
    public void Spawn_AddsTheEntityAndRaisesEntitySpawned()
    {
        IWorld world = new StubWorld();
        var entity = new TestEntity();
        IEntity? announced = null;
        world.EntitySpawned += e => announced = e;

        world.Spawn(entity);

        Assert.Contains(entity, world.Entities);
        Assert.Same(entity, announced);
    }

    [Fact]
    public void Step_AdvancesEveryLivingEntity()
    {
        IWorld world = new StubWorld();
        var a = new TestEntity();
        var b = new TestEntity();
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
        IWorld world = new StubWorld();
        var survivor = new TestEntity();
        var doomed = new TestEntity();
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
        IWorld world = new StubWorld();
        var entity = new TestEntity();
        world.Spawn(entity);

        world.Step(1f);

        Assert.Contains(entity, world.Entities);
    }
}
