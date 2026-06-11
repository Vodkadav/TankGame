using System;
using System.Collections.Generic;
using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain;
using TankGame.Domain.Net;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Drives the 3D networked play scene (ADR-0019 step 3) against a fake transport, both roles:
// the HOST runs the real World (HostSession) and broadcasts snapshots; a GUEST predicts its own
// tank and mirrors the rest from snapshots. No socket, no relay — the fake delivers frames.
public class NetArena3DSceneTests : TestClass
{
    private sealed class FakeTransport : IMatchTransport
    {
        public List<InputFrame> SentInputs { get; } = new();
        public List<SnapshotFrame> Broadcast { get; } = new();
        public event Action<byte>? WelcomeReceived;
        public event Action<SnapshotFrame>? SnapshotReceived;
        public event Action<InputFrame>? InputReceived;

        public void SendInput(InputFrame input) => SentInputs.Add(input);
        public void SendSnapshot(SnapshotFrame snapshot) => Broadcast.Add(snapshot);
        public void Poll() { }

        public void DeliverWelcome(byte slot) => WelcomeReceived?.Invoke(slot);
        public void DeliverSnapshot(SnapshotFrame snapshot) => SnapshotReceived?.Invoke(snapshot);
        public void DeliverInput(InputFrame frame) => InputReceived?.Invoke(frame);
    }

    private Func<string, IMatchTransport> _originalFactory = default!;
    private FakeTransport _transport = default!;
    private NetArena3DScene _scene = default!;

    public NetArena3DSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalFactory = NetworkSession.TransportFactory;
        _transport = new FakeTransport();
        NetworkSession.TransportFactory = _ => _transport;
        NetworkSession.Join("ABC123");

        _scene = GD.Load<PackedScene>("res://src/Presentation/Arena/NetArena3D.tscn")
            .Instantiate<NetArena3DScene>();
        TestScene.AddChild(_scene);
    }

    // Free immediately + force GC (the Arena3D teardown-leak pattern): no managed wrapper to a freed
    // Godot resource may survive to engine shutdown.
    [Cleanup]
    public void Cleanup()
    {
        if (GodotObject.IsInstanceValid(_scene))
        {
            _scene.Free();
        }

        NetworkSession.TransportFactory = _originalFactory;
        NetworkSession.Reset();
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [Test]
    public void GuestWelcome_AdoptsTheSlot_AndShowsItsOwnTank()
    {
        _transport.DeliverWelcome(1);

        if (_scene.LocalSlot != 1)
        {
            throw new Exception($"Expected local slot 1, got {_scene.LocalSlot}.");
        }

        if (!_scene.Tanks.ContainsKey(1))
        {
            throw new Exception("The welcomed guest must have its own (predicted) tank.");
        }
    }

    [Test]
    public void GuestSnapshot_MirrorsTheHostTank_AndAppliesWallDeltas()
    {
        _transport.DeliverWelcome(1);
        _transport.DeliverSnapshot(new SnapshotFrame(1, 0,
            new List<TankState> { new(0, 96f, 160f, 0.5f, 1f, 8, 0) },
            new List<WallDelta> { new(3, 1, 0, 0) })); // the cell at (3,1) broke to floor

        if (!_scene.Tanks.TryGetValue(0, out var host))
        {
            throw new Exception("A snapshot slot the guest has not seen must be mirrored into a tank.");
        }

        if (Math.Abs(host.Position.X - 96f) > 0.01f || Math.Abs(host.Position.Y - 160f) > 0.01f)
        {
            throw new Exception($"The remote tank must mirror the snapshot; got {host.Position}.");
        }

        if (_scene.CellAt(3, 1).Material != CellMaterial.Floor)
        {
            throw new Exception("A wall delta must be applied to the shared grid.");
        }
    }

    [Test]
    public void GuestTick_SendsOneInputFrame_PerFixedTick()
    {
        _transport.DeliverWelcome(1);

        _scene.Tick(0.06f);  // one 20 Hz tick (0.01 carried over)
        _scene.Tick(0.02f);  // not enough for another
        _scene.Tick(0.03f);  // accumulates past the second tick

        if (_transport.SentInputs.Count != 2)
        {
            throw new Exception($"Expected 2 input frames over 0.11s at 20 Hz, sent {_transport.SentInputs.Count}.");
        }

        if (_transport.SentInputs[1].Seq != _transport.SentInputs[0].Seq + 1)
        {
            throw new Exception("Input frames must carry a monotonically increasing seq.");
        }
    }

    [Test]
    public void HostWelcome_BuildsTheAuthoritativeMatch_AndBroadcastsSnapshots()
    {
        _transport.DeliverWelcome(0);

        if (_scene.LocalSlot != 0)
        {
            throw new Exception($"Expected local slot 0, got {_scene.LocalSlot}.");
        }

        if (!_scene.Tanks.ContainsKey(0) || !_scene.Tanks.ContainsKey(1))
        {
            throw new Exception("The host must build the authoritative world with both players' tanks.");
        }

        _scene.Tick(0.05f);

        if (_transport.Broadcast.Count != 1)
        {
            throw new Exception($"One 20 Hz tick must broadcast one snapshot; sent {_transport.Broadcast.Count}.");
        }
    }

    [Test]
    public void HostTick_DrivesTheGuestTank_FromARelayedInput()
    {
        _transport.DeliverWelcome(0);
        var guestStart = _scene.Tanks[1].Position;
        _transport.DeliverInput(new InputFrame(Seq: 1, MoveX: 1f, MoveY: 0f, Aim: 0f, Buttons: 0));

        for (var i = 0; i < 4; i++)
        {
            _scene.Tick(0.05f);
        }

        if (_scene.Tanks[1].Position.X <= guestStart.X)
        {
            throw new Exception("A relayed guest input must drive the guest tank in the host world.");
        }

        var last = _transport.Broadcast[^1];
        if (last.AckSeq != 1)
        {
            throw new Exception($"The broadcast snapshot must ack the applied guest input; got {last.AckSeq}.");
        }
    }
}
