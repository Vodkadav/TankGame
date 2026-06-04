using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>Tracks a best-of-N match across rounds: each decided round credits a win to its
/// winner (a draw credits no one), and the match is over once a team reaches
/// <see cref="RoundsToWin"/>. Pure C# — deterministic and engine-free, so the same series can be
/// computed server-side later. Survives a play-scene reload by being held in
/// <c>GameSetup.Series</c>.</summary>
public sealed class SeriesTracker
{
    private readonly Dictionary<int, int> _wins = new();

    /// <param name="roundsToWin">Round wins a team needs to take the match (2 for best-of-three).</param>
    public SeriesTracker(int roundsToWin) => RoundsToWin = roundsToWin;

    /// <summary>Round wins a team needs to win the match.</summary>
    public int RoundsToWin { get; }

    /// <summary>Records a decided round. <paramref name="winningTeam"/> is null for a draw,
    /// which advances no one.</summary>
    public void RecordRound(int? winningTeam)
    {
        if (winningTeam is int team)
        {
            _wins[team] = WinsFor(team) + 1;
        }
    }

    /// <summary>Round wins taken by <paramref name="team"/> so far.</summary>
    public int WinsFor(int team) => _wins.GetValueOrDefault(team);

    /// <summary>True once a team has reached <see cref="RoundsToWin"/>.</summary>
    public bool IsMatchOver => MatchWinner is not null;

    /// <summary>The team that has won the match, or null while it is still in progress.</summary>
    public int? MatchWinner
    {
        get
        {
            foreach (var (team, wins) in _wins)
            {
                if (wins >= RoundsToWin)
                {
                    return team;
                }
            }

            return null;
        }
    }
}
