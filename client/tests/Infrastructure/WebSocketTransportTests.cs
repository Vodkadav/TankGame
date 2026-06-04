using System;
using System.Collections.Generic;
using TankGame.Domain.Net;
using TankGame.Infrastructure.Net;
using Xunit;

namespace TankGame.Tests.Infrastructure;

// Exercises WebSocketTransport's framing against a fake byte socket — no Godot, no live server.
// The encode/decode round-trip itself is guaranteed by ProtocolCodec's tests (and the cross-language
// byte-vector test); here we only prove the transport routes input out and snapshots in correctly.
public class WebSocketTransportTests
{
    private sealed class FakeSocket : IMatchSocket
    {
        public List<byte[]> Sent { get; } = new();
        public Queue<byte[]> Inbound { get; } = new();

        public void Send(byte[] data) => Sent.Add(data);

        public IReadOnlyList<byte[]> Poll()
        {
            var batch = new List<byte[]>(Inbound);
            Inbound.Clear();
            return batch;
        }
    }

    [Fact]
    public void SendInput_EncodesTheFrameOntoTheSocket()
    {
        var socket = new FakeSocket();
        var transport = new WebSocketTransport(socket);
        var frame = new InputFrame(7, 0.5f, -0.5f, 1.25f, InputFrame.FireBit);

        transport.SendInput(frame);

        Assert.Single(socket.Sent);
        Assert.Equal(ProtocolCodec.EncodeInput(frame), socket.Sent[0]);
    }

    [Fact]
    public void Poll_DecodesEachInboundMessageAndRaisesSnapshotReceived()
    {
        var socket = new FakeSocket();
        var transport = new WebSocketTransport(socket);
        var received = new List<SnapshotFrame>();
        transport.SnapshotReceived += received.Add;

        var first = new SnapshotFrame(
            1, 7, new[] { new TankState(0, 10f, 20f, 0.1f, 0.2f, 100, 0) }, Array.Empty<WallDelta>());
        var second = new SnapshotFrame(
            2, 8, new[] { new TankState(1, 30f, 40f, 0.3f, 0.4f, 80, 1) },
            new[] { new WallDelta(3, 4, 1, 2) });
        socket.Inbound.Enqueue(ProtocolCodec.EncodeSnapshot(first));
        socket.Inbound.Enqueue(ProtocolCodec.EncodeSnapshot(second));

        transport.Poll();

        Assert.Equal(2, received.Count);
        Assert.Equal(1u, received[0].Tick);
        Assert.Equal(7u, received[0].AckSeq);
        Assert.Equal(2u, received[1].Tick);
        Assert.Equal(3, received[1].WallDeltas[0].CellX);
    }

    [Fact]
    public void Poll_WithNoInboundMessages_RaisesNothing()
    {
        var socket = new FakeSocket();
        var transport = new WebSocketTransport(socket);
        var count = 0;
        transport.SnapshotReceived += _ => count++;

        transport.Poll();

        Assert.Equal(0, count);
    }
}
