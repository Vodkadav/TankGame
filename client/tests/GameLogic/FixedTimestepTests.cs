using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class FixedTimestepTests
{
    [Fact]
    public void SmallFrames_AccumulateUntilAFullStepIsDue()
    {
        var timestep = new FixedTimestep();

        Assert.Equal(0, timestep.Advance(0.010f)); // not a full 1/60 yet
        Assert.Equal(1, timestep.Advance(0.010f)); // 0.020 accumulated → one step due
    }

    [Fact]
    public void ANormalFrame_YieldsExactlyOneStep()
    {
        var timestep = new FixedTimestep();

        Assert.Equal(1, timestep.Advance(FixedTimestep.DefaultStepSeconds));
    }

    [Fact]
    public void ALaggyFrame_YieldsCatchUpSteps_SoSimTimeTracksRealTime()
    {
        var timestep = new FixedTimestep();

        Assert.Equal(2, timestep.Advance(0.034f)); // two 1/60 steps fit in a 34 ms frame
    }

    [Fact]
    public void TheRemainder_CarriesIntoTheNextFrame()
    {
        var timestep = new FixedTimestep();

        Assert.Equal(1, timestep.Advance(0.025f)); // ~8.3 ms left over
        Assert.Equal(1, timestep.Advance(0.009f)); // leftover + 9 ms crosses the next step
    }

    [Fact]
    public void AHugeHitch_IsCappedAndTheDebtDropped_NoSpiralOfDeath()
    {
        var timestep = new FixedTimestep();

        Assert.Equal(FixedTimestep.DefaultMaxSubsteps, timestep.Advance(1f)); // capped, not 60
        Assert.Equal(0, timestep.Advance(0.001f)); // the un-run second was dropped, not owed
    }
}
