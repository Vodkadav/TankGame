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

    public static Choice Resolve(string map, string lobbyCode)
    {
        var seed = StableHash.Of(lobbyCode);
        return map switch
        {
            "CliffsAndValleys" => new Cliffs(),
            "DesertWar" => new Desert(seed),
            "" => (seed & 1) == 0 ? new Desert(seed) : new Cliffs(), // random: a shared coin flip
            _ => new Desert(seed),
        };
    }
}
