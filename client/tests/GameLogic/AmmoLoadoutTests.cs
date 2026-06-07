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
    public void Behaviour_IsOneAxis_PiercingReplacesBouncing()
    {
        var loadout = new AmmoLoadout();
        new BouncingAmmo(bounces: 3).ApplyTo(loadout);
        new PiercingAmmo(pierces: 1, tileSize: 64f).ApplyTo(loadout);

        Assert.IsType<PiercingBehaviour>(loadout.BehaviourFactory()); // last behaviour wins
        Assert.Equal(1, loadout.Pierce);
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
