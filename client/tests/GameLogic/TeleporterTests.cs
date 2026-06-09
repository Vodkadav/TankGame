using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class TeleporterTests
{
    private const float Radius = 20f;
    private const float Cooldown = 3.5f;

    // One link: pad A at the origin, pad B 200 units east, both on the ground.
    private static Teleporter PairedPads() => new(
        new[] { (new TeleportPad(Vector2.Zero, 0), new TeleportPad(new Vector2(200f, 0f), 0)) },
        padRadius: Radius,
        cooldownSeconds: Cooldown);

    [Fact]
    public void TryTeleport_OnAReadyPad_WarpsToTheLinkedPad()
    {
        var teleporter = PairedPads();

        var warped = teleporter.TryTeleport(new Vector2(5f, 0f), layer: 0, out var dest, out var destLayer);

        Assert.True(warped);
        Assert.Equal(new Vector2(200f, 0f), dest);
        Assert.Equal(0, destLayer);
    }

    [Fact]
    public void TryTeleport_OffEveryPad_DoesNothing()
    {
        var teleporter = PairedPads();

        var warped = teleporter.TryTeleport(new Vector2(100f, 0f), layer: 0, out _, out _);

        Assert.False(warped);
    }

    [Fact]
    public void TryTeleport_IsBidirectional()
    {
        var teleporter = PairedPads();

        var warped = teleporter.TryTeleport(new Vector2(200f, 0f), layer: 0, out var dest, out _);

        Assert.True(warped);
        Assert.Equal(Vector2.Zero, dest); // B sends the tank back to A
    }

    [Fact]
    public void TryTeleport_ArrivesOnACooledPad_SoItDoesNotBounceBack()
    {
        var teleporter = PairedPads();
        teleporter.TryTeleport(Vector2.Zero, layer: 0, out _, out _); // A → B puts both ends on cooldown

        var bounced = teleporter.TryTeleport(new Vector2(200f, 0f), layer: 0, out _, out _);

        Assert.False(bounced); // the destination pad is dormant, so the tank can drive off freely
    }

    [Fact]
    public void Step_AgesTheCooldown_UntilThePadIsReadyAgain()
    {
        var teleporter = PairedPads();
        teleporter.TryTeleport(Vector2.Zero, layer: 0, out _, out _);

        teleporter.Step(Cooldown - 0.1f);
        Assert.False(teleporter.TryTeleport(Vector2.Zero, layer: 0, out _, out _)); // still cooling

        teleporter.Step(0.2f);
        Assert.True(teleporter.TryTeleport(Vector2.Zero, layer: 0, out _, out _)); // ready again
    }

    [Fact]
    public void PadStatuses_ReportReadyThenCooling_AfterAWarp()
    {
        var teleporter = PairedPads();

        var ready = teleporter.PadStatuses();
        Assert.Equal(2, ready.Count);
        Assert.All(ready, s => Assert.True(s.Ready));
        Assert.All(ready, s => Assert.Equal(0f, s.CooldownFraction));

        teleporter.TryTeleport(Vector2.Zero, layer: 0, out _, out _); // both ends now on cooldown

        var cooling = teleporter.PadStatuses();
        Assert.All(cooling, s => Assert.False(s.Ready));
        Assert.All(cooling, s => Assert.True(s.CooldownFraction > 0.9f)); // just fired → near full

        teleporter.Step(Cooldown);
        Assert.All(teleporter.PadStatuses(), s => Assert.True(s.Ready)); // drained → ready again
    }

    [Fact]
    public void TryTeleport_OnlyAcceptsATankOnThePadsLayer()
    {
        // A raised pad on layer 1 linked to a pad on the ground (layer 0): using the raised pad warps
        // the tank down to the ground (cross-layer warp falls out of the layer field for free).
        var teleporter = new Teleporter(
            new[] { (new TeleportPad(Vector2.Zero, 1), new TeleportPad(new Vector2(200f, 0f), 0)) },
            padRadius: Radius,
            cooldownSeconds: Cooldown);

        Assert.False(teleporter.TryTeleport(Vector2.Zero, layer: 0, out _, out _)); // wrong layer for this pad

        var warped = teleporter.TryTeleport(Vector2.Zero, layer: 1, out var dest, out var destLayer);
        Assert.True(warped);
        Assert.Equal(new Vector2(200f, 0f), dest);
        Assert.Equal(0, destLayer); // arrives on the ground
    }
}
