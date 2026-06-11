using System;
using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A powerup spawn marker on an authored map: which kind sits where (grid cell).</summary>
/// <param name="Kind">The powerup that spawns here.</param>
/// <param name="X">Column.</param>
/// <param name="Y">Row.</param>
public readonly record struct PowerupSpawn(PowerupKind Kind, int X, int Y);

/// <summary>A linked pair of teleport pads on an authored map, given by the two cells they sit on. Driving
/// onto one pad warps a tank to its partner — across elevation layers too (teleport pads T3): each end's
/// layer is derived from the cell it sits on in the map's layer grid, never stored here, so the link can
/// never disagree with the terrain.</summary>
/// <param name="AX">Pad A column.</param>
/// <param name="AY">Pad A row.</param>
/// <param name="BX">Pad B column.</param>
/// <param name="BY">Pad B row.</param>
public readonly record struct TeleportPadLink(int AX, int AY, int BX, int BY);

/// <summary>An authored arena: the full document a map editor writes and the loader reads. It unifies
/// what is otherwise split across <see cref="LevelMap"/> (materials/bushes/player spawn) and
/// <see cref="ArenaGenerator"/> output (enemy spawns, pickup cells, sandbags) into one serializable
/// value. Pure C# — no Godot, no I/O; <see cref="MapCodec"/> serialises it and <see cref="MapValidator"/>
/// checks it is playable. Construction only enforces structural consistency (grids match the
/// dimensions); playability (reachable spawns, spawns on floor) is the validator's job so the editor can
/// hold a half-built map.</summary>
public sealed class MapDefinition
{
    public MapDefinition(
        string name,
        CellMaterial[,] materials,
        bool[,] bushes,
        bool[,] sandbags,
        (int X, int Y) playerSpawn,
        IReadOnlyList<(int X, int Y)> enemySpawns,
        IReadOnlyList<PowerupSpawn> powerupSpawns,
        IReadOnlyList<TeleportPadLink>? teleportPads = null,
        int[,]? layers = null,
        bool[,]? ramps = null,
        GroundTheme groundTheme = GroundTheme.Sand,
        int[,]? orientations = null)
    {
        GroundTheme = groundTheme;
        if (orientations is not null
            && (orientations.GetLength(0) != materials.GetLength(0) || orientations.GetLength(1) != materials.GetLength(1)))
        {
            throw new ArgumentException("orientations must match the materials' dimensions", nameof(orientations));
        }

        Orientations = orientations;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Materials = materials ?? throw new ArgumentNullException(nameof(materials));
        Width = materials.GetLength(0);
        Height = materials.GetLength(1);

        RequireMatchingDimensions(bushes, nameof(bushes));
        RequireMatchingDimensions(sandbags, nameof(sandbags));
        if (layers is not null && (layers.GetLength(0) != Width || layers.GetLength(1) != Height))
        {
            throw new ArgumentException("layers must match the materials' dimensions", nameof(layers));
        }

        if (ramps is not null && (ramps.GetLength(0) != Width || ramps.GetLength(1) != Height))
        {
            throw new ArgumentException("ramps must match the materials' dimensions", nameof(ramps));
        }

        Bushes = bushes;
        Sandbags = sandbags;
        Layers = layers;
        Ramps = ramps;
        PlayerSpawn = playerSpawn;
        EnemySpawns = enemySpawns ?? throw new ArgumentNullException(nameof(enemySpawns));
        PowerupSpawns = powerupSpawns ?? throw new ArgumentNullException(nameof(powerupSpawns));
        TeleportPads = teleportPads ?? Array.Empty<TeleportPadLink>();
    }

    /// <summary>The map's display name (shown in the "My Maps" browser).</summary>
    public string Name { get; }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Cell materials indexed <c>[x, y]</c>.</summary>
    public CellMaterial[,] Materials { get; }

    /// <summary>Which floor cells are bushes (conceal a tank), indexed <c>[x, y]</c>.</summary>
    public bool[,] Bushes { get; }

    /// <summary>Which floor cells are sandbags (slow a tank), indexed <c>[x, y]</c>.</summary>
    public bool[,] Sandbags { get; }

    /// <summary>The player's spawn cell.</summary>
    public (int X, int Y) PlayerSpawn { get; }

    /// <summary>Enemy tank spawn cells.</summary>
    public IReadOnlyList<(int X, int Y)> EnemySpawns { get; }

    /// <summary>Powerup spawn markers.</summary>
    public IReadOnlyList<PowerupSpawn> PowerupSpawns { get; }

    /// <summary>Authored teleport-pad pairs; empty when the map has none (then play auto-places a pair).</summary>
    public IReadOnlyList<TeleportPadLink> TeleportPads { get; }

    /// <summary>Per-cell elevation layers indexed <c>[x, y]</c> (0 = ground, ADR-0018), or <c>null</c>
    /// for a flat map — the pre-elevation document shape, kept so older maps stay valid.</summary>
    public int[,]? Layers { get; }

    /// <summary>Which cells are ramps joining their layer to the one above (ADR-0018), indexed
    /// <c>[x, y]</c>; <c>null</c> on a flat map.</summary>
    public bool[,]? Ramps { get; }

    /// <summary>The whole-arena ground tileset; <see cref="GroundTheme.Sand"/> (the launch look)
    /// when the author never picked one.</summary>
    public GroundTheme GroundTheme { get; }

    /// <summary>Per-cell facing in clockwise quarter turns (0–3), indexed <c>[x, y]</c> — cosmetic,
    /// the view turns the prop's mesh (owner feedback 2026-06-11). Null when nothing is rotated,
    /// keeping the lean document shape.</summary>
    public int[,]? Orientations { get; }

    /// <summary>A fresh arena of the given size: a steel-walled border around an all-floor interior,
    /// with the player spawn seated at the top-left interior corner and no enemies or powerups yet.
    /// The size must leave at least one interior cell (width and height ≥ 3).</summary>
    public static MapDefinition CreateBlank(string name, int width, int height)
    {
        if (width < 3 || height < 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), "a blank arena needs an interior — width and height must be at least 3");
        }

        var materials = new CellMaterial[width, height];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                materials[x, y] = border ? CellMaterial.Steel : CellMaterial.Floor;
            }
        }

        return new MapDefinition(
            name,
            materials,
            new bool[width, height],
            new bool[width, height],
            (1, 1),
            Array.Empty<(int, int)>(),
            Array.Empty<PowerupSpawn>());
    }

    private void RequireMatchingDimensions(bool[,] grid, string paramName)
    {
        if (grid is null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (grid.GetLength(0) != Width || grid.GetLength(1) != Height)
        {
            throw new ArgumentException($"{paramName} must match the materials' dimensions", paramName);
        }
    }
}
