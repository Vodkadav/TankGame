using System.Collections.Generic;
using TankGame.Domain;
using TankGame.Domain.Net;

namespace TankGame.GameLogic;

/// <summary>The authority of a networked match (ADR-0019 step 3): the host client's loop that runs
/// the REAL <see cref="IWorld"/> — the same GameLogic as single-player, no second implementation —
/// from the host's local input plus the guest's relayed input, and broadcasts one
/// <see cref="SnapshotFrame"/> per fixed tick. Wall changes are captured from the grid's own
/// <see cref="IWallGrid.CellChanged"/> event and ride the next snapshot once, so a brick broken on
/// the host breaks on every guest. Pure C# — the net scene owns the fixed-tick cadence and the
/// world's contents; this class only wires relay→input and world→snapshot.</summary>
public sealed class HostSession
{
    private readonly IMatchTransport _transport;
    private readonly IWorld _world;
    private readonly IReadOnlyList<(byte Slot, ITank Tank)> _tanks;
    private readonly RelayedInputSource _guestInput;
    private readonly List<WallDelta> _pendingWallDeltas = new();

    /// <param name="transport">The relay link: relayed guest inputs in, snapshots out.</param>
    /// <param name="world">The authoritative world, already populated by the scene with every
    /// tank in <paramref name="tanks"/>.</param>
    /// <param name="walls">The shared maze; its change events become the snapshots' wall deltas.</param>
    /// <param name="tanks">Each player's slot and the tank that drives it, in snapshot order.</param>
    /// <param name="guestInput">The guest tank's input source, fed from the relayed frames.</param>
    public HostSession(
        IMatchTransport transport,
        IWorld world,
        IWallGrid walls,
        IReadOnlyList<(byte Slot, ITank Tank)> tanks,
        RelayedInputSource guestInput)
    {
        _transport = transport;
        _world = world;
        _tanks = tanks;
        _guestInput = guestInput;

        transport.InputReceived += guestInput.Receive;
        walls.CellChanged += changed => _pendingWallDeltas.Add(new WallDelta(
            (ushort)changed.X, (ushort)changed.Y, (byte)changed.Cell.Material, (byte)changed.Cell.Hp));
    }

    /// <summary>The authoritative tick counter, stamped on each outgoing snapshot.</summary>
    public uint Tick { get; private set; }

    /// <summary>One authoritative tick: advances the world (every tank reads its input source —
    /// keyboard for the host, the relayed frames for the guest), then broadcasts the resulting
    /// snapshot with any wall changes since the last one.</summary>
    public void Step(float deltaSeconds)
    {
        _world.Step(deltaSeconds);
        Tick++;

        var states = new List<TankState>(_tanks.Count);
        foreach (var (slot, tank) in _tanks)
        {
            states.Add(new TankState(
                slot, tank.Position.X, tank.Position.Y, tank.Rotation, tank.TurretRotation,
                (byte)tank.Hp, (byte)tank.Team));
        }

        var deltas = _pendingWallDeltas.Count == 0
            ? (IReadOnlyList<WallDelta>)System.Array.Empty<WallDelta>()
            : new List<WallDelta>(_pendingWallDeltas);
        _pendingWallDeltas.Clear();

        _transport.SendSnapshot(new SnapshotFrame(Tick, _guestInput.LastAppliedSeq, states, deltas));
    }
}
