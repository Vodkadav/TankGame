using System;
using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Thrown when a map is loaded for play but fails validation — carries the specific problems
/// so the caller (or editor) can show them.</summary>
public sealed class InvalidMapException : Exception
{
    public InvalidMapException(IReadOnlyList<MapValidationError> errors)
        : base("map is not playable")
    {
        Errors = errors;
    }

    public IReadOnlyList<MapValidationError> Errors { get; }
}

/// <summary>Turns an authored <see cref="MapDefinition"/> into the <see cref="LevelMap"/> the play scene
/// already knows how to build a match from — validating first so a broken map fails loudly rather than
/// part-way through scene setup. The scene reads spawns, sandbags, and powerup placements straight off
/// the document; this is just the grid half. Pure C#.</summary>
public static class MapLoader
{
    public static LevelMap ToLevel(MapDefinition map)
    {
        var result = MapValidator.Validate(map);
        if (!result.IsValid)
        {
            throw new InvalidMapException(result.Errors);
        }

        return LevelMap.FromCells(
            map.Materials, map.Bushes, map.PlayerSpawn.X, map.PlayerSpawn.Y, map.Layers, map.Ramps,
            map.Orientations);
    }
}
