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
        [CellMaterial.Brick] = new Color(0.55f, 0.41f, 0.26f),   // wooden fence
        [CellMaterial.Crate] = new Color(0.58f, 0.41f, 0.22f),   // wood
        [CellMaterial.Steel] = new Color(0.38f, 0.42f, 0.50f),   // dark steel-blue (was washing out white)
        [CellMaterial.Mountain] = new Color(0.46f, 0.44f, 0.40f), // rock
    };

    private static readonly Color BuildingColour = new(0.74f, 0.62f, 0.50f); // warm concrete

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

        BuildBuildings(grid); // one model per connected block of building cells, not one per cell
        BuildBushes(bushes);
        BuildOilPuddles(sandbags);
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
            case CellMaterial.Building: // buildings are placed as one model per block, in BuildBuildings
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

    // The slow-going patches render as a dark oil spill: two merged flat discs (a filled figure-8), the
    // larger overlapping a smaller one. Oily sheen (low roughness, a little metallic).
    private void BuildOilPuddles(bool[,] oil)
    {
        var slick = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.05f, 0.07f),
            Metallic = 0.4f,
            Roughness = 0.2f,
        };
        for (var x = 0; x < oil.GetLength(0); x++)
        {
            for (var y = 0; y < oil.GetLength(1); y++)
            {
                if (!oil[x, y])
                {
                    continue;
                }

                var centre = CellCentre(x, y);
                var holder = new Node3D { Name = $"Oil_{x}_{y}", Position = new Vector3(centre.X, 0.6f, centre.Y) };
                AddChild(holder);
                holder.AddChild(Blob(slick, 22f, new Vector3(-7f, 0f, -4f)));  // larger lobe
                holder.AddChild(Blob(slick, 15f, new Vector3(13f, 0f, 7f)));   // smaller lobe → a filled "8"
                holder.AddChild(DebugLabel.Make("Oil", 30f));
            }
        }
    }

    private static MeshInstance3D Blob(Material material, float radius, Vector3 offset) => new()
    {
        Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.8f },
        Position = offset,
        MaterialOverride = material,
    };

    // Each connected block of building cells (the generator lays them as small rectangles) is one
    // building model stretched over the whole block, instead of a separate building per cell.
    private void BuildBuildings(IWallGrid grid)
    {
        var visited = new bool[grid.Width, grid.Height];
        for (var x = 0; x < grid.Width; x++)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                if (visited[x, y] || grid.GetCell(x, y).Material != CellMaterial.Building)
                {
                    continue;
                }

                int minX = x, maxX = x, minY = y, maxY = y;
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((x, y));
                visited[x, y] = true;
                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();
                    minX = Mathf.Min(minX, cx); maxX = Mathf.Max(maxX, cx);
                    minY = Mathf.Min(minY, cy); maxY = Mathf.Max(maxY, cy);
                    foreach (var (nx, ny) in new[] { (cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1) })
                    {
                        if (nx >= 0 && ny >= 0 && nx < grid.Width && ny < grid.Height && !visited[nx, ny]
                            && grid.GetCell(nx, ny).Material == CellMaterial.Building)
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }
                }

                PlaceBuilding(minX, maxX, minY, maxY);
            }
        }
    }

    private void PlaceBuilding(int minX, int maxX, int minY, int maxY)
    {
        var centreX = (minX + maxX + 1) / 2f * _tileSize;
        var centreZ = (minY + maxY + 1) / 2f * _tileSize;
        var spanX = (maxX - minX + 1) * _tileSize;
        var spanZ = (maxY - minY + 1) * _tileSize;

        var holder = new Node3D { Name = $"Building_{minX}_{minY}", Position = new Vector3(centreX, 0f, centreZ) };
        AddChild(holder);
        var model = Model(CellMaterial.Building).Instantiate<Node3D>();
        holder.AddChild(model);
        ModelFit.ApplyBox(model, spanX * 0.98f, spanZ * 0.98f, seatOnGround: true);
        ModelFit.Tint(model, BuildingColour);
        holder.AddChild(DebugLabel.Make("Building", 90f));
    }

    private NVector2 CellCentre(int x, int y) => new((x + 0.5f) * _tileSize, (y + 0.5f) * _tileSize);
}
