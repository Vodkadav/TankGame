using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TankGame.Infrastructure;

/// <summary>One placeable model the asset browser offers.</summary>
/// <param name="Id">Stable <c>pack/model</c> id stored in maps (e.g. <c>kenney_nature-kit/tree_oak</c>).</param>
/// <param name="DisplayName">The model name, prettied for the list.</param>
/// <param name="Category">The browser's expandable group (Nature, Buildings, …).</param>
/// <param name="SourcePath">Absolute path of the source file, for the copy-on-place import.</param>
public sealed record AssetEntry(string Id, string DisplayName, string Category, string SourcePath);

/// <summary>The map editor's catalogue over the local 3D asset library (owner ask 2026-06-11):
/// every SELF-CONTAINED <c>.glb</c> under the library root, grouped into browse categories inferred
/// from the pack names and searchable by substring. Packs with unusable or unclear licenses are
/// excluded (see <c>docs/research/asset-browser-survey.md</c> — Kenney and KayKit are CC0). The
/// pure parts (category mapping, cataloguing, search) are static and unit-tested; only
/// <see cref="Scan"/> touches the file system.</summary>
public sealed class AssetLibrary
{
    /// <summary>The default library root on the dev machine; <see cref="Scan"/> returns an empty
    /// catalogue when it does not exist (any other machine, CI, Android).</summary>
    public const string DefaultRoot = @"C:\programmering\Assets\visual\3d";

    // Survey verdicts: no license file, or license terms the project cannot meet by default.
    private static readonly string[] ExcludedPacks =
    {
        "attackchopper", "Crates Asset Package", "low_poly_tanks", "military_vehicles_lp",
    };

    // Geometry-only .glb (the bare-Kenney case) still render — tinted — so they stay browsable;
    // truly empty files do not.
    private const long MinUsableBytes = 1024;

    private readonly string _root;

    public AssetLibrary(string root = DefaultRoot) => _root = root;

    /// <summary>Every self-contained .glb under the root, catalogued. Empty when the root is
    /// missing — the browser simply shows only already-imported assets then.</summary>
    public IReadOnlyList<AssetEntry> Scan()
    {
        if (!Directory.Exists(_root))
        {
            return Array.Empty<AssetEntry>();
        }

        var files = Directory.EnumerateFiles(_root, "*.glb", SearchOption.AllDirectories)
            .Where(path => new FileInfo(path).Length >= MinUsableBytes);
        return Catalogue(files, _root);
    }

    /// <summary>Builds the catalogue from paths (pure — tests feed fake lists). The pack is the
    /// path's first folder under the root; excluded packs are dropped; ids are
    /// <c>pack/filename-without-extension</c>, unique by first sighting. Separator handling is
    /// plain string work, NOT <see cref="Path"/> — the tests feed Windows paths and CI runs on
    /// Linux, where a backslash is not a separator (the #209 red-main lesson).</summary>
    public static IReadOnlyList<AssetEntry> Catalogue(IEnumerable<string> glbPaths, string root)
    {
        var entries = new List<AssetEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedRoot = root.Replace('\\', '/').TrimEnd('/') + "/";
        foreach (var path in glbPaths)
        {
            var normalized = path.Replace('\\', '/');
            var relative = normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? normalized[normalizedRoot.Length..]
                : normalized;
            var pack = relative.Split('/')[0];
            if (ExcludedPacks.Any(excluded => string.Equals(excluded, pack, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var file = relative.Split('/')[^1];
            var model = file.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
            var id = $"{pack}/{model}";
            if (!seen.Add(id))
            {
                continue;
            }

            entries.Add(new AssetEntry(id, Prettify(model), CategoryFor(pack), path));
        }

        return entries
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Case-insensitive substring search over names, ids, and categories (pure).</summary>
    public static IReadOnlyList<AssetEntry> Search(IReadOnlyList<AssetEntry> catalogue, string query)
    {
        var needle = query.Trim();
        if (needle.Length == 0)
        {
            return catalogue;
        }

        return catalogue
            .Where(e => e.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || e.Id.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || e.Category.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>The browse category for a pack, keyed off the survey's groupings (pure). Unknown
    /// packs land in Props rather than vanishing.</summary>
    public static string CategoryFor(string packName)
    {
        var name = packName.ToLowerInvariant();
        return name switch
        {
            _ when Has(name, "nature", "forest", "graveyard", "survival") => "Nature",
            _ when Has(name, "building", "city", "town", "castle", "medieval", "hexagon", "holiday", "modular") => "Buildings",
            _ when Has(name, "car", "train", "watercraft", "coaster", "racing") => "Vehicles",
            _ when Has(name, "space", "platformer", "prototype") => "Sci-fi",
            _ when Has(name, "dungeon", "fantasy", "pirate") => "Dungeon",
            _ when Has(name, "character", "adventurer", "skeleton", "pets") => "Characters",
            _ when Has(name, "blaster", "weapon", "tower-defense") => "Weapons",
            _ when Has(name, "tank", "military", "arena") => "Military",
            _ when Has(name, "brick", "terrain") => "Terrain",
            _ => "Props",
        };
    }

    private static bool Has(string name, params string[] keywords) => keywords.Any(name.Contains);

    private static string Prettify(string model) =>
        model.Replace('-', ' ').Replace('_', ' ');
}
