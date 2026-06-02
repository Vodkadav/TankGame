using System;
using System.Collections.Generic;

namespace TankGame.Domain;

/// <summary>Owns the live <see cref="IEntity"/> collection, advances every entity each
/// tick, and reaps the dead. Spawns and deaths are events the Presentation layer
/// subscribes to once, so a new kind of entity appears on screen without the play scene
/// learning about it. Pure C# — no Godot, no networking. This is the client-prediction
/// container; the authoritative server simulation is a separate concern (see
/// <c>docs/adr/PROPOSAL-entity-spine.md</c> §8).</summary>
public interface IWorld
{
    /// <summary>The currently live entities, in spawn order.</summary>
    IReadOnlyCollection<IEntity> Entities { get; }

    /// <summary>Adds <paramref name="entity"/> to the world and raises
    /// <see cref="EntitySpawned"/> synchronously.</summary>
    void Spawn(IEntity entity);

    /// <summary>Raised when an entity is spawned. Presentation maps this to a view.</summary>
    event Action<IEntity> EntitySpawned;

    /// <summary>Raised when an entity is reaped during <see cref="Step"/>. Presentation
    /// frees its view.</summary>
    event Action<IEntity> EntityDespawned;

    /// <summary>Advances every live entity by <paramref name="deltaSeconds"/>, then
    /// removes any whose <see cref="IEntity.IsAlive"/> is false, raising
    /// <see cref="EntityDespawned"/> for each. The single tick owner for the simulation.</summary>
    void Step(float deltaSeconds);
}
