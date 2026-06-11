using System;
using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Per-tank combat bookkeeping for the end-of-match screen (owner feedback 2026-06-11):
/// shots fired, hits, misses, kills, deaths, damage dealt and taken. Subscribes once to the world
/// (projectile spawns = shots, projectile despawns with no tank hit = misses, tank spawns register
/// the tally) and the combat resolver (hits, damage, kills, deaths) — the game loop needs no extra
/// calls. Counted per projectile, so a 3-pellet spread is 3 shots and accuracy stays meaningful.
/// Deaths count lethal shot hits only; an airstrike kill is currently untallied. Pure C#.</summary>
public sealed class BattleStats
{
    /// <summary>One tank's running tally. Mutable — the screen reads it once at match end.</summary>
    public sealed class TankTally
    {
        public string Name { get; internal set; } = string.Empty;
        public int Team { get; internal set; }
        public int ShotsFired { get; internal set; }
        public int Hits { get; internal set; }
        public int Misses { get; internal set; }
        public int Kills { get; internal set; }
        public int Deaths { get; internal set; }
        public int DamageDealt { get; internal set; }
        public int DamageTaken { get; internal set; }
    }

    private readonly Dictionary<Guid, TankTally> _byTank = new();
    private readonly List<TankTally> _ordered = new(); // registration order = the scene's spawn order

    public BattleStats(IWorld world, CombatResolver combat)
    {
        world.EntitySpawned += OnSpawned;
        world.EntityDespawned += OnDespawned;
        combat.Hit += OnHit;
    }

    /// <summary>Every registered tank's tally, in spawn order (the player first in the play scenes).</summary>
    public IReadOnlyList<TankTally> Tallies => _ordered;

    /// <summary>The tally for one tank (created on first sight, so it is never null).</summary>
    public TankTally For(Guid tankId) => GetOrCreate(tankId);

    private void OnSpawned(IEntity entity)
    {
        switch (entity)
        {
            case ITank tank:
                var tally = GetOrCreate(tank.Id);
                tally.Name = tank.DisplayName;
                tally.Team = tank.Team;
                break;
            case Projectile shot:
                GetOrCreate(shot.Owner).ShotsFired++;
                break;
        }
    }

    private void OnDespawned(IEntity entity)
    {
        if (entity is Projectile { TanksHit: 0 } shot)
        {
            GetOrCreate(shot.Owner).Misses++; // died on a wall or the border without touching anyone
        }
    }

    private void OnHit(CombatResolver.CombatHit hit)
    {
        var dealt = GetOrCreate(hit.Shooter);
        dealt.Hits++;
        dealt.DamageDealt += hit.Amount;

        var taken = GetOrCreate(hit.Victim);
        taken.DamageTaken += hit.Amount;

        if (hit.Killed)
        {
            dealt.Kills++;
            taken.Deaths++;
        }
    }

    private TankTally GetOrCreate(Guid id)
    {
        if (!_byTank.TryGetValue(id, out var tally))
        {
            tally = new TankTally();
            _byTank[id] = tally;
            _ordered.Add(tally);
        }

        return tally;
    }
}
