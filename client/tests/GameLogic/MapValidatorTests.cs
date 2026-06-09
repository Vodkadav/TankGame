using System.Linq;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class MapValidatorTests
{
    // A 6x5 steel-ringed arena with an open floor interior, one reachable enemy spawn.
    private static MapDefinition OpenArena()
    {
        var map = MapDefinition.CreateBlank("Open", 6, 5);
        return new MapDefinition(
            map.Name, map.Materials, map.Bushes, map.Sandbags,
            (1, 1),
            new (int X, int Y)[] { (4, 3) },
            new[] { new PowerupSpawn(PowerupKind.Repair, 2, 2) });
    }

    [Fact]
    public void Validate_PassesForAWellFormedReachableArena()
    {
        var result = MapValidator.Validate(OpenArena());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_FlagsPlayerSpawnNotOnFloor()
    {
        var open = OpenArena();
        var map = new MapDefinition(
            open.Name, open.Materials, open.Bushes, open.Sandbags,
            (0, 0), // the steel corner
            open.EnemySpawns, open.PowerupSpawns);

        var result = MapValidator.Validate(map);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.PlayerSpawnNotFloor);
    }

    [Fact]
    public void Validate_FlagsEnemySpawnOutOfBounds()
    {
        var open = OpenArena();
        var map = new MapDefinition(
            open.Name, open.Materials, open.Bushes, open.Sandbags,
            open.PlayerSpawn,
            new (int X, int Y)[] { (99, 99) },
            open.PowerupSpawns);

        var result = MapValidator.Validate(map);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.EnemySpawnOutOfBounds);
    }

    [Fact]
    public void Validate_FlagsAnEnemySpawnWalledOffFromThePlayer()
    {
        var map = MapDefinition.CreateBlank("Walled", 7, 5);
        // Seal off column 4 so the right pocket is unreachable from the player at (1,1).
        for (var y = 1; y < map.Height - 1; y++)
        {
            map.Materials[4, y] = CellMaterial.Steel;
        }

        var walled = new MapDefinition(
            map.Name, map.Materials, map.Bushes, map.Sandbags,
            (1, 1),
            new (int X, int Y)[] { (5, 2) },
            System.Array.Empty<PowerupSpawn>());

        var result = MapValidator.Validate(walled);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.SpawnUnreachable);
    }

    [Fact]
    public void Validate_FlagsAnArenaWithNoEnemySpawns()
    {
        var open = OpenArena();
        var map = new MapDefinition(
            open.Name, open.Materials, open.Bushes, open.Sandbags,
            open.PlayerSpawn,
            System.Array.Empty<(int, int)>(),
            open.PowerupSpawns);

        var result = MapValidator.Validate(map);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.NoEnemySpawns);
    }
}
