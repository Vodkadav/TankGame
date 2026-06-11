using System;
using System.Collections.Generic;
using TankGame.Domain.Net;
using Xunit;

namespace TankGame.Tests.Domain;

public class ProtocolCodecTests
{
    [Fact]
    public void InputFrame_RoundTrips()
    {
        var original = new InputFrame(Seq: 42, MoveX: 0.25f, MoveY: -0.5f, Aim: 1.5f, Buttons: InputFrame.FireBit);

        var decoded = ProtocolCodec.DecodeInput(ProtocolCodec.EncodeInput(original));

        Assert.Equal(original, decoded);
        Assert.True(decoded.Fire);
    }

    [Fact]
    public void SnapshotFrame_RoundTrips_WithTanksAndWallDeltas()
    {
        var original = new SnapshotFrame(
            Tick: 100,
            AckSeq: 41,
            Tanks: new List<TankState>
            {
                new(Slot: 0, X: 64f, Y: 128f, Rotation: 0f, TurretRotation: 0.5f, Hp: 3, Team: 0),
                new(Slot: 1, X: 200f, Y: 96f, Rotation: 3.14f, TurretRotation: -1f, Hp: 2, Team: 1),
            },
            WallDeltas: new List<WallDelta>
            {
                new(CellX: 5, CellY: 6, Material: 1, Hp: 2),
                new(CellX: 12, CellY: 0, Material: 0, Hp: 0),
            });

        var decoded = ProtocolCodec.DecodeSnapshot(ProtocolCodec.EncodeSnapshot(original));

        Assert.Equal(original.Tick, decoded.Tick);
        Assert.Equal(original.AckSeq, decoded.AckSeq);
        Assert.Equal(original.Tanks, decoded.Tanks);
        Assert.Equal(original.WallDeltas, decoded.WallDeltas);
    }

    [Fact]
    public void EmptySnapshot_RoundTrips()
    {
        var original = new SnapshotFrame(7, 7, new List<TankState>(), new List<WallDelta>());

        var decoded = ProtocolCodec.DecodeSnapshot(ProtocolCodec.EncodeSnapshot(original));

        Assert.Equal(7u, decoded.Tick);
        Assert.Empty(decoded.Tanks);
        Assert.Empty(decoded.WallDeltas);
    }

    // Cross-language parity anchor: the TypeScript codec MUST produce these exact bytes for the
    // same frame (server/worker/src/protocol/codec.test.ts asserts the identical vector). All
    // float values are exact in IEEE-754 single precision so the bytes are unambiguous.
    [Fact]
    public void InputFrame_EncodesToTheCanonicalByteVector()
    {
        var frame = new InputFrame(Seq: 1, MoveX: 1f, MoveY: -1f, Aim: 0.5f, Buttons: 1);

        var bytes = ProtocolCodec.EncodeInput(frame);

        Assert.Equal(new byte[]
        {
            0x01, 0x00, 0x00, 0x00, // seq = 1
            0x00, 0x00, 0x80, 0x3F, // moveX = 1.0
            0x00, 0x00, 0x80, 0xBF, // moveY = -1.0
            0x00, 0x00, 0x00, 0x3F, // aim = 0.5
            0x01,                   // buttons = fire
        }, bytes);
    }

    // ADR-0019 step 3: inputs gained a kind tag. The relay forwards guest bytes to the HOST CLIENT,
    // whose one socket also carries the welcome — so every message must self-identify, inputs included.
    [Fact]
    public void InputMessage_EncodesToTheTaggedCanonicalByteVector()
    {
        var frame = new InputFrame(Seq: 1, MoveX: 1f, MoveY: -1f, Aim: 0.5f, Buttons: 1);

        var bytes = ProtocolCodec.EncodeInputMessage(frame);

        Assert.Equal(new byte[]
        {
            0x03,                   // MsgInput
            0x01, 0x00, 0x00, 0x00, // seq = 1
            0x00, 0x00, 0x80, 0x3F, // moveX = 1.0
            0x00, 0x00, 0x80, 0xBF, // moveY = -1.0
            0x00, 0x00, 0x00, 0x3F, // aim = 0.5
            0x01,                   // buttons = fire
        }, bytes);
        Assert.Equal(frame, ProtocolCodec.DecodeInput(bytes.AsSpan(1)));
    }

    [Fact]
    public void Welcome_EncodesToTheCanonicalByteVector()
    {
        // Parity anchor — codec.test.ts asserts the identical [MSG_WELCOME, slot] bytes.
        Assert.Equal(new byte[] { 0x01, 0x01 }, ProtocolCodec.EncodeWelcome(1));
        Assert.Equal(new byte[] { 0x01, 0x00 }, ProtocolCodec.EncodeWelcome(0));
        Assert.Equal((byte)1, ProtocolCodec.DecodeWelcome(ProtocolCodec.EncodeWelcome(1)));
    }

    [Fact]
    public void Snapshot_EncodesToTheCanonicalByteVector()
    {
        var frame = new SnapshotFrame(
            Tick: 2,
            AckSeq: 1,
            Tanks: new List<TankState> { new(Slot: 0, X: 64f, Y: 128f, Rotation: 0f, TurretRotation: 0.5f, Hp: 3, Team: 1) },
            WallDeltas: new List<WallDelta> { new(CellX: 5, CellY: 6, Material: 1, Hp: 2) });

        var bytes = ProtocolCodec.EncodeSnapshot(frame);

        Assert.Equal(new byte[]
        {
            0x02, 0x00, 0x00, 0x00, // tick = 2
            0x01, 0x00, 0x00, 0x00, // ackSeq = 1
            0x01,                   // tankCount = 1
            0x00,                   // slot = 0
            0x00, 0x00, 0x80, 0x42, // x = 64.0
            0x00, 0x00, 0x00, 0x43, // y = 128.0
            0x00, 0x00, 0x00, 0x00, // rotation = 0.0
            0x00, 0x00, 0x00, 0x3F, // turret = 0.5
            0x03,                   // hp = 3
            0x01,                   // team = 1
            0x01, 0x00,             // wallCount = 1
            0x05, 0x00,             // cellX = 5
            0x06, 0x00,             // cellY = 6
            0x01,                   // material = 1 (brick)
            0x02,                   // hp = 2
        }, bytes);
    }
}
