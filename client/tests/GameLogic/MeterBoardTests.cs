using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class MeterBoardTests
{
    [Fact]
    public void Record_AccumulatesDamage_PerTeam()
    {
        var board = new MeterBoard();

        board.Record(shooterTeam: 0, victimTeam: 1, amount: 2, killed: false);
        board.Record(shooterTeam: 0, victimTeam: 1, amount: 3, killed: false);
        board.Record(shooterTeam: 1, victimTeam: 0, amount: 1, killed: false);

        Assert.Equal(5, board.DamageBy(0));
        Assert.Equal(1, board.DamageBy(1));
    }

    [Fact]
    public void Record_AKill_CreditsTheShooter_AndADeathToTheVictim()
    {
        var board = new MeterBoard();

        board.Record(shooterTeam: 0, victimTeam: 1, amount: 3, killed: true);

        Assert.Equal(1, board.KillsBy(0));
        Assert.Equal(0, board.DeathsOf(0));
        Assert.Equal(1, board.DeathsOf(1));
        Assert.Equal(0, board.KillsBy(1));
        Assert.Equal(3, board.DamageBy(0)); // the killing blow's damage still counts
    }

    [Fact]
    public void ANonFatalHit_DoesNotChangeKillsOrDeaths()
    {
        var board = new MeterBoard();

        board.Record(shooterTeam: 0, victimTeam: 1, amount: 1, killed: false);

        Assert.Equal(0, board.KillsBy(0));
        Assert.Equal(0, board.DeathsOf(1));
    }

    [Fact]
    public void Record_RaisesChanged()
    {
        var board = new MeterBoard();
        var changed = 0;
        board.Changed += () => changed++;

        board.Record(shooterTeam: 0, victimTeam: 1, amount: 1, killed: false);

        Assert.Equal(1, changed);
    }

    [Fact]
    public void UntouchedTeams_ReadZero()
    {
        var board = new MeterBoard();

        Assert.Equal(0, board.DamageBy(0));
        Assert.Equal(0, board.KillsBy(7));
        Assert.Equal(0, board.DeathsOf(3));
    }
}
