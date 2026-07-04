using System;
using System.Collections.Generic;
using TankGame.Infrastructure;

namespace TankGame.Presentation;

/// <summary>Chooses the map for a Solo match when the player didn't pick a specific one from Select Map.
/// The pool is every available map — the built-in arenas AND the player's created maps — so a plain
/// "Solo" drops into a random one of them (owner ask) instead of always the built-in Desert War. Pure
/// (no Godot, no I/O): the caller supplies the built-ins and the created-map list and applies the choice.</summary>
public static class SoloMapSelection
{
    /// <summary>Which map a Solo launch landed on.</summary>
    public abstract record Choice;

    /// <summary>Play a built-in arena — <c>Arena3DScene</c> builds it; no custom map.</summary>
    public sealed record BuiltIn(ArenaId Arena) : Choice;

    /// <summary>Play one of the player's saved maps, identified by its repository id.</summary>
    public sealed record Created(string MapId) : Choice;

    /// <summary>Uniformly picks one map from the whole pool. Built-ins come first, then created maps, so
    /// when there are no created maps every pick is a built-in — the built-in arena stays the fallback.
    /// <paramref name="builtIns"/> must be non-empty (the game always ships at least one arena).</summary>
    public static Choice Pick(IReadOnlyList<ArenaId> builtIns, IReadOnlyList<StoredMap> created, Random rng)
    {
        var index = rng.Next(builtIns.Count + created.Count);
        return index < builtIns.Count
            ? new BuiltIn(builtIns[index])
            : new Created(created[index - builtIns.Count].Id);
    }
}
