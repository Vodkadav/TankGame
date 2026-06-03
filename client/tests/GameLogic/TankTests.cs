using System;
using System.Linq;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class TankTests
{
    private sealed class ScriptedInput(TankInput value) : IInputSource
    {
        public TankInput Value { get; set; } = value;
        public TankInput Read() => Value;
    }

    // Never reports a hit, so spawned projectiles travel freely in the fire tests.
    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
    }

    private const float Speed = 100f;
    private const float FireInterval = 0.3f;
    private const float ProjectileSpeed = 600f;

    // The movement/rotation tests don't fire, so they take a throwaway world + arena.
    private static Tank NewTank(IInputSource input, IWorld? world = null) =>
        new(input, world ?? new World(), new OpenArena(), Vector2.Zero, Speed, FireInterval, ProjectileSpeed);

    [Fact]
    public void Step_MovesAlongInput_AtSpeedOverTime()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        var tank = NewTank(input);

        for (var i = 0; i < 10; i++)
        {
            tank.Step(0.1f); // 10 ticks * 0.1s = 1s at 100 u/s = 100 units
        }

        Assert.Equal(100f, tank.Position.X, precision: 3);
        Assert.Equal(0f, tank.Position.Y, precision: 3);
    }

    [Fact]
    public void Step_NormalisesDiagonalInput_SoSpeedIsConstant()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 1f), Aim: 0f, Fire: false));
        var tank = NewTank(input);

        tank.Step(1f);

        // A raw (1,1) would travel ~141 units; normalised it travels exactly 100.
        Assert.Equal(100f, tank.Position.Length(), precision: 3);
    }

    [Fact]
    public void Step_SetsChassisRotationFromMovementDirection()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(0f, 1f), Aim: 0f, Fire: false));
        var tank = NewTank(input);

        tank.Step(0.1f);

        Assert.Equal(MathF.Atan2(1f, 0f), tank.Rotation, precision: 4);
    }

    [Fact]
    public void Step_SetsTurretRotationFromAim()
    {
        var input = new ScriptedInput(new TankInput(Vector2.Zero, Aim: 2.0f, Fire: false));
        var tank = NewTank(input);

        tank.Step(0.1f);

        Assert.Equal(2.0f, tank.TurretRotation, precision: 4);
    }

    [Fact]
    public void Step_WithNoMovement_KeepsPositionAndChassisRotation()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        var tank = NewTank(input);
        tank.Step(0.1f);
        var rotationAfterMove = tank.Rotation;
        var positionAfterMove = tank.Position;

        input.Value = new TankInput(Vector2.Zero, Aim: 0f, Fire: false);
        tank.Step(0.1f);

        Assert.Equal(positionAfterMove, tank.Position);
        Assert.Equal(rotationAfterMove, tank.Rotation); // chassis keeps last facing when idle
    }

    [Fact]
    public void IsAlive_IsTrue_UntilHealthExists()
    {
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)));

        Assert.True(tank.IsAlive);
    }

    [Fact]
    public void Id_IsAssignedAndUniquePerTank()
    {
        var a = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)));
        var b = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)));

        Assert.NotEqual(Guid.Empty, a.Id);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Step_SpawnsAProjectileIntoTheWorld_WhenFireIsHeld()
    {
        var world = new World();
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: true)), world);

        tank.Step(0.1f);

        Assert.Single(world.Entities);
        Assert.IsType<Projectile>(world.Entities.First());
    }

    [Fact]
    public void Step_DoesNotSpawn_WhenFireIsNotHeld()
    {
        var world = new World();
        var tank = NewTank(new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false)), world);

        tank.Step(0.1f);

        Assert.Empty(world.Entities);
    }

    [Fact]
    public void Step_RateLimitsFiring_ByTheCooldown()
    {
        var world = new World();
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: true)), world);

        tank.Step(0.05f); // cooldown elapsed → fires (1), resets to 0.3s
        tank.Step(0.05f); // 0.25s left → no fire
        tank.Step(0.05f); // 0.20s left → no fire
        Assert.Single(world.Entities); // held fire is rate-limited within the interval

        tank.Step(0.40f); // well past the 0.3s cooldown → fires (2)
        Assert.Equal(2, world.Entities.Count);
    }

    [Fact]
    public void Step_FiresAlongTheTurretAim()
    {
        var world = new World();
        // Aim straight up (+Y): the shot must travel up, not along the chassis +X.
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: MathF.PI / 2f, Fire: true)), world);

        tank.Step(0.1f);
        var shot = world.Entities.First();
        var spawn = shot.Position;
        shot.Step(0.1f);

        Assert.True(shot.Position.Y > spawn.Y);
        Assert.Equal(spawn.X, shot.Position.X, precision: 3);
    }
}
