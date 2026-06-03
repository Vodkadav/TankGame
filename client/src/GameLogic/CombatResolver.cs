using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The combat pass: every live shot that overlaps an enemy tank (different
/// <see cref="Tank.Team"/>) damages it and is spent. A shot passes harmlessly through its own
/// team, so a tank never shoots itself or an ally. Pure C# — deterministic, no Godot, no
/// networking — so it runs identically on a client and (later) on the authoritative
/// server.</summary>
public sealed class CombatResolver : ICombatResolver
{
    private readonly float _hitRadius;

    /// <param name="hitRadius">World distance within which a shot counts as hitting a tank.</param>
    public CombatResolver(float hitRadius) => _hitRadius = hitRadius;

    public void Resolve(IReadOnlyCollection<IEntity> entities)
    {
        var tanks = entities.OfType<Tank>().Where(t => t.IsAlive).ToList();
        if (tanks.Count == 0)
        {
            return;
        }

        foreach (var shot in entities.OfType<Projectile>().Where(p => p.IsAlive))
        {
            foreach (var tank in tanks)
            {
                if (tank.Team == shot.Team || !tank.IsAlive)
                {
                    continue; // friendly fire passes through; skip the already-dead
                }

                if (Vector2.Distance(shot.Position, tank.Position) <= _hitRadius)
                {
                    tank.TakeDamage(shot.Damage);
                    shot.Expire();
                    break; // a shot lands on at most one tank
                }
            }
        }
    }
}
