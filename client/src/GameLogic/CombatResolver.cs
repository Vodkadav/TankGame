using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The combat pass: every live shot that overlaps a tank damages it and is spent. A shot
/// never hits its own shooter, and tanks on the <see cref="_alliedTeam"/> (the player team) do not
/// hit each other — but every other team is free-for-all, so the AI tanks fight one another as well
/// as the player. Pure C# — deterministic, no Godot, no networking — so it runs identically on a
/// client and (later) on the authoritative server.</summary>
public sealed class CombatResolver : ICombatResolver
{
    /// <summary>A team value no tank uses — the default "no allied team", i.e. a full free-for-all.</summary>
    public const int NoAllies = -1;

    private readonly float _hitRadius;
    private readonly int _alliedTeam;

    /// <param name="hitRadius">World distance within which a shot counts as hitting a tank.</param>
    /// <param name="alliedTeam">A team whose members never hit each other (the local player team in
    /// co-op). Every other team is free-for-all. Defaults to <see cref="NoAllies"/>.</param>
    public CombatResolver(float hitRadius, int alliedTeam = NoAllies)
    {
        _hitRadius = hitRadius;
        _alliedTeam = alliedTeam;
    }

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
                if (tank.Id == shot.Owner || tank.Hp <= 0 || shot.HasHit(tank.Id))
                {
                    continue; // never hit the shooter; skip the downed and the already-pierced
                }

                if (tank.Layer != shot.Layer)
                {
                    continue; // elevated zones (ADR-0018): a shot only hits tanks on its own layer
                }

                if (tank.Team == shot.Team && tank.Team == _alliedTeam)
                {
                    continue; // allies on the player team do not friendly-fire each other
                }

                if (Vector2.Distance(shot.Position, tank.Position) <= _hitRadius)
                {
                    tank.TakeDamage(shot.Damage);
                    var killed = tank.Hp <= 0;
                    Hit?.Invoke(new CombatHit(shot.Team, tank.Team, shot.Damage, killed));
                    if (killed)
                    {
                        TankKilled?.Invoke(shot.Team); // credit the kill to the shooter's team
                    }

                    // Spends a pierce, or stops the shot if none is left. An ordinary shot (no
                    // budget) stops on its first tank — the prior behaviour; a piercing shot passes
                    // through and may strike a second tank in range this same step.
                    shot.RegisterTankHit(tank.Id);
                    if (!shot.IsAlive)
                    {
                        break;
                    }
                }
            }
        }
    }
}
