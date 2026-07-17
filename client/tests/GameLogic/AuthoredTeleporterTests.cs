using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

// The shared pad derivation both arenas use (local Arena3DScene and the networked NetArena3DScene):
// authored cell links become world-space pads whose elevation layer comes from the grid, so the
// same resolved map yields the identical Teleporter on every peer — nothing about pads on the wire.
public class AuthoredTeleporterTests
{
    private const float TileSize = 64f;
    private const float PadRadius = 40f;

    [Fact]
    public void Build_DerivesCliffsPads_AtCellCentres_WithGridLayers()
    {
        var cliffs = CliffsArena.Create();
        var grid = cliffs.Map.BuildGrid();

        var (_, pads) = AuthoredTeleporter.Build(
            cliffs.Pads, grid, TileSize, Vector2.Zero, PadRadius);

        Assert.Equal(2, pads.Count);
        // Cliffs authors one cross-layer link: (2,2) on the valley floor ↔ (22,18) on the plateau.
        Assert.Equal(new Vector2(160f, 160f), pads[0].Position);
        Assert.Equal(0, pads[0].Layer);
        Assert.Equal(new Vector2(1440f, 1184f), pads[1].Position);
        Assert.Equal(1, pads[1].Layer); // the layer is derived from the grid, never authored

        // Both scenes rely on link order (A then B) to mirror PadStatuses by index.
        var (teleporter, _) = AuthoredTeleporter.Build(
            cliffs.Pads, grid, TileSize, Vector2.Zero, PadRadius);
        var statuses = teleporter.PadStatuses();
        Assert.Equal(pads[0].Position, statuses[0].Position);
        Assert.Equal(pads[1].Position, statuses[1].Position);
    }

    [Fact]
    public void Build_CliffsTeleporter_WarpsCrossLayer_ValleyToPlateau()
    {
        var cliffs = CliffsArena.Create();
        var grid = cliffs.Map.BuildGrid();
        var (teleporter, pads) = AuthoredTeleporter.Build(
            cliffs.Pads, grid, TileSize, Vector2.Zero, PadRadius);

        // A tank on the valley pad (layer 0) warps up onto the plateau pad (layer 1)…
        Assert.True(teleporter.TryTeleport(pads[0].Position, 0, out var destination, out var layer));
        Assert.Equal(pads[1].Position, destination);
        Assert.Equal(1, layer);

        // …and arrives on a dormant pad (both ends cool down), so it does not bounce straight back.
        Assert.False(teleporter.TryTeleport(destination, layer, out _, out _));
    }

    [Fact]
    public void Build_WithNoLinks_YieldsAnEmptyTeleporter()
    {
        var cliffs = CliffsArena.Create();
        var grid = cliffs.Map.BuildGrid();

        var (teleporter, pads) = AuthoredTeleporter.Build(
            System.Array.Empty<TeleportPadLink>(), grid, TileSize, Vector2.Zero, PadRadius);

        Assert.Empty(pads);
        Assert.False(teleporter.TryTeleport(new Vector2(160f, 160f), 0, out _, out _));
    }
}
