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
    private const float WaterY = 1.5f;

    // The dusty rock colour for the raised plateau tops and the cliff faces, so the high ground reads as
    // a solid earthen mass rather than floating walls (ADR-0018).
    private static readonly Color PlateauColour = new(0.62f, 0.52f, 0.36f);
    private static readonly Color CliffColour = new(0.50f, 0.42f, 0.30f);

    private static readonly Dictionary<CellMaterial, string> ModelPaths = new()
    {
        [CellMaterial.Brick] = "res://src/Presentation/Arena/models/TerrainBrick.glb",
        [CellMaterial.Crate] = "res://src/Presentation/Arena/models/TerrainCrate.glb",
        [CellMaterial.Steel] = "res://src/Presentation/Arena/models/TerrainSteel.glb",
        [CellMaterial.Mountain] = "res://src/Presentation/Arena/models/TerrainMountain.glb",
        [CellMaterial.Building] = "res://src/Presentation/Arena/models/TerrainBuilding.glb",
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

        BuildElevation(grid); // raised plateau tops + sloped ramps (flat maps add nothing) — ADR-0018
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
        var baseY = _grid.LayerAt(x, y) * GroundProjection.LayerHeight; // walls on a plateau sit on its top (ADR-0018)
        switch (cell.Material)
        {
            case CellMaterial.Floor:
            case CellMaterial.Building: // buildings are placed as one model per block, in BuildBuildings
                return;
            case CellMaterial.Water:
                AddWater(centre, $"Water_{x}_{y}");
                return;
            case CellMaterial.Lava:
                AddLava(centre, $"Lava_{x}_{y}");
                return;
            case CellMaterial.Bridge:
                AddRoad(centre, $"Bridge_{x}_{y}"); // a flat road tile, flush with the ground (no raised deck)
                return;
            case CellMaterial.Mountain:
                // Each mountain cell is one enlarged rock spun to a per-cell angle, so adjacent cells'
                // rocks overlap and vary into one cohesive rocky mass instead of separate tidy lumps.
                // The authored pose composes with the spin (its yaw adds), so mountains scale and
                // rotate under the gizmo like any other placed prop (owner feedback 2026-06-11).
                var rock = Place(cell.Material, centre, MountainFootprint, $"Mountain_{x}_{y}", baseY);
                var rockPose = _grid.TransformAt(x, y);
                rock.RotationDegrees = new Vector3(
                    rockPose.PitchDeg,
                    rockPose.YawDeg + (((x * 71) + (y * 137)) % 360),
                    rockPose.RollDeg);
                rock.Scale = Vector3.One * rockPose.Scale;
                return;
            default:
                var node = Place(cell.Material, centre, WallFootprint, $"Wall_{x}_{y}", baseY);
                // The authored pose (owner follow-up 2026-06-11): free rotation + uniform scale
                // from the editor's selection gizmo, cosmetic only.
                var pose = _grid.TransformAt(x, y);
                node.RotationDegrees = new Vector3(pose.PitchDeg, pose.YawDeg, pose.RollDeg);
                node.Scale = Vector3.One * pose.Scale;
                if (cell.Material is CellMaterial.Brick or CellMaterial.Crate)
                {
                    _destructibles[(x, y)] = node; // track so it can be removed when destroyed
                }

                return;
        }
    }

    // Instance the model for a material, auto-fit it to the cell, and seat it on the ground (or on a
    // raised plateau top when baseY > 0, ADR-0018).
    private Node3D Place(CellMaterial material, NVector2 centre, float footprint, string name, float baseY = 0f)
    {
        var holder = new Node3D { Name = name, Position = new Vector3(centre.X, baseY, centre.Y) };
        AddChild(holder);

        var model = Model(material).Instantiate<Node3D>();
        holder.AddChild(model); // add first so global transforms are valid for the fit measure
        ModelFit.Apply(model, _tileSize * footprint, seatOnGround: true);
        if (TintColours.TryGetValue(material, out var colour))
        {
            ModelFit.Tint(model, colour);
        }

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

    // A molten lava pool: a glowing red plane sitting near the ground, emissive so it reads as hot even
    // in shadow. Flush enough that a bridge tile (drawn just above) sits over it.
    private void AddLava(NVector2 centre, string name) =>
        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new PlaneMesh { Size = new Vector2(_tileSize, _tileSize) },
            Position = new Vector3(centre.X, 0.6f, centre.Y),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.85f, 0.18f, 0.05f),
                EmissionEnabled = true,
                Emission = new Color(0.95f, 0.30f, 0.05f),
                EmissionEnergyMultiplier = 2.5f,
                Roughness = 0.6f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            },
        });

    // A flat road tile flush with the ground (the bridge crossing over water).
    private void AddRoad(NVector2 centre, string name) =>
        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new PlaneMesh { Size = new Vector2(_tileSize, _tileSize) },
            Position = new Vector3(centre.X, WaterY + 0.5f, centre.Y),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.34f, 0.32f, 0.30f), // asphalt
                Roughness = 1f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            },
        });

    // A bush cell is a dense clump: a 3×4 grid of grass tufts filling the cell, each at its own angle and
    // stretched taller (~1.5 cells) so it reads as one big, tall, dense bush rather than a single sprig.
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
                for (var col = 0; col < 3; col++)
                {
                    for (var row = 0; row < 4; row++)
                    {
                        var ox = (col - 1) * (_tileSize * 0.30f);
                        var oz = (row - 1.5f) * (_tileSize * 0.24f);
                        var holder = new Node3D
                        {
                            Name = $"Bush_{x}_{y}_{col}_{row}",
                            Position = new Vector3(centre.X + ox, 0f, centre.Y + oz),
                        };
                        AddChild(holder);
                        var model = scene.Instantiate<Node3D>();
                        holder.AddChild(model);
                        ModelFit.Apply(model, _tileSize * 0.42f, seatOnGround: true);
                        holder.Scale = new Vector3(1.05f, 1.9f, 1.05f); // taller than a block, ~1.5 cells
                        holder.RotateY(Mathf.DegToRad(((col * 53) + (row * 97) + (x * 29) + (y * 41)) % 360));
                    }
                }
            }
        }
    }

    // The slow-going patches render as a glossy black oil puddle with an iridescent purple/blue sheen — a
    // flat decal (a textured quad lying on the floor) rotated per cell so repeats don't visibly tile. The
    // art (true-alpha PNG, AI-generated — see docs/credits/assets.md) carries the puddle shape and sheen,
    // so the mesh is just a square the texture sits on; unshaded keeps the baked sheen vivid.
    private void BuildOilPuddles(bool[,] oil)
    {
        var tex = GD.Load<Texture2D>("res://src/Presentation/Arena/icons/oil_spill.png");
        for (var x = 0; x < oil.GetLength(0); x++)
        {
            for (var y = 0; y < oil.GetLength(1); y++)
            {
                if (!oil[x, y])
                {
                    continue;
                }

                var centre = CellCentre(x, y);
                var puddle = new MeshInstance3D
                {
                    Name = $"Oil_{x}_{y}",
                    Mesh = new PlaneMesh { Size = new Vector2(_tileSize * 1.1f, _tileSize * 1.1f) },
                    Position = new Vector3(centre.X, 0.6f, centre.Y),
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoTexture = tex,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
                    },
                };
                puddle.RotateY(Mathf.DegToRad(((x * 53) + (y * 97)) % 360)); // vary orientation between cells
                AddChild(puddle);
            }
        }
    }

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
        // No tint — this model ships with its colormap texture embedded, so its walls/roof/windows/
        // chimneys keep their own colours.
    }

    // Raise the high ground (ADR-0018): every layer>0 floor cell gets a solid earthen block from the
    // valley floor up to its layer top, so the plateau reads as one raised mass with cliff faces; every
    // ramp cell gets a wedge sloping from its low side up to the next layer, the climb a tank drives. A
    // flat map (no layered or ramp cells) adds nothing here, so it stays pixel-identical.
    private void BuildElevation(IWallGrid grid)
    {
        var plateauMat = new StandardMaterial3D { AlbedoColor = PlateauColour, Roughness = 1f };
        var cliffMat = new StandardMaterial3D { AlbedoColor = CliffColour, Roughness = 1f };

        for (var x = 0; x < grid.Width; x++)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                var layer = grid.LayerAt(x, y);
                if (grid.IsRamp(x, y))
                {
                    AddRamp(grid, x, y, layer, cliffMat);
                }
                else if (layer > 0)
                {
                    AddPlateauBlock(x, y, layer, plateauMat);
                }
            }
        }
    }

    // A solid block filling the cell from the ground up to the cell's layer top: its top face is the
    // plateau a tank drives on, its sides the cliff a tank below cannot pass.
    private void AddPlateauBlock(int x, int y, int layer, Material material)
    {
        var top = layer * GroundProjection.LayerHeight;
        var centre = CellCentre(x, y);
        AddChild(new MeshInstance3D
        {
            Name = $"Plateau_{x}_{y}",
            Mesh = new BoxMesh { Size = new Vector3(_tileSize, top, _tileSize) },
            Position = new Vector3(centre.X, top / 2f, centre.Y),
            MaterialOverride = material,
        });
    }

    // A ramp wedge: a flat slab tilted so it rises from the low neighbour's level to the high one, giving
    // a visible slope a tank climbs. The tilt axis points from the low side toward the adjacent higher
    // (layer+1) cell, so the slope faces the plateau it leads onto.
    private void AddRamp(IWallGrid grid, int x, int y, int layer, Material material)
    {
        var top = (layer + 1) * GroundProjection.LayerHeight;
        var centre = CellCentre(x, y);
        var (dx, dy) = HigherNeighbour(grid, x, y, layer);

        var holder = new Node3D { Name = $"Ramp_{x}_{y}", Position = new Vector3(centre.X, top / 2f, centre.Y) };
        AddChild(holder);
        holder.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(_tileSize, top, _tileSize) },
            MaterialOverride = material,
        });
        // Tilt about the axis perpendicular to the climb direction so the slab's top face slopes up
        // toward the plateau. ~30° reads as a ramp without clipping badly into the neighbours.
        var tilt = Mathf.DegToRad(30f);
        holder.Rotation = dx != 0 ? new Vector3(0f, 0f, -dx * tilt) : new Vector3(dy * tilt, 0f, 0f);
    }

    // The cardinal direction from a ramp cell toward the adjacent cell one layer higher (the plateau the
    // ramp climbs onto); falls back to +X if none is found (a lone ramp), so the slope still has a facing.
    private static (int Dx, int Dy) HigherNeighbour(IWallGrid grid, int x, int y, int layer)
    {
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            if (grid.LayerAt(x + dx, y + dy) == layer + 1 && !grid.IsRamp(x + dx, y + dy))
            {
                return (dx, dy);
            }
        }

        return (1, 0);
    }

    private NVector2 CellCentre(int x, int y) => new((x + 0.5f) * _tileSize, (y + 0.5f) * _tileSize);
}
