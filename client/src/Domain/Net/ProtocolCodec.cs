using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace TankGame.Domain.Net;

/// <summary>The wire format: little-endian, fixed-layout binary for <see cref="InputFrame"/> and
/// <see cref="SnapshotFrame"/>. Pure C#, no Godot, no dependencies. The TypeScript server encodes
/// and decodes the identical bytes (<c>server/worker/src/protocol/codec.ts</c>); a shared
/// byte-vector test in each language guards against drift. Hand-rolled rather than a serializer
/// library so the format is explicit and trivially portable (ADR-0005).</summary>
public static class ProtocolCodec
{
    /// <summary>Encoded size of an <see cref="InputFrame"/>: seq(4) + move(4+4) + aim(4) + buttons(1).</summary>
    public const int InputFrameSize = 17;

    /// <summary>Encoded size of one <see cref="TankState"/>: slot(1) + 4 floats(16) + hp(1) + team(1).</summary>
    public const int TankStateSize = 19;

    /// <summary>Encoded size of one <see cref="WallDelta"/>: cell(2+2) + material(1) + hp(1).</summary>
    public const int WallDeltaSize = 6;

    public static byte[] EncodeInput(InputFrame frame)
    {
        var buffer = new byte[InputFrameSize];
        var span = buffer.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..], frame.Seq);
        BinaryPrimitives.WriteSingleLittleEndian(span[4..], frame.MoveX);
        BinaryPrimitives.WriteSingleLittleEndian(span[8..], frame.MoveY);
        BinaryPrimitives.WriteSingleLittleEndian(span[12..], frame.Aim);
        span[16] = frame.Buttons;
        return buffer;
    }

    public static InputFrame DecodeInput(ReadOnlySpan<byte> data)
    {
        if (data.Length < InputFrameSize)
        {
            throw new ArgumentException($"input frame needs {InputFrameSize} bytes, got {data.Length}");
        }

        return new InputFrame(
            BinaryPrimitives.ReadUInt32LittleEndian(data[0..]),
            BinaryPrimitives.ReadSingleLittleEndian(data[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(data[8..]),
            BinaryPrimitives.ReadSingleLittleEndian(data[12..]),
            data[16]);
    }

    public static byte[] EncodeSnapshot(SnapshotFrame frame)
    {
        var size = 4 + 4 + 1 + (frame.Tanks.Count * TankStateSize) + 2 + (frame.WallDeltas.Count * WallDeltaSize);
        var buffer = new byte[size];
        var span = buffer.AsSpan();
        var offset = 0;

        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], frame.Tick);
        offset += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], frame.AckSeq);
        offset += 4;

        span[offset++] = (byte)frame.Tanks.Count;
        foreach (var tank in frame.Tanks)
        {
            span[offset++] = tank.Slot;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], tank.X);
            offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], tank.Y);
            offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], tank.Rotation);
            offset += 4;
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], tank.TurretRotation);
            offset += 4;
            span[offset++] = tank.Hp;
            span[offset++] = tank.Team;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], (ushort)frame.WallDeltas.Count);
        offset += 2;
        foreach (var wall in frame.WallDeltas)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], wall.CellX);
            offset += 2;
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], wall.CellY);
            offset += 2;
            span[offset++] = wall.Material;
            span[offset++] = wall.Hp;
        }

        return buffer;
    }

    public static SnapshotFrame DecodeSnapshot(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        var tick = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
        offset += 4;
        var ackSeq = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
        offset += 4;

        var tankCount = data[offset++];
        var tanks = new List<TankState>(tankCount);
        for (var i = 0; i < tankCount; i++)
        {
            var slot = data[offset++];
            var x = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
            offset += 4;
            var y = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
            offset += 4;
            var rotation = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
            offset += 4;
            var turret = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
            offset += 4;
            var hp = data[offset++];
            var team = data[offset++];
            tanks.Add(new TankState(slot, x, y, rotation, turret, hp, team));
        }

        var wallCount = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
        offset += 2;
        var walls = new List<WallDelta>(wallCount);
        for (var i = 0; i < wallCount; i++)
        {
            var cellX = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
            offset += 2;
            var cellY = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
            offset += 2;
            var material = data[offset++];
            var hp = data[offset++];
            walls.Add(new WallDelta(cellX, cellY, material, hp));
        }

        return new SnapshotFrame(tick, ackSeq, tanks, walls);
    }
}
