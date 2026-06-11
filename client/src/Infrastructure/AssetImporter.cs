using System.IO;

namespace TankGame.Infrastructure;

/// <summary>Copy-on-place import (owner ask 2026-06-11, design in
/// <c>docs/research/asset-browser-survey.md</c>): the first time an external library asset is
/// placed, its file is copied into the project's <c>models/imported/&lt;pack&gt;/&lt;model&gt;.glb</c>
/// so the map's asset id keeps resolving after a clone or an export — absolute paths into the
/// library would break everywhere but the dev machine. Idempotent: an already-imported file is
/// never overwritten, so a re-place is free and local edits stick.</summary>
public static class AssetImporter
{
    /// <summary>Ensures <paramref name="entry"/> exists under <paramref name="importRoot"/> and
    /// returns the imported file's path.</summary>
    public static string EnsureImported(AssetEntry entry, string importRoot)
    {
        var target = TargetPath(entry.Id, importRoot);
        if (!File.Exists(target))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(entry.SourcePath, target);
        }

        return target;
    }

    /// <summary>Where an asset id lives under an import root (pure).</summary>
    public static string TargetPath(string assetId, string importRoot) =>
        Path.Combine(importRoot, assetId.Replace('/', Path.DirectorySeparatorChar) + ".glb");
}
