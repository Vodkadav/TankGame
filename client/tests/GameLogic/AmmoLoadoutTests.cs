using System.Linq;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class AmmoLoadoutTests
{
    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    private const float Speed = 600f;

    private static AmmoLoadout Fired(out World world, params AmmoModifier[] modifiers)
    {
        var loadout = new AmmoLoadout();
        foreach (var modifier in modifiers)
        {
            modifier.ApplyTo(loadout);
        }

        world = new World();
        loadout.Fire(world, new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed, team: 2);
        return loadout;
    }

    [Fact]
    public void Default_FiresOneStraightShot_StampedWithTheTeam()
    {
        Fired(out var world);

        var shot = Assert.Single(world.Entities.OfType<Projectile>());
        Assert.Equal(2, shot.Team);
    }

    [Fact]
    public void SpreadAmmo_FiresOnePelletPerCount_FannedAroundTheAim()
    {
        Fired(out var world, new SpreadAmmo(count: 3, radians: 0.2f));

        var pellets = world.Entities.OfType<Projectile>().ToList();
        Assert.Equal(3, pellets.Count);

        foreach (var p in pellets)
        {
            p.Step(0.1f);
        }

        Assert.True(pellets.Select(p => p.Position.Y).Distinct().Count() > 1, "pellets should diverge");
    }

    [Fact]
    public void PiercingAmmo_SeedsThePierceBudget_SoAShotPassesThroughOneTarget()
    {
        Fired(out var world, new PiercingAmmo(pierces: 1, tileSize: 64f));
        var shot = Assert.Single(world.Entities.OfType<Projectile>());

        shot.RegisterTankHit(System.Guid.NewGuid());
        Assert.True(shot.IsAlive, "a piercing shot survives its first target");

        shot.RegisterTankHit(System.Guid.NewGuid());
        Assert.False(shot.IsAlive, "and stops on the next");
    }

    [Fact]
    public void Modifiers_Stack_SpreadOfBouncingPellets()
    {
        // The headline: holding bouncing ammo and then picking up spread gives a fan of pellets that
        // each still bounce — the two axes are set independently.
        var loadout = new AmmoLoadout();
        new BouncingAmmo(bounces: 3).ApplyTo(loadout);
        new SpreadAmmo(count: 3, radians: 0.18f).ApplyTo(loadout);

        Assert.Equal(3, loadout.SpreadCount);                       // spread axis set…
        Assert.IsType<BouncingBehaviour>(loadout.BehaviourFactory()); // …and the bouncing axis kept
    }

    [Fact]
    public void Modifiers_Stack_RegardlessOfPickupOrder()
    {
        var loadout = new AmmoLoadout();
        new SpreadAmmo(count: 5, radians: 0.1f).ApplyTo(loadout);
        new PiercingAmmo(pierces: 2, tileSize: 64f).ApplyTo(loadout);

        Assert.Equal(5, loadout.SpreadCount);                        // spread kept
        Assert.Equal(2, loadout.Pierce);                             // piercing applied
        Assert.IsType<PiercingBehaviour>(loadout.BehaviourFactory());
    }

    [Fact]
    public void MissileAmmo_StacksWithSpread_FiringAFanOfPiercingMissiles()
    {
        var loadout = new AmmoLoadout();
        new SpreadAmmo(count: 3, radians: 0.2f).ApplyTo(loadout); // spread loaded…
        new MissileAmmo(tileSize: 64f).ApplyTo(loadout);          // …and the missile stacks onto it

        Assert.Equal(3, loadout.SpreadCount); // three missiles, not a single lance
        Assert.Equal(MissileAmmo.Pierce, loadout.Pierce);
        Assert.IsType<PiercingBehaviour>(loadout.BehaviourFactory());
        Assert.Equal(ProjectileStyle.Missile, loadout.Style);
    }

    [Fact]
    public void Default_FiresANormalStyledProjectile()
    {
        Fired(out var world);
        var shot = Assert.Single(world.Entities.OfType<Projectile>());
        Assert.Equal(ProjectileStyle.Normal, shot.Style);
    }

    [Fact]
    public void MissileAmmo_FiresAMissileStyledProjectile()
    {
        Fired(out var world, new MissileAmmo(tileSize: 64f));
        var shot = Assert.Single(world.Entities.OfType<Projectile>());
        Assert.Equal(ProjectileStyle.Missile, shot.Style);
    }

    [Fact]
    public void FiredMissile_PiercesThroughMultipleTanks_DamagingEach()
    {
        var combat = new CombatResolver(hitRadius: 28f);
        var world = new World(combat);
        var arena = new OpenArena();
        var a = new Tank(new NoInput(), world, arena, new Vector2(50f, 0f), 100f, 0.3f, 600f, maxHp: 5, team: 0);
        var b = new Tank(new NoInput(), world, arena, new Vector2(110f, 0f), 100f, 0.3f, 600f, maxHp: 5, team: 0);
        world.Spawn(a);
        world.Spawn(b);

        var loadout = new AmmoLoadout();
        new MissileAmmo(tileSize: 64f).ApplyTo(loadout);
        loadout.Fire(world, arena, Vector2.Zero, new Vector2(1f, 0f), speed: 600f, team: 2);

        for (var i = 0; i < 6; i++)
        {
            world.Step(0.05f);
        }

        Assert.True(a.Hp < a.MaxHp, "the missile should damage the first tank");
        Assert.True(b.Hp < b.MaxHp, "and pierce through to damage the second tank too");
    }

    private sealed class NoInput : IInputSource
    {
        public TankInput Read() => new(Vector2.Zero, Aim: 0f, Fire: false);
    }

    [Fact]
    public void Behaviour_IsOneAxis_PiercingReplacesBouncing()
    {
        var loadout = new AmmoLoadout();
        new BouncingAmmo(bounces: 3).ApplyTo(loadout);
        new PiercingAmmo(pierces: 1, tileSize: 64f).ApplyTo(loadout);

        Assert.IsType<PiercingBehaviour>(loadout.BehaviourFactory()); // last behaviour wins
        Assert.Equal(1, loadout.Pierce);
    }

    [Fact]
    public void Default_FiresAShotWithDamage1()
    {
        Fired(out var world);
        var shot = Assert.Single(world.Entities.OfType<Projectile>());
        Assert.Equal(1, shot.Damage);
    }

    [Fact]
    public void MissileAmmo_SetsDamage3_StampedOnEachPellet()
    {
        var loadout = Fired(out var world, new MissileAmmo(tileSize: 64f));

        Assert.Equal(3, loadout.Damage);
        var shot = Assert.Single(world.Entities.OfType<Projectile>());
        Assert.Equal(3, shot.Damage);
    }

    [Fact]
    public void PiercingAmmo_SetsDamage2()
    {
        var loadout = Fired(out var world, new PiercingAmmo(pierces: 1, tileSize: 64f));

        Assert.Equal(2, loadout.Damage);
        var shot = Assert.Single(world.Entities.OfType<Projectile>());
        Assert.Equal(2, shot.Damage);
    }

    [Fact]
    public void SpreadAmmo_LeavesDamageAlone()
    {
        var loadout = new AmmoLoadout();
        new MissileAmmo(tileSize: 64f).ApplyTo(loadout);
        new SpreadAmmo(count: 3, radians: 0.2f).ApplyTo(loadout);

        Assert.Equal(3, loadout.Damage); // spread is the other axis — the missile punch is kept
    }

    [Fact]
    public void BouncingAmmo_ReplacingTheBehaviourAxis_ResetsDamageTo1()
    {
        var loadout = new AmmoLoadout();
        new MissileAmmo(tileSize: 64f).ApplyTo(loadout);
        new BouncingAmmo(bounces: 3).ApplyTo(loadout);

        Assert.Equal(1, loadout.Damage); // bouncing replaced the missile, so its damage goes too
    }

    [Fact]
    public void Fire_ScalesDamageByTheMultiplier_RoundedWithAFloorOf1()
    {
        var loadout = new AmmoLoadout();
        new MissileAmmo(tileSize: 64f).ApplyTo(loadout); // damage 3

        var world = new World();
        loadout.Fire(world, new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed, team: 0, damageMult: 2f);
        Assert.Equal(6, Assert.Single(world.Entities.OfType<Projectile>()).Damage);

        var weak = new World();
        loadout.Fire(weak, new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed, team: 0, damageMult: 0.1f);
        Assert.Equal(1, Assert.Single(weak.Entities.OfType<Projectile>()).Damage); // never below 1
    }

    [Fact]
    public void Reset_RestoresDamageTo1()
    {
        var loadout = new AmmoLoadout();
        new MissileAmmo(tileSize: 64f).ApplyTo(loadout);

        loadout.Reset();

        Assert.Equal(1, loadout.Damage);
    }

    [Fact]
    public void Reset_ReturnsToTheSingleStraightShot()
    {
        var loadout = new AmmoLoadout();
        new SpreadAmmo(count: 3, radians: 0.2f).ApplyTo(loadout);
        new PiercingAmmo(pierces: 2, tileSize: 64f).ApplyTo(loadout);

        loadout.Reset();

        Assert.Equal(1, loadout.SpreadCount);
        Assert.Equal(0, loadout.Pierce);
        Assert.IsType<StraightBehaviour>(loadout.BehaviourFactory());
    }
}
