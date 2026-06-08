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
    public void Heal_RestoresHp_ClampedAtMaxHp()
    {
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)));
        tank.TakeDamage(2); // 3 -> 1

        tank.Heal(1);
        Assert.Equal(2, tank.Hp);

        tank.Heal(10); // clamps at MaxHp, no overheal
        Assert.Equal(tank.MaxHp, tank.Hp);
    }

    [Fact]
    public void Heal_IsANoOp_OnADownedTank()
    {
        var tank = new Tank(
            new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)),
            new World(), new OpenArena(), Vector2.Zero, Speed, FireInterval, ProjectileSpeed, maxHp: 1, lives: 2);
        tank.TakeDamage(1); // downed (Hp 0), awaiting respawn

        tank.Heal(1); // repair cannot revive — only the respawn timer does

        Assert.Equal(0, tank.Hp);
    }

    [Fact]
    public void Shield_AbsorbsDamage_BeforeHitPoints()
    {
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)));
        tank.AddShield(2);

        tank.TakeDamage(1); // soaked by the shield
        Assert.Equal(1, tank.Shield);
        Assert.Equal(tank.MaxHp, tank.Hp);

        tank.TakeDamage(1); // depletes the shield, still no Hp lost
        Assert.Equal(0, tank.Shield);
        Assert.Equal(tank.MaxHp, tank.Hp);
    }

    [Fact]
    public void Shield_OverflowDamage_SpillsOntoHitPoints()
    {
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)));
        tank.AddShield(1);

        tank.TakeDamage(3); // 1 absorbed, 2 hits Hp

        Assert.Equal(0, tank.Shield);
        Assert.Equal(tank.MaxHp - 2, tank.Hp);
    }

    [Fact]
    public void AddShield_Stacks_AndIgnoresNonPositiveAmounts()
    {
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)));

        tank.AddShield(2);
        tank.AddShield(3);
        tank.AddShield(-5); // ignored

        Assert.Equal(5, tank.Shield);
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
    public void Step_FiresProjectilesOnTheTanksOwnLayer()
    {
        var world = new World();
        var tank = new Tank(
            new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: true)),
            world, new OpenArena(), Vector2.Zero, Speed, FireInterval, ProjectileSpeed, layer: 2);
        world.Spawn(tank);

        tank.Step(0.05f);

        var shot = Assert.Single(world.Entities.OfType<Projectile>());
        Assert.Equal(2, shot.Layer); // the shot stays on the shooter's elevation
    }

    [Fact]
    public void LoadAmmo_KeepsFiringTheSpecialShot_ForEveryShotWhileTheTankLives()
    {
        var world = new World();
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: true)), world);
        tank.LoadAmmo(new SpreadAmmo(count: 3, radians: 0.2f));

        tank.Step(0.05f); // 3
        tank.Step(0.4f);  // +3 — no shot limit, still the spread
        tank.Step(0.4f);  // +3

        Assert.Equal(9, world.Entities.OfType<Projectile>().Count()); // never reverts while alive
    }

    [Fact]
    public void Death_ShedsHeldAmmoAndBuffs_SoTheRespawnedTankIsBareAgain()
    {
        var world = new World();
        var tank = new Tank(
            new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: true)),
            world, new OpenArena(), Vector2.Zero, Speed, FireInterval, ProjectileSpeed, maxHp: 1, lives: 2);
        world.Spawn(tank);
        tank.LoadAmmo(new SpreadAmmo(count: 3, radians: 0.2f)); // held special ammo

        tank.TakeDamage(1);                 // killed; a life remains, so it will respawn
        world.Step(Tank.RespawnDelay + 0.1f); // revive at full health (no fire on the revive step)

        var before = world.Entities.OfType<Projectile>().Count();
        world.Step(0.05f);                  // first shot after respawn
        Assert.Equal(1, world.Entities.OfType<Projectile>().Count() - before); // the plain shot, not a spread
    }

    [Fact]
    public void LoadAmmo_StacksCrates_SoSpreadAfterBouncingFiresAFanOfBouncingShots()
    {
        var world = new World();
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: true)), world);
        tank.LoadAmmo(new BouncingAmmo(bounces: 3));
        tank.LoadAmmo(new SpreadAmmo(count: 3, radians: 0.18f)); // stacks onto the bouncing

        tank.Step(0.05f);

        Assert.Equal(3, world.Entities.OfType<Projectile>().Count()); // a fan, not a single shot
    }

    private sealed class SlowGround : ITerrain
    {
        public float SpeedFactorAt(System.Numerics.Vector2 point) => 0.5f;
    }

    [Fact]
    public void Step_MovesSlower_OverSlowingTerrain()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        var plain = NewTank(input);
        var slowed = new Tank(input, new World(), new OpenArena(), Vector2.Zero, Speed, FireInterval,
            ProjectileSpeed, terrain: new SlowGround());

        plain.Step(0.1f);
        slowed.Step(0.1f);

        Assert.Equal(plain.Position.X * 0.5f, slowed.Position.X, precision: 3); // half speed on sandbags
    }

    [Fact]
    public void Step_MovesFasterUnderASpeedBoost_UntilItExpires()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        var tank = NewTank(input); // base speed 100
        tank.ApplyEffect(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 1f));

        tank.Step(0.5f); // boosted to 200 u/s → +100
        Assert.Equal(100f, tank.Position.X, precision: 3);

        tank.Step(0.6f); // boost expires this step → base 100 u/s → +60
        Assert.Equal(160f, tank.Position.X, precision: 3);
    }

    [Fact]
    public void Step_FiresFasterUnderARapidFireEffect()
    {
        var world = new World();
        var tank = NewTank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: true)), world);
        tank.ApplyEffect(new StatusEffect(StatKind.FireInterval, Mult: 0.5f, AddFlat: 0f, Seconds: 5f));

        tank.Step(0.05f); // fires (1); cooldown set to 0.3 * 0.5 = 0.15s
        tank.Step(0.16f); // 0.16 > 0.15 → fires (2); base 0.3s would not have fired yet

        Assert.Equal(2, world.Entities.Count);
    }

    [Fact]
    public void DownedTank_WithALifeLeft_StaysInTheMatch_ThenRespawnsAtSpawnAtFullHealth()
    {
        var spawn = new Vector2(50f, 60f);
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        var tank = new Tank(input, new World(), new OpenArena(), spawn,
            Speed, FireInterval, ProjectileSpeed, maxHp: 1, team: 0, lives: 2);

        tank.Step(0.1f); // drives +X away from the spawn cell
        Assert.True(tank.Position.X > spawn.X);

        tank.TakeDamage(1); // downed, but a life remains
        Assert.Equal(0, tank.Hp);
        Assert.True(tank.IsAlive); // still in the match — it will respawn

        tank.Step(Tank.RespawnDelay / 2f); // mid-respawn: still down
        Assert.Equal(0, tank.Hp);

        tank.Step(Tank.RespawnDelay); // delay elapsed → revive at spawn, full health
        Assert.Equal(tank.MaxHp, tank.Hp);
        Assert.Equal(spawn, tank.Position);
    }

    [Fact]
    public void DownedTank_DoesNotMove_WhileAwaitingRespawn()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        var tank = new Tank(input, new World(), new OpenArena(), Vector2.Zero,
            Speed, FireInterval, ProjectileSpeed, maxHp: 1, team: 0, lives: 2);

        tank.TakeDamage(1); // downed
        var resting = tank.Position;
        tank.Step(Tank.RespawnDelay / 4f); // input says drive, but a downed tank is inert

        Assert.Equal(resting, tank.Position);
    }

    [Fact]
    public void DownedTank_DoesNotFire_WhileAwaitingRespawn()
    {
        var world = new World();
        var tank = new Tank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: true)),
            world, new OpenArena(), Vector2.Zero, Speed, FireInterval, ProjectileSpeed, maxHp: 1, team: 0, lives: 2);

        tank.TakeDamage(1); // downed
        tank.Step(Tank.RespawnDelay / 4f);

        Assert.Empty(world.Entities); // a downed tank holds fire
    }

    [Fact]
    public void Tank_OutOfLives_IsReapedByTheWorld()
    {
        var world = new World();
        var tank = new Tank(new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)),
            world, new OpenArena(), Vector2.Zero, Speed, FireInterval, ProjectileSpeed, maxHp: 1, team: 0, lives: 2);
        world.Spawn(tank);

        tank.TakeDamage(1); // first death — respawns
        world.Step(Tank.RespawnDelay + 0.1f);
        Assert.Contains(tank, world.Entities);
        Assert.True(tank.Hp > 0); // back in the fight

        tank.TakeDamage(tank.MaxHp); // second death — out of lives
        Assert.False(tank.IsAlive);
        world.Step(0.1f);
        Assert.DoesNotContain(tank, world.Entities); // permanently reaped
    }

    [Fact]
    public void MatchTracker_KeepsADownedTankWithLivesLeft_InTheMatch()
    {
        var stand = new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false));
        var downed = new Tank(stand, new World(), new OpenArena(), Vector2.Zero,
            Speed, FireInterval, ProjectileSpeed, maxHp: 1, team: 0, lives: 2);
        var enemy = new Tank(stand, new World(), new OpenArena(), new Vector2(500f, 0f),
            Speed, FireInterval, ProjectileSpeed, maxHp: 1, team: 1, lives: 1);
        downed.TakeDamage(1); // Hp 0 but a life remains

        var result = new MatchTracker().Evaluate(new IEntity[] { downed, enemy });

        Assert.False(result.Decided); // team 0 is down but still in the match
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
    public void Step_StampsItsTeamOnFiredProjectiles()
    {
        var world = new World();
        var tank = new Tank(
            new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: true)),
            world, new OpenArena(), Vector2.Zero, Speed, FireInterval, ProjectileSpeed, maxHp: 3, team: 2);

        tank.Step(0.1f);

        var shot = Assert.IsType<Projectile>(world.Entities.First());
        Assert.Equal(2, tank.Team);
        Assert.Equal(2, shot.Team); // the shot belongs to the firing tank's team
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
    public void Step_StopsBeforeOverlappingAnotherTank()
    {
        var world = new World();
        var blocker = new Tank(
            new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)),
            world, new OpenArena(), new Vector2(200f, 32f), Speed, FireInterval, ProjectileSpeed);
        var mover = new Tank(
            new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false)),
            world, new OpenArena(), new Vector2(32f, 32f), Speed, FireInterval, ProjectileSpeed);
        world.Spawn(blocker);
        world.Spawn(mover);

        for (var i = 0; i < 40; i++)
        {
            mover.Step(0.1f); // drive +X straight into the stationary blocker
        }

        Assert.True(mover.Position.X > 32f, "mover should advance toward the other tank");
        var gap = blocker.Position.X - mover.Position.X;
        Assert.True(gap >= (2f * Tank.CollisionRadius) - 0.5f, $"tanks overlap: centre gap {gap}");
        Assert.Equal(32f, mover.Position.Y, precision: 3); // no drift on the free axis
    }

    [Fact]
    public void Step_PushingIntoAnotherTank_DoesNotDamageWallsBehindIt()
    {
        var (grid, arena) = ColumnArena(CellMaterial.Brick);
        var world = new World();
        // A blocker sits just in front of the brick column; the mover jams into the blocker,
        // never reaching the wall, so no demolition should occur.
        var blocker = new Tank(
            new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false)),
            world, arena, new Vector2(80f, 32f), Speed, FireInterval, ProjectileSpeed);
        var mover = new Tank(
            new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false)),
            world, arena, new Vector2(32f, 32f), Speed, FireInterval, ProjectileSpeed);
        world.Spawn(blocker);
        world.Spawn(mover);

        for (var i = 0; i < 60; i++)
        {
            mover.Step(0.1f);
        }

        Assert.Equal(WallGrid.DefaultBrickHp, grid.GetCell(2, 0).Hp); // wall untouched
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
