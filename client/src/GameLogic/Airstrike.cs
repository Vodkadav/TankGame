using System;
using System.Linq;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>An airstrike on the field (called in by a telephone pickup). It sits at its target for a
/// short telegraph delay — long enough for tanks to scramble clear — then detonates once, damaging
/// every tank within <see cref="Radius"/> that is not on the caller's team (so it strikes the caller's
/// foes, not their allies or themselves), and expires. Pure C# — the Presentation layer draws the
/// warning marker.</summary>
public sealed class Airstrike : IAirstrike
{
    private readonly IWorld _world;
    private readonly int _callerTeam;
    private readonly int _damage;
    private float _delay;

    /// <param name="world">The world whose tanks the blast scans.</param>
    /// <param name="target">Where the strike lands.</param>
    /// <param name="callerTeam">The calling tank's team — its side is spared.</param>
    /// <param name="delay">Telegraph time before it detonates, seconds.</param>
    /// <param name="radius">Blast radius, world units.</param>
    /// <param name="damage">Damage dealt to each tank caught in the blast.</param>
    public Airstrike(IWorld world, Vector2 target, int callerTeam, float delay, float radius, int damage)
    {
        Id = Guid.NewGuid();
        _world = world;
        Position = target;
        _callerTeam = callerTeam;
        _delay = delay;
        Radius = radius;
        _damage = damage;
        IsAlive = true;
    }

    public Guid Id { get; }
    public Vector2 Position { get; }
    public float Radius { get; }
    public bool IsAlive { get; private set; }

    public void Step(float deltaSeconds)
    {
        if (!IsAlive)
        {
            return;
        }

        _delay -= deltaSeconds;
        if (_delay > 0f)
        {
            return; // still telegraphing — tanks can scramble out of the blast
        }

        foreach (var tank in _world.Entities.OfType<Tank>())
        {
            if (tank.Hp > 0 && tank.Team != _callerTeam
                && Vector2.DistanceSquared(Position, tank.Position) <= Radius * Radius)
            {
                tank.TakeDamage(_damage);
            }
        }

        IsAlive = false;
    }
}
