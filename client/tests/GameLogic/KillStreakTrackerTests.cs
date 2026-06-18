using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

// The local player's kill-streak announcer (owner ask 2026-06-18): consecutive kills inside a
// sliding window escalate Single -> Double -> Triple -> Multi; a gap or a death resets the streak.
public class KillStreakTrackerTests
{
    [Fact]
    public void FirstKill_IsASingle()
    {
        var streak = new KillStreakTracker(windowSeconds: 4f);
        Assert.Equal(StreakTier.Single, streak.RegisterKill(now: 0f));
        Assert.Equal(1, streak.Count);
    }

    [Fact]
    public void KillsInsideTheWindow_Escalate()
    {
        var streak = new KillStreakTracker(windowSeconds: 4f);
        Assert.Equal(StreakTier.Single, streak.RegisterKill(0f));
        Assert.Equal(StreakTier.Double, streak.RegisterKill(2f));
        Assert.Equal(StreakTier.Triple, streak.RegisterKill(4f));
        Assert.Equal(StreakTier.Multi, streak.RegisterKill(6f));
        Assert.Equal(StreakTier.Multi, streak.RegisterKill(8f)); // stays Multi for 5+
    }

    [Fact]
    public void AKillAfterTheWindowLapses_StartsFresh()
    {
        var streak = new KillStreakTracker(windowSeconds: 4f);
        streak.RegisterKill(0f);
        Assert.Equal(StreakTier.Double, streak.RegisterKill(3f));
        Assert.Equal(StreakTier.Single, streak.RegisterKill(10f)); // 7s gap > 4s window
        Assert.Equal(1, streak.Count);
    }

    [Fact]
    public void Reset_DropsTheStreak()
    {
        var streak = new KillStreakTracker(windowSeconds: 4f);
        streak.RegisterKill(0f);
        streak.RegisterKill(1f); // Double
        streak.Reset();
        Assert.Equal(0, streak.Count);
        Assert.Equal(StreakTier.Single, streak.RegisterKill(2f)); // fresh despite being inside window
    }
}
