using System;
using System.Numerics;

namespace TankGame.Domain;

/// <summary>The minimal contract every world object satisfies. The <see cref="IWorld"/>
/// owns a collection of these, advances them each tick, and reaps the dead. Pure C# —
/// no Godot. Tanks, projectiles, and (later) mines, drones, and turrets are all entities.</summary>
public interface IEntity
{
    /// <summary>Stable identity for the lifetime of the entity — used to pair a world
    /// entity with its Presentation view and to despawn it.</summary>
    Guid Id { get; }

    /// <summary>Current world-space position.</summary>
    Vector2 Position { get; }

    /// <summary>Which discrete elevation layer the entity occupies (0 = ground, 1 = first plateau, …),
    /// per ADR-0018. Combat and collision act only within a single layer. Defaults to ground so a flat
    /// arena — and every entity/test fake that predates elevation — stays layer 0 with no change; tanks
    /// and projectiles override it (a shot keeps its shooter's layer for its whole flight).</summary>
    int Layer => 0;

    /// <summary>False once the entity has expired; the world removes it on the next
    /// <see cref="Step"/> and raises <see cref="IWorld.EntityDespawned"/>.</summary>
    bool IsAlive { get; }

    /// <summary>Advances the entity by <paramref name="deltaSeconds"/>.</summary>
    void Step(float deltaSeconds);
}
