using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Outcome of a match at a point in time.</summary>
/// <param name="Decided">True once at most one team still has a living tank.</param>
/// <param name="WinningTeam">The surviving team, or <c>null</c> for a draw (no team left).
/// Meaningless while <see cref="Decided"/> is false.</param>
public readonly record struct MatchResult(bool Decided, int? WinningTeam);

/// <summary>Decides a last-team-standing match by inspecting the live tanks: while two or more
/// teams have a tank alive the match is ongoing; once one (or zero) remains it is decided.
/// Pure C# — deterministic and engine-free, so the same rule can run server-side later.</summary>
public sealed class MatchTracker
{
    public MatchResult Evaluate(IReadOnlyCollection<IEntity> entities)
    {
        var liveTeams = new HashSet<int>();
        foreach (var entity in entities)
        {
            if (entity is ITank { IsAlive: true } tank)
            {
                liveTeams.Add(tank.Team);
            }
        }

        if (liveTeams.Count >= 2)
        {
            return new MatchResult(Decided: false, WinningTeam: null);
        }

        int? winner = null;
        foreach (var team in liveTeams)
        {
            winner = team; // the single surviving team, if any
        }

        return new MatchResult(Decided: true, WinningTeam: winner);
    }
}
