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

    // One zone: it arms immediately (arm time 0) and detonates after the delay (0.5s).
    [Fact]
    public void Airstrike_DetonatesAZone_DamagingEnemiesInItsBlast()
    {
        var world = new World();
        var enemy = TankAt(world, new Vector2(40f, 0f), team: 0);
        world.Spawn(enemy);
        var strike = new Airstrike(world, new[] { new Vector2(40f, 0f) }, callerTeam: 1, zoneRadius: 100f,
            armWindow: 0.5f, delay: 0.5f, damage: 2);
        world.Spawn(strike);

        world.Step(0.3f); // still in the highlight delay
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

    // Two zones over a 0.5s arm window with a 0.5s delay: zone0 lights at 0 → booms at 0.5;
    // zone1 lights at 0.5 (the last of two) → booms at 1.0. The blasts sweep in the same order.
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

        world.Step(0.6f); // zone0 has gone (≥0.5), zone1 not yet (<1.0)
        Assert.Equal(first.MaxHp - 2, first.Hp);
        Assert.Equal(second.MaxHp, second.Hp);

        world.Step(0.5f); // zone1 detonates (1.1 ≥ 1.0)
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
        // A ~30-cell blob, jittered a little — well short of the old ~55%-of-field carpet (an 8x8 grid is
        // 64 cells, so ~30 is under half).
        Assert.InRange(strike.Zones.Count, 25, 35);
    }
}
