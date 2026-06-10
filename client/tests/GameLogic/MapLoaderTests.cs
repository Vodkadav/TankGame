using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class MapLoaderTests
{
    [Fact]
    public void ToLevel_BuildsALevelMatchingTheDocument()
    {
        var map = SampleArena.Build();

        var level = MapLoader.ToLevel(map);

        Assert.Equal(map.Width, level.Width);
        Assert.Equal(map.Height, level.Height);
        Assert.Equal(map.PlayerSpawn.X, level.SpawnX);
        Assert.Equal(map.PlayerSpawn.Y, level.SpawnY);
    }

    [Fact]
    public void ToLevel_ThrowsInvalidMapException_WhenTheMapIsNotPlayable()
    {
        var blank = MapDefinition.CreateBlank("Empty", 6, 5); // no enemy spawns => not playable

        var ex = Assert.Throws<InvalidMapException>(() => MapLoader.ToLevel(blank));
        Assert.Contains(ex.Errors, e => e.Code == MapValidationCode.NoEnemySpawns);
    }

    [Fact]
    public void SampleArena_IsValid()
    {
        Assert.True(MapValidator.Validate(SampleArena.Build()).IsValid);
    }

    [Fact]
    public void ToLevel_CarriesLayersAndRamps_IntoTheLevel()
    {
        // A small valley-and-plateau map: ramp at (3,2) joins the ground to the layer-1 plateau (x 4-6).
        var blank = MapDefinition.CreateBlank("Cliffy", 8, 5);
        var layers = new int[8, 5];
        var ramps = new bool[8, 5];
        for (var x = 4; x <= 6; x++)
        {
            for (var y = 1; y <= 3; y++)
            {
                layers[x, y] = 1;
            }
        }

        ramps[3, 2] = true;
        var map = new MapDefinition(
            blank.Name, blank.Materials, blank.Bushes, blank.Sandbags,
            (1, 1), new (int X, int Y)[] { (5, 2) }, System.Array.Empty<PowerupSpawn>(),
            layers: layers, ramps: ramps);

        var level = MapLoader.ToLevel(map);

        Assert.Equal(1, level.LayerAt(5, 2));
        Assert.Equal(0, level.LayerAt(1, 1));
        Assert.True(level.IsRamp(3, 2));
    }
}
