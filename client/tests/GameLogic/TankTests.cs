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

    // Never reports a hit and never blocks, so the movement/fire tests run unobstructed.
    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
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
    public void NewTank_StartsAtFullHealthAndAlive()
    {
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)));

        Assert.Equal(tank.MaxHp, tank.Hp);
        Assert.True(tank.IsAlive);
    }

    [Fact]
    public void TakeDamage_ReducesHp_AndKillsTheTankAtZero()
    {
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)));
        var max = tank.MaxHp;

        tank.TakeDamage(1);
        Assert.Equal(max - 1, tank.Hp);
        Assert.True(tank.IsAlive);

        tank.TakeDamage(max); // overkill clamps at 0
        Assert.Equal(0, tank.Hp);
        Assert.False(tank.IsAlive);

        tank.TakeDamage(1); // damage to a dead tank is a no-op
        Assert.Equal(0, tank.Hp);
    }

    [Fact]
    public void DeadTank_IsReapedByTheWorld()
    {
        var world = new World();
        var tank = new Tank(
            new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)),
            world, new OpenArena(), Vector2.Zero, Speed, FireInterval, ProjectileSpeed, maxHp: 1);
        world.Spawn(tank);

        tank.TakeDamage(1);
        world.Step(0.1f);

        Assert.DoesNotContain(tank, world.Entities);
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

    // A grid (64-unit tiles, origin 0) whose column x=2 is a steel wall (world x in
    // [128,192)); every other cell is floor. Tall enough to slide along.
    private static GridArena WallColumnArena(int height = 16)
    {
        var materials = new CellMaterial[4, height];
        for (var x = 0; x < 4; x++)
        {
            for (var y = 0; y < height; y++)
            {
                materials[x, y] = x == 2 ? CellMaterial.Steel : CellMaterial.Floor;
            }
        }

        return new GridArena(WallGrid.FromMaterials(materials), tileSize: 64f, origin: Vector2.Zero);
    }

    [Fact]
    public void Step_StopsAtAWall_WhenDrivingStraightIntoIt()
    {
        var arena = WallColumnArena();
        var tank = new Tank(
            new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false)),
            new World(), arena, new Vector2(32f, 32f), Speed, FireInterval, ProjectileSpeed);

        for (var i = 0; i < 40; i++)
        {
            tank.Step(0.1f); // keep driving +X into the wall at x=128
        }

        Assert.True(tank.Position.X > 32f, "tank should advance toward the wall");
        Assert.True(tank.Position.X + Tank.CollisionRadius <= 128f, "tank must not enter the wall");
        Assert.Equal(32f, tank.Position.Y, precision: 3); // no drift on the free axis

        var stuckX = tank.Position.X;
        tank.Step(0.1f);
        Assert.Equal(stuckX, tank.Position.X, precision: 3); // fully stopped
    }

    [Fact]
    public void Step_SlidesAlongAWall_WhenDrivingDiagonallyIntoIt()
    {
        var arena = WallColumnArena();
        var tank = new Tank(
            new ScriptedInput(new TankInput(new Vector2(1f, 1f), Aim: 0f, Fire: false)),
            new World(), arena, new Vector2(32f, 32f), Speed, FireInterval, ProjectileSpeed);

        for (var i = 0; i < 12; i++)
        {
            tank.Step(0.1f); // push diagonally; +X jams on the wall, +Y stays free
        }

        Assert.True(tank.Position.X + Tank.CollisionRadius <= 128f, "tank must not pass the wall");
        Assert.True(tank.Position.Y > 90f, "tank should keep sliding along +Y");
    }

    // A grid whose column x=2 is the given destructible/indestructible material.
    private static (WallGrid grid, GridArena arena) ColumnArena(CellMaterial wall, int height = 16)
    {
        var materials = new CellMaterial[4, height];
        for (var x = 0; x < 4; x++)
        {
            for (var y = 0; y < height; y++)
            {
                materials[x, y] = x == 2 ? wall : CellMaterial.Floor;
            }
        }

        var grid = WallGrid.FromMaterials(materials);
        return (grid, new GridArena(grid, tileSize: 64f, origin: Vector2.Zero));
    }

    [Fact]
    public void Step_PushingIntoBrick_DemolishesIt_AndTheTankRollsThrough()
    {
        var (grid, arena) = ColumnArena(CellMaterial.Brick);
        var tank = new Tank(
            new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false)),
            new World(), arena, new Vector2(32f, 32f), Speed, FireInterval, ProjectileSpeed);

        for (var i = 0; i < 60; i++)
        {
            tank.Step(0.1f); // shove +X into the brick at cell (2,0) for ~6s
        }

        Assert.Equal(CellMaterial.Floor, grid.GetCell(2, 0).Material); // demolished
        Assert.True(tank.Position.X > 150f, "tank should roll into the freed cell");
    }

    [Fact]
    public void Step_PushingIntoSteel_NeverBreaksThrough()
    {
        var (grid, arena) = ColumnArena(CellMaterial.Steel);
        var tank = new Tank(
            new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false)),
            new World(), arena, new Vector2(32f, 32f), Speed, FireInterval, ProjectileSpeed);

        for (var i = 0; i < 60; i++)
        {
            tank.Step(0.1f);
        }

        Assert.Equal(CellMaterial.Steel, grid.GetCell(2, 0).Material);
        Assert.True(tank.Position.X + Tank.CollisionRadius <= 128f, "steel must stay solid");
    }

    [Fact]
    public void Step_BriefPush_BelowTheInterval_DoesNotDamageTheWall()
    {
        var (grid, arena) = ColumnArena(CellMaterial.Brick);
        // Start jammed against the wall so pushing counts from the first step.
        var tank = new Tank(
            new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false)),
            new World(), arena, new Vector2(100f, 32f), Speed, FireInterval, ProjectileSpeed);

        tank.Step(0.1f);
        tank.Step(0.1f);
        tank.Step(0.1f); // 0.3s of pushing, below the 0.4s interval

        Assert.Equal(WallGrid.DefaultBrickHp, grid.GetCell(2, 0).Hp); // not yet chipped
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
