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
    private const long BareGeometryBytes = 20_000; // below this a Kenney .glb has no texture — tint it

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
        if (new FileInfo(path).Length < BareGeometryBytes)
        {
            ModelFit.Tint(model, CategoryTint(AssetLibrary.CategoryFor(_assetId.Split('/')[0])));
        }

        RotationDegrees = new Vector3(_pose.PitchDeg, _pose.YawDeg, _pose.RollDeg);
        Scale = Vector3.One * _pose.Scale;
    }

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
