using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>The gray placeholder names a lobby room shows on its un-joined seats — and the names
/// the AI tanks that fill those seats carry into the match. Seeded by the room's lobby code with a
/// stable hash, so every member of the same room sees the same cast (string.GetHashCode is
/// per-process-randomised and would desync them) and the host's AI fill matches what the room
/// promised. Pure C# — no Godot.</summary>
public static class LobbySeats
{
    /// <summary>One placeholder name per seat, deterministic for a lobby code.</summary>
    public static IReadOnlyList<string> PlaceholderNames(string lobbyCode, int seats)
    {
        var names = new TankNameGenerator(StableHash.Of(lobbyCode));
        var placeholders = new string[seats];
        for (var i = 0; i < seats; i++)
        {
            placeholders[i] = names.Next();
        }

        return placeholders;
    }
}
