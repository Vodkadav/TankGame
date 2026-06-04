using System;
using System.Collections.Generic;
using System.Numerics;

namespace TankGame.GameLogic;

/// <summary>The mutable per-shot state an <see cref="IProjectileBehaviour"/> reads and drives
/// each tick: where the shot is, where it is heading, and the data it carries. A plain state bag
/// (public fields) so a behaviour can advance, reflect, or expire it without ceremony. The
/// owning <see cref="Projectile"/> exposes the read-only slices the rest of the game needs.</summary>
public sealed class ProjectileState
{
    /// <summary>World-space position.</summary>
    public Vector2 Position;

    /// <summary>Unit travel direction.</summary>
    public Vector2 Direction;

    /// <summary>Travel speed in units per second.</summary>
    public float Speed;

    /// <summary>Damage dealt to a wall or tank on impact.</summary>
    public int Damage;

    /// <summary>The firing tank's team (combat spares the same team).</summary>
    public int Team;

    /// <summary>False once the shot is spent; the world reaps it.</summary>
    public bool IsAlive = true;

    /// <summary>How many more targets this shot can pass through before it stops. 0 (default) is an
    /// ordinary shot that spends itself on the first target. A piercing shot starts above 0; both
    /// the wall pass (<see cref="PiercingBehaviour"/>) and the tank pass (the combat resolver)
    /// decrement this shared budget, so "pierce one target, the next stops it" holds across both.</summary>
    public int Pierce;

    /// <summary>Tanks this shot has already damaged — so a piercing shot that overlaps the same
    /// tank across several ticks hits it once, not once per tick.</summary>
    public readonly HashSet<Guid> HitTanks = new();
}
