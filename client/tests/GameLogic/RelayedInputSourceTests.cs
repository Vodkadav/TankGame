using System.Numerics;
using TankGame.Domain;
using TankGame.Domain.Net;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class RelayedInputSourceTests
{
    [Fact]
    public void Read_BeforeAnyFrame_IsNeutral()
    {
        var source = new RelayedInputSource();

        var input = source.Read();

        Assert.Equal(Vector2.Zero, input.Move);
        Assert.False(input.Fire);
        Assert.Equal(0u, source.LastAppliedSeq);
    }

    [Fact]
    public void Receive_MapsTheFrame_IntoTheNextRead()
    {
        var source = new RelayedInputSource();

        source.Receive(new InputFrame(Seq: 3, MoveX: 1f, MoveY: -0.5f, Aim: 0.25f, Buttons: InputFrame.FireBit));
        var input = source.Read();

        Assert.Equal(new Vector2(1f, -0.5f), input.Move);
        Assert.Equal(0.25f, input.Aim);
        Assert.True(input.Fire);
        Assert.Equal(3u, source.LastAppliedSeq);
    }

    [Fact]
    public void Receive_DropsAStaleOutOfOrderFrame()
    {
        var source = new RelayedInputSource();

        source.Receive(new InputFrame(Seq: 5, MoveX: 1f, MoveY: 0f, Aim: 0f, Buttons: 0));
        source.Receive(new InputFrame(Seq: 3, MoveX: -1f, MoveY: 0f, Aim: 0f, Buttons: 0));
        var input = source.Read();

        Assert.Equal(new Vector2(1f, 0f), input.Move); // the late seq-3 frame must not regress the intent
        Assert.Equal(5u, source.LastAppliedSeq);
    }

    [Fact]
    public void LastAppliedSeq_OnlyAdvances_WhenTheIntentIsActuallyRead()
    {
        var source = new RelayedInputSource();

        source.Receive(new InputFrame(Seq: 7, MoveX: 0f, MoveY: 1f, Aim: 0f, Buttons: 0));

        Assert.Equal(0u, source.LastAppliedSeq); // received but not yet fed into a world step
        source.Read();
        Assert.Equal(7u, source.LastAppliedSeq);
    }
}
