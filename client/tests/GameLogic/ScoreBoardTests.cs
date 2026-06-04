using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class ScoreBoardTests
{
    [Fact]
    public void FreshBoard_ReportsZeroForEveryTeam()
    {
        var board = new ScoreBoard();

        Assert.Equal(0, board.KillsFor(0));
        Assert.Equal(0, board.KillsFor(1));
    }

    [Fact]
    public void RecordKill_CreditsOnlyThatTeam()
    {
        var board = new ScoreBoard();

        board.RecordKill(0);

        Assert.Equal(1, board.KillsFor(0));
        Assert.Equal(0, board.KillsFor(1));
    }

    [Fact]
    public void RecordKill_Accumulates()
    {
        var board = new ScoreBoard();

        board.RecordKill(1);
        board.RecordKill(1);

        Assert.Equal(2, board.KillsFor(1));
    }

    [Fact]
    public void RecordKill_RaisesChanged()
    {
        var board = new ScoreBoard();
        var raised = 0;
        board.Changed += () => raised++;

        board.RecordKill(0);

        Assert.Equal(1, raised);
    }
}
