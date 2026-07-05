using System;
using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The client-prediction entity container. Owns every live <see cref="IEntity"/>
/// in insertion order, advances them once per <see cref="Step"/>, runs the optional combat
/// pass, then reaps the dead — raising <see cref="EntitySpawned"/>/<see cref="EntityDespawned"/>
/// so Presentation can mirror the set without the play scene hand-spawning. Pure C# — no
/// Godot, no networking (see <c>docs/adr/0010-entity-spine.md</c>).</summary>
public sealed class World : IWorld
{
    private readonly List<IEntity> _entities = new();
    private readonly ICombatResolver? _combat;

    /// <param name="combat">Optional combat pass run each step between advancing entities and
    /// reaping the dead. Null for a world with no combat (e.g. the entity-spine tests).</param>
    public World(ICombatResolver? combat = null) => _combat = combat;

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
        // Only entities alive at the start of the step are advanced, so a child spawned
        // mid-step (the future-spawner seam) joins the world now but is not stepped until the
        // next tick. Entities are only ever appended during a step (reaping happens below),
        // so an index bound gives the same snapshot the old per-frame ToArray copy did —
        // without allocating on every frame of the WASM build.
        var liveAtStepStart = _entities.Count;
        for (var i = 0; i < liveAtStepStart; i++)
        {
            _entities[i].Step(deltaSeconds);
        }

        // Resolve combat after everyone has moved, before reaping — so a tank killed by a
        // shot (or a spent shot) is removed in the same step that the dead are cleared.
        _combat?.Resolve(_entities);

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
