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

    // One zone: it detonates at (count·step) + 0·step = 1·0.5 = 0.5s.
    [Fact]
    public void Airstrike_DetonatesAZone_DamagingEnemiesInItsBlast()
    {
        var world = new World();
        var enemy = TankAt(world, new Vector2(40f, 0f), team: 0);
        world.Spawn(enemy);
        var strike = new Airstrike(world, new[] { new Vector2(40f, 0f) }, callerTeam: 1, zoneRadius: 100f, step: 0.5f, damage: 2);
        world.Spawn(strike);

        world.Step(0.3f); // still arming
        Assert.Equal(enemy.MaxHp, enemy.Hp);
        Assert.Contains(strike, world.Entities);

        world.Step(0.3f); // 0.6 ≥ 0.5 → detonates
        Assert.Equal(enemy.MaxHp - 2, enemy.Hp);
        Assert.DoesNotContain(strike, world.Entities); // all zones spent → reaped
    }

    [Fact]
    public void Airstrike_SparesTheCallersOwnTeam()
    {
        var world = new World();
        var ally = TankAt(world, Vector2.Zero, team: 1);
        world.Spawn(ally);
        world.Spawn(new Airstrike(world, new[] { Vector2.Zero }, callerTeam: 1, zoneRadius: 100f, step: 0.1f, damage: 2));

        world.Step(0.3f);

        Assert.Equal(ally.MaxHp, ally.Hp);
    }

    [Fact]
    public void Airstrike_MissesTanksOutsideAZoneRadius()
    {
        var world = new World();
        var enemy = TankAt(world, new Vector2(500f, 0f), team: 0);
        world.Spawn(enemy);
        world.Spawn(new Airstrike(world, new[] { Vector2.Zero }, callerTeam: 1, zoneRadius: 100f, step: 0.1f, damage: 2));

        world.Step(0.3f);

        Assert.Equal(enemy.MaxHp, enemy.Hp);
    }

    // Two zones detonate in order: zone0 at 2·0.5=1.0s, zone1 at 1.0+0.5=1.5s.
    [Fact]
    public void Airstrike_DetonatesZonesInOrder_Staggered()
    {
        var world = new World();
        var first = TankAt(world, Vector2.Zero, team: 0);
        var second = TankAt(world, new Vector2(1000f, 0f), team: 0);
        world.Spawn(first);
        world.Spawn(second);
        var zones = new[] { Vector2.Zero, new Vector2(1000f, 0f) };
        world.Spawn(new Airstrike(world, zones, callerTeam: 1, zoneRadius: 100f, step: 0.5f, damage: 2));

        world.Step(1.1f); // zone0 has gone (≥1.0), zone1 not yet (<1.5)
        Assert.Equal(first.MaxHp - 2, first.Hp);
        Assert.Equal(second.MaxHp, second.Hp);

        world.Step(0.5f); // zone1 detonates (1.6 ≥ 1.5)
        Assert.Equal(second.MaxHp - 2, second.Hp);
    }

    [Fact]
    public void Airstrike_ArmsZonesBeforeDetonating_ForTheTelegraph()
    {
        var world = new World();
        var strike = new Airstrike(world, new[] { Vector2.Zero, new Vector2(100f, 0f) },
            callerTeam: 1, zoneRadius: 80f, step: 0.5f, damage: 1);

        var zones = strike.Zones; // before any step: zone0 armed (arm time 0), zone1 still pending (0.5)
        Assert.Equal(AirstrikeZonePhase.Armed, zones[0].Phase);
        Assert.Equal(AirstrikeZonePhase.Pending, zones[1].Phase);
    }

    [Fact]
    public void Telephone_CarpetBombsAConnectedSwatheOfTheField()
    {
        var world = new World();
        var caller = TankAt(world, Vector2.Zero, team: 1);
        var foe = TankAt(world, new Vector2(300f, 300f), team: 0);
        world.Spawn(caller);
        world.Spawn(foe);

        new AirstrikePickup(Vector2.Zero, new Vector2(640f, 640f), zoneRadius: 80f, step: 0.4f, damage: 3)
            .ApplyTo(caller, world);

        var strike = Assert.Single(world.Entities.OfType<Airstrike>());
        Assert.True(strike.Zones.Count > 5, $"the carpet bomb should cover many zones; was {strike.Zones.Count}");
    }
}
