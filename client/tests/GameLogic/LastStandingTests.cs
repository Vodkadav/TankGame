using System.Collections.Generic;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

// The networked round's win rule: last TEAM standing. FFA needs no special case — every tank is
// its own team there (team = slot), so last-team-standing IS last-tank-standing.
public class LastStandingTests
{
    [Fact]
    public void TwoTeamsAlive_IsUndecided()
    {
        var result = LastStanding.Evaluate(new List<(int Team, bool Alive)>
        {
            (0, true), (1, true), (1, true),
        });

        Assert.False(result.Decided);
    }

    [Fact]
    public void OneTeamLeft_WinsTheRound()
    {
        var result = LastStanding.Evaluate(new List<(int Team, bool Alive)>
        {
            (0, false), (1, true), (1, true), (0, false),
        });

        Assert.True(result.Decided);
        Assert.Equal(1, result.WinningTeam);
    }

    [Fact]
    public void LastTankStanding_WinsAnFfa()
    {
        // FFA: team == slot, so four distinct teams; three dead leaves one team alive.
        var result = LastStanding.Evaluate(new List<(int Team, bool Alive)>
        {
            (0, false), (1, false), (2, true), (3, false),
        });

        Assert.True(result.Decided);
        Assert.Equal(2, result.WinningTeam);
    }

    [Fact]
    public void EveryoneDown_IsADrawnRound()
    {
        var result = LastStanding.Evaluate(new List<(int Team, bool Alive)>
        {
            (0, false), (1, false),
        });

        Assert.True(result.Decided);
        Assert.Equal(LastStanding.NoWinner, result.WinningTeam);
    }
}
