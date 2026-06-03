using System.Collections.Generic;

namespace TankGame.Domain;

/// <summary>Resolves interactions among the live entities once per world step — between the
/// entities advancing and the dead being reaped. The combat implementation lands shots on
/// enemy tanks, applies damage, and expires spent shots; the world then reaps whatever died.
/// Pure C# — deterministic and engine-free, so the same pass can run as client prediction
/// today and server authority later (see <c>docs/adr/PROPOSAL-local-first-combat.md</c>).</summary>
public interface ICombatResolver
{
    /// <summary>Resolves combat among <paramref name="entities"/>. May mutate entities
    /// (damage, expiry) but must not add or remove them — the world owns the collection.</summary>
    void Resolve(IReadOnlyCollection<IEntity> entities);
}
