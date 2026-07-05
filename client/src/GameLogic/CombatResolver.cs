using System;
using System.Collections.Generic;
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
    /// whether it was the killing blow. Feeds the per-team damage / kill-death meters (S9) and the
    /// per-tank <see cref="BattleStats"/> — <see cref="Shooter"/>/<see cref="Victim"/> carry the
    /// tank identities the team fields cannot.</summary>
    public readonly record struct CombatHit(
        int ShooterTeam, int VictimTeam, int Amount, bool Killed,
        Guid Shooter = default, Guid Victim = default);

    /// <summary>Raised once per shot that lands on an enemy tank, with the full hit detail.</summary>
    public event Action<CombatHit>? Hit;

    // Reused across steps so the per-frame combat pass allocates nothing — GC hitches on the
    // single-threaded WASM build both lag the game and (via a huge delta) feed the tunnelling
    // this resolver guards against.
    private readonly List<Tank> _targets = new();
    private readonly List<(float Along, Tank Tank)> _crossed = new();
    private static readonly Comparison<(float Along, Tank Tank)> ByPathOrder =
        (a, b) => a.Along.CompareTo(b.Along);

    public void Resolve(IReadOnlyList<IEntity> entities)
    {
        // Only tangible tanks (positive hit points) are targets; a downed tank awaiting respawn
        // has Hp 0 but is still "alive" in the match sense, so it is skipped here.
        _targets.Clear();
        for (var i = 0; i < entities.Count; i++)
        {
            if (entities[i] is Tank { Hp: > 0 } tank)
            {
                _targets.Add(tank);
            }
        }

        if (_targets.Count == 0)
        {
            return;
        }

        for (var i = 0; i < entities.Count; i++)
        {
            if (entities[i] is Projectile { IsAlive: true } shot)
            {
                ResolveShot(shot);
            }
        }
    }

    private void ResolveShot(Projectile shot)
    {
        _crossed.Clear();
        foreach (var tank in _targets)
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

            // Sweep the segment the shot travelled this step, not just its end position — a
            // single laggy step can jump far past a tank, and a point sample would miss it
            // (walls are already swept by the behaviour's raycast; tanks must match).
            if (PathOverlap(shot.PreviousPosition, shot.Position, tank.Position) is { } along)
            {
                _crossed.Add((along, tank));
            }
        }

        // Nearest-first along the travel path, so a big step crossing two tanks spends the shot
        // on the one it really reached first — entity insertion order must not decide combat.
        _crossed.Sort(ByPathOrder);
        foreach (var (_, tank) in _crossed)
        {
            tank.TakeDamage(shot.Damage);
            var killed = tank.Hp <= 0;
            Hit?.Invoke(new CombatHit(shot.Team, tank.Team, shot.Damage, killed, shot.Owner, tank.Id));
            if (killed)
            {
                TankKilled?.Invoke(shot.Team); // credit the kill to the shooter's team
            }

            // Spends a pierce, or stops the shot if none is left. An ordinary shot (no
            // budget) stops on its first tank — the prior behaviour; a piercing shot passes
            // through and may strike a second tank along this same step.
            shot.RegisterTankHit(tank.Id);
            if (!shot.IsAlive)
            {
                break;
            }
        }
    }

    /// <summary>Segment-vs-circle: how far along the step's travel segment the shot first comes
    /// within the hit radius of <paramref name="centre"/> (0..1), or null if it never does. A
    /// zero-length segment (a shot resolved before its first step) degenerates to the old point
    /// test.</summary>
    private float? PathOverlap(Vector2 from, Vector2 to, Vector2 centre)
    {
        var path = to - from;
        var lengthSquared = path.LengthSquared();
        var along = lengthSquared > 0f
            ? Math.Clamp(Vector2.Dot(centre - from, path) / lengthSquared, 0f, 1f)
            : 0f;
        var closest = from + (path * along);
        return Vector2.DistanceSquared(closest, centre) <= _hitRadius * _hitRadius ? along : null;
    }
}
