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
/// slot (0 = host, 1 = guest) — a stable per-match id the client pairs with its view.
/// <see cref="Shield"/> is over-shield points (a guest must see a shielded remote tank, ADR-0019
/// step 4) and <see cref="Layer"/> is the elevation layer the tank stands on (ADR-0018), so a
/// mirrored tank renders at the right height.</summary>
public readonly record struct TankState(
    byte Slot, float X, float Y, float Rotation, float TurretRotation, byte Hp, byte Team,
    byte Shield, byte Layer)
{
    /// <summary>A tank state with no shield, on the ground layer — the pre-step-4 shape, kept so the
    /// call sites and tests that predate networked shield/elevation stay terse.</summary>
    public TankState(byte slot, float x, float y, float rotation, float turretRotation, byte hp, byte team)
        : this(slot, x, y, rotation, turretRotation, hp, team, 0, 0)
    {
    }
}

/// <summary>A change to one wall cell since the last snapshot — so a brick chipped or broken on
/// the server appears for both clients. <see cref="Material"/> is 0 floor / 1 brick / 2 steel.</summary>
public readonly record struct WallDelta(ushort CellX, ushort CellY, byte Material, byte Hp);

/// <summary>One live shot's authoritative state in a snapshot, so a guest can see shots in flight
/// (ADR-0019 step 4) — without this a guest only ever saw tanks teleport between hits. The full live
/// set rides every snapshot; the guest mirrors it as throwaway view-models. <see cref="Rotation"/> is
/// the travel heading (<c>Atan2(dir.X, dir.Y)</c>, the angle the view points a missile along);
/// <see cref="Style"/> is 0 bullet / 1 missile; <see cref="Layer"/> is the elevation layer the shot
/// rides (ADR-0018).</summary>
public readonly record struct ProjectileState(float X, float Y, float Rotation, byte Style, byte Layer);

/// <summary>One server→client world snapshot at <see cref="Tick"/>. <see cref="AckSeq"/> is the
/// last <see cref="InputFrame.Seq"/> the server applied for the receiving client (the
/// reconciliation anchor). Carries the full tank set, the live projectiles, plus any wall changes
/// this snapshot.</summary>
public sealed record SnapshotFrame(
    uint Tick, uint AckSeq, IReadOnlyList<TankState> Tanks, IReadOnlyList<WallDelta> WallDeltas,
    IReadOnlyList<ProjectileState> Projectiles)
{
    /// <summary>A snapshot with no projectiles — the pre-step-4 shape, kept so the many call sites and
    /// tests that predate networked shots stay terse.</summary>
    public SnapshotFrame(
        uint tick, uint ackSeq, IReadOnlyList<TankState> tanks, IReadOnlyList<WallDelta> wallDeltas)
        : this(tick, ackSeq, tanks, wallDeltas, System.Array.Empty<ProjectileState>())
    {
    }
}
