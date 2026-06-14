using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;
using TankGame.Domain.Net;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

// Deterministic prediction + reconciliation (M3-T7). PredictedTank advances the local tank from
// its own inputs immediately (no input lag), and on a host snapshot snaps to authority then replays
// the inputs the host has not yet acknowledged. The movement model mirrors the host's real GameLogic
// Tank (ADR-0019 step 4): move·speed·dt per tick, axis-separated, Tank.CollisionRadius leading edge.
public class PredictedTankTests
{
    // The host's net-arena tank speed and the shared fixed tick — PredictedTank's defaults.
    private const float TankSpeed = 200f;
    private const float TickSeconds = 1f / 20f;
    private const float StepDistance = TankSpeed * TickSeconds; // one tick along a unit axis.

    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    // A wall filling everything at or beyond a world X — the tank can drive up to it but not through.
    private sealed class WallAtX : IArena
    {
        private readonly float _x;
        public WallAtX(float x) => _x = x;
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => point.X >= _x;
    }

    // Feeds the real Tank a fixed intent each Step — the authority side of the parity tests.
    private sealed class ScriptedInput(TankInput value) : IInputSource
    {
        public TankInput Value { get; set; } = value;
        public TankInput Read() => Value;
    }

    private static InputFrame Move(uint seq, float x, float y) => new(seq, x, y, 0f, 0);

    private static SnapshotFrame SnapshotWith(uint ackSeq, byte slot, float x, float y, int hp = 3, int team = 1)
        => new(1, ackSeq,
            new[] { new TankState(slot, x, y, 0f, 0f, (byte)hp, (byte)team) },
            new List<WallDelta>());

    [Fact]
    public void Predict_AdvancesImmediately_AlongTheInput()
    {
        var tank = new PredictedTank(0, new OpenArena(), Vector2.Zero);

        tank.Predict(Move(1, 1f, 0f));

        Assert.Equal(StepDistance, tank.Position.X, precision: 3);
        Assert.Equal(0f, tank.Position.Y, precision: 3);
        Assert.Equal(1, tank.PendingInputCount);
    }

    [Fact]
    public void Predict_ClampsDiagonalToUnitMagnitude()
    {
        var tank = new PredictedTank(0, new OpenArena(), Vector2.Zero);

        tank.Predict(Move(1, 1f, 1f)); // length √2 > 1 — must be normalised, like the server's hypot clamp

        Assert.Equal(StepDistance / 1.41421356f, tank.Position.X, precision: 3);
        Assert.Equal(StepDistance / 1.41421356f, tank.Position.Y, precision: 3);
    }

    [Fact]
    public void Predict_UpdatesTurretFromAim()
    {
        var tank = new PredictedTank(0, new OpenArena(), Vector2.Zero);

        tank.Predict(new InputFrame(1, 0f, 0f, 1.25f, 0));

        Assert.Equal(1.25f, tank.TurretRotation, precision: 3);
    }

    [Fact]
    public void Reconcile_WithNoPendingInputs_SnapsToAuthority()
    {
        var tank = new PredictedTank(1, new OpenArena(), Vector2.Zero);

        tank.Reconcile(SnapshotWith(ackSeq: 0, slot: 1, x: 100f, y: 50f, hp: 2, team: 1));

        Assert.Equal(100f, tank.Position.X, precision: 3);
        Assert.Equal(50f, tank.Position.Y, precision: 3);
        Assert.Equal(2, tank.Hp);
        Assert.Equal(1, tank.Team);
    }

    [Fact]
    public void Reconcile_ReplaysUnacknowledgedInputs_OverAuthority()
    {
        var tank = new PredictedTank(0, new OpenArena(), Vector2.Zero);
        tank.Predict(Move(1, 1f, 0f));
        tank.Predict(Move(2, 1f, 0f));
        tank.Predict(Move(3, 1f, 0f)); // predicted x = 30

        // Server acked only seq 1, agreeing the tank is at x = 10. Seq 2 and 3 are still in flight.
        tank.Reconcile(SnapshotWith(ackSeq: 1, slot: 0, x: StepDistance, y: 0f));

        Assert.Equal(2, tank.PendingInputCount);            // seq 1 dropped, 2 and 3 kept
        Assert.Equal(StepDistance * 3f, tank.Position.X, precision: 3); // 10 (auth) + replay 2,3 → 30
    }

    [Fact]
    public void Reconcile_DiscardsAllAckedInputs_AndRestsAtAuthority()
    {
        var tank = new PredictedTank(0, new OpenArena(), Vector2.Zero);
        tank.Predict(Move(1, 1f, 0f));
        tank.Predict(Move(2, 1f, 0f));

        tank.Reconcile(SnapshotWith(ackSeq: 2, slot: 0, x: StepDistance * 2f, y: 0f));

        Assert.Equal(0, tank.PendingInputCount);
        Assert.Equal(StepDistance * 2f, tank.Position.X, precision: 3);
    }

    [Fact]
    public void Reconcile_AppliesServerCorrection_WhenPredictionDiverged()
    {
        var tank = new PredictedTank(0, new OpenArena(), Vector2.Zero);
        tank.Predict(Move(1, 1f, 0f));
        tank.Predict(Move(2, 1f, 0f));
        tank.Predict(Move(3, 1f, 0f)); // predicted x = 30

        // Server disagrees: it placed the tank at x = 5 at ack seq 1 (e.g. it saw a wall the client
        // mispredicted). Reconcile snaps to 5 and replays seq 2, 3 → 25, pulling the client to truth.
        tank.Reconcile(SnapshotWith(ackSeq: 1, slot: 0, x: 5f, y: 0f));

        Assert.Equal(5f + (StepDistance * 2f), tank.Position.X, precision: 3);
    }

    [Fact]
    public void Predict_StopsAtAWall_LikeTheServer()
    {
        // Wall at x = 24: the leading edge (centre + 24) hits it once the centre would reach 0.
        var tank = new PredictedTank(0, new WallAtX(24f), Vector2.Zero);

        tank.Predict(Move(1, 1f, 0f));

        Assert.Equal(0f, tank.Position.X, precision: 3); // blocked — leading edge 0+10+24 ≥ 24
    }

    // ── ADR-0019 step 4: PredictedTank movement parity with the host's real Tank ──
    // The guest's PredictedTank must replay the host's authoritative Tank step-for-step, or
    // reconciliation jitters. These drive a real Tank (the authority) and a PredictedTank from the
    // SAME inputs against the SAME arena at the SAME spawn, stepping both at the shared 20 Hz tick,
    // and assert the transforms stay in lock-step.

    private const float ParityTolerance = 0.01f;

    // Builds the host's real Tank with the net-arena's speed at a given spawn, on a shared arena.
    private static Tank HostTank(IInputSource input, IArena arena, Vector2 spawn) =>
        new(input, new World(), arena, spawn, TankSpeed, fireInterval: 0.3f, projectileSpeed: 600f, maxHp: 8);

    private static void AssertParity(Tank host, PredictedTank guest)
    {
        Assert.Equal(host.Position.X, guest.Position.X, ParityTolerance);
        Assert.Equal(host.Position.Y, guest.Position.Y, ParityTolerance);
        Assert.Equal(host.Rotation, guest.Rotation, ParityTolerance);
    }

    [Fact]
    public void Movement_MatchesHostTank_OverAStraightAndDiagonalRun()
    {
        var arena = new OpenArena();
        var spawn = new Vector2(200f, 200f);
        var hostInput = new ScriptedInput(new TankInput(Vector2.Zero, Aim: 0f, Fire: false));
        var host = HostTank(hostInput, arena, spawn);
        var guest = new PredictedTank(0, arena, spawn);

        // A varied path: straight east, a (1,1) diagonal (the normalisation case), then straight south.
        var legs = new (Vector2 Move, int Ticks)[]
        {
            (new Vector2(1f, 0f), 5),
            (new Vector2(1f, 1f), 7),
            (new Vector2(0f, 1f), 4),
        };

        uint seq = 0;
        foreach (var (move, ticks) in legs)
        {
            hostInput.Value = new TankInput(move, Aim: 0f, Fire: false);
            for (var i = 0; i < ticks; i++)
            {
                host.Step(TickSeconds);
                guest.Predict(new InputFrame(++seq, move.X, move.Y, 0f, 0));
                AssertParity(host, guest);
            }
        }

        Assert.NotEqual(spawn, guest.Position); // sanity: the run actually moved the tank
    }

    [Fact]
    public void Movement_MatchesHostTank_WhenSlidingIntoAWall()
    {
        // A wall to the east: both the host and the guest must stop their leading edge at it, and the
        // diagonal push must still let them slide south along it — identically.
        var arena = new WallAtX(300f);
        var spawn = new Vector2(200f, 200f);
        var hostInput = new ScriptedInput(new TankInput(new Vector2(1f, 1f), Aim: 0f, Fire: false));
        var host = HostTank(hostInput, arena, spawn);
        var guest = new PredictedTank(0, arena, spawn);

        for (uint seq = 1; seq <= 40; seq++) // long enough to jam the X axis against the wall
        {
            host.Step(TickSeconds);
            guest.Predict(new InputFrame(seq, 1f, 1f, 0f, 0));
            AssertParity(host, guest);
        }

        Assert.True(guest.Position.X < 300f); // the X axis was actually blocked by the wall
        Assert.True(guest.Position.Y > spawn.Y); // yet the Y axis kept sliding past the wall
    }

    [Fact]
    public void Movement_MatchesHostTank_AtACustomSpeedAndTick()
    {
        // Parity is not tied to 200 u/s / 20 Hz: any matched speed+dt pair tracks the host Tank.
        const float speed = 137f;
        const float tick = 1f / 30f;
        var arena = new OpenArena();
        var spawn = Vector2.Zero;
        var hostInput = new ScriptedInput(new TankInput(new Vector2(1f, 0.5f), Aim: 0f, Fire: false));
        var host = new Tank(hostInput, new World(), arena, spawn, speed, 0.3f, 600f, maxHp: 8);
        var guest = new PredictedTank(0, arena, spawn, speed, tick);

        for (uint seq = 1; seq <= 12; seq++)
        {
            host.Step(tick);
            guest.Predict(new InputFrame(seq, 1f, 0.5f, 0f, 0));
            AssertParity(host, guest);
        }
    }

    [Fact]
    public void Reconcile_IgnoresSnapshotsWithoutThisSlot()
    {
        var tank = new PredictedTank(1, new OpenArena(), Vector2.Zero);
        tank.Predict(Move(1, 1f, 0f));

        // Snapshot only carries slot 0 — nothing to reconcile this tank against; prediction stands.
        tank.Reconcile(SnapshotWith(ackSeq: 1, slot: 0, x: 999f, y: 999f));

        Assert.Equal(StepDistance, tank.Position.X, precision: 3);
        Assert.Equal(1, tank.PendingInputCount);
    }

    [Fact]
    public void Reconcile_FromTransportSnapshotEvent_DrivesTheFullLoop()
    {
        var transport = new LoopbackTransport();
        var tank = new PredictedTank(0, new OpenArena(), Vector2.Zero);
        transport.SnapshotReceived += tank.Reconcile;

        // The netplay loop: send intent to the server and predict locally in the same beat.
        var input = Move(1, 1f, 0f);
        transport.SendInput(input);
        tank.Predict(input);

        transport.DeliverSnapshot(SnapshotWith(ackSeq: 1, slot: 0, x: StepDistance, y: 0f));

        Assert.Single(transport.Sent);
        Assert.Equal(0, tank.PendingInputCount);
        Assert.Equal(StepDistance, tank.Position.X, precision: 3);
    }

    private sealed class LoopbackTransport : IMatchTransport
    {
        public List<InputFrame> Sent { get; } = new();
        public event System.Action<byte>? WelcomeReceived;
        public event System.Action<SnapshotFrame>? SnapshotReceived;
        public void SendInput(InputFrame input) => Sent.Add(input);
        public void Poll() { }
        public void DeliverWelcome(byte slot) => WelcomeReceived?.Invoke(slot);
        public void DeliverSnapshot(SnapshotFrame snapshot) => SnapshotReceived?.Invoke(snapshot);
    }
}
