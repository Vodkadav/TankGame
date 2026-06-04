using System.Collections.Generic;

namespace TankGame.Domain.Net;

/// <summary>One client→server input for a simulation tick. <see cref="Seq"/> is the client's
/// monotonically-increasing input number, echoed back as <see cref="SnapshotFrame.AckSeq"/> so the
/// client can discard acknowledged inputs and re-predict the rest (reconciliation). Move is the
/// raw stick/keys intent; <see cref="Aim"/> is the turret angle in radians; <see cref="Buttons"/>
/// is a bitfield (<see cref="FireBit"/>). The wire layout is fixed and mirrored byte-for-byte by
/// the TypeScript server (see <c>server/worker/src/protocol/</c> and ADR-0005).</summary>
public readonly record struct InputFrame(uint Seq, float MoveX, float MoveY, float Aim, byte Buttons)
{
    /// <summary><see cref="Buttons"/> bit set while fire is held.</summary>
    public const byte FireBit = 1 << 0;

    public bool Fire => (Buttons & FireBit) != 0;
}

/// <summary>A single tank's authoritative state in a snapshot. <see cref="Slot"/> is the player
/// slot (0 = host, 1 = guest) — a stable per-match id the client pairs with its view.</summary>
public readonly record struct TankState(
    byte Slot, float X, float Y, float Rotation, float TurretRotation, byte Hp, byte Team);

/// <summary>A change to one wall cell since the last snapshot — so a brick chipped or broken on
/// the server appears for both clients. <see cref="Material"/> is 0 floor / 1 brick / 2 steel.</summary>
public readonly record struct WallDelta(ushort CellX, ushort CellY, byte Material, byte Hp);

/// <summary>One server→client world snapshot at <see cref="Tick"/>. <see cref="AckSeq"/> is the
/// last <see cref="InputFrame.Seq"/> the server applied for the receiving client (the
/// reconciliation anchor). Carries the full tank set plus any wall changes this snapshot.</summary>
public sealed record SnapshotFrame(
    uint Tick, uint AckSeq, IReadOnlyList<TankState> Tanks, IReadOnlyList<WallDelta> WallDeltas);
