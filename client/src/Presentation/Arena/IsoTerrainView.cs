using System;
using System.Collections.Generic;
using Godot;
using TankGame.Domain;
using TankGame.GameLogic;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>Renders every non-floor cell of an <see cref="IWallGrid"/> as a native isometric tile —
/// one <see cref="Sprite2D"/> per cell (Phase 2). Flat terrain (water, bridge) sits just above the
/// ground and below every entity; the raised blocks (mountain, brick, steel, crate, building)
/// depth-sort against the tanks via <see cref="IsoProjection.DepthOf"/> (greater <c>x+y</c> draws in
/// front), which a single flat tilemap could not do. A pure mirror: it tracks
/// <see cref="IWallGrid.CellChanged"/> to re-tile a cell that changes — so a brick weakens through its
/// damage frames and a broken crate/brick vanishes. Replaces the old square <c>WallGridView</c>.</summary>
public partial class IsoTerrainView : Node2D
{
    // Flat terrain sits above the ground (ZIndex -10) but below every entity (ZIndex ≥ 0).
    private const int FlatTerrainZ = -5;

    // The tile art is anchored by its base diamond, which every tile places 33 px from its bottom
    // edge (the PixVoxel tiles and the generated blocks share this), so a tall block rises while its
    // base stays on the cell. Centred sprite ⇒ lift the art by (33 − height/2).
    private const float BaseDiamondFromBottom = 33f;

    private readonly Dictionary<(int X, int Y), Sprite2D> _tiles = new();
    private float _tileSize = 64f;
    private IWallGrid? _grid;

    /// <summary>Binds the grid, lays the terrain once, and tracks future cell changes.
    /// <paramref name="tileSize"/> is the world-space size of one cell.</summary>
    public void Bind(IWallGrid grid, float tileSize)
    {
        _grid = grid;
        _tileSize = tileSize;
        grid.CellChanged += OnCellChanged;

        for (var x = 0; x < grid.Width; x++)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                Retile(x, y, grid.GetCell(x, y));
            }
        }
    }

    private void OnCellChanged(WallCellChanged change) => Retile(change.X, change.Y, change.Cell);

    private void Retile(int x, int y, WallCell cell)
    {
        if (_tiles.Remove((x, y), out var existing))
        {
            // Detach now (not just QueueFree, which is deferred) so the replacement is the only tile
            // at this cell immediately — a material/damage change is rare, so re-tiling is cheap.
            RemoveChild(existing);
            existing.QueueFree();
        }

        if (TextureFor(cell) is not { } path)
        {
            return; // a floor cell — nothing to draw
        }

        var texture = GD.Load<Texture2D>(path);
        var centre = IsoProjection.WorldToScreen(CellCentre(x, y));
        var sprite = new Sprite2D
        {
            Name = $"Terrain_{x}_{y}",
            Texture = texture,
            Position = new Vector2(centre.X, centre.Y),
            Offset = new Vector2(0f, BaseDiamondFromBottom - ((texture?.GetHeight() ?? 0) / 2f)),
            ZIndex = IsRaised(cell.Material)
                ? IsoProjection.DepthOf(CellCentre(x, y)) // sort against the tanks
                : FlatTerrainZ,                            // flat water/bridge under every entity
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, // keep the pixel art crisp
        };
        AddChild(sprite);
        _tiles[(x, y)] = sprite;
    }

    // Solid blocks have height and must interleave with the tanks; water and bridge lie flat.
    private static bool IsRaised(CellMaterial material) => material is not (CellMaterial.Water or CellMaterial.Bridge);

    /// <summary>The catalogue tile path for a cell, or null for a floor cell. Brick reports its damage
    /// frame from the cell's hit points. Public so a test can assert the mapping.</summary>
    public static string? TextureFor(WallCell cell)
    {
        var c = AssetCatalogue.Active;
        return cell.Material switch
        {
            CellMaterial.Water => c.WaterTile,
            CellMaterial.Bridge => c.BridgeTile,
            CellMaterial.Mountain => c.MountainTile,
            CellMaterial.Steel => c.SteelTile,
            CellMaterial.Crate => c.CrateTile,
            CellMaterial.Building => c.BuildingTile,
            CellMaterial.Brick => Math.Clamp(WallGrid.DefaultBrickHp - cell.Hp, 0, 2) switch
            {
                0 => c.BrickIntactTile,
                1 => c.BrickCrackedTile,
                _ => c.BrickRubbleTile,
            },
            _ => null,
        };
    }

    private NVector2 CellCentre(int x, int y) =>
        new((x + 0.5f) * _tileSize, (y + 0.5f) * _tileSize);
}
