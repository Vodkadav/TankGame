using System.Collections.Generic;
using System.Linq;

namespace TankGame.GameLogic;

/// <summary>Resolves a lobby's map id into a level every member can build for itself — the wire
/// carries only the id, so the choice must be derivable identically on the host and every guest.
/// "" (random) and the Desert generator seed both derive from the shared lobby code via
/// <see cref="StableHash"/>. A custom-map id ("custom:…") falls back to Desert for now: a guest
/// does not have the creator's map file, and syncing map content over the lobby channel is a
/// follow-up — better a wrong map than a crashed match.</summary>
public static class NetMapPick
{
    public abstract record Choice;

    /// <summary>Generate a Desert War arena with this seed (same seed on every member).</summary>
    public sealed record Desert(int Seed) : Choice;

    /// <summary>The hand-authored Cliffs &amp; Valleys map — deterministic from code alone.</summary>
    public sealed record Cliffs : Choice;

    /// <summary>A registered code arena (<see cref="ArenaBuilders"/>) built by its id name — deterministic
    /// from code alone, so a guest builds the very same level the host chose.</summary>
    public sealed record BuiltIn(string ArenaId) : Choice;

    public static Choice Resolve(string map, string lobbyCode)
    {
        var seed = StableHash.Of(lobbyCode);
        return map switch
        {
            "CliffsAndValleys" => new Cliffs(),
            "DesertWar" => new Desert(seed),
            "" => RandomPick(seed), // random: a shared draw over the whole built-in pool
            _ when ArenaBuilders.Has(map) => new BuiltIn(map), // a themed code arena — same on every member
            _ => new Desert(seed),
        };
    }

    // The random ("") pick draws from every built-in — Desert, Cliffs, and each themed arena — by a
    // shared, deterministic index off the lobby seed, so every member lands on the same map. The pool is
    // ordinally sorted so its order is identical on every member regardless of registry internals.
    private static Choice RandomPick(int seed)
    {
        var pool = new List<string> { "DesertWar", "CliffsAndValleys" };
        pool.AddRange(ArenaBuilders.Ids);
        pool.Sort(System.StringComparer.Ordinal);
        var pick = pool[(int)((uint)seed % (uint)pool.Count)];
        return pick switch
        {
            "DesertWar" => new Desert(seed),
            "CliffsAndValleys" => new Cliffs(),
            _ => new BuiltIn(pick),
        };
    }
}
