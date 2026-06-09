using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Why a map failed validation. Maps to a localised message in Presentation.</summary>
public enum MapValidationCode
{
    PlayerSpawnOutOfBounds,
    PlayerSpawnNotFloor,
    EnemySpawnOutOfBounds,
    EnemySpawnNotFloor,
    PowerupSpawnOutOfBounds,
    PowerupSpawnNotFloor,
    SpawnUnreachable,
    NoEnemySpawns,
}

/// <summary>One validation problem, with the cell it concerns (or (-1, -1) when it is not about a
/// specific cell).</summary>
public readonly record struct MapValidationError(MapValidationCode Code, int X, int Y);

/// <summary>The outcome of validating a map: valid when there are no errors.</summary>
public sealed class MapValidationResult
{
    public MapValidationResult(IReadOnlyList<MapValidationError> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<MapValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}

/// <summary>Checks that a <see cref="MapDefinition"/> is actually playable: every spawn sits on an
/// in-bounds floor cell, there is someone to fight, and every spawn is reachable on foot from the
/// player's start (no walled-off pockets). Pure C# — mirrors the playability guarantees that
/// <see cref="LevelMap"/> and <see cref="ArenaGenerator"/> enforce for built-in arenas.</summary>
public static class MapValidator
{
    public static MapValidationResult Validate(MapDefinition map)
    {
        var errors = new List<MapValidationError>();

        var playerInBounds = InBounds(map, map.PlayerSpawn.X, map.PlayerSpawn.Y);
        if (!playerInBounds)
        {
            errors.Add(new MapValidationError(MapValidationCode.PlayerSpawnOutOfBounds, map.PlayerSpawn.X, map.PlayerSpawn.Y));
        }
        else if (!IsFloor(map, map.PlayerSpawn.X, map.PlayerSpawn.Y))
        {
            errors.Add(new MapValidationError(MapValidationCode.PlayerSpawnNotFloor, map.PlayerSpawn.X, map.PlayerSpawn.Y));
        }

        if (map.EnemySpawns.Count == 0)
        {
            errors.Add(new MapValidationError(MapValidationCode.NoEnemySpawns, -1, -1));
        }

        foreach (var (x, y) in map.EnemySpawns)
        {
            CheckSpawnCell(map, x, y, errors, MapValidationCode.EnemySpawnOutOfBounds, MapValidationCode.EnemySpawnNotFloor);
        }

        foreach (var spawn in map.PowerupSpawns)
        {
            CheckSpawnCell(map, spawn.X, spawn.Y, errors, MapValidationCode.PowerupSpawnOutOfBounds, MapValidationCode.PowerupSpawnNotFloor);
        }

        // Reachability only makes sense from a real standing-room player start.
        if (playerInBounds && !CellMaterials.BlocksMovement(map.Materials[map.PlayerSpawn.X, map.PlayerSpawn.Y]))
        {
            var reachable = FloodFill(map);
            foreach (var (x, y) in map.EnemySpawns)
            {
                if (InBounds(map, x, y) && !reachable[x, y])
                {
                    errors.Add(new MapValidationError(MapValidationCode.SpawnUnreachable, x, y));
                }
            }

            foreach (var spawn in map.PowerupSpawns)
            {
                if (InBounds(map, spawn.X, spawn.Y) && !reachable[spawn.X, spawn.Y])
                {
                    errors.Add(new MapValidationError(MapValidationCode.SpawnUnreachable, spawn.X, spawn.Y));
                }
            }
        }

        return new MapValidationResult(errors);
    }

    private static void CheckSpawnCell(
        MapDefinition map, int x, int y, List<MapValidationError> errors,
        MapValidationCode outOfBounds, MapValidationCode notFloor)
    {
        if (!InBounds(map, x, y))
        {
            errors.Add(new MapValidationError(outOfBounds, x, y));
        }
        else if (!IsFloor(map, x, y))
        {
            errors.Add(new MapValidationError(notFloor, x, y));
        }
    }

    private static bool[,] FloodFill(MapDefinition map)
    {
        var reachable = new bool[map.Width, map.Height];
        var queue = new Queue<(int X, int Y)>();
        reachable[map.PlayerSpawn.X, map.PlayerSpawn.Y] = true;
        queue.Enqueue(map.PlayerSpawn);

        var steps = new (int Dx, int Dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in steps)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (!InBounds(map, nx, ny) || reachable[nx, ny])
                {
                    continue;
                }

                if (CellMaterials.BlocksMovement(map.Materials[nx, ny]))
                {
                    continue;
                }

                reachable[nx, ny] = true;
                queue.Enqueue((nx, ny));
            }
        }

        return reachable;
    }

    private static bool InBounds(MapDefinition map, int x, int y) =>
        x >= 0 && y >= 0 && x < map.Width && y < map.Height;

    private static bool IsFloor(MapDefinition map, int x, int y) =>
        map.Materials[x, y] == CellMaterial.Floor;
}
