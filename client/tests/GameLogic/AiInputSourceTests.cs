using System;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class AiInputSourceTests
{
    // A positioned, teamed tank stand-in — enough to be a target or the AI's own tank.
    private sealed class StubTank : ITank
    {
        public StubTank(Vector2 position, int team)
        {
            Position = position;
            Team = team;
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }
        public Vector2 Position { get; }
        public float Rotation => 0f;
        public float TurretRotation => 0f;
        public int Team { get; }
        public int Hp => 1;
        public int MaxHp => 1;
        public int Shield => 0;
        public bool IsAlive { get; set; } = true;
        public void TakeDamage(int amount) { }
        public void Step(float deltaSeconds) { }
    }

    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    // A positioned pickup on the field — enough for the AI to notice and steer toward.
    private sealed class StubPowerup : IPowerup
    {
        public StubPowerup(Vector2 position, bool available = true)
        {
            Position = position;
            IsAvailable = available;
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }
        public Vector2 Position { get; }
        public PowerupKind Kind => PowerupKind.SpeedBoost;
        public bool IsAlive => true;
        public bool IsAvailable { get; set; }
        public event Action<PowerupKind>? Collected { add { } remove { } }
        public void Step(float deltaSeconds) { }
    }

    // A teamed shot in flight — enough for the AI to hear and investigate.
    private sealed class StubProjectile : IProjectile
    {
        public StubProjectile(Vector2 position, int team) { Position = position; Team = team; Id = Guid.NewGuid(); }
        public Guid Id { get; }
        public Vector2 Position { get; }
        public int Team { get; }
        public Vector2 Direction => Vector2.UnitX;
        public bool IsAlive => true;
        public void Step(float deltaSeconds) { }
    }

    // Concealment everywhere — isolates the AI's "spot a bushed target only up close" rule.
    private sealed class AllConcealing : IConcealment
    {
        public bool ConcealsAt(Vector2 point) => true;
        public Vector2? NearestConcealment(Vector2 from, float maxRange) => from;
    }

    // A single patch of grass at a point — for the ambusher tests.
    private sealed class GrassAt : IConcealment
    {
        private readonly Vector2 _spot;
        private readonly float _radius;
        public GrassAt(Vector2 spot, float radius = 32f) { _spot = spot; _radius = radius; }
        public bool ConcealsAt(Vector2 point) => Vector2.Distance(point, _spot) <= _radius;
        public Vector2? NearestConcealment(Vector2 from, float maxRange) =>
            Vector2.Distance(from, _spot) <= maxRange ? _spot : null;
    }

    private static AiInputSource AiDriving(StubTank self, IWorld world, IArena? arena = null)
    {
        var ai = new AiInputSource(world, arena ?? new OpenArena());
        ai.Bind(self);
        return ai;
    }

    [Fact]
    public void AimsAndAdvances_TowardADistantEnemy()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(300f, 0f), team: 0)); // far enemy on +X
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.Equal(0f, intent.Aim, precision: 3);   // aims at +X
        Assert.True(intent.Move.X > 0f);              // drives toward it
        Assert.True(intent.Fire);                     // in range, open sight
    }

    [Fact]
    public void HoldsPosition_ButStillFires_WhenWithinStandoffRange()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(100f, 0f), team: 0)); // close enemy
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.Equal(Vector2.Zero, intent.Move); // close enough — stop advancing
        Assert.True(intent.Fire);
    }

    [Fact]
    public void Roams_WhenItHasNoTarget_InsteadOfStandingStill()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self); // no other tanks on the field
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.NotEqual(Vector2.Zero, intent.Move); // wanders to find a fight
        Assert.False(intent.Fire);                  // but does not fire at nothing
    }

    [Fact]
    public void Targets_AnyOtherTank_IncludingItsOwnTeam()
    {
        // Free-for-all: the AI attacks the nearest tank that is not itself, even a same-team one.
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(200f, 0f), team: 1)); // same team — now a target
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.Equal(0f, intent.Aim, precision: 3); // aims at the same-team tank on +X
        Assert.True(intent.Fire);
    }

    [Fact]
    public void Targets_TheNearestEnemy()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(0f, 300f), team: 0)); // farther, +Y
        world.Spawn(new StubTank(new Vector2(200f, 0f), team: 0)); // nearer, +X
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.Equal(0f, intent.Aim, precision: 3); // aims at the nearer (+X) enemy
    }

    [Fact]
    public void HoldsFire_WhenAWallBlocksTheLineOfSight()
    {
        // tiles of 64: self in cell 0, enemy in cell 4, a steel wall in cell 2 between them.
        var materials = new CellMaterial[5, 1];
        for (var x = 0; x < 5; x++)
        {
            materials[x, 0] = x == 2 ? CellMaterial.Steel : CellMaterial.Floor;
        }

        var arena = new GridArena(WallGrid.FromMaterials(materials), tileSize: 64f, origin: Vector2.Zero);
        var world = new World();
        var self = new StubTank(new Vector2(32f, 32f), team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(288f, 32f), team: 0)); // behind the wall
        var ai = AiDriving(self, world, arena);

        var intent = ai.Read();

        Assert.False(intent.Fire); // cannot see the target through steel
    }

    [Fact]
    public void IgnoresAnEnemyBeyondVisionRange()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(5000f, 0f), team: 0)); // far past sight, open ground
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.False(intent.Fire); // too far to see — not engaged (it roams instead of hunting it)
    }

    [Fact]
    public void IsUnawareOfAnEnemyOutsideItsVisionCircle()
    {
        // An enemy in the open at 800 on +X — within the old whole-map sight, but outside the ~640
        // circle. A SEEN enemy this far would make the AI close straight in (Move == unit +X); an
        // unseen one leaves it roaming, so it does not drive at the enemy.
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(800f, 0f), team: 0));
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.False(intent.Fire);
        Assert.NotEqual(new Vector2(1f, 0f), intent.Move); // not closing in on the unseen enemy
    }

    [Fact]
    public void InvestigatesGunfire_WhenNoTargetIsInItsCircle()
    {
        // No enemy in sight, but an enemy shot streaks by on +X within earshot — the AI is drawn
        // toward the gunfire to investigate.
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubProjectile(new Vector2(700f, 0f), team: 0)); // enemy fire on +X
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.True(intent.Move.X > 0.5f); // heading toward the shot, not roaming randomly
    }

    [Fact]
    public void IgnoresGunfire_BeyondEarshot()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubProjectile(new Vector2(2000f, 0f), team: 0)); // far past earshot
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.NotEqual(new Vector2(1f, 0f), intent.Move); // not drawn to a shot it cannot hear
    }

    [Fact]
    public void ChasesTheNearestEnemyItCanSee_NotACloserHiddenOne()
    {
        // A steel wall in cell 2 hides a close enemy in cell 0's row; a visible enemy sits
        // in the open below. The AI must pick the one it can see, not the nearer hidden one.
        var materials = new CellMaterial[5, 5];
        for (var x = 0; x < 5; x++)
        {
            for (var y = 0; y < 5; y++)
            {
                materials[x, y] = (x == 2 && y == 0) ? CellMaterial.Steel : CellMaterial.Floor;
            }
        }

        var arena = new GridArena(WallGrid.FromMaterials(materials), tileSize: 64f, origin: Vector2.Zero);
        var world = new World();
        var self = new StubTank(new Vector2(32f, 32f), team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(288f, 32f), team: 0));  // nearer but behind steel
        world.Spawn(new StubTank(new Vector2(32f, 288f), team: 0));  // farther but in the open (+Y)
        var ai = AiDriving(self, world, arena);

        var intent = ai.Read();

        Assert.True(intent.Move.Y > 0f, "AI should chase the visible enemy below, not the hidden one");
        Assert.True(MathF.Abs(intent.Move.X) < 0.01f);
    }

    [Fact]
    public void DoesNotTargetADistantTankHidingInABush()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(300f, 0f), team: 0)); // in the open ground… but bushed
        var ai = new AiInputSource(world, new OpenArena(), new AllConcealing());
        ai.Bind(self);

        var intent = ai.Read();

        Assert.False(intent.Fire); // can't pick it out of the foliage from afar — not engaged
    }

    [Fact]
    public void SpotsATankInABush_WhenRightOnTopOfIt()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(60f, 0f), team: 0)); // within the bush-reveal range
        var ai = new AiInputSource(world, new OpenArena(), new AllConcealing());
        ai.Bind(self);

        var intent = ai.Read();

        Assert.True(intent.Fire); // brushing the bush — the hidden tank is revealed and engaged
    }

    [Fact]
    public void SeeksAvailablePowerup_WhenThereIsNoEnemy()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubPowerup(new Vector2(300f, 0f))); // a pickup on +X
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.True(intent.Move.X > 0f);        // drives toward the pickup
        Assert.Equal(0f, intent.Aim, precision: 3);
        Assert.False(intent.Fire);
    }

    [Fact]
    public void IgnoresPowerups_WhileFiringOnAnEnemyInRange()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(100f, 0f), team: 0)); // enemy in fire range on +X
        world.Spawn(new StubPowerup(new Vector2(0f, 300f)));        // a pickup off on +Y
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.True(intent.Fire);                 // combat takes priority
        Assert.Equal(Vector2.Zero, intent.Move);  // holds at standoff, does not wander to the pickup
    }

    [Fact]
    public void DetoursForAPowerup_WhenTheEnemyIsOutOfFireRange()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(600f, 0f), team: 0)); // seen on +X but beyond fire range
        world.Spawn(new StubPowerup(new Vector2(0f, 300f)));        // a pickup on +Y to grab on the way
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.True(intent.Move.Y > 0f);            // steers toward the pickup
        Assert.Equal(0f, intent.Aim, precision: 3); // still keeps the gun on the enemy
        Assert.False(intent.Fire);
    }

    [Fact]
    public void IgnoresAnUnavailablePowerup_AndDoesNotDetourToIt()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(600f, 0f), team: 0));               // enemy out of fire range on +X
        world.Spawn(new StubPowerup(new Vector2(0f, 300f), available: false));   // dormant pickup on +Y
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.True(intent.Move.X > 0f);            // advances on the enemy, not toward the dormant pickup
        Assert.True(MathF.Abs(intent.Move.Y) < 0.01f);
    }

    [Fact]
    public void Ambusher_SlipsTowardNearbyGrass_WhenItHasPreyInSight()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(0f, 600f), team: 0)); // an enemy to ambush, out of range on +Y
        var ai = new AiInputSource(world, new OpenArena(), new GrassAt(new Vector2(200f, 0f)), ambusher: true);
        ai.Bind(self);

        var intent = ai.Read();

        Assert.True(intent.Move.X > 0f); // heads for the grass patch on +X to lie in wait
    }

    [Fact]
    public void Ambusher_HoldsAndFires_FromTheGrass_WhenAnEnemyComesIntoRange()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(120f, 0f), team: 0)); // enemy within fire range
        var ai = new AiInputSource(world, new OpenArena(), new GrassAt(Vector2.Zero), ambusher: true);
        ai.Bind(self); // self sits in the grass

        var intent = ai.Read();

        Assert.Equal(Vector2.Zero, intent.Move); // stays hidden — does not break cover
        Assert.True(intent.Fire);                // snipes from concealment
    }

    [Fact]
    public void Ambusher_WithNoGrassWithinReach_FightsNormally()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(300f, 0f), team: 0)); // a plain enemy on +X
        var ai = new AiInputSource(world, new OpenArena(), new GrassAt(new Vector2(5000f, 0f)), ambusher: true);
        ai.Bind(self);

        var intent = ai.Read();

        Assert.True(intent.Move.X > 0f); // no grass to use — advances on the enemy as usual
        Assert.True(intent.Fire);
    }

    [Fact]
    public void IgnoresAPowerupBeyondSeekRange_AndDoesNotDetourToIt()
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(new Vector2(600f, 0f), team: 0));   // enemy out of fire range on +X
        world.Spawn(new StubPowerup(new Vector2(0f, 5000f)));        // pickup way across the map on +Y
        var ai = AiDriving(self, world);

        var intent = ai.Read();

        Assert.True(intent.Move.X > 0f);            // advances on the enemy; the far pickup is out of reach
        Assert.True(MathF.Abs(intent.Move.Y) < 0.01f);
    }
}
