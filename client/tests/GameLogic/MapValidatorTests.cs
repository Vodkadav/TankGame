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
    public void Validate_PassesForAReachableTeleportPadPair()
    {
        var open = OpenArena();
        var map = new MapDefinition(
            open.Name, open.Materials, open.Bushes, open.Sandbags,
            open.PlayerSpawn, open.EnemySpawns, open.PowerupSpawns,
            new[] { new TeleportPadLink(1, 1, 4, 3) });

        Assert.True(MapValidator.Validate(map).IsValid);
    }

    [Fact]
    public void Validate_TreatsATeleportPadPair_AsAConnection_ToAWalledOffPocket()
    {
        var map = MapDefinition.CreateBlank("PadBridged", 7, 5);
        // Seal off column 4 so the right pocket is only reachable through the pad pair.
        for (var y = 1; y < map.Height - 1; y++)
        {
            map.Materials[4, y] = CellMaterial.Steel;
        }

        var bridged = new MapDefinition(
            map.Name, map.Materials, map.Bushes, map.Sandbags,
            (1, 1),
            new (int X, int Y)[] { (5, 2) },
            System.Array.Empty<PowerupSpawn>(),
            new[] { new TeleportPadLink(2, 2, 5, 3) });

        var result = MapValidator.Validate(bridged);

        Assert.True(result.IsValid, string.Join(", ", result.Errors));
    }

    [Fact]
    public void Validate_TreatsATeleportPadPair_AsAConnection_ToAnIslandPlateau_AcrossLayers()
    {
        var map = MapDefinition.CreateBlank("Island", 7, 5);
        var layers = new int[7, 5];
        layers[4, 2] = 1;
        layers[5, 2] = 1; // a layer-1 island with no ramp up — pad-only access

        var island = new MapDefinition(
            map.Name, map.Materials, map.Bushes, map.Sandbags,
            (1, 1),
            new (int X, int Y)[] { (5, 2) },
            System.Array.Empty<PowerupSpawn>(),
            new[] { new TeleportPadLink(2, 2, 4, 2) },
            layers);

        var result = MapValidator.Validate(island);

        Assert.True(result.IsValid, string.Join(", ", result.Errors));
    }

    [Fact]
    public void Validate_FlagsAnIslandPlateau_WithNoPadOrRampUp()
    {
        var map = MapDefinition.CreateBlank("Stranded", 7, 5);
        var layers = new int[7, 5];
        layers[4, 2] = 1;
        layers[5, 2] = 1;

        var stranded = new MapDefinition(
            map.Name, map.Materials, map.Bushes, map.Sandbags,
            (1, 1),
            new (int X, int Y)[] { (5, 2) },
            System.Array.Empty<PowerupSpawn>(),
            layers: layers);

        var result = MapValidator.Validate(stranded);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.SpawnUnreachable && e.X == 5 && e.Y == 2);
    }

    [Fact]
    public void Validate_FlagsATeleportPadOutOfBounds()
    {
        var open = OpenArena();
        var map = new MapDefinition(
            open.Name, open.Materials, open.Bushes, open.Sandbags,
            open.PlayerSpawn, open.EnemySpawns, open.PowerupSpawns,
            new[] { new TeleportPadLink(1, 1, 99, 99) });

        var result = MapValidator.Validate(map);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.TeleportPadOutOfBounds);
    }

    [Fact]
    public void Validate_FlagsATeleportPadInsideAWall()
    {
        var open = OpenArena();
        var map = new MapDefinition(
            open.Name, open.Materials, open.Bushes, open.Sandbags,
            open.PlayerSpawn, open.EnemySpawns, open.PowerupSpawns,
            new[] { new TeleportPadLink(1, 1, 0, 0) }); // (0,0) is the steel corner

        var result = MapValidator.Validate(map);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.TeleportPadNotFloor);
    }

    [Fact]
    public void Validate_FlagsATeleportPadLinkedToItself()
    {
        var open = OpenArena();
        var map = new MapDefinition(
            open.Name, open.Materials, open.Bushes, open.Sandbags,
            open.PlayerSpawn, open.EnemySpawns, open.PowerupSpawns,
            new[] { new TeleportPadLink(1, 1, 1, 1) });

        var result = MapValidator.Validate(map);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.TeleportPadEndpointsCoincide);
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

    // ── Elevation (ADR-0020 Wave B step 5) ──
    // An 8x5 steel-ringed arena whose right half (x 4-6) is a layer-1 plateau; the player starts on
    // the ground at (1,1) and the enemy up on the plateau at (5,2).
    private static (MapDefinition Map, int[,] Layers, bool[,] Ramps) PlateauArena()
    {
        var blank = MapDefinition.CreateBlank("Plateau", 8, 5);
        var layers = new int[8, 5];
        var ramps = new bool[8, 5];
        for (var x = 4; x <= 6; x++)
        {
            for (var y = 1; y <= 3; y++)
            {
                layers[x, y] = 1;
            }
        }

        var map = new MapDefinition(
            blank.Name, blank.Materials, blank.Bushes, blank.Sandbags,
            (1, 1), new (int X, int Y)[] { (5, 2) }, System.Array.Empty<PowerupSpawn>(),
            layers: layers, ramps: ramps);
        return (map, layers, ramps);
    }

    [Fact]
    public void Validate_FlagsAPlateauSpawn_WithNoRampUpToIt()
    {
        // Drops only go DOWN — without a ramp the player can never reach the enemy up top.
        var (map, _, _) = PlateauArena();

        var result = MapValidator.Validate(map);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.SpawnUnreachable && e.X == 5 && e.Y == 2);
    }

    [Fact]
    public void Validate_PassesWhenARampConnectsTheGroundToThePlateau()
    {
        var (map, _, ramps) = PlateauArena();
        ramps[3, 2] = true; // a ground-layer ramp cell joining layers 0 and 1, flush with the plateau

        Assert.True(MapValidator.Validate(map).IsValid);
    }

    [Fact]
    public void Validate_AcceptsADropOnlyDescent_FromARaisedPlayerSpawn()
    {
        // The player starts on the plateau and the enemy is below: a rampless map is still playable
        // because the tank can drive off the ledge and fall (ADR-0020 Wave B step 4).
        var (map, _, _) = PlateauArena();
        var dropOnly = new MapDefinition(
            map.Name, map.Materials, map.Bushes, map.Sandbags,
            (5, 2), new (int X, int Y)[] { (1, 1) }, System.Array.Empty<PowerupSpawn>(),
            layers: map.Layers, ramps: map.Ramps);

        Assert.True(MapValidator.Validate(dropOnly).IsValid);
    }

    [Fact]
    public void Validate_FlagsALayerOutsideTheAllowedRange()
    {
        var (map, layers, _) = PlateauArena();
        layers[2, 2] = MapValidator.MaxLayer + 1;

        var result = MapValidator.Validate(map);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.LayerOutOfRange && e.X == 2 && e.Y == 2);
    }

    [Fact]
    public void Validate_FlagsARampOnANonFloorCell()
    {
        var (map, _, ramps) = PlateauArena();
        map.Materials[2, 2] = CellMaterial.Steel;
        ramps[2, 2] = true;

        var result = MapValidator.Validate(map);

        Assert.Contains(result.Errors, e => e.Code == MapValidationCode.RampNotOnFloor && e.X == 2 && e.Y == 2);
    }
}
