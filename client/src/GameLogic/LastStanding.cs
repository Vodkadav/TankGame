using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>The networked round's win rule: the round is decided when at most one TEAM still has a
/// tank standing. FFA needs no special case — there every tank is its own team (team = slot), so
/// last-team-standing is last-tank-standing. Pure C#, evaluated by the host each tick.</summary>
public static class LastStanding
{
    /// <summary>The winning team when nobody survived — a drawn round.</summary>
    public const int NoWinner = -1;

    public static (bool Decided, int WinningTeam) Evaluate(IReadOnlyList<(int Team, bool Alive)> tanks)
    {
        var aliveTeam = NoWinner;
        var aliveTeams = 0;
        foreach (var (team, alive) in tanks)
        {
            if (!alive || team == aliveTeam)
            {
                continue;
            }

            if (aliveTeams > 0)
            {
                return (false, NoWinner); // a second team still stands — fight on
            }

            aliveTeam = team;
            aliveTeams = 1;
        }

        return (true, aliveTeam);
    }
}
