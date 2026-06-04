using System;
using System.Collections.Generic;
using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain;
using TankGame.Domain.Net;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Drives NetArenaScene against a fake transport: the welcome, snapshots, and per-frame Tick are
// pushed deterministically (no socket, no server), proving the scene predicts the local tank,
// mirrors the remote one, and applies wall deltas — the headless half of the M3 play-scene wiring.
public class NetArenaSceneTests : TestClass
{
    private Func<string, IMatchTransport> _originalFactory = default!;
    private FakeTransport _transport = default!;
    private NetArenaScene _scene = default!;

    public NetArenaSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalFactory = NetworkSession.TransportFactory;
        _transport = new FakeTransport();
        NetworkSession.TransportFactory = _ => _transport;
        NetworkSession.Join("TEST01"); // sets NetworkSession.Active to the fake the scene will read

        _scene = GD.Load<PackedScene>("res://src/Presentation/Arena/NetArena.tscn")
            .Instantiate<NetArenaScene>();
        TestScene.AddChild(_scene); // runs _Ready, which subscribes to the transport
    }

    [Cleanup]
    public void Cleanup()
    {
        _scene.QueueFree();
        NetworkSession.TransportFactory = _originalFactory;
    }

    [Test]
    public void Welcome_AdoptsTheSlotAndCreatesTheLocalTank()
    {
        _transport.DeliverWelcome(0);

        if (_scene.LocalSlot != 0)
        {
            throw new Exception($"Expected local slot 0, got {_scene.LocalSlot}.");
        }
        if (!_scene.Tanks.ContainsKey(0))
        {
            throw new Exception("Welcome should create the local tank view-model.");
        }
    }

    [Test]
    public void Snapshot_MirrorsTheRemoteTank()
    {
        _transport.DeliverWelcome(0);
        _transport.DeliverSnapshot(Snapshot(ackSeq: 0,
            new TankState(0, 10f, 10f, 0f, 0f, 3, 0),
            new TankState(1, 200f, 96f, 0.25f, -1f, 2, 1)));

        if (!_scene.Tanks.TryGetValue(1, out var remote))
        {
            throw new Exception("The snapshot's remote slot should be rendered.");
        }
        if (Math.Abs(remote.Position.X - 200f) > 0.01f || Math.Abs(remote.Position.Y - 96f) > 0.01f)
        {
            throw new Exception($"Remote tank should mirror the snapshot position, was {remote.Position}.");
        }
        if (remote.Hp != 2 || remote.Team != 1)
        {
            throw new Exception("Remote tank should mirror the snapshot health and team.");
        }
    }

    [Test]
    public void Snapshot_ReconcilesTheLocalTankToAuthority()
    {
        _transport.DeliverWelcome(0);
        _transport.DeliverSnapshot(Snapshot(ackSeq: 0, new TankState(0, 100f, 50f, 0f, 0f, 3, 0)));

        var local = _scene.Tanks[0];
        if (Math.Abs(local.Position.X - 100f) > 0.01f || Math.Abs(local.Position.Y - 50f) > 0.01f)
        {
            throw new Exception($"Local tank should reconcile to the authoritative position, was {local.Position}.");
        }
    }

    [Test]
    public void Tick_SendsAnInputFrameEachFrame_OnceWelcomed()
    {
        // Before the welcome, there is no slot to send for.
        _scene.Tick(0.016f);
        if (_transport.Sent.Count != 0)
        {
            throw new Exception("No input should be sent before the welcome.");
        }

        _transport.DeliverWelcome(0);
        _scene.Tick(0.016f);
        _scene.Tick(0.016f);

        if (_transport.Sent.Count != 2)
        {
            throw new Exception($"Expected two input frames sent, got {_transport.Sent.Count}.");
        }
        if (_transport.Sent[0].Seq != 1 || _transport.Sent[1].Seq != 2)
        {
            throw new Exception("Input sequence numbers should increase monotonically.");
        }
    }

    [Test]
    public void Snapshot_AppliesAWallDeltaToTheGrid()
    {
        _transport.DeliverWelcome(0);
        var deltas = new List<WallDelta> { new(5, 5, (byte)CellMaterial.Brick, 2) };
        _transport.DeliverSnapshot(new SnapshotFrame(1, 0, new List<TankState>(), deltas));

        var cell = _scene.CellAt(5, 5);
        if (cell.Material != CellMaterial.Brick || cell.Hp != 2)
        {
            throw new Exception($"Wall delta should set cell (5,5) to brick/2, was {cell.Material}/{cell.Hp}.");
        }
    }

    private static SnapshotFrame Snapshot(uint ackSeq, params TankState[] tanks) =>
        new(1, ackSeq, new List<TankState>(tanks), new List<WallDelta>());

    private sealed class FakeTransport : IMatchTransport
    {
        public List<InputFrame> Sent { get; } = new();
        public event Action<byte>? WelcomeReceived;
        public event Action<SnapshotFrame>? SnapshotReceived;
        public void SendInput(InputFrame input) => Sent.Add(input);
        public void Poll() { }
        public void DeliverWelcome(byte slot) => WelcomeReceived?.Invoke(slot);
        public void DeliverSnapshot(SnapshotFrame snapshot) => SnapshotReceived?.Invoke(snapshot);
    }
}
