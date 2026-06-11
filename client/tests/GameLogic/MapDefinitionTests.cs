using System;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class MapDefinitionTests
{
    [Fact]
    public void Constructor_ExposesDimensionsSpawnsAndGrids()
    {
        var materials = new CellMaterial[2, 3];
        var bushes = new bool[2, 3];
        var sandbags = new bool[2, 3];

        var map = new MapDefinition(
            "Arena",
            materials,
            bushes,
            sandbags,
            (1, 2),
            new (int X, int Y)[] { (0, 0) },
            new[] { new PowerupSpawn(PowerupKind.Repair, 1, 1) });

        Assert.Equal("Arena", map.Name);
        Assert.Equal(2, map.Width);
        Assert.Equal(3, map.Height);
        Assert.Equal((1, 2), map.PlayerSpawn);
        Assert.Single(map.EnemySpawns);
        Assert.Equal(PowerupKind.Repair, map.PowerupSpawns[0].Kind);
    }

    [Fact]
    public void Constructor_ThrowsWhenOverlayDimensionsDoNotMatchMaterials()
    {
        var materials = new CellMaterial[4, 4];
        var goodBushes = new bool[4, 4];
        var badSandbags = new bool[3, 4];

        Assert.Throws<ArgumentException>(() => new MapDefinition(
            "x", materials, goodBushes, badSandbags, (0, 0),
            Array.Empty<(int, int)>(), Array.Empty<PowerupSpawn>()));
    }

    [Fact]
    public void CreateBlank_FillsInteriorWithFloorAndRingsItWithSteel()
    {
        var map = MapDefinition.CreateBlank("Blank", 6, 5);

        for (var x = 0; x < map.Width; x++)
        {
            Assert.Equal(CellMaterial.Steel, map.Materials[x, 0]);
            Assert.Equal(CellMaterial.Steel, map.Materials[x, map.Height - 1]);
        }

        for (var y = 0; y < map.Height; y++)
        {
            Assert.Equal(CellMaterial.Steel, map.Materials[0, y]);
            Assert.Equal(CellMaterial.Steel, map.Materials[map.Width - 1, y]);
        }

        Assert.Equal(CellMaterial.Floor, map.Materials[1, 1]);
        Assert.Equal(CellMaterial.Floor, map.Materials[map.PlayerSpawn.X, map.PlayerSpawn.Y]);
    }

    [Fact]
    public void CreateBlank_RejectsArenasTooSmallToHaveAnInterior()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapDefinition.CreateBlank("tiny", 2, 5));
    }

    [Fact]
    public void TeleportPads_DefaultToEmpty_WhenOmitted()
    {
        var map = MapDefinition.CreateBlank("Blank", 6, 5);

        Assert.Empty(map.TeleportPads);
    }

    [Fact]
    public void Transforms_DefaultToNull_AndAreCarried()
    {
        var plain = MapDefinition.CreateBlank("Plain", 5, 5);
        Assert.Null(plain.Transforms);

        var posed = new MapDefinition(
            plain.Name, plain.Materials, plain.Bushes, plain.Sandbags,
            plain.PlayerSpawn, plain.EnemySpawns, plain.PowerupSpawns,
            transforms: new System.Collections.Generic.Dictionary<(int X, int Y), PropTransform>
            {
                [(2, 2)] = new(90f, 0f, 0f, 1f),
            });
        Assert.Equal(90f, posed.Transforms![(2, 2)].YawDeg);
    }

    [Fact]
    public void GroundTheme_DefaultsToSand_AndIsCarried()
    {
        var plain = MapDefinition.CreateBlank("Plain", 5, 5);
        Assert.Equal(GroundTheme.Sand, plain.GroundTheme);

        var themed = new MapDefinition(
            plain.Name, plain.Materials, plain.Bushes, plain.Sandbags,
            plain.PlayerSpawn, plain.EnemySpawns, plain.PowerupSpawns,
            groundTheme: GroundTheme.Jungle);
        Assert.Equal(GroundTheme.Jungle, themed.GroundTheme);
    }

    [Fact]
    public void Constructor_ExposesTeleportPadLinks()
    {
        var materials = new CellMaterial[6, 5];
        var map = new MapDefinition(
            "Pads",
            materials,
            new bool[6, 5],
            new bool[6, 5],
            (1, 1),
            new (int X, int Y)[] { (4, 3) },
            Array.Empty<PowerupSpawn>(),
            new[] { new TeleportPadLink(1, 2, 4, 2) });

        Assert.Single(map.TeleportPads);
        Assert.Equal(new TeleportPadLink(1, 2, 4, 2), map.TeleportPads[0]);
    }

    // ── Elevation (ADR-0020 Wave B step 5) ──

    [Fact]
    public void Elevation_DefaultsToFlat_WhenOmitted()
    {
        var map = MapDefinition.CreateBlank("Blank", 6, 5);

        Assert.Null(map.Layers);
        Assert.Null(map.Ramps);
    }

    [Fact]
    public void Constructor_ExposesLayersAndRamps()
    {
        var layers = new int[6, 5];
        layers[3, 2] = 1;
        var ramps = new bool[6, 5];
        ramps[2, 2] = true;

        var map = new MapDefinition(
            "Cliffy", new CellMaterial[6, 5], new bool[6, 5], new bool[6, 5],
            (1, 1), new (int X, int Y)[] { (4, 3) }, Array.Empty<PowerupSpawn>(),
            layers: layers, ramps: ramps);

        Assert.Equal(1, map.Layers![3, 2]);
        Assert.True(map.Ramps![2, 2]);
    }

    [Fact]
    public void Constructor_ThrowsWhenElevationDimensionsDoNotMatchMaterials()
    {
        Assert.Throws<ArgumentException>(() => new MapDefinition(
            "Bad", new CellMaterial[6, 5], new bool[6, 5], new bool[6, 5],
            (1, 1), Array.Empty<(int, int)>(), Array.Empty<PowerupSpawn>(),
            layers: new int[4, 4]));

        Assert.Throws<ArgumentException>(() => new MapDefinition(
            "Bad", new CellMaterial[6, 5], new bool[6, 5], new bool[6, 5],
            (1, 1), Array.Empty<(int, int)>(), Array.Empty<PowerupSpawn>(),
            ramps: new bool[4, 4]));
    }
}
