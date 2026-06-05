using System.Linq;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class AirstrikeTests
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

    private static Tank TankAt(IWorld world, Vector2 pos, int team) =>
        new(new NoInput(), world, new OpenArena(), pos, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 3, team: team);

    [Fact]
    public void Airstrike_DetonatesAfterItsDelay_DamagingEnemiesInTheBlast()
    {
        var world = new World();
        var enemy = TankAt(world, new Vector2(40f, 0f), team: 0);
        world.Spawn(enemy);
        var strike = new Airstrike(world, new Vector2(40f, 0f), callerTeam: 1, delay: 0.5f, radius: 100f, damage: 2);
        world.Spawn(strike);

        world.Step(0.3f); // still telegraphing
        Assert.Equal(enemy.MaxHp, enemy.Hp);
        Assert.Contains(strike, world.Entities);

        world.Step(0.3f); // delay elapsed → detonates
        Assert.Equal(enemy.MaxHp - 2, enemy.Hp);
        Assert.DoesNotContain(strike, world.Entities); // spent and reaped
    }

    [Fact]
    public void Airstrike_SparesTheCallersOwnTeam()
    {
        var world = new World();
        var ally = TankAt(world, Vector2.Zero, team: 1);
        world.Spawn(ally);
        world.Spawn(new Airstrike(world, Vector2.Zero, callerTeam: 1, delay: 0.1f, radius: 100f, damage: 2));

        world.Step(0.2f);

        Assert.Equal(ally.MaxHp, ally.Hp); // friendly fire spared
    }

    [Fact]
    public void Airstrike_MissesTanksOutsideTheRadius()
    {
        var world = new World();
        var enemy = TankAt(world, new Vector2(500f, 0f), team: 0);
        world.Spawn(enemy);
        world.Spawn(new Airstrike(world, Vector2.Zero, callerTeam: 1, delay: 0.1f, radius: 100f, damage: 2));

        world.Step(0.2f);

        Assert.Equal(enemy.MaxHp, enemy.Hp);
    }

    [Fact]
    public void Telephone_CallsAnAirstrike_OnTheNearestFoe()
    {
        var world = new World();
        var caller = TankAt(world, Vector2.Zero, team: 1);
        var foe = TankAt(world, new Vector2(200f, 0f), team: 0);
        world.Spawn(caller);
        world.Spawn(foe);

        new AirstrikePickup(delay: 1f, radius: 100f, damage: 3).ApplyTo(caller, world);

        var strike = Assert.Single(world.Entities.OfType<Airstrike>());
        Assert.Equal(foe.Position, strike.Position); // landed on the foe
    }

    [Fact]
    public void Telephone_DoesNothing_WhenThereIsNoFoe()
    {
        var world = new World();
        var caller = TankAt(world, Vector2.Zero, team: 1);
        world.Spawn(caller);

        new AirstrikePickup(delay: 1f, radius: 100f, damage: 3).ApplyTo(caller, world);

        Assert.Empty(world.Entities.OfType<Airstrike>());
    }
}
