using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class CombatResolverTests
{
    private sealed class NoInput : IInputSource
    {
        public TankInput Read() => new(Vector2.Zero, Aim: 0f, Fire: false);
    }

    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    private const float HitRadius = 24f;

    private static Tank TankAt(Vector2 pos, int team, int lives = 1) =>
        new(new NoInput(), new World(), new OpenArena(), pos, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 3, team: team, lives: lives);

    private static Projectile ShotAt(Vector2 pos, int team) =>
        new(new OpenArena(), pos, new Vector2(1f, 0f), speed: 600f, damage: 1, team: team);

    private static Projectile ShotFrom(Tank shooter) =>
        new(new OpenArena(), shooter.Position, new Vector2(1f, 0f), speed: 600f, damage: 1,
            team: shooter.Team, owner: shooter.Id);

    private static Projectile PiercingShotAt(Vector2 pos, int team, int pierce) =>
        new(new OpenArena(), pos, new Vector2(1f, 0f), speed: 600f, damage: 1, team: team, pierce: pierce);

    [Fact]
    public void EnemyShot_OnATank_DamagesItAndExpiresTheShot()
    {
        var tank = TankAt(Vector2.Zero, team: 0);
        var shot = ShotAt(Vector2.Zero, team: 1);

        new CombatResolver(HitRadius).Resolve(new List<IEntity> { tank, shot });

        Assert.Equal(tank.MaxHp - 1, tank.Hp);
        Assert.False(shot.IsAlive);
    }

    [Fact]
    public void AShot_NeverHitsItsOwnShooter()
    {
        var tank = TankAt(Vector2.Zero, team: 0);
        var shot = ShotFrom(tank); // owned by the tank, at its muzzle

        new CombatResolver(HitRadius).Resolve(new List<IEntity> { tank, shot });

        Assert.Equal(tank.MaxHp, tank.Hp); // the shooter is spared
        Assert.True(shot.IsAlive);
    }

    [Fact]
    public void SameTeamTanks_DamageEachOther_InAFreeForAll()
    {
        // Default (no allied team): two tanks on the same team still hit each other — so the AI
        // enemies (all on one team) fight one another. The shot sits on the victim, owned by the shooter.
        var shooter = TankAt(new Vector2(200f, 0f), team: 1);
        var victim = TankAt(Vector2.Zero, team: 1);
        var shot = new Projectile(new OpenArena(), victim.Position, new Vector2(1f, 0f),
            speed: 600f, damage: 1, team: shooter.Team, owner: shooter.Id);

        new CombatResolver(HitRadius).Resolve(new List<IEntity> { shooter, victim, shot });

        Assert.Equal(victim.MaxHp - 1, victim.Hp); // a same-team tank is fair game
    }

    [Fact]
    public void AlliedTeam_DoesNotFriendlyFireEachOther()
    {
        // Co-op: the player team is protected, so P1's shot passes through P2.
        var p1 = TankAt(new Vector2(200f, 0f), team: 0);
        var p2 = TankAt(Vector2.Zero, team: 0);
        var shot = new Projectile(new OpenArena(), p2.Position, new Vector2(1f, 0f),
            speed: 600f, damage: 1, team: p1.Team, owner: p1.Id);

        new CombatResolver(HitRadius, alliedTeam: 0).Resolve(new List<IEntity> { p1, p2, shot });

        Assert.Equal(p2.MaxHp, p2.Hp); // ally unharmed
        Assert.True(shot.IsAlive);
    }

    [Fact]
    public void Shot_OutOfRange_Misses()
    {
        var tank = TankAt(Vector2.Zero, team: 0);
        var shot = ShotAt(new Vector2(1000f, 0f), team: 1);

        new CombatResolver(HitRadius).Resolve(new List<IEntity> { tank, shot });

        Assert.Equal(tank.MaxHp, tank.Hp);
        Assert.True(shot.IsAlive);
    }

    [Fact]
    public void Combat_RunsInsideWorldStep_AndReapsTheCasualties()
    {
        var world = new World(new CombatResolver(HitRadius));
        var tank = TankAt(Vector2.Zero, team: 0);
        var shot = ShotAt(Vector2.Zero, team: 1);
        world.Spawn(tank);
        world.Spawn(shot);

        world.Step(0.016f);

        Assert.Equal(tank.MaxHp - 1, tank.Hp); // damaged by the enemy shot
        Assert.DoesNotContain(shot, world.Entities); // spent shot reaped
        Assert.Contains(tank, world.Entities); // still alive at 2 hp
    }

    [Fact]
    public void ThreeEnemyHits_DestroyATank_AndTheWorldReapsIt()
    {
        var world = new World(new CombatResolver(HitRadius));
        var tank = TankAt(Vector2.Zero, team: 0);
        world.Spawn(tank);

        for (var i = 0; i < tank.MaxHp; i++)
        {
            world.Spawn(ShotAt(Vector2.Zero, team: 1));
            world.Step(0.016f);
        }

        Assert.DoesNotContain(tank, world.Entities); // destroyed and reaped
    }

    [Fact]
    public void Hit_CarriesTheShooterAndVictimIdentities()
    {
        // Per-tank stats (BattleStats) need who shot whom, not just the teams.
        var shooter = TankAt(new Vector2(200f, 0f), team: 0);
        var victim = TankAt(Vector2.Zero, team: 1);
        var shot = new Projectile(new OpenArena(), victim.Position, new Vector2(1f, 0f),
            speed: 600f, damage: 1, team: shooter.Team, owner: shooter.Id);
        var resolver = new CombatResolver(HitRadius);
        CombatResolver.CombatHit? captured = null;
        resolver.Hit += hit => captured = hit;

        resolver.Resolve(new List<IEntity> { shooter, victim, shot });

        Assert.NotNull(captured);
        Assert.Equal(shooter.Id, captured!.Value.Shooter);
        Assert.Equal(victim.Id, captured.Value.Victim);
    }

    [Fact]
    public void AFatalHit_RaisesTankKilled_WithTheShootersTeam()
    {
        var tank = TankAt(Vector2.Zero, team: 0);
        tank.TakeDamage(tank.MaxHp - 1); // one hit from death
        var shot = ShotAt(Vector2.Zero, team: 1);
        var resolver = new CombatResolver(HitRadius);
        int? killerTeam = null;
        resolver.TankKilled += team => killerTeam = team;

        resolver.Resolve(new List<IEntity> { tank, shot });

        Assert.Equal(1, killerTeam);
    }

    [Fact]
    public void ANonFatalHit_DoesNotRaiseTankKilled()
    {
        var tank = TankAt(Vector2.Zero, team: 0); // full health, survives one hit
        var shot = ShotAt(Vector2.Zero, team: 1);
        var resolver = new CombatResolver(HitRadius);
        var raised = false;
        resolver.TankKilled += _ => raised = true;

        resolver.Resolve(new List<IEntity> { tank, shot });

        Assert.False(raised);
    }

    [Fact]
    public void TankKilled_FeedsAScoreBoard_CreditingTheKill()
    {
        var board = new ScoreBoard();
        var world = new World(BuildScoringResolver(board));
        var tank = TankAt(Vector2.Zero, team: 0);
        world.Spawn(tank);

        for (var i = 0; i < tank.MaxHp; i++)
        {
            world.Spawn(ShotAt(Vector2.Zero, team: 1));
            world.Step(0.016f);
        }

        Assert.Equal(1, board.KillsFor(1)); // team 1 destroyed exactly one tank
        Assert.Equal(0, board.KillsFor(0));
    }

    private static CombatResolver BuildScoringResolver(ScoreBoard board)
    {
        var resolver = new CombatResolver(HitRadius);
        resolver.TankKilled += board.RecordKill;
        return resolver;
    }

    [Fact]
    public void AFatalHit_CreditsTheKill_EvenWhenTheVictimWillRespawn()
    {
        var tank = TankAt(Vector2.Zero, team: 0, lives: 2);
        tank.TakeDamage(tank.MaxHp - 1); // one hit from death
        var shot = ShotAt(Vector2.Zero, team: 1);
        var resolver = new CombatResolver(HitRadius);
        int? killerTeam = null;
        resolver.TankKilled += team => killerTeam = team;

        resolver.Resolve(new List<IEntity> { tank, shot });

        Assert.Equal(1, killerTeam);
        Assert.Equal(0, tank.Hp);  // downed
        Assert.True(tank.IsAlive);  // but still in the match, awaiting respawn
    }

    [Fact]
    public void AHit_RaisesTheHitEvent_WithShooterVictimAmountAndKillFlag()
    {
        var tank = TankAt(Vector2.Zero, team: 0); // full health → survives this hit
        var shot = ShotAt(Vector2.Zero, team: 1);
        var resolver = new CombatResolver(HitRadius);
        CombatResolver.CombatHit? captured = null;
        resolver.Hit += hit => captured = hit;

        resolver.Resolve(new List<IEntity> { tank, shot });

        Assert.NotNull(captured);
        Assert.Equal(1, captured!.Value.ShooterTeam);
        Assert.Equal(0, captured.Value.VictimTeam);
        Assert.Equal(1, captured.Value.Amount);
        Assert.False(captured.Value.Killed); // not the killing blow
    }

    [Fact]
    public void AFatalHit_RaisesTheHitEvent_WithKilledTrue()
    {
        var tank = TankAt(Vector2.Zero, team: 0);
        tank.TakeDamage(tank.MaxHp - 1); // one hit from death
        var shot = ShotAt(Vector2.Zero, team: 1);
        var resolver = new CombatResolver(HitRadius);
        CombatResolver.CombatHit? captured = null;
        resolver.Hit += hit => captured = hit;

        resolver.Resolve(new List<IEntity> { tank, shot });

        Assert.True(captured!.Value.Killed);
    }

    [Fact]
    public void APiercingShot_DamagesTwoTanksInRange_ThenStops()
    {
        var a = TankAt(Vector2.Zero, team: 0);
        var b = TankAt(new Vector2(10f, 0f), team: 0); // also within the hit radius
        var shot = PiercingShotAt(Vector2.Zero, team: 1, pierce: 1);

        new CombatResolver(HitRadius).Resolve(new List<IEntity> { shot, a, b });

        Assert.Equal(a.MaxHp - 1, a.Hp); // pierced through the first
        Assert.Equal(b.MaxHp - 1, b.Hp); // and damaged the second
        Assert.False(shot.IsAlive);      // which stopped it (budget spent)
    }

    [Fact]
    public void APiercingShot_DoesNotReHitTheSameTank_AndLingersUntilItMeetsAnother()
    {
        var tank = TankAt(Vector2.Zero, team: 0);
        var shot = PiercingShotAt(Vector2.Zero, team: 1, pierce: 1);
        var resolver = new CombatResolver(HitRadius);

        resolver.Resolve(new List<IEntity> { shot, tank });
        resolver.Resolve(new List<IEntity> { shot, tank }); // still overlapping next tick

        Assert.Equal(tank.MaxHp - 1, tank.Hp); // damaged exactly once, not once per tick
        Assert.True(shot.IsAlive);             // passed through, still flying for a second target
    }

    [Fact]
    public void AnOrdinaryShot_StillStopsOnItsFirstTank()
    {
        var a = TankAt(Vector2.Zero, team: 0);
        var b = TankAt(new Vector2(10f, 0f), team: 0);
        var shot = ShotAt(Vector2.Zero, team: 1); // no pierce budget

        new CombatResolver(HitRadius).Resolve(new List<IEntity> { shot, a, b });

        Assert.Equal(a.MaxHp - 1, a.Hp); // hit the first
        Assert.Equal(b.MaxHp, b.Hp);     // the second is untouched
        Assert.False(shot.IsAlive);
    }

    [Fact]
    public void AShot_OnlyHitsTanksOnItsOwnLayer()
    {
        var ground = TankAt(Vector2.Zero, team: 0); // layer 0 (default)
        var raised = new Tank(new NoInput(), new World(), new OpenArena(), Vector2.Zero,
            speed: 100f, fireInterval: 0.3f, projectileSpeed: 600f, maxHp: 3, team: 0, layer: 1);
        var groundShot = ShotAt(Vector2.Zero, team: 1); // a layer-0 shot, overlapping both tanks

        new CombatResolver(HitRadius).Resolve(new List<IEntity> { ground, raised, groundShot });

        Assert.Equal(ground.MaxHp - 1, ground.Hp); // the tank on the shot's layer is hit
        Assert.Equal(raised.MaxHp, raised.Hp);      // the tank a layer up is untouched
        Assert.False(groundShot.IsAlive);           // spent on the same-layer tank
    }

    [Fact]
    public void ADownedTank_AwaitingRespawn_IsIntangibleToShots()
    {
        var tank = TankAt(Vector2.Zero, team: 0, lives: 2);
        tank.TakeDamage(tank.MaxHp); // downed, a life remains
        var shot = ShotAt(Vector2.Zero, team: 1);

        new CombatResolver(HitRadius).Resolve(new List<IEntity> { tank, shot });

        Assert.True(shot.IsAlive); // passes through the downed tank
        Assert.Equal(0, tank.Hp);
    }
}
