using System;
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

    /// <summary>Raised with the shooting tank's <see cref="Tank.Team"/> each time a shot's damage
    /// destroys a tank — once per death, since the dead are skipped thereafter. A
    /// <see cref="ScoreBoard"/> subscribes to tally per-team kills.</summary>
    public event Action<int>? TankKilled;

    /// <summary>A shot landing on an enemy tank: who shot, who was hit, the damage value, and
    /// whether it was the killing blow. Feeds the per-team damage / kill-death meters (S9), which
    /// need more than the kill credit <see cref="TankKilled"/> carries.</summary>
    public readonly record struct CombatHit(int ShooterTeam, int VictimTeam, int Amount, bool Killed);

    /// <summary>Raised once per shot that lands on an enemy tank, with the full hit detail.</summary>
    public event Action<CombatHit>? Hit;

    public void Resolve(IReadOnlyCollection<IEntity> entities)
    {
        // Only tangible tanks (positive hit points) are targets; a downed tank awaiting respawn
        // has Hp 0 but is still "alive" in the match sense, so it is skipped here.
        var tanks = entities.OfType<Tank>().Where(t => t.Hp > 0).ToList();
        if (tanks.Count == 0)
        {
            return;
        }

        foreach (var shot in entities.OfType<Projectile>().Where(p => p.IsAlive))
        {
            foreach (var tank in tanks)
            {
                if (tank.Team == shot.Team || tank.Hp <= 0)
                {
                    continue; // friendly fire passes through; skip the already-downed
                }

                if (Vector2.Distance(shot.Position, tank.Position) <= _hitRadius)
                {
                    tank.TakeDamage(shot.Damage);
                    shot.Expire();
                    var killed = tank.Hp <= 0;
                    Hit?.Invoke(new CombatHit(shot.Team, tank.Team, shot.Damage, killed));
                    if (killed)
                    {
                        TankKilled?.Invoke(shot.Team); // credit the kill to the shooter's team
                    }

                    break; // a shot lands on at most one tank
                }
            }
        }
    }
}
