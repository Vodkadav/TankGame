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
    }
}
