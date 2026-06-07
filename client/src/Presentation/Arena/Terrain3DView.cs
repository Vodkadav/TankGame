using System;
using System.Collections.Generic;
using Godot;
using TankGame.Domain;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>Renders the arena's terrain in 3D (ADR-0017, the 3D replacement for <c>IsoTerrainView</c>):
/// a box per wall cell (brick/crate/steel/mountain/building), a plane per water cell, a deck per bridge
/// cell, plus bush clumps and sandbag mounds on the passable cells. Subscribes to the grid's
/// <see cref="IWallGrid.CellChanged"/> so a brick that cracks recolours and a destroyed wall disappears,
/// keeping the view a pure mirror of the model. The depth buffer sorts everything — no ZIndex.</summary>
public partial class Terrain3DView : Node3D
{
    private const float WallHeight = 64f;
    private const float MountainHeight = 122f;
    private const float WaterY = 1.5f;
    private const float BridgeDeckHeight = 10f;

    private IWallGrid _grid = null!;
    private float _tileSize;
    private readonly Dictionary<(int X, int Y), MeshInstance3D> _wallNodes = new();
    private BoxMesh _wallBox = null!;
    private BoxMesh _mountainBox = null!;

    public void Bind(IWallGrid grid, bool[,] bushes, bool[,] sandbags, float tileSize)
    {
        _grid = grid;
        _tileSize = tileSize;
        _wallBox = new BoxMesh { Size = new Vector3(tileSize, WallHeight, tileSize) };
        _mountainBox = new BoxMesh { Size = new Vector3(tileSize, MountainHeight, tileSize) };

        for (var x = 0; x < grid.Width; x++)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                BuildCell(x, y, grid.GetCell(x, y));
            }
        }

        BuildScatter(bushes, BushMesh(), "Bush");
        BuildScatter(sandbags, SandbagMesh(), "Sandbag");

        grid.CellChanged += OnCellChanged;
    }

    public override void _ExitTree()
    {
        if (_grid is not null)
        {
            _grid.CellChanged -= OnCellChanged;
        }
    }

    private void OnCellChanged(WallCellChanged change)
    {
        if (_wallNodes.Remove((change.X, change.Y), out var old))
        {
            old.QueueFree(); // brick cracked or broke — rebuild it (or leave it gone if now floor)
        }

        BuildCell(change.X, change.Y, change.Cell);
    }

    private void BuildCell(int x, int y, WallCell cell)
    {
        var centre = CellCentre(x, y);
        switch (cell.Material)
        {
            case CellMaterial.Floor:
                return;
            case CellMaterial.Water:
                AddFlat(centre, WaterY, _tileSize, new Color(0.20f, 0.42f, 0.66f), $"Water_{x}_{y}");
                return;
            case CellMaterial.Bridge:
                AddBox(new BoxMesh { Size = new Vector3(_tileSize, BridgeDeckHeight, _tileSize) },
                    centre, BridgeDeckHeight / 2f, new Color(0.55f, 0.40f, 0.24f), $"Bridge_{x}_{y}");
                return;
            case CellMaterial.Mountain:
                Track(x, y, AddBox(_mountainBox, centre, MountainHeight / 2f, WallColour(cell), $"Mountain_{x}_{y}"));
                return;
            default:
                Track(x, y, AddBox(_wallBox, centre, WallHeight / 2f, WallColour(cell), $"Wall_{x}_{y}"));
                return;
        }
    }

    private void Track(int x, int y, MeshInstance3D node) => _wallNodes[(x, y)] = node;

    private MeshInstance3D AddBox(Mesh mesh, NVector2 centre, float halfHeight, Color colour, string name)
    {
        var node = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            Position = new Vector3(centre.X, halfHeight, centre.Y),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = colour, Roughness = 1f },
        };
        AddChild(node);
        return node;
    }

    private void AddFlat(NVector2 centre, float y, float size, Color colour, string name) =>
        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new PlaneMesh { Size = new Vector2(size, size) },
            Position = new Vector3(centre.X, y, centre.Y),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.4f },
        });

    private void BuildScatter(bool[,] cells, Mesh mesh, string name)
    {
        for (var x = 0; x < cells.GetLength(0); x++)
        {
            for (var y = 0; y < cells.GetLength(1); y++)
            {
                if (!cells[x, y])
                {
                    continue;
                }

                var centre = CellCentre(x, y);
                AddChild(new MeshInstance3D
                {
                    Name = $"{name}_{x}_{y}",
                    Mesh = mesh,
                    Position = new Vector3(centre.X, mesh is SphereMesh ? 12f : 8f, centre.Y),
                    MaterialOverride = name == "Bush"
                        ? new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.48f, 0.22f), Roughness = 1f }
                        : new StandardMaterial3D { AlbedoColor = new Color(0.72f, 0.64f, 0.42f), Roughness = 1f },
                });
            }
        }
    }

    private Mesh BushMesh() => new SphereMesh { Radius = _tileSize * 0.38f, Height = _tileSize * 0.42f };

    private Mesh SandbagMesh() => new BoxMesh { Size = new Vector3(_tileSize * 0.8f, 16f, _tileSize * 0.8f) };

    // Brick and crate darken as they lose hit points, so damage reads before they break into floor.
    private static Color WallColour(WallCell cell) => cell.Material switch
    {
        CellMaterial.Brick => Damaged(new Color(0.80f, 0.38f, 0.26f), cell.Hp, GameLogic.WallGrid.DefaultBrickHp),
        CellMaterial.Crate => Damaged(new Color(0.62f, 0.45f, 0.25f), cell.Hp, GameLogic.WallGrid.DefaultCrateHp),
        CellMaterial.Steel => new Color(0.55f, 0.57f, 0.60f),
        CellMaterial.Mountain => new Color(0.42f, 0.44f, 0.42f),
        CellMaterial.Building => new Color(0.74f, 0.74f, 0.72f),
        _ => new Color(0.5f, 0.5f, 0.5f),
    };

    private static Color Damaged(Color full, int hp, int maxHp)
    {
        var t = maxHp > 0 ? Mathf.Clamp(hp / (float)maxHp, 0.35f, 1f) : 1f;
        return new Color(full.R * t, full.G * t, full.B * t);
    }

    private NVector2 CellCentre(int x, int y) => new((x + 0.5f) * _tileSize, (y + 0.5f) * _tileSize);
}
