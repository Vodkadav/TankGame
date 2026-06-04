using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A weapon that fires a single piercing shot: it passes through <c>pierces</c> targets
/// (tanks or brick walls), damaging each, before the next one stops it; steel always stops it. The
/// pierce budget is seeded on the projectile so both the wall pass (<see cref="PiercingBehaviour"/>)
/// and the combat resolver draw from the same count. Needs the tile size for the wall pass.</summary>
public sealed class PiercingWeapon : IWeapon
{
    private readonly int _pierces;
    private readonly float _tileSize;

    public PiercingWeapon(int pierces, float tileSize)
    {
        _pierces = pierces;
        _tileSize = tileSize;
    }

    public void Fire(IWorld world, IArena arena, Vector2 muzzle, Vector2 direction, float speed, int team) =>
        world.Spawn(new Projectile(arena, muzzle, direction, speed, team: team,
            behaviour: new PiercingBehaviour(_tileSize), pierce: _pierces));
}
