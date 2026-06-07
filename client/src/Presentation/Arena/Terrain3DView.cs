using System.Collections.Generic;
using Godot;
using TankGame.Domain;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>Renders the arena's terrain in 3D (ADR-0017, the 3D replacement for <c>IsoTerrainView</c>)
/// from real CC0 Kenney models — a brick/crate/steel/mountain/building per wall cell, a wooden deck per
/// bridge cell, grass tufts on bush cells — plus water planes and sandbag mounds. Each model is
/// auto-fitted at runtime: its bounding box is measured and it is scaled to the cell and seated on the
/// ground, so no per-model scale constant is needed. Subscribes to <see cref="IWallGrid.CellChanged"/> so
/// a destroyed wall disappears. The depth buffer sorts everything — no ZIndex.</summary>
public partial class Terrain3DView : Node3D
{
    private const float WallFootprint = 0.98f;     // walls fill the cell
    private const float MountainFootprint = 1.2f;  // rocks spill a little past the cell
    private const float BushFootprint = 0.85f;
    private const float BridgeFootprint = 1.0f;
    private const float WaterY = 1.5f;

    private static readonly Dictionary<CellMaterial, string> ModelPaths = new()
    {
        [CellMaterial.Brick] = "res://src/Presentation/Arena/models/TerrainBrick.glb",
        [CellMaterial.Crate] = "res://src/Presentation/Arena/models/TerrainCrate.glb",
        [CellMaterial.Steel] = "res://src/Presentation/Arena/models/TerrainSteel.glb",
        [CellMaterial.Mountain] = "res://src/Presentation/Arena/models/TerrainMountain.glb",
        [CellMaterial.Building] = "res://src/Presentation/Arena/models/TerrainBuilding.glb",
        [CellMaterial.Bridge] = "res://src/Presentation/Arena/models/TerrainBridge.glb",
    };

    private readonly Dictionary<CellMaterial, PackedScene> _cache = new();
    private readonly Dictionary<(int X, int Y), Node3D> _destructibles = new();
    private IWallGrid _grid = null!;
    private float _tileSize;

    public void Bind(IWallGrid grid, bool[,] bushes, bool[,] sandbags, float tileSize)
    {
        _grid = grid;
        _tileSize = tileSize;

        for (var x = 0; x < grid.Width; x++)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                BuildCell(x, y, grid.GetCell(x, y));
            }
        }

        BuildBushes(bushes);
        BuildSandbags(sandbags);
        grid.CellChanged += OnCellChanged;
    }

    public override void _ExitTree()
    {
        if (_grid is not null)
        {
            _grid.CellChanged -= OnCellChanged;
        }
    }

    // Only destructibles (brick/crate) ever change, always to Floor — drop the model when they break.
    private void OnCellChanged(WallCellChanged change)
    {
        if (CellMaterials.BlocksMovement(change.Cell.Material))
        {
            return; // still solid (a brick that only lost hit points) — keep the model
        }

        if (_destructibles.Remove((change.X, change.Y), out var node))
        {
            node.QueueFree();
        }
    }

    private void BuildCell(int x, int y, WallCell cell)
    {
        var centre = CellCentre(x, y);
        switch (cell.Material)
        {
            case CellMaterial.Floor:
                return;
            case CellMaterial.Water:
                AddWater(centre, $"Water_{x}_{y}");
                return;
            case CellMaterial.Bridge:
                Place(cell.Material, centre, BridgeFootprint, $"Bridge_{x}_{y}");
                return;
            case CellMaterial.Mountain:
                Place(cell.Material, centre, MountainFootprint, $"Mountain_{x}_{y}");
                return;
            default:
                var node = Place(cell.Material, centre, WallFootprint, $"Wall_{x}_{y}");
                if (cell.Material is CellMaterial.Brick or CellMaterial.Crate)
                {
                    _destructibles[(x, y)] = node; // track so it can be removed when destroyed
                }

                return;
        }
    }

    // Instance the model for a material, auto-fit it to the cell, and seat it on the ground.
    private Node3D Place(CellMaterial material, NVector2 centre, float footprint, string name)
    {
        var holder = new Node3D { Name = name, Position = new Vector3(centre.X, 0f, centre.Y) };
        AddChild(holder);

        var model = Model(material).Instantiate<Node3D>();
        holder.AddChild(model); // add first so global transforms are valid for the fit measure
        ModelFit.Apply(model, _tileSize * footprint, seatOnGround: true);
        return holder;
    }

    private PackedScene Model(CellMaterial material)
    {
        if (!_cache.TryGetValue(material, out var scene))
        {
            scene = GD.Load<PackedScene>(ModelPaths[material]);
            _cache[material] = scene;
        }

        return scene;
    }

    private void AddWater(NVector2 centre, string name) =>
        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new PlaneMesh { Size = new Vector2(_tileSize, _tileSize) },
            Position = new Vector3(centre.X, WaterY, centre.Y),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.20f, 0.42f, 0.66f), Roughness = 0.3f },
        });

    private void BuildBushes(bool[,] bushes)
    {
        var scene = GD.Load<PackedScene>("res://src/Presentation/Arena/models/TerrainBush.glb");
        for (var x = 0; x < bushes.GetLength(0); x++)
        {
            for (var y = 0; y < bushes.GetLength(1); y++)
            {
                if (!bushes[x, y])
                {
                    continue;
                }

                var centre = CellCentre(x, y);
                var holder = new Node3D { Name = $"Bush_{x}_{y}", Position = new Vector3(centre.X, 0f, centre.Y) };
                AddChild(holder);
                var model = scene.Instantiate<Node3D>();
                holder.AddChild(model);
                ModelFit.Apply(model, _tileSize * BushFootprint, seatOnGround: true);
            }
        }
    }

    private void BuildSandbags(bool[,] sandbags)
    {
        var mesh = new BoxMesh { Size = new Vector3(_tileSize * 0.8f, 16f, _tileSize * 0.8f) };
        for (var x = 0; x < sandbags.GetLength(0); x++)
        {
            for (var y = 0; y < sandbags.GetLength(1); y++)
            {
                if (!sandbags[x, y])
                {
                    continue;
                }

                var centre = CellCentre(x, y);
                AddChild(new MeshInstance3D
                {
                    Name = $"Sandbag_{x}_{y}",
                    Mesh = mesh,
                    Position = new Vector3(centre.X, 8f, centre.Y),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.64f, 0.42f), Roughness = 1f },
                });
            }
        }
    }

    private NVector2 CellCentre(int x, int y) => new((x + 0.5f) * _tileSize, (y + 0.5f) * _tileSize);
}
