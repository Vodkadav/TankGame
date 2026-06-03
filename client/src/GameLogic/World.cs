using System;
using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The client-prediction entity container. Owns every live <see cref="IEntity"/>
/// in insertion order, advances them once per <see cref="Step"/>, and reaps the dead —
/// raising <see cref="EntitySpawned"/>/<see cref="EntityDespawned"/> so Presentation can
/// mirror the set without the play scene hand-spawning. Pure C# — no Godot, no networking
/// (see <c>docs/adr/PROPOSAL-entity-spine.md</c>).</summary>
public sealed class World : IWorld
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
        // Snapshot the live set so an entity that spawns a child mid-step (the
        // future-spawner seam) does not mutate the collection we are iterating;
        // the child joins the world now but is not stepped until the next tick.
        foreach (var entity in _entities.ToArray())
        {
            entity.Step(deltaSeconds);
        }

        var index = 0;
        while (index < _entities.Count)
        {
            var entity = _entities[index];
            if (entity.IsAlive)
            {
                index++;
                continue;
            }

            _entities.RemoveAt(index);
            EntityDespawned?.Invoke(entity);
        }
    }
}
