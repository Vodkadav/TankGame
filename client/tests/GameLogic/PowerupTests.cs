using System.Linq;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class PowerupTests
{
    private sealed class NoInput : IInputSource
    {
        public TankInput Read() => new(Vector2.Zero, Aim: 0f, Fire: false);
    }

    private sealed class DriveRight : IInputSource
    {
        public TankInput Read() => new(new Vector2(1f, 0f), Aim: 0f, Fire: false);
    }

    private sealed class HoldFire : IInputSource
    {
        public TankInput Read() => new(Vector2.Zero, Aim: 0f, Fire: true);
    }

    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    private const float PickupRadius = 20f;

    private static readonly StatusEffect SpeedBoost = new(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 5f);

    private static Tank TankAt(IWorld world, Vector2 pos, IInputSource input, int hp = 3) =>
        new(input, world, new OpenArena(), pos, speed: 100f, fireInterval: 0.3f, projectileSpeed: 600f, maxHp: hp);

    private static Powerup SpeedPowerupAt(IWorld world, Vector2 pos) =>
        new(world, pos, PowerupKind.SpeedBoost, new StatusEffectPickup(SpeedBoost), PickupRadius);

    [Fact]
    public void Powerup_IsCollected_WhenALiveTankOverlapsIt()
    {
        var world = new World();
        var tank = TankAt(world, Vector2.Zero, new NoInput());
        var powerup = SpeedPowerupAt(world, Vector2.Zero);
        world.Spawn(tank);
        world.Spawn(powerup);

        world.Step(0.016f);

        Assert.DoesNotContain(powerup, world.Entities); // collected and reaped
    }

    [Fact]
    public void Powerup_IsNotCollected_WhenNoTankIsWithinRange()
    {
        var world = new World();
        var tank = TankAt(world, new Vector2(500f, 0f), new NoInput());
        var powerup = SpeedPowerupAt(world, Vector2.Zero);
        world.Spawn(tank);
        world.Spawn(powerup);

        world.Step(0.016f);

        Assert.Contains(powerup, world.Entities); // out of range — still on the field
    }

    [Fact]
    public void Powerup_IsNotCollected_ByADownedTank()
    {
        var world = new World();
        var tank = TankAt(world, Vector2.Zero, new NoInput(), hp: 1);
        tank.TakeDamage(1); // downed (Hp 0)
        var powerup = SpeedPowerupAt(world, Vector2.Zero);
        world.Spawn(tank);
        world.Spawn(powerup);

        world.Step(0.016f);

        Assert.Contains(powerup, world.Entities); // a downed tank cannot collect
    }

    [Fact]
    public void Powerup_GrantsItsEffect_ToTheTankThatCollectsIt()
    {
        Assert.True(TravelWithPowerup(true) > TravelWithPowerup(false),
            "a tank that collected a speed boost should out-travel one that did not");
    }

    [Fact]
    public void ARepairPickup_RestoresHp_OnTheCollectingTank()
    {
        var world = new World();
        var tank = TankAt(world, Vector2.Zero, new NoInput(), hp: 3);
        tank.TakeDamage(2); // Hp 1
        var repair = new Powerup(world, Vector2.Zero, PowerupKind.Repair, new RepairPickup(2), PickupRadius);
        world.Spawn(tank);
        world.Spawn(repair);

        world.Step(0.016f);

        Assert.Equal(3, tank.Hp); // repaired (clamped at MaxHp)
        Assert.DoesNotContain(repair, world.Entities); // collected
    }

    [Fact]
    public void AShieldPickup_GrantsOverShield_OnTheCollectingTank()
    {
        var world = new World();
        var tank = TankAt(world, Vector2.Zero, new NoInput());
        var shield = new Powerup(world, Vector2.Zero, PowerupKind.Shield, new ShieldPickup(3), PickupRadius);
        world.Spawn(tank);
        world.Spawn(shield);

        world.Step(0.016f);

        Assert.Equal(3, tank.Shield);
        Assert.DoesNotContain(shield, world.Entities);
    }

    [Fact]
    public void AnAmmoCrate_LoadsItsSpecialWeapon_OnTheCollectingTank()
    {
        var world = new World();
        var tank = TankAt(world, Vector2.Zero, new HoldFire());
        var crate = new Powerup(world, Vector2.Zero, PowerupKind.SpreadAmmo,
            new AmmoPickup(new SpreadWeapon(count: 3, spreadRadians: 0.2f), shots: 1), PickupRadius);
        world.Spawn(tank);
        world.Spawn(crate);

        world.Step(0.05f); // tank fires its default shot (1), then collects the crate
        world.Step(0.4f);  // next shot uses the loaded spread → +3

        Assert.Equal(4, world.Entities.OfType<Projectile>().Count());
        Assert.DoesNotContain(crate, world.Entities); // crate consumed
    }

    [Fact]
    public void ARespawningPowerup_StaysInTheWorld_AndReturnsAfterItsDelay()
    {
        var world = new World();
        var tank = TankAt(world, Vector2.Zero, new NoInput());
        var powerup = new Powerup(world, Vector2.Zero, PowerupKind.SpeedBoost,
            new StatusEffectPickup(SpeedBoost), PickupRadius, respawnDelay: 5f);
        world.Spawn(tank);
        world.Spawn(powerup);

        world.Step(0.016f); // collected
        Assert.Contains(powerup, world.Entities); // not reaped — it respawns
        Assert.False(powerup.IsAvailable);        // dormant on its cooldown

        world.Step(2f);
        Assert.False(powerup.IsAvailable); // still cooling down

        world.Step(3.1f);
        Assert.True(powerup.IsAvailable); // back on the field after the delay
    }

    [Fact]
    public void ADormantRespawningPowerup_CannotBeCollected()
    {
        var world = new World();
        var collector = TankAt(world, Vector2.Zero, new NoInput(), hp: 3);
        collector.TakeDamage(2); // Hp 1, so a repair would be observable
        var powerup = new Powerup(world, Vector2.Zero, PowerupKind.Repair,
            new RepairPickup(2), PickupRadius, respawnDelay: 5f);
        world.Spawn(collector);
        world.Spawn(powerup);

        world.Step(0.016f); // first collect → Hp back to 3, now dormant
        Assert.Equal(3, collector.Hp);

        collector.TakeDamage(2); // Hp 1 again
        world.Step(0.016f); // overlapping but dormant → no second collect

        Assert.Equal(1, collector.Hp);
    }

    [Fact]
    public void ARespawnedPowerup_CanBeCollectedAgain()
    {
        var world = new World();
        var tank = TankAt(world, Vector2.Zero, new NoInput(), hp: 3);
        var powerup = new Powerup(world, Vector2.Zero, PowerupKind.Shield,
            new ShieldPickup(2), PickupRadius, respawnDelay: 1f);
        world.Spawn(tank);
        world.Spawn(powerup);

        world.Step(0.016f); // collect → +2 shield
        Assert.Equal(2, tank.Shield);

        world.Step(1.1f);   // cooldown elapses → available again (no collect on the respawn step)
        Assert.True(powerup.IsAvailable);
        world.Step(0.016f); // now the overlapping tank collects it a second time
        Assert.Equal(4, tank.Shield);
    }

    private static float TravelWithPowerup(bool withPowerup)
    {
        var world = new World();
        var tank = TankAt(world, Vector2.Zero, new DriveRight());
        world.Spawn(tank);
        if (withPowerup)
        {
            world.Spawn(SpeedPowerupAt(world, Vector2.Zero));
        }

        for (var i = 0; i < 20; i++)
        {
            world.Step(0.05f);
        }

        return tank.Position.X;
    }
}
