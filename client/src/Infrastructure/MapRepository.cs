using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TankGame.GameLogic;

namespace TankGame.Infrastructure;

/// <summary>A stored map's id (its file stem, used to load it) and display name.</summary>
public readonly record struct StoredMap(string Id, string Name);

/// <summary>Persists user-built maps as JSON files in a directory (the play build points it at
/// <c>user://maps</c>; tests point it at a temp folder). Plain <see cref="System.IO"/> so it is testable
/// without Godot — the Godot caller resolves <c>user://</c> to a real path. Corrupt files are skipped by
/// <see cref="List"/> rather than crashing the browser.</summary>
public sealed class MapRepository
{
    private readonly string _directory;

    public MapRepository(string directory)
    {
        _directory = directory;
    }

    /// <summary>Writes the map as <c>{slug}.json</c> (overwriting a map of the same name) and returns its
    /// id.</summary>
    public string Save(MapDefinition map)
    {
        Directory.CreateDirectory(_directory);
        var id = Slug(map.Name);
        File.WriteAllText(PathFor(id), MapCodec.Encode(map));
        return id;
    }

    /// <summary>The stored maps, sorted by name. Empty when the directory does not exist yet.</summary>
    public IReadOnlyList<StoredMap> List()
    {
        if (!Directory.Exists(_directory))
        {
            return Array.Empty<StoredMap>();
        }

        var maps = new List<StoredMap>();
        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            string name;
            try
            {
                name = MapCodec.Decode(File.ReadAllText(file)).Name;
            }
            catch (MapFormatException)
            {
                continue; // a hand-corrupted file shouldn't break the whole browser
            }

            maps.Add(new StoredMap(Path.GetFileNameWithoutExtension(file), name));
        }

        maps.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return maps;
    }

    public MapDefinition Load(string id) => MapCodec.Decode(File.ReadAllText(PathFor(id)));

    private string PathFor(string id) => Path.Combine(_directory, id + ".json");

    private static string Slug(string name)
    {
        var slug = new string(name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return slug.Length == 0 ? "map" : slug;
    }
}
