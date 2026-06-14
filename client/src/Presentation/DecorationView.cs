using System.IO;
using System.Linq;
using Godot;
using TankGame.Domain;
using TankGame.Infrastructure;

namespace TankGame.Presentation;

/// <summary>Resolves an asset id to the model file to load: the project's imported copy first (the
/// thing that exists after a clone or in an export), the external dev library second. The external
/// catalogue is scanned once and cached — the browser and every decoration share it.</summary>
public static class DecorationAssets
{
    private static System.Collections.Generic.IReadOnlyList<AssetEntry>? _external;

    /// <summary>Absolute path of the project's imported-models folder.</summary>
    public static string ImportRoot =>
        ProjectSettings.GlobalizePath("res://src/Presentation/Arena/models/imported");

    /// <summary>The external library catalogue (empty off the dev machine), scanned once.</summary>
    public static System.Collections.Generic.IReadOnlyList<AssetEntry> External =>
        _external ??= new AssetLibrary().Scan();

    /// <summary>The catalogue the browser shows: everything already imported plus the external
    /// library, deduplicated by id (the imported copy wins — it is what the map will load).</summary>
    public static System.Collections.Generic.IReadOnlyList<AssetEntry> Catalogue()
    {
        var imported = new AssetLibrary(ImportRoot).Scan();
        var ids = new System.Collections.Generic.HashSet<string>(imported.Select(e => e.Id));
        return imported.Concat(External.Where(e => !ids.Contains(e.Id))).ToList();
    }

    /// <summary>The file behind an asset id, or null when it exists on neither side (a map made on
    /// another machine whose imported file was never committed).</summary>
    public static string? ResolvePath(string assetId)
    {
        var imported = AssetImporter.TargetPath(assetId, ImportRoot);
        if (File.Exists(imported))
        {
            return imported;
        }

        return External.FirstOrDefault(e => e.Id == assetId)?.SourcePath;
    }
}

/// <summary>A placed library prop in the 3D world (owner ask 2026-06-11): loads its .glb at runtime
/// via <see cref="GltfDocument"/> — unlike <c>GD.Load</c>, that also works for files imported
/// mid-session and for absolute library paths — auto-fits it to the cell, and applies the authored
/// pose. Bare geometry-only files (the small Kenney .glb that render white) get a flat tint from
/// their pack's category, so every asset reads as something even before a real texture pass.</summary>
public partial class DecorationView : Node3D
{
    private const float Footprint = 0.95f; // of a cell, like the terrain props

    private string _assetId = "";
    private PropTransform _pose = PropTransform.Identity;
    private float _tileSize = 64f;

    /// <summary>The asset this view renders — lets tests and the editor identify it.</summary>
    public string AssetId => _assetId;

    /// <summary>Set before adding to the tree; the model is built in <see cref="_Ready"/> (the fit
    /// measure needs the node in the tree).</summary>
    public void Configure(string assetId, PropTransform pose, float tileSize)
    {
        _assetId = assetId;
        _pose = pose;
        _tileSize = tileSize;
    }

    public override void _Ready()
    {
        if (DecorationAssets.ResolvePath(_assetId) is not { } path)
        {
            return; // the asset is on neither side — an empty cell beats a crash
        }

        var document = new GltfDocument();
        var state = new GltfState();
        if (document.AppendFromFile(path, state) != Error.Ok || document.GenerateScene(state) is not Node3D model)
        {
            return;
        }

        AddChild(model);
        ModelFit.Apply(model, _tileSize * Footprint, seatOnGround: true);

        // Smart multi-colour (owner feedback 2026-06-11): the biggest part wears the category's
        // main colour, the details cycle its secondary palette — seeded by the asset id, so one
        // asset always looks the same while its pack-mates vary. Applied unconditionally: the tint
        // itself skips any surface that carries a real texture (file size was a poor bare-model
        // proxy — big geometry-only files stayed white).
        var category = AssetLibrary.CategoryFor(_assetId.Split('/')[0]);
        ModelFit.TintPalette(model, CategoryTint(category), SecondaryTints(category), StableSeed(_assetId));

        RotationDegrees = new Vector3(_pose.PitchDeg, _pose.YawDeg, _pose.RollDeg);
        Scale = Vector3.One * _pose.Scale;
    }

    // String.GetHashCode is randomised per process — a stable char sum keeps an asset's colour
    // arrangement identical across sessions and machines.
    private static int StableSeed(string id)
    {
        var seed = 0;
        foreach (var c in id)
        {
            seed = (seed * 31) + c;
        }

        return seed & 0x7FFFFFFF;
    }

    // Each category's 3 detail colours, picked to read against its primary.
    private static Color[] SecondaryTints(string category) => category switch
    {
        "Nature" => new[] { new Color(0.42f, 0.30f, 0.18f), new Color(0.20f, 0.36f, 0.18f), new Color(0.62f, 0.58f, 0.36f) },
        "Buildings" => new[] { new Color(0.55f, 0.28f, 0.22f), new Color(0.35f, 0.40f, 0.46f), new Color(0.78f, 0.74f, 0.64f) },
        "Vehicles" => new[] { new Color(0.16f, 0.16f, 0.18f), new Color(0.75f, 0.72f, 0.65f), new Color(0.70f, 0.35f, 0.20f) },
        "Sci-fi" => new[] { new Color(0.25f, 0.28f, 0.34f), new Color(0.85f, 0.55f, 0.20f), new Color(0.45f, 0.75f, 0.80f) },
        "Dungeon" => new[] { new Color(0.30f, 0.26f, 0.22f), new Color(0.55f, 0.50f, 0.42f), new Color(0.50f, 0.30f, 0.20f) },
        "Characters" => new[] { new Color(0.30f, 0.25f, 0.20f), new Color(0.55f, 0.20f, 0.18f), new Color(0.85f, 0.78f, 0.65f) },
        "Weapons" => new[] { new Color(0.15f, 0.15f, 0.17f), new Color(0.80f, 0.60f, 0.20f), new Color(0.50f, 0.12f, 0.10f) },
        "Military" => new[] { new Color(0.25f, 0.28f, 0.20f), new Color(0.16f, 0.16f, 0.15f), new Color(0.60f, 0.55f, 0.40f) },
        "Terrain" => new[] { new Color(0.42f, 0.32f, 0.24f), new Color(0.66f, 0.60f, 0.52f), new Color(0.36f, 0.42f, 0.30f) },
        _ => new[] { new Color(0.40f, 0.35f, 0.28f), new Color(0.66f, 0.62f, 0.54f), new Color(0.50f, 0.26f, 0.20f) },
    };

    private static Color CategoryTint(string category) => category switch
    {
        "Nature" => new Color(0.32f, 0.50f, 0.26f),
        "Buildings" => new Color(0.58f, 0.55f, 0.50f),
        "Vehicles" => new Color(0.45f, 0.50f, 0.60f),
        "Sci-fi" => new Color(0.60f, 0.62f, 0.70f),
        "Dungeon" => new Color(0.52f, 0.46f, 0.40f),
        "Characters" => new Color(0.70f, 0.55f, 0.40f),
        "Weapons" => new Color(0.38f, 0.38f, 0.44f),
        "Military" => new Color(0.42f, 0.48f, 0.34f),
        "Terrain" => new Color(0.60f, 0.45f, 0.34f),
        _ => new Color(0.60f, 0.55f, 0.45f),
    };
}
