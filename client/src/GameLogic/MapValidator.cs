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
    TeleportPadOutOfBounds,
    TeleportPadNotFloor,
    TeleportPadEndpointsCoincide,
    LayerOutOfRange,
    RampNotOnFloor,
    TooManyTankSpawns,
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
    /// <summary>The highest elevation layer a map may author (ADR-0020 Wave B). Four levels
    /// (0–3) is plenty of verticality for an arena this size and keeps the editor palette small.</summary>
    public const int MaxLayer = 3;

    /// <summary>The most tanks any mode fields — 4v4 (owner feedback 2026-06-11). The player spawn
    /// plus the enemy spawns may not exceed it.</summary>
    public const int MaxTankSpawns = 8;

    public static MapValidationResult Validate(MapDefinition map)
    {
        var errors = new List<MapValidationError>();
        CheckElevation(map, errors);

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

        if (1 + map.EnemySpawns.Count > MaxTankSpawns)
        {
            errors.Add(new MapValidationError(MapValidationCode.TooManyTankSpawns, -1, -1));
        }

        foreach (var (x, y) in map.EnemySpawns)
        {
            CheckSpawnCell(map, x, y, errors, MapValidationCode.EnemySpawnOutOfBounds, MapValidationCode.EnemySpawnNotFloor);
        }

        foreach (var spawn in map.PowerupSpawns)
        {
            CheckSpawnCell(map, spawn.X, spawn.Y, errors, MapValidationCode.PowerupSpawnOutOfBounds, MapValidationCode.PowerupSpawnNotFloor);
        }

        foreach (var pad in map.TeleportPads)
        {
            CheckSpawnCell(map, pad.AX, pad.AY, errors, MapValidationCode.TeleportPadOutOfBounds, MapValidationCode.TeleportPadNotFloor);
            CheckSpawnCell(map, pad.BX, pad.BY, errors, MapValidationCode.TeleportPadOutOfBounds, MapValidationCode.TeleportPadNotFloor);
            if (pad.AX == pad.BX && pad.AY == pad.BY)
            {
                errors.Add(new MapValidationError(MapValidationCode.TeleportPadEndpointsCoincide, pad.AX, pad.AY));
            }
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

    private static void CheckElevation(MapDefinition map, List<MapValidationError> errors)
    {
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                if (map.Layers is { } layers && (layers[x, y] < 0 || layers[x, y] > MaxLayer))
                {
                    errors.Add(new MapValidationError(MapValidationCode.LayerOutOfRange, x, y));
                }

                if (map.Ramps is { } ramps && ramps[x, y] && !IsFloor(map, x, y))
                {
                    errors.Add(new MapValidationError(MapValidationCode.RampNotOnFloor, x, y));
                }
            }
        }
    }

    // BFS over (cell, layer) states, mirroring how a tank actually moves (GridArena, ADR-0018/0020):
    // a plain cell is entered on its own layer; a ramp cell is stood on at either layer it joins
    // (LayerAt and LayerAt+1); a LOWER plain cell is entered by dropping off the ledge — one-way, so
    // high ground with no ramp up is correctly unreachable from below. Teleport pads are extra edges:
    // standing on a pad (on the pad cell's own layer) warps to its partner, so a pad pair may be the
    // only route into a pocket or up onto a plateau (teleport pads T3).
    private static bool[,] FloodFill(MapDefinition map)
    {
        var reachable = new bool[map.Width, map.Height];
        var visited = new HashSet<(int X, int Y, int Layer)>();
        var queue = new Queue<(int X, int Y, int Layer)>();
        var warps = PadWarps(map);
        var start = (map.PlayerSpawn.X, map.PlayerSpawn.Y, LayerAt(map, map.PlayerSpawn.X, map.PlayerSpawn.Y));
        reachable[start.Item1, start.Item2] = true;
        visited.Add(start);
        queue.Enqueue(start);

        var steps = new (int Dx, int Dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0)
        {
            var (x, y, layer) = queue.Dequeue();
            if (layer == LayerAt(map, x, y) && warps.TryGetValue((x, y), out var partners))
            {
                foreach (var (px, py) in partners)
                {
                    var arrival = (px, py, LayerAt(map, px, py));
                    if (visited.Add(arrival))
                    {
                        reachable[px, py] = true;
                        queue.Enqueue(arrival);
                    }
                }
            }

            foreach (var (dx, dy) in steps)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (!InBounds(map, nx, ny) || CellMaterials.BlocksMovement(map.Materials[nx, ny]))
                {
                    continue;
                }

                foreach (var nextLayer in EnterableLayers(map, nx, ny, layer))
                {
                    if (!visited.Add((nx, ny, nextLayer)))
                    {
                        continue;
                    }

                    reachable[nx, ny] = true;
                    queue.Enqueue((nx, ny, nextLayer));
                }
            }
        }

        return reachable;
    }

    // The layers a tank on fromLayer can occupy after moving onto cell (x, y), or none when the move
    // is blocked (the cell is a cliff face above the tank).
    private static IEnumerable<int> EnterableLayers(MapDefinition map, int x, int y, int fromLayer)
    {
        var cellLayer = LayerAt(map, x, y);
        if (IsRamp(map, x, y))
        {
            // Entered from either level it joins — or by dropping onto it from higher up — a ramp
            // lets the tank leave at both levels it connects.
            if (fromLayer >= cellLayer)
            {
                yield return cellLayer;
                yield return cellLayer + 1;
            }

            yield break;
        }

        if (cellLayer == fromLayer || cellLayer < fromLayer)
        {
            yield return cellLayer; // its own level, or a one-way drop off the ledge
        }
    }

    // Each well-formed pad link as a two-way warp edge, keyed by cell. Links with an end out of
    // bounds or inside a wall are skipped — those are already their own validation errors, not routes.
    private static Dictionary<(int X, int Y), List<(int X, int Y)>> PadWarps(MapDefinition map)
    {
        var warps = new Dictionary<(int X, int Y), List<(int X, int Y)>>();
        foreach (var pad in map.TeleportPads)
        {
            if (!InBounds(map, pad.AX, pad.AY) || !InBounds(map, pad.BX, pad.BY)
                || !IsFloor(map, pad.AX, pad.AY) || !IsFloor(map, pad.BX, pad.BY))
            {
                continue;
            }

            AddWarp(warps, (pad.AX, pad.AY), (pad.BX, pad.BY));
            AddWarp(warps, (pad.BX, pad.BY), (pad.AX, pad.AY));
        }

        return warps;
    }

    private static void AddWarp(
        Dictionary<(int X, int Y), List<(int X, int Y)>> warps, (int X, int Y) from, (int X, int Y) to)
    {
        if (!warps.TryGetValue(from, out var partners))
        {
            partners = new List<(int X, int Y)>();
            warps[from] = partners;
        }

        partners.Add(to);
    }

    private static int LayerAt(MapDefinition map, int x, int y) => map.Layers?[x, y] ?? 0;

    private static bool IsRamp(MapDefinition map, int x, int y) => map.Ramps?[x, y] ?? false;

    private static bool InBounds(MapDefinition map, int x, int y) =>
        x >= 0 && y >= 0 && x < map.Width && y < map.Height;

    private static bool IsFloor(MapDefinition map, int x, int y) =>
        map.Materials[x, y] == CellMaterial.Floor;
}
