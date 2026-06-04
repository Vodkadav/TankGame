using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class SeriesTrackerTests
{
    private static SeriesTracker BestOfThree() => new(roundsToWin: 2);

    [Fact]
    public void FreshSeries_HasNoWins_AndIsNotOver()
    {
        var series = BestOfThree();

        Assert.Equal(0, series.WinsFor(0));
        Assert.Equal(0, series.WinsFor(1));
        Assert.False(series.IsMatchOver);
        Assert.Null(series.MatchWinner);
    }

    [Fact]
    public void RecordRound_CreditsTheRoundWinner()
    {
        var series = BestOfThree();

        series.RecordRound(0);

        Assert.Equal(1, series.WinsFor(0));
        Assert.Equal(0, series.WinsFor(1));
        Assert.False(series.IsMatchOver); // one of two needed
    }

    [Fact]
    public void ADrawnRound_CreditsNoOne()
    {
        var series = BestOfThree();

        series.RecordRound(null);

        Assert.Equal(0, series.WinsFor(0));
        Assert.Equal(0, series.WinsFor(1));
        Assert.False(series.IsMatchOver);
    }

    [Fact]
    public void ReachingRoundsToWin_EndsTheMatch_ForThatTeam()
    {
        var series = BestOfThree();

        series.RecordRound(1);
        series.RecordRound(1);

        Assert.True(series.IsMatchOver);
        Assert.Equal(1, series.MatchWinner);
    }

    [Fact]
    public void TradedRounds_AreDecidedByTheFirstToReachTheTarget()
    {
        var series = BestOfThree();

        series.RecordRound(0);
        series.RecordRound(1);
        Assert.False(series.IsMatchOver); // 1 - 1
        series.RecordRound(0);

        Assert.True(series.IsMatchOver);
        Assert.Equal(0, series.MatchWinner); // 2 - 1
    }
}
