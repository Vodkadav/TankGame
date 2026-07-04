using System.Linq;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public sealed class LeaderboardOrderTests
{
    [Fact]
    public void Rank_HigherIsBetter_OrdersDescending_SoTheBiggestNumberIsFirst()
    {
        var items = new[] { 3, 1, 2 };

        Assert.Equal(new[] { 3, 2, 1 }, LeaderboardOrder.Rank(items, x => x, lowerIsBetter: false).ToArray());
    }

    [Fact]
    public void Rank_LowerIsBetter_OrdersAscending_SoFewerDeathsOrLessDamageTakenRanksFirst()
    {
        var items = new[] { 3, 1, 2 };

        Assert.Equal(new[] { 1, 2, 3 }, LeaderboardOrder.Rank(items, x => x, lowerIsBetter: true).ToArray());
    }
}
