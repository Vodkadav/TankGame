using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>A tunable quantity that a <see cref="StatusEffect"/> can modify.</summary>
public enum StatKind
{
    /// <summary>Movement speed in units per second.</summary>
    Speed,

    /// <summary>Minimum seconds between shots (lower is faster — a rapid-fire effect uses
    /// a multiplier below 1).</summary>
    FireInterval,
}

/// <summary>A timed modifier to one <see cref="StatKind"/>: the affected stat becomes
/// <c>(base + AddFlat) × Mult</c> while the effect is live, for <see cref="Seconds"/> seconds.
/// A pure additive effect uses <c>Mult: 1</c>; a pure multiplicative one uses
/// <c>AddFlat: 0</c>. Powerups and traps are "apply a <see cref="StatusEffect"/>".</summary>
public sealed record StatusEffect(StatKind Stat, float Mult, float AddFlat, float Seconds);

/// <summary>A computed stat block: base values composed with a list of expiring
/// <see cref="StatusEffect"/> modifiers. <see cref="Current"/> folds the live effects over a
/// stat's base; <see cref="Step"/> ages the effects and drops the expired ones. Pure C# —
/// deterministic and engine-free, so the same stats run server-side later
/// (<c>docs/adr/0012-stats-and-status-effects.md</c>).</summary>
public sealed class Stats
{
    private readonly Dictionary<StatKind, float> _base;
    private readonly List<ActiveEffect> _active = new();

    public Stats(IReadOnlyDictionary<StatKind, float> baseValues) =>
        _base = new Dictionary<StatKind, float>(baseValues);

    /// <summary>The unmodified base value of <paramref name="kind"/> (0 if it has none).</summary>
    public float Base(StatKind kind) => _base.GetValueOrDefault(kind);

    /// <summary>The value of <paramref name="kind"/> after all live effects: flat additions are
    /// summed onto the base, then the multipliers are applied.</summary>
    public float Current(StatKind kind)
    {
        var addFlat = 0f;
        var mult = 1f;
        foreach (var active in _active)
        {
            if (active.Effect.Stat != kind)
            {
                continue;
            }

            addFlat += active.Effect.AddFlat;
            mult *= active.Effect.Mult;
        }

        return (Base(kind) + addFlat) * mult;
    }

    /// <summary>Adds an effect, live for its <see cref="StatusEffect.Seconds"/>.</summary>
    public void Apply(StatusEffect effect) => _active.Add(new ActiveEffect(effect, effect.Seconds));

    /// <summary>Ages every live effect by <paramref name="deltaSeconds"/> and removes any that
    /// have run out.</summary>
    public void Step(float deltaSeconds)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var remaining = _active[i].Remaining - deltaSeconds;
            if (remaining <= 0f)
            {
                _active.RemoveAt(i);
            }
            else
            {
                _active[i] = _active[i] with { Remaining = remaining };
            }
        }
    }

    private readonly record struct ActiveEffect(StatusEffect Effect, float Remaining);
}
