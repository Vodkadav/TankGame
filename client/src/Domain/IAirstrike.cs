using System.Collections.Generic;
using System.Numerics;

namespace TankGame.Domain;

/// <summary>The phase of one zone of a carpet-bombing airstrike: waiting to be marked, marked (armed) and
/// counting down, or already detonated.</summary>
public enum AirstrikeZonePhase
{
    Pending,
    Armed,
    Detonated,
}

/// <summary>One zone of an <see cref="IAirstrike"/> carpet bomb — where it lands, how big the blast is,
/// what phase it is in, and the seconds until it detonates (for the countdown). Pure data.</summary>
/// <param name="Position">World-space centre of the zone.</param>
/// <param name="Radius">Blast radius.</param>
/// <param name="Phase">Pending / Armed / Detonated.</param>
/// <param name="Countdown">Seconds until this zone detonates (0 once it has).</param>
public readonly record struct AirstrikeZone(Vector2 Position, float Radius, AirstrikeZonePhase Phase, float Countdown);

/// <summary>A carpet-bombing airstrike: a connected run of blast zones that light up red one after another
/// (staggered), then detonate in the same order — each detonation damaging tanks not on the caller's team
/// within its radius. The first zones detonate as the last ones are still lighting up. Pure C# — the
/// Presentation layer draws the zones and the countdown from <see cref="Zones"/>.</summary>
public interface IAirstrike : IEntity
{
    /// <summary>Blast radius of each zone (also the legacy single-ring radius for the iso view).</summary>
    float Radius { get; }

    /// <summary>The zones with their live phase and countdown, in light-up/detonation order.</summary>
    IReadOnlyList<AirstrikeZone> Zones { get; }
}
