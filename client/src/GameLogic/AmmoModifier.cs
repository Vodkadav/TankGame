using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>An ammo pickup's effect on a tank's <see cref="AmmoLoadout"/> — it sets ONE axis, leaving
/// the others as they are, so pickups stack (a spread modifier keeps a bouncing behaviour, etc.).
/// See <c>docs/adr/0016-composable-ammo.md</c>.</summary>
public abstract class AmmoModifier
{
    public abstract void ApplyTo(AmmoLoadout loadout);
}

/// <summary>Turns the shot into a fan of <c>count</c> pellets, <c>radians</c> apart — the spread axis.
/// Leaves the per-pellet behaviour untouched, so a spread of bouncing pellets is possible.</summary>
public sealed class SpreadAmmo : AmmoModifier
{
    private readonly int _count;
    private readonly float _radians;

    public SpreadAmmo(int count, float radians)
    {
        _count = count;
        _radians = radians;
    }

    public override void ApplyTo(AmmoLoadout loadout)
    {
        loadout.SpreadCount = _count;
        loadout.SpreadRadians = _radians;
    }
}

/// <summary>Makes each pellet ricochet off walls for <c>bounces</c> hops — the behaviour axis. Leaves
/// the spread untouched, and clears any pierce budget (a pellet bounces or pierces, not both).</summary>
public sealed class BouncingAmmo : AmmoModifier
{
    private readonly int _bounces;

    public BouncingAmmo(int bounces) => _bounces = bounces;

    public override void ApplyTo(AmmoLoadout loadout)
    {
        loadout.BehaviourFactory = () => new BouncingBehaviour(_bounces);
        loadout.Pierce = 0;
        loadout.Damage = 1;
        loadout.Style = ProjectileStyle.Normal;
    }
}

/// <summary>Makes each pellet punch through <c>pierces</c> targets (tanks or brick) before stopping —
/// the behaviour axis. Leaves the spread untouched. Needs the tile size for the wall pass.</summary>
public sealed class PiercingAmmo : AmmoModifier
{
    private readonly int _pierces;
    private readonly float _tileSize;

    public PiercingAmmo(int pierces, float tileSize)
    {
        _pierces = pierces;
        _tileSize = tileSize;
    }

    public override void ApplyTo(AmmoLoadout loadout)
    {
        loadout.BehaviourFactory = () => new PiercingBehaviour(_tileSize);
        loadout.Pierce = _pierces;
        loadout.Damage = 2; // a piercing round hits harder than the plain shot
        loadout.Style = ProjectileStyle.Normal;
    }
}

/// <summary>A missile: a shot with a huge pierce budget that plows through a whole line of tanks and
/// destructible walls — damaging everything in its wake — and only stops at steel (or the map's steel
/// border). It is the behaviour axis (piercing) plus the missile style, and it STACKS with the spread
/// axis: collecting spread as well fires a fan of missiles. Reuses the piercing behaviour.</summary>
public sealed class MissileAmmo : AmmoModifier
{
    /// <summary>How many tanks/destructible cells the missile can punch through — far more than a field
    /// is wide, so it never runs out before steel or the border.</summary>
    public const int Pierce = 999;

    private readonly float _tileSize;

    public MissileAmmo(float tileSize) => _tileSize = tileSize;

    public override void ApplyTo(AmmoLoadout loadout)
    {
        // Leaves the spread axis untouched, so spread + missile fires three missiles (the pickups stack).
        loadout.BehaviourFactory = () => new PiercingBehaviour(_tileSize);
        loadout.Pierce = Pierce;
        loadout.Damage = 3; // the heavy round: three points per victim in its wake
        loadout.Style = ProjectileStyle.Missile;
    }
}
