using System.Threading.Tasks;

namespace TankGame.Domain.Net;

/// <summary>The lobby directory a networked match starts from (ADR-0019 step 2): hosting mints a
/// short shareable code, joining checks a friend's code names a live lobby. Pure contract — the
/// HTTP implementation lives in Infrastructure; scene tests substitute an in-memory fake.</summary>
public interface ILobbyClient
{
    /// <summary>Creates a fresh lobby and returns its shareable 6-char code, or null when the
    /// service cannot be reached — the UI's cue to show the error and stay put.</summary>
    Task<string?> CreateLobbyAsync();

    /// <summary>Whether <paramref name="code"/> names an existing lobby.</summary>
    Task<bool> JoinLobbyAsync(string code);
}
