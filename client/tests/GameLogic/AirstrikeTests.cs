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

    // One zone: it arms immediately (arm time 0) but does not detonate until armWindow + delay = 1.0s.
    [Fact]
    public void Airstrike_DetonatesAZone_DamagingEnemiesInItsBlast()
    {
        var world = new World();
        var enemy = TankAt(world, new Vector2(40f, 0f), team: 0);
        world.Spawn(enemy);
        var strike = new Airstrike(world, new[] { new Vector2(40f, 0f) }, callerTeam: 1, zoneRadius: 100f,
            armWindow: 0.5f, delay: 0.5f, damage: 2);
        world.Spawn(strike);

        world.Step(0.3f); // still telegraphing / waiting out the delay
        Assert.Equal(enemy.MaxHp, enemy.Hp);
        Assert.Contains(strike, world.Entities);

        world.Step(0.8f); // 1.1 ≥ 1.0 → detonates
        Assert.Equal(enemy.MaxHp - 2, enemy.Hp);
        Assert.DoesNotContain(strike, world.Entities); // all zones spent → reaped
    }

    [Fact]
    public void Airstrike_SparesTheCallersOwnTeam()
    {
        var world = new World();
        var ally = TankAt(world, Vector2.Zero, team: 1);
        world.Spawn(ally);
        world.Spawn(new Airstrike(world, new[] { Vector2.Zero }, callerTeam: 1, zoneRadius: 100f,
            armWindow: 0.1f, delay: 0.1f, damage: 2));

        world.Step(0.3f);

        Assert.Equal(ally.MaxHp, ally.Hp);
    }

    [Fact]
    public void Airstrike_MissesTanksOutsideAZoneRadius()
    {
        var world = new World();
        var enemy = TankAt(world, new Vector2(500f, 0f), team: 0);
        world.Spawn(enemy);
        world.Spawn(new Airstrike(world, new[] { Vector2.Zero }, callerTeam: 1, zoneRadius: 100f,
            armWindow: 0.1f, delay: 0.1f, damage: 2));

        world.Step(0.3f);

        Assert.Equal(enemy.MaxHp, enemy.Hp);
    }

    // Telegraph-first, then sweep: with a 0.5s arm window and 0.5s delay, zone0 booms at
    // armWindow + delay = 1.0, and zone1 (last of two, arms at 0.5) booms at 1.5 — same order.
    [Fact]
    public void Airstrike_DetonatesZonesInOrder_Staggered()
    {
        var world = new World();
        var first = TankAt(world, Vector2.Zero, team: 0);
        var second = TankAt(world, new Vector2(1000f, 0f), team: 0);
        world.Spawn(first);
        world.Spawn(second);
        var zones = new[] { Vector2.Zero, new Vector2(1000f, 0f) };
        world.Spawn(new Airstrike(world, zones, callerTeam: 1, zoneRadius: 100f,
            armWindow: 0.5f, delay: 0.5f, damage: 2));

        world.Step(1.1f); // zone0 has gone (≥1.0), zone1 not yet (<1.5)
        Assert.Equal(first.MaxHp - 2, first.Hp);
        Assert.Equal(second.MaxHp, second.Hp);

        world.Step(0.5f); // zone1 detonates (1.6 ≥ 1.5)
        Assert.Equal(second.MaxHp - 2, second.Hp);
    }

    [Fact]
    public void Airstrike_ArmsZonesInAnExpandingSweep_BeforeDetonating()
    {
        var world = new World();
        var strike = new Airstrike(world, new[] { Vector2.Zero, new Vector2(100f, 0f) },
            callerTeam: 1, zoneRadius: 80f, armWindow: 0.5f, delay: 0.5f, damage: 1);

        var zones = strike.Zones; // before any step: zone0 lights at 0, zone1 lights later (0.5)
        Assert.Equal(AirstrikeZonePhase.Armed, zones[0].Phase);
        Assert.Equal(AirstrikeZonePhase.Pending, zones[1].Phase);
    }

    [Fact]
    public void Telephone_DropsASmallRandomizedBlob_NotTheWholeField()
    {
        var world = new World();
        var caller = TankAt(world, Vector2.Zero, team: 1);
        var foe = TankAt(world, new Vector2(300f, 300f), team: 0);
        world.Spawn(caller);
        world.Spawn(foe);

        new AirstrikePickup(Vector2.Zero, new Vector2(640f, 640f), zoneRadius: 80f,
            armWindow: 3f, delay: 3f, damage: 3)
            .ApplyTo(caller, world);

        var strike = Assert.Single(world.Entities.OfType<Airstrike>());
        // A ~6-cell blob (5–7 with jitter) — a small clump, ~20% of the former ~30-cell strike.
        Assert.InRange(strike.Zones.Count, 5, 7);
    }

    // Telegraph-first timing: the whole blob lights (arms) before ANYTHING detonates, then the zones
    // detonate in growth order. With a 0.6s arm window and 0.2s delay, the last zone arms at t=0.6 and
    // the first boom lands at armWindow + delay = 0.8 (zones then at 0.8, 1.1, 1.4).
    [Fact]
    public void Airstrike_TelegraphsEveryZone_BeforeAnyDetonation_ThenSweepsInOrder()
    {
        var world = new World();
        var a = TankAt(world, Vector2.Zero, team: 0);
        var b = TankAt(world, new Vector2(1000f, 0f), team: 0);
        var c = TankAt(world, new Vector2(2000f, 0f), team: 0);
        world.Spawn(a);
        world.Spawn(b);
        world.Spawn(c);
        var zones = new[] { Vector2.Zero, new Vector2(1000f, 0f), new Vector2(2000f, 0f) };
        var strike = new Airstrike(world, zones, callerTeam: 1, zoneRadius: 100f,
            armWindow: 0.6f, delay: 0.2f, damage: 1);
        world.Spawn(strike);

        // (a) just under the arm window: nothing has detonated yet (first boom is at 0.8).
        world.Step(0.59f);
        Assert.All(strike.Zones, z => Assert.NotEqual(AirstrikeZonePhase.Detonated, z.Phase));
        Assert.Equal(a.MaxHp, a.Hp);

        // (b) by t = armWindow every zone has armed (telegraph complete), still none detonated.
        world.Step(0.02f); // 0.61
        Assert.All(strike.Zones, z => Assert.Equal(AirstrikeZonePhase.Armed, z.Phase));

        // (c) detonations sweep in growth order: zone0 first (0.8), then zone1 (1.1), then zone2 (1.4).
        world.Step(0.24f); // 0.85
        Assert.Equal(a.MaxHp - 1, a.Hp);
        Assert.Equal(b.MaxHp, b.Hp);
        world.Step(0.3f); // 1.15
        Assert.Equal(b.MaxHp - 1, b.Hp);
        Assert.Equal(c.MaxHp, c.Hp);
        world.Step(0.3f); // 1.45
        Assert.Equal(c.MaxHp - 1, c.Hp);

        // (d) all zones spent → the strike is no longer alive.
        Assert.False(strike.IsAlive);
    }
}
