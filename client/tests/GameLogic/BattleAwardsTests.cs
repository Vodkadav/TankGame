using System.Linq;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

// The end-of-match award tags (owner feedback 2026-06-11): "most deadly", "most evasive", etc.,
// computed from the BattleStats tallies with deterministic tie-breaks.
public class BattleAwardsTests
{
    private static BattleStats.TankTally Tally(
        string name, int kills = 0, int deaths = 0, int hits = 0, int misses = 0,
        int shots = 0, int dealt = 0, int taken = 0) => new()
    {
        Name = name,
        Kills = kills,
        Deaths = deaths,
        Hits = hits,
        Misses = misses,
        ShotsFired = shots,
        DamageDealt = dealt,
        DamageTaken = taken,
    };

    [Fact]
    public void MostDeadly_GoesToTheTopKiller_DamageBreaksTies()
    {
        var tallies = new[]
        {
            Tally("Greg", kills: 2, dealt: 5),
            Tally("Kevin", kills: 2, dealt: 9),
            Tally("Crouton", kills: 1, dealt: 20),
        };

        var awards = BattleAwards.Compute(tallies);

        Assert.Equal("Kevin", awards.Single(a => a.Kind == AwardKind.MostDeadly).Winner.Name);
    }

    [Fact]
    public void MostDeadly_IsNotAwarded_WhenNobodyKilledAnything()
    {
        var awards = BattleAwards.Compute(new[] { Tally("Greg"), Tally("Kevin") });

        Assert.DoesNotContain(awards, a => a.Kind == AwardKind.MostDeadly);
    }

    [Fact]
    public void MostEvasive_GoesToTheFewestDeaths_LeastDamageTakenBreaksTies()
    {
        var tallies = new[]
        {
            Tally("Greg", deaths: 0, taken: 3),
            Tally("Kevin", deaths: 0, taken: 1),
            Tally("Crouton", deaths: 2, taken: 0),
        };

        var awards = BattleAwards.Compute(tallies);

        Assert.Equal("Kevin", awards.Single(a => a.Kind == AwardKind.MostEvasive).Winner.Name);
    }

    [Fact]
    public void Sharpshooter_GoesToTheBestAccuracy_WithAMinimumSample()
    {
        var tallies = new[]
        {
            Tally("Greg", hits: 1, misses: 0, shots: 1),   // 100% but only one shot — no sample
            Tally("Kevin", hits: 6, misses: 2, shots: 8),  // 75%
            Tally("Crouton", hits: 3, misses: 3, shots: 6), // 50%
        };

        var awards = BattleAwards.Compute(tallies);

        Assert.Equal("Kevin", awards.Single(a => a.Kind == AwardKind.Sharpshooter).Winner.Name);
    }

    [Fact]
    public void Sharpshooter_IsNotAwarded_WhenNobodyShotEnough()
    {
        var awards = BattleAwards.Compute(new[]
        {
            Tally("Greg", hits: 1, shots: 1),
            Tally("Kevin", hits: 2, shots: 2),
        });

        Assert.DoesNotContain(awards, a => a.Kind == AwardKind.Sharpshooter);
    }

    [Fact]
    public void BulletSponge_GoesToTheMostDamageTaken()
    {
        var tallies = new[]
        {
            Tally("Greg", taken: 2),
            Tally("Kevin", taken: 11),
        };

        var awards = BattleAwards.Compute(tallies);

        Assert.Equal("Kevin", awards.Single(a => a.Kind == AwardKind.BulletSponge).Winner.Name);
    }

    // ── The banner's champion (victory screen v2): the winning team's top killer ──

    [Fact]
    public void Champion_IsTheWinningTeamsTopKiller_DamageBreaksTies()
    {
        var tallies = new[]
        {
            Tally("Greg", kills: 5, dealt: 9),   // not on the winning team — irrelevant
            Tally("Kevin", kills: 2, dealt: 3),
            Tally("Crouton", kills: 2, dealt: 7),
        };
        tallies[0].Team = 1;
        tallies[1].Team = 0;
        tallies[2].Team = 0;

        Assert.Equal("Crouton", BattleAwards.Champion(tallies, winningTeam: 0)!.Name);
    }

    [Fact]
    public void Champion_IsNull_OnADraw()
    {
        Assert.Null(BattleAwards.Champion(new[] { Tally("Greg") }, winningTeam: null));
    }

    [Fact]
    public void OneTank_CanSweepSeveralAwards()
    {
        var tallies = new[]
        {
            Tally("Greg", kills: 3, deaths: 0, hits: 9, misses: 1, shots: 10, dealt: 9, taken: 8),
            Tally("Kevin", kills: 0, deaths: 3, hits: 0, misses: 5, shots: 5, dealt: 0, taken: 2),
        };

        var awards = BattleAwards.Compute(tallies);
        var gregsAwards = awards.Where(a => a.Winner.Name == "Greg").Select(a => a.Kind).ToList();

        Assert.Contains(AwardKind.MostDeadly, gregsAwards);
        Assert.Contains(AwardKind.MostEvasive, gregsAwards);
        Assert.Contains(AwardKind.Sharpshooter, gregsAwards);
        Assert.Contains(AwardKind.BulletSponge, gregsAwards);
    }
}
