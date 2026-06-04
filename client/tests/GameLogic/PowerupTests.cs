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
        new(world, pos, PowerupKind.SpeedBoost, SpeedBoost, PickupRadius);

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
