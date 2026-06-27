using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

// The local player's kill-streak announcer (owner ask 2026-06-18): every kill since the player last
// died escalates Single -> Double -> Triple -> Multi, with no time limit between kills; only a death
// (Reset) ends the streak.
public class KillStreakTrackerTests
{
    [Fact]
    public void FirstKill_IsASingle()
    {
        var streak = new KillStreakTracker();
        Assert.Equal(StreakTier.Single, streak.RegisterKill());
        Assert.Equal(1, streak.Count);
    }

    [Fact]
    public void ConsecutiveKills_Escalate()
    {
        var streak = new KillStreakTracker();
        Assert.Equal(StreakTier.Single, streak.RegisterKill());
        Assert.Equal(StreakTier.Double, streak.RegisterKill());
        Assert.Equal(StreakTier.Triple, streak.RegisterKill());
        Assert.Equal(StreakTier.Multi, streak.RegisterKill());
        Assert.Equal(StreakTier.Multi, streak.RegisterKill()); // stays Multi for 5+
    }

    [Fact]
    public void KillsNeverLapseWithoutADeath()
    {
        // There is no time window: any kill while alive keeps extending the streak, however many.
        var streak = new KillStreakTracker();
        streak.RegisterKill();
        Assert.Equal(StreakTier.Double, streak.RegisterKill());
        Assert.Equal(StreakTier.Triple, streak.RegisterKill());
        Assert.Equal(3, streak.Count);
    }

    [Fact]
    public void Reset_DropsTheStreak()
    {
        var streak = new KillStreakTracker();
        streak.RegisterKill();
        streak.RegisterKill(); // Double
        streak.Reset();
        Assert.Equal(0, streak.Count);
        Assert.Equal(StreakTier.Single, streak.RegisterKill()); // fresh after a death
    }
}
