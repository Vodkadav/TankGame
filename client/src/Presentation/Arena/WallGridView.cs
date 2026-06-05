using System;
using Godot;
using TankGame.Domain;
using TankGame.GameLogic;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IWallGrid"/> as a tilemap. A pure mirror: it draws each
/// cell with the atlas frame matching its material and brick damage, and subscribes to
/// <see cref="IWallGrid.CellChanged"/> to re-tile just the cells that change. The atlas is
/// the placeholder <c>Walls.png</c> (frames: 0 brick intact, 1 cracked, 2 rubble, 3 steel;
/// floor draws no tile). Built in code so no binary TileSet resource is needed.</summary>
public partial class WallGridView : TileMapLayer
{
    private const int AtlasTile = 32;
    private const int SteelFrame = 3;
    private const int CrateFrame = 4;

    private int _sourceId = -1;
    private IWallGrid? _grid;

    /// <summary>World-space size of one rendered tile. Defaults to the atlas tile size; set
    /// larger to scale the placeholder art up to the gameplay grid (the arena uses 64).</summary>
    public int RenderTileSize { get; set; } = AtlasTile;

    public override void _Ready() => EnsureTileSet();

    /// <summary>Binds the grid, draws it once, and tracks future cell changes.</summary>
    public void Bind(IWallGrid grid)
    {
        EnsureTileSet();
        _grid = grid;
        grid.CellChanged += OnCellChanged;

        for (var x = 0; x < grid.Width; x++)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                Draw(x, y, grid.GetCell(x, y));
            }
        }
    }

    private void OnCellChanged(WallCellChanged change) => Draw(change.X, change.Y, change.Cell);

    private void Draw(int x, int y, WallCell cell)
    {
        var coords = new Vector2I(x, y);
        if (FrameFor(cell) is { } frame)
        {
            SetCell(coords, _sourceId, new Vector2I(frame, 0));
        }
        else
        {
            EraseCell(coords); // floor
        }
    }

    // Brick damage maps onto the first three frames (intact -> rubble); steel is the last
    // frame; floor has no frame.
    private static int? FrameFor(WallCell cell) => cell.Material switch
    {
        CellMaterial.Steel => SteelFrame,
        CellMaterial.Crate => CrateFrame, // one frame; a crate just pops to floor when destroyed
        CellMaterial.Brick => Math.Clamp(WallGrid.DefaultBrickHp - cell.Hp, 0, 2),
        _ => null,
    };

    private void EnsureTileSet()
    {
        if (TileSet is not null)
        {
            return;
        }

        var texture = GD.Load<Texture2D>(AssetCatalogue.Active.WallAtlas);
        var source = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = new Vector2I(AtlasTile, AtlasTile),
        };
        for (var frame = 0; frame <= CrateFrame; frame++)
        {
            source.CreateTile(new Vector2I(frame, 0));
        }

        var tileSet = new TileSet { TileSize = new Vector2I(RenderTileSize, RenderTileSize) };
        _sourceId = tileSet.AddSource(source);
        TileSet = tileSet;
    }
}
