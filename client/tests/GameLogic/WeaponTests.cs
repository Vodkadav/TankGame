using System.Linq;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class WeaponTests
{
    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    private const float Speed = 600f;

    [Fact]
    public void BehaviourWeapon_FiresOneProjectile_StampedWithTheTeam()
    {
        var world = new World();
        var weapon = new BehaviourWeapon(() => StraightBehaviour.Instance);

        weapon.Fire(world, new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed, team: 2);

        var shot = Assert.Single(world.Entities.OfType<Projectile>());
        Assert.Equal(2, shot.Team);
    }

    [Fact]
    public void SpreadWeapon_FiresOneProjectilePerPellet_FannedAroundTheAim()
    {
        var world = new World();
        var weapon = new SpreadWeapon(count: 3, spreadRadians: 0.2f);

        weapon.Fire(world, new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed, team: 0);

        var pellets = world.Entities.OfType<Projectile>().ToList();
        Assert.Equal(3, pellets.Count);

        // Step them and confirm they fan out: not all end at the same Y.
        foreach (var p in pellets)
        {
            p.Step(0.1f);
        }

        Assert.True(pellets.Select(p => p.Position.Y).Distinct().Count() > 1, "pellets should diverge");
    }

    [Fact]
    public void PiercingWeapon_FiresOneShot_ThatCanPassThroughATank()
    {
        var world = new World();
        new PiercingWeapon(pierces: 1, tileSize: 64f)
            .Fire(world, new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed, team: 1);
        var shot = Assert.Single(world.Entities.OfType<Projectile>());

        // It pierces the first tank (stays alive) rather than expiring on contact.
        shot.RegisterTankHit(System.Guid.NewGuid());
        Assert.True(shot.IsAlive, "a piercing shot survives its first target");

        shot.RegisterTankHit(System.Guid.NewGuid());
        Assert.False(shot.IsAlive, "and stops on the next");
    }

    [Fact]
    public void SpreadWeapon_CentrePellet_TravelsAlongTheAim()
    {
        var world = new World();
        new SpreadWeapon(count: 3, spreadRadians: 0.2f)
            .Fire(world, new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed, team: 0);

        // The middle pellet of an odd spread fires straight along the aim (Y stays ~0).
        var centre = world.Entities.OfType<Projectile>().OrderBy(p => p.Position.Y).Skip(1).First();
        centre.Step(0.1f);

        Assert.Equal(0f, centre.Position.Y, precision: 2);
    }
}
