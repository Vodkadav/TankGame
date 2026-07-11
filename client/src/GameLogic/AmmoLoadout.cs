using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A tank's current shot, as two independent axes that ammo pickups modify separately so
/// they STACK: a spread pattern (how many pellets, fanned how wide) and a per-pellet behaviour
/// (straight / bouncing / piercing, with a pierce budget). Picking up spread ammo while holding
/// bouncing ammo gives a fan of bouncing pellets, because each axis is set on its own. The default
/// state is one straight pellet — the ordinary tank shot. Pure C#; supersedes the per-combination
/// IWeapon strategies (see <c>docs/adr/0016-composable-ammo.md</c>).</summary>
public sealed class AmmoLoadout
{
    private static readonly Func<IProjectileBehaviour> Straight = () => StraightBehaviour.Instance;

    /// <summary>Pellets per shot (1 = a single shot).</summary>
    public int SpreadCount { get; set; } = 1;

    /// <summary>Angle between adjacent pellets, radians (0 for a single shot).</summary>
    public float SpreadRadians { get; set; }

    /// <summary>Makes the per-pellet behaviour — a fresh one per pellet, so per-shot budgets are
    /// independent. Defaults to a shared straight behaviour.</summary>
    public Func<IProjectileBehaviour> BehaviourFactory { get; set; } = Straight;

    /// <summary>Pierce budget seeded on each pellet (0 = no piercing).</summary>
    public int Pierce { get; set; }

    /// <summary>Damage each pellet deals on impact (1 = the ordinary shot). Part of the behaviour
    /// axis — a missile hits harder, so behaviour modifiers set it and Reset restores 1.</summary>
    public int Damage { get; set; } = 1;

    /// <summary>How the pellets are drawn (ordinary bullet vs missile). Cosmetic; the behaviour axis
    /// modifiers set it (a missile is a missile, a bouncing/piercing/plain shot is a bullet).</summary>
    public ProjectileStyle Style { get; set; } = ProjectileStyle.Normal;

    /// <summary>Returns the loadout to the ordinary single straight shot.</summary>
    public void Reset()
    {
        SpreadCount = 1;
        SpreadRadians = 0f;
        BehaviourFactory = Straight;
        Pierce = 0;
        Damage = 1;
        Style = ProjectileStyle.Normal;
    }

    /// <summary>Spawns this shot — <see cref="SpreadCount"/> pellets fanned symmetrically about the
    /// aim, each its own behaviour and pierce budget. <paramref name="layer"/> is the firing tank's
    /// elevation layer, stamped on every pellet so the shot stays on that level (ADR-0018).</summary>
    /// <paramref name="damageMult"/> scales each pellet's <see cref="Damage"/> (the firer's damage
    /// stat), rounded to the nearest int and never below 1.
    public void Fire(IWorld world, IArena arena, Vector2 muzzle, Vector2 direction, float speed, int team,
        Guid owner = default, int layer = 0, float damageMult = 1f)
    {
        var baseAngle = MathF.Atan2(direction.Y, direction.X);
        var firstOffset = -SpreadRadians * (SpreadCount - 1) / 2f; // centre the fan on the aim
        var damage = Math.Max(1, (int)MathF.Round(Damage * damageMult));

        for (var i = 0; i < SpreadCount; i++)
        {
            var angle = baseAngle + firstOffset + (SpreadRadians * i);
            var pelletDir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            world.Spawn(new Projectile(arena, muzzle, pelletDir, speed, damage: damage, team: team,
                behaviour: BehaviourFactory(), pierce: Pierce, owner: owner, style: Style, layer: layer));
        }
    }
}
