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
    private const float MountainFootprint = 1.7f;  // rocks overlap their neighbours into one mass
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

    // The bare Kenney .glb ship without their shared colormap texture, so these models render white — give
    // each a suitable flat colour. Bridge (Nature Kit) keeps its own look, so it is not listed here.
    // Brick/steel are plain blocks tinted here; crate/mountain keep shape but get a colour; building keeps
    // its own embedded texture (a real city model), so it is NOT tinted.
    private static readonly Dictionary<CellMaterial, Color> TintColours = new()
    {
        [CellMaterial.Brick] = new Color(0.74f, 0.33f, 0.22f),   // red brick block
        [CellMaterial.Crate] = new Color(0.58f, 0.41f, 0.22f),   // wood
        [CellMaterial.Steel] = new Color(0.38f, 0.42f, 0.50f),   // dark steel-blue (was washing out white)
        [CellMaterial.Mountain] = new Color(0.46f, 0.44f, 0.40f), // rock
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
                // Each mountain cell is one enlarged rock spun to a per-cell angle, so adjacent cells'
                // rocks overlap and vary into one cohesive rocky mass instead of separate tidy lumps.
                var rock = Place(cell.Material, centre, MountainFootprint, $"Mountain_{x}_{y}");
                rock.RotateY(Mathf.DegToRad(((x * 71) + (y * 137)) % 360));
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
        if (TintColours.TryGetValue(material, out var colour))
        {
            ModelFit.Tint(model, colour);
        }

        holder.AddChild(DebugLabel.Make(material.ToString(), 75f));
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

    private void AddWater(NVector2 centre, string name)
    {
        var water = new MeshInstance3D
        {
            Name = name,
            Mesh = new PlaneMesh { Size = new Vector2(_tileSize, _tileSize) },
            Position = new Vector3(centre.X, WaterY, centre.Y),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.20f, 0.42f, 0.66f), Roughness = 0.3f },
        };
        water.AddChild(DebugLabel.Make("Water", 30f));
        AddChild(water);
    }

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
                holder.AddChild(DebugLabel.Make("Bush", 55f));
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
                var bag = new MeshInstance3D
                {
                    Name = $"Sandbag_{x}_{y}",
                    Mesh = mesh,
                    Position = new Vector3(centre.X, 8f, centre.Y),
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.40f, 0.44f, 0.30f), // olive-khaki, distinct from the sand
                        Roughness = 1f,
                        SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
                    },
                };
                bag.AddChild(DebugLabel.Make("Sandbag", 40f));
                AddChild(bag);
            }
        }
    }

    private NVector2 CellCentre(int x, int y) => new((x + 0.5f) * _tileSize, (y + 0.5f) * _tileSize);
}
