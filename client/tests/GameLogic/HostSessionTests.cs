using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TankGame.Domain;
using TankGame.Domain.Net;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

// The authoritative half of ADR-0019 step 3, driven headless: a HostSession steps the real World
// from local + relayed guest inputs and broadcasts a SnapshotFrame per tick through the transport.
public class HostSessionTests
{
    private sealed class RecordingTransport : IMatchTransport
    {
        public List<SnapshotFrame> Broadcast { get; } = new();
        public event Action<byte> WelcomeReceived { add { } remove { } }
        public event Action<SnapshotFrame> SnapshotReceived { add { } remove { } }
        public event Action<InputFrame>? InputReceived;
        public void SendInput(InputFrame input) { }
        public void Poll() { }
        public void SendSnapshot(SnapshotFrame snapshot) => Broadcast.Add(snapshot);

        // Test hook standing in for "the relay forwarded a guest's input".
        public void DeliverInput(InputFrame frame) => InputReceived?.Invoke(frame);
    }

    private sealed class ScriptedInput(TankInput value) : IInputSource
    {
        public TankInput Value { get; set; } = value;
        public TankInput Read() => Value;
    }

    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    private const float Speed = 100f;

    private sealed class Rig
    {
        public RecordingTransport Transport { get; } = new();
        public World World { get; } = new();
        public WallGrid Walls { get; }
        public ScriptedInput HostInput { get; } = new(new TankInput(Vector2.Zero, 0f, false));
        public RelayedInputSource GuestInput { get; } = new();
        public Tank HostTank { get; }
        public Tank GuestTank { get; }
        public HostSession Session { get; }

        public Rig()
        {
            Walls = WallGrid.FromMaterials(new[,]
            {
                { CellMaterial.Floor, CellMaterial.Floor },
                { CellMaterial.Brick, CellMaterial.Floor },
            });
            var arena = new OpenArena();
            HostTank = new Tank(HostInput, World, arena, new Vector2(50f, 50f),
                Speed, fireInterval: 0.3f, projectileSpeed: 600f, team: 0);
            GuestTank = new Tank(GuestInput, World, arena, new Vector2(200f, 50f),
                Speed, fireInterval: 0.3f, projectileSpeed: 600f, team: 1);
            World.Spawn(HostTank);
            World.Spawn(GuestTank);
            Session = new HostSession(
                Transport, World, Walls,
                new (byte Slot, ITank Tank)[] { (0, HostTank), (1, GuestTank) },
                GuestInput);
        }
    }

    [Fact]
    public void Step_AdvancesTheWorld_AndBroadcastsEveryTankState()
    {
        var rig = new Rig();
        rig.HostInput.Value = new TankInput(new Vector2(1f, 0f), Aim: 0.5f, Fire: false);

        rig.Session.Step(0.1f);

        var snapshot = Assert.Single(rig.Transport.Broadcast);
        Assert.Equal(1u, snapshot.Tick);

        var host = snapshot.Tanks.Single(t => t.Slot == 0);
        Assert.True(host.X > 50f, "the host tank's local input must drive the authoritative world");
        Assert.Equal(0.5f, host.TurretRotation);
        Assert.Equal(0, host.Team);

        var guest = snapshot.Tanks.Single(t => t.Slot == 1);
        Assert.Equal(200f, guest.X); // no guest input yet — the guest tank idles
        Assert.Equal(1, guest.Team);
        Assert.Equal((byte)rig.GuestTank.Hp, guest.Hp);
    }

    [Fact]
    public void AckSeq_IsZero_BeforeAnyGuestInput()
    {
        var rig = new Rig();

        rig.Session.Step(0.1f);

        Assert.Equal(0u, rig.Transport.Broadcast[0].AckSeq);
    }

    [Fact]
    public void RelayedGuestInput_DrivesTheGuestTank_AndIsAcked()
    {
        var rig = new Rig();
        rig.Transport.DeliverInput(new InputFrame(Seq: 4, MoveX: 1f, MoveY: 0f, Aim: 1f, Buttons: 0));

        rig.Session.Step(0.1f);

        var snapshot = Assert.Single(rig.Transport.Broadcast);
        var guest = snapshot.Tanks.Single(t => t.Slot == 1);
        Assert.True(guest.X > 200f, "a relayed input frame must drive the guest tank in the host world");
        Assert.Equal(1f, guest.TurretRotation);
        Assert.Equal(4u, snapshot.AckSeq); // the reconciliation anchor the guest replays from
    }

    [Fact]
    public void Ticks_CountUpAcrossSteps()
    {
        var rig = new Rig();

        rig.Session.Step(0.05f);
        rig.Session.Step(0.05f);
        rig.Session.Step(0.05f);

        Assert.Equal(new uint[] { 1, 2, 3 }, rig.Transport.Broadcast.Select(s => s.Tick).ToArray());
    }

    [Fact]
    public void FiredShot_RidesTheSnapshot_SoAGuestCanSeeIt()
    {
        var rig = new Rig();
        rig.HostInput.Value = new TankInput(Vector2.Zero, Aim: 0f, Fire: true);

        rig.Session.Step(0.1f); // the host tank fires; the shot is now a live world entity

        var snapshot = Assert.Single(rig.Transport.Broadcast);
        var shot = Assert.Single(snapshot.Projectiles);
        Assert.Equal((byte)ProjectileStyle.Normal, shot.Style);
        Assert.True(rig.World.Entities.OfType<IProjectile>().Any(),
            "the fired shot must be a live world entity the snapshot reflects");
    }

    [Fact]
    public void WallDamage_RidesTheNextSnapshot_ThenClears()
    {
        var rig = new Rig();

        rig.Walls.DamageCell(1, 0, 1); // chip the brick between ticks
        rig.Session.Step(0.05f);
        rig.Session.Step(0.05f);

        var first = rig.Transport.Broadcast[0];
        var delta = Assert.Single(first.WallDeltas);
        Assert.Equal(1, delta.CellX);
        Assert.Equal(0, delta.CellY);
        Assert.Equal((byte)rig.Walls.GetCell(1, 0).Material, delta.Material);
        Assert.Equal((byte)rig.Walls.GetCell(1, 0).Hp, delta.Hp);

        Assert.Empty(rig.Transport.Broadcast[1].WallDeltas); // sent once, not re-broadcast forever
    }
}
