using System.Collections.Generic;
using Godot;
using TankGame.Domain;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>Renders the natural terrain materials (water, bridge, mountain) of an
/// <see cref="IWallGrid"/> as native PixVoxel isometric tiles — one <see cref="Sprite2D"/> per cell
/// (Phase 2). The flat ones (water, bridge) sit just above the ground and below every entity; the
/// raised mountain depth-sorts against the tanks via <see cref="IsoProjection.DepthOf"/> (greater
/// <c>x+y</c> draws in front), which a single flat tilemap could not do. A pure mirror: it tracks
/// <see cref="IWallGrid.CellChanged"/> to re-tile a cell that changes. The man-made walls
/// (brick/steel/crate/building) stay on <see cref="WallGridView"/> until their own iso pass.</summary>
public partial class IsoTerrainView : Node2D
{
    // Flat terrain sits above the ground (ZIndex -10) but below every entity (ZIndex ≥ 0).
    private const int FlatTerrainZ = -5;

    // The tile art is lifted so its diamond centres on the cell, matching the ground tilemap's
    // texture origin; raised tiles share the same base diamond and naturally extend upward.
    private static readonly Vector2 TileArtOffset = new(0f, -11f);

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
            existing.QueueFree(); // re-tile from scratch (a material change is rare and cheap here)
        }

        if (TextureFor(cell.Material) is not { } path)
        {
            return; // not a natural-terrain cell — WallGridView (or nothing) handles it
        }

        var centre = IsoProjection.WorldToScreen(CellCentre(x, y));
        var sprite = new Sprite2D
        {
            Name = $"Terrain_{x}_{y}",
            Texture = GD.Load<Texture2D>(path),
            Position = new Vector2(centre.X, centre.Y),
            Offset = TileArtOffset,
            ZIndex = cell.Material == CellMaterial.Mountain
                ? IsoProjection.DepthOf(CellCentre(x, y)) // raised: sort against the tanks
                : FlatTerrainZ,                            // flat water/bridge: under every entity
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, // keep the pixel art crisp
        };
        AddChild(sprite);
        _tiles[(x, y)] = sprite;
    }

    /// <summary>The catalogue tile path for a natural-terrain material, or null for anything this
    /// view does not render. Public so a test can assert the mapping.</summary>
    public static string? TextureFor(CellMaterial material) => material switch
    {
        CellMaterial.Water => AssetCatalogue.Active.WaterTile,
        CellMaterial.Bridge => AssetCatalogue.Active.BridgeTile,
        CellMaterial.Mountain => AssetCatalogue.Active.MountainTile,
        _ => null,
    };

    private NVector2 CellCentre(int x, int y) =>
        new((x + 0.5f) * _tileSize, (y + 0.5f) * _tileSize);
}
