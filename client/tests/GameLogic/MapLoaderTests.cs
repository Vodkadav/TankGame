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
}
