using System.Collections.Generic;
using TankGame.Domain.Net;
using Xunit;

namespace TankGame.Tests.Domain;

// Contract tests for the M3 networking seam against hand-written fakes (no mocking framework,
// per ADR-0003): a loopback transport and a settable clock. They pin the intended semantics that
// the real WebSocket transport (M3-T6) and net clock must honour.
public class MatchTransportContractTests
{
    private sealed class LoopbackTransport : IMatchTransport
    {
        public List<InputFrame> Sent { get; } = new();
        public event System.Action<SnapshotFrame>? SnapshotReceived;

        public void SendInput(InputFrame input) => Sent.Add(input);

        // Test hook standing in for "a snapshot arrived from the server".
        public void DeliverSnapshot(SnapshotFrame snapshot) => SnapshotReceived?.Invoke(snapshot);
    }

    private sealed class FakeClock : INetClock
    {
        public double Now { get; set; }
        public int TickRateHz { get; init; } = 20;
    }

    [Fact]
    public void SendInput_HandsTheFrameToTheTransport()
    {
        var transport = new LoopbackTransport();

        transport.SendInput(new InputFrame(1, 0f, 0f, 0f, 0));
        transport.SendInput(new InputFrame(2, 1f, 0f, 0f, InputFrame.FireBit));

        Assert.Equal(2, transport.Sent.Count);
        Assert.Equal(2u, transport.Sent[1].Seq);
    }

    [Fact]
    public void SnapshotReceived_DeliversTheServerSnapshot()
    {
        var transport = new LoopbackTransport();
        SnapshotFrame? received = null;
        transport.SnapshotReceived += s => received = s;

        transport.DeliverSnapshot(new SnapshotFrame(9, 5, new List<TankState>(), new List<WallDelta>()));

        Assert.NotNull(received);
        Assert.Equal(9u, received!.Tick);
        Assert.Equal(5u, received.AckSeq);
    }

    [Fact]
    public void NetClock_ExposesMonotonicTimeAndTickRate()
    {
        var clock = new FakeClock { TickRateHz = 20 };

        clock.Now = 0.05;
        Assert.Equal(0.05, clock.Now);
        Assert.Equal(20, clock.TickRateHz);
    }
}
