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
    public void FriendlyShot_PassesThroughItsOwnTeam()
    {
        var tank = TankAt(Vector2.Zero, team: 0);
        var shot = ShotAt(Vector2.Zero, team: 0);

        new CombatResolver(HitRadius).Resolve(new List<IEntity> { tank, shot });

        Assert.Equal(tank.MaxHp, tank.Hp);
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
