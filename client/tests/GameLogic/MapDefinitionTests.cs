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
}
