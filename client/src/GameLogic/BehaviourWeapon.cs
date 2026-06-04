using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A weapon that fires a single projectile driven by a behaviour from
/// <c>behaviourFactory</c> — one fresh behaviour per shot (so per-shot counters like a bounce
/// budget are independent). The default tank weapon is this over
/// <see cref="StraightBehaviour"/>; a bouncing weapon is this over a new
/// <see cref="BouncingBehaviour"/> each shot.</summary>
public sealed class BehaviourWeapon : IWeapon
{
    private readonly Func<IProjectileBehaviour> _behaviourFactory;

    public BehaviourWeapon(Func<IProjectileBehaviour> behaviourFactory) => _behaviourFactory = behaviourFactory;

    public void Fire(IWorld world, IArena arena, Vector2 muzzle, Vector2 direction, float speed, int team) =>
        world.Spawn(new Projectile(arena, muzzle, direction, speed, team: team, behaviour: _behaviourFactory()));
}
