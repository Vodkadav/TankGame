using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;
using TankGame.Domain.Net;

namespace TankGame.GameLogic;

/// <summary>Client-side prediction + reconciliation for the local player's tank (M3-T7). The
/// server is authoritative, but waiting a round-trip before the local tank moves would feel laggy,
/// so the client <see cref="Predict"/>s each input immediately and buffers it. When a snapshot
/// arrives, <see cref="Reconcile"/> snaps the tank to the server's authoritative state and replays
/// only the inputs the server has not yet acknowledged — so a correct prediction is invisible and a
/// wrong one is pulled smoothly toward the truth.
///
/// The movement model mirrors the server sim (<c>server/worker/src/sim/matchSim.ts</c>) exactly —
/// 200 u/s, the same axis-separated leading-edge wall collision — so that replaying the same inputs
/// reproduces the same path. Pure C#: the wall source is the same <see cref="IArena"/> the local
/// tank uses; no Godot, no transport (the caller wires snapshot delivery to
/// <see cref="Reconcile"/>).</summary>
public sealed class PredictedTank
{
    // Mirror the server constants. Speed and tick rate must match matchSim.ts (TANK_SPEED, 20 Hz),
    // and the collision radius the client's Tank (Tank.CollisionRadius) — otherwise replayed inputs
    // would diverge from the server path and reconciliation would jitter.
    private const float TankSpeed = 200f;
    private const float TickSeconds = 1f / 20f;
    private const float StepDistance = TankSpeed * TickSeconds;
    private const float CollisionRadius = 24f;

    private readonly IArena _arena;
    private readonly List<InputFrame> _pending = new();

    /// <param name="slot">This tank's authoritative slot (0 host / 1 guest) — the one to read from
    /// each snapshot and the inputs of which the server acknowledges.</param>
    /// <param name="arena">Wall source for collision, the same the local tank drives against.</param>
    /// <param name="spawn">Initial predicted position until the first snapshot arrives.</param>
    public PredictedTank(byte slot, IArena arena, Vector2 spawn)
    {
        Slot = slot;
        _arena = arena;
        Position = spawn;
    }

    public byte Slot { get; }
    public Vector2 Position { get; private set; }
    public float Rotation { get; private set; }
    public float TurretRotation { get; private set; }

    /// <summary>Authoritative hit points from the last reconciled snapshot.</summary>
    public int Hp { get; private set; }

    /// <summary>Authoritative team from the last reconciled snapshot.</summary>
    public int Team { get; private set; }

    /// <summary>Inputs sent to the server but not yet acknowledged by a snapshot — the ones replayed
    /// on top of each authoritative correction.</summary>
    public int PendingInputCount => _pending.Count;

    /// <summary>Applies one tick of local intent: buffers it (so it can be replayed after a
    /// correction) and advances the predicted transform immediately.</summary>
    public void Predict(InputFrame input)
    {
        _pending.Add(input);
        ApplyMovement(input);
    }

    /// <summary>Reconciles against an authoritative snapshot: snap this slot's transform and health
    /// to the server, drop every input the server has acknowledged, and replay the rest so the
    /// predicted position stays ahead of the acknowledged truth. A snapshot that does not carry this
    /// slot is ignored (nothing authoritative to correct against).</summary>
    public void Reconcile(SnapshotFrame snapshot)
    {
        if (!TryFindSlot(snapshot, out var authoritative))
        {
            return;
        }

        Position = new Vector2(authoritative.X, authoritative.Y);
        Rotation = authoritative.Rotation;
        TurretRotation = authoritative.TurretRotation;
        Hp = authoritative.Hp;
        Team = authoritative.Team;

        _pending.RemoveAll(frame => frame.Seq <= snapshot.AckSeq);
        foreach (var frame in _pending)
        {
            ApplyMovement(frame);
        }
    }

    private bool TryFindSlot(SnapshotFrame snapshot, out TankState state)
    {
        foreach (var tank in snapshot.Tanks)
        {
            if (tank.Slot == Slot)
            {
                state = tank;
                return true;
            }
        }

        state = default;
        return false;
    }

    // The server's stepTank movement, reproduced: clamp the intent to unit magnitude, move each
    // axis independently and only if the leading edge clears a wall, face the movement direction,
    // and aim the turret at the input.
    private void ApplyMovement(InputFrame input)
    {
        var move = new Vector2(input.MoveX, input.MoveY);
        if (move.LengthSquared() > 1f)
        {
            move = Vector2.Normalize(move);
        }

        var position = Position;
        if (move.X != 0f)
        {
            var nextX = position.X + (move.X * StepDistance);
            var edgeX = nextX + (System.MathF.Sign(move.X) * CollisionRadius);
            if (!_arena.IsBlocked(new Vector2(edgeX, position.Y)))
            {
                position.X = nextX;
            }
        }
        if (move.Y != 0f)
        {
            var nextY = position.Y + (move.Y * StepDistance);
            var edgeY = nextY + (System.MathF.Sign(move.Y) * CollisionRadius);
            if (!_arena.IsBlocked(new Vector2(position.X, edgeY)))
            {
                position.Y = nextY;
            }
        }
        Position = position;

        if (move != Vector2.Zero)
        {
            Rotation = System.MathF.Atan2(move.Y, move.X);
        }
        TurretRotation = input.Aim;
    }
}
