using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>A whole-round rule change (S9): a set of <see cref="StatusEffect"/>s applied to every
/// tank as it spawns — the "everyone starts with effect X" modifier. Reuses the stats machinery
/// (ADR-0012, ADR-0015), so it needs no new combat code. <see cref="None"/> is the default no-op.
/// Pure C# — deterministic and engine-free, so it runs server-side later unchanged.</summary>
public sealed record MatchModifier(IReadOnlyList<StatusEffect> StartingEffects)
{
    /// <summary>No modifier: a default match plays as authored.</summary>
    public static readonly MatchModifier None = new(System.Array.Empty<StatusEffect>());

    /// <summary>An example preset — everyone is permanently faster and rapid-firing.</summary>
    public static readonly MatchModifier Blitz = new(new[]
    {
        Permanent(StatKind.Speed, mult: 1.4f),
        Permanent(StatKind.FireInterval, mult: 0.5f),
    });

    /// <summary>A whole-match effect: <see cref="Stats.Step"/> never expires an effect whose remaining
    /// time stays positive, so an infinite duration lasts the round.</summary>
    public static StatusEffect Permanent(StatKind stat, float mult = 1f, float addFlat = 0f) =>
        new(stat, Mult: mult, AddFlat: addFlat, Seconds: float.PositiveInfinity);

    /// <summary>Applies every starting effect to <paramref name="tank"/>.</summary>
    public void ApplyTo(Tank tank)
    {
        foreach (var effect in StartingEffects)
        {
            tank.ApplyEffect(effect);
        }
    }
}
