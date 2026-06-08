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
    public void Powerup_RaisesCollected_WithItsKind_WhenPickedUp()
    {
        var world = new World();
        var tank = TankAt(world, Vector2.Zero, new NoInput());
        var powerup = SpeedPowerupAt(world, Vector2.Zero);
        PowerupKind? collected = null;
        powerup.Collected += kind => collected = kind;
        world.Spawn(tank);
        world.Spawn(powerup);

        world.Step(0.016f);

        Assert.Equal(PowerupKind.SpeedBoost, collected); // fired so Presentation can show a floater
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
            new AmmoPickup(new SpreadAmmo(count: 3, radians: 0.2f)), PickupRadius);
        world.Spawn(tank);
        world.Spawn(crate);

        world.Step(0.05f); // tank fires its default shot (1), then collects the crate
        world.Step(0.4f);  // next shot uses the loaded spread → +3

        Assert.Equal(4, world.Entities.OfType<Projectile>().Count());
        Assert.DoesNotContain(crate, world.Entities); // crate consumed
    }

    [Fact]
    public void ADropPickup_GoesDormant_OnCollection_ThenDropsWhereTheCarrierDies()
    {
        var world = new World();
        var carrier = TankAt(world, Vector2.Zero, new DriveRight()); // collects, then drives off +X
        var powerup = new Powerup(world, Vector2.Zero, PowerupKind.SpeedBoost,
            new StatusEffectPickup(SpeedBoost), PickupRadius, dropOnCarrierDeath: true);
        world.Spawn(carrier);
        world.Spawn(powerup);

        world.Step(0.016f); // collected → dormant, carried away (not reaped)
        Assert.Contains(powerup, world.Entities);
        Assert.False(powerup.IsAvailable);

        for (var i = 0; i < 20; i++)
        {
            world.Step(0.05f); // the carrier drives well away from the origin
        }

        carrier.TakeDamage(carrier.MaxHp); // the carrier falls
        world.Step(0.016f);

        Assert.True(powerup.IsAvailable);          // it returns to the field…
        Assert.True(powerup.Position.X > 50f);     // …where the carrier died, not where it was picked up
    }

    [Fact]
    public void ACarriedDropPickup_CannotBeCollectedByAnotherTank_WhileItsCarrierLives()
    {
        var world = new World();
        var carrier = TankAt(world, Vector2.Zero, new NoInput());
        var powerup = new Powerup(world, Vector2.Zero, PowerupKind.Shield,
            new ShieldPickup(2), PickupRadius, dropOnCarrierDeath: true);
        world.Spawn(carrier);
        world.Spawn(powerup);

        world.Step(0.016f); // carrier collects → dormant
        Assert.Equal(2, carrier.Shield);

        var other = TankAt(world, Vector2.Zero, new NoInput());
        world.Spawn(other);
        world.Step(0.016f); // overlapping but dormant (carrier still alive) → no second collect

        Assert.Equal(0, other.Shield);
        Assert.False(powerup.IsAvailable);
    }

    [Fact]
    public void ADroppedPickup_CanBeCollectedAgain()
    {
        var world = new World();
        var carrier = TankAt(world, Vector2.Zero, new NoInput());
        var powerup = new Powerup(world, Vector2.Zero, PowerupKind.Shield,
            new ShieldPickup(2), PickupRadius, dropOnCarrierDeath: true);
        world.Spawn(carrier);
        world.Spawn(powerup);

        world.Step(0.016f);     // carrier collects → dormant (and gains +2 shield)
        carrier.TakeDamage(99); // lethal even through the shield it just picked up
        world.Step(0.016f);     // pickup drops back, available again
        Assert.True(powerup.IsAvailable);

        var taker = TankAt(world, Vector2.Zero, new NoInput());
        world.Spawn(taker);
        world.Step(0.016f); // the new tank collects the dropped pickup

        Assert.Equal(2, taker.Shield);
    }

    [Fact]
    public void AStationPickup_GoesDormantOnCollection_ThenRefillsAtTheSameSpotAfterItsCooldown()
    {
        var world = new World();
        var spot = new Vector2(0f, 0f);
        var tank = TankAt(world, spot, new NoInput());
        var station = new Powerup(world, spot, PowerupKind.Telephone,
            new ShieldPickup(1), PickupRadius, respawnCooldown: 2f);
        world.Spawn(tank);
        world.Spawn(station);

        world.Step(0.016f); // collected → dormant, but stays on the field at its fixed spot
        Assert.Contains(station, world.Entities);
        Assert.False(station.IsAvailable);

        world.Step(1.0f);
        Assert.False(station.IsAvailable); // still cooling down

        world.Step(1.1f);                  // cooldown elapsed
        Assert.True(station.IsAvailable);  // collectable again…
        Assert.Equal(spot, station.Position); // …right where it started, not carried off
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
