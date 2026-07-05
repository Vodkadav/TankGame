using System.Collections.Generic;
using System.Threading.Tasks;

namespace TankGame.Domain.Net;

/// <summary>One row of the lobby browser: an open, joinable game (mirrors the worker's
/// <c>OpenLobby</c> summary). <paramref name="Map"/> is "" for a random map.</summary>
public readonly record struct OpenLobbyInfo(string Code, GameMode Mode, int Players, string Map);

/// <summary>The lobby directory a networked match starts from (ADR-0019 step 2): creating mints a
/// short code, joining checks a code names a live lobby, and the browser lists every open one.
/// Pure contract — the HTTP implementation lives in Infrastructure; scene tests substitute an
/// in-memory fake.</summary>
public interface ILobbyClient
{
    /// <summary>Creates a fresh lobby and returns its shareable 6-char code, or null when the
    /// service cannot be reached — the UI's cue to show the error and stay put.</summary>
    Task<string?> CreateLobbyAsync();

    /// <summary>Whether <paramref name="code"/> names an existing lobby.</summary>
    Task<bool> JoinLobbyAsync(string code);

    /// <summary>Every currently-joinable lobby for the browser list, or null when the service
    /// cannot be reached (distinct from an empty list, which is a fine, quiet Tuesday).</summary>
    Task<IReadOnlyList<OpenLobbyInfo>?> ListOpenLobbiesAsync();
}
