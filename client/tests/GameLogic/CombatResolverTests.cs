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

    private static Tank TankAt(Vector2 pos, int team) =>
        new(new NoInput(), new World(), new OpenArena(), pos, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 3, team: team);

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
}
