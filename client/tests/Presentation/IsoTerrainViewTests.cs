using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain;
using TankGame.GameLogic;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class IsoTerrainViewTests : TestClass
{
    private IsoTerrainView _view = default!;

    public IsoTerrainViewTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _view = new IsoTerrainView();
        TestScene.AddChild(_view);
    }

    [Cleanup]
    public void Cleanup() => _view.QueueFree();

    [Test]
    public void Bind_DrawsWaterBridgeAndMountain_ButNotWallsOrFloor()
    {
        // [x, y]: (0,0) water, (1,0) bridge, (2,0) mountain, (3,0) brick, (4,0) floor.
        var grid = WallGrid.FromMaterials(new[,]
        {
            { CellMaterial.Water },
            { CellMaterial.Bridge },
            { CellMaterial.Mountain },
            { CellMaterial.Brick },
            { CellMaterial.Floor },
        });

        _view.Bind(grid, tileSize: 64f);

        // Three natural-terrain tiles; the brick and floor are not this view's job.
        if (_view.GetChildCount() != 3)
        {
            throw new System.Exception($"Expected 3 terrain tiles (water/bridge/mountain), got {_view.GetChildCount()}.");
        }

        if (_view.GetNodeOrNull<Sprite2D>("Terrain_3_0") is not null
            || _view.GetNodeOrNull<Sprite2D>("Terrain_4_0") is not null)
        {
            throw new System.Exception("IsoTerrainView must not draw walls or floor.");
        }
    }

    [Test]
    public void MountainDepthSorts_AboveFlatWater()
    {
        var grid = WallGrid.FromMaterials(new[,]
        {
            { CellMaterial.Water },
            { CellMaterial.Mountain },
        });

        _view.Bind(grid, tileSize: 64f);

        var water = _view.GetNode<Sprite2D>("Terrain_0_0");
        var mountain = _view.GetNode<Sprite2D>("Terrain_1_0");

        // Flat water sits beneath every entity; the raised mountain carries a positive depth so it
        // sorts against the tanks.
        if (water.ZIndex >= 0)
        {
            throw new System.Exception($"Flat water must sit below entities; ZIndex was {water.ZIndex}.");
        }

        if (mountain.ZIndex <= water.ZIndex)
        {
            throw new System.Exception($"Raised mountain must depth-sort above flat water; was {mountain.ZIndex} vs {water.ZIndex}.");
        }
    }

    [Test]
    public void Tile_SitsAtTheProjectedCellCentre()
    {
        var grid = WallGrid.FromMaterials(new[,] { { CellMaterial.Floor }, { CellMaterial.Mountain } });
        _view.Bind(grid, tileSize: 64f);

        var mountain = _view.GetNode<Sprite2D>("Terrain_1_0");
        // Cell (1,0) centre world (96,32) → iso ((96-32)*1, (96+32)*0.5) = (64, 64).
        if (Mathf.Abs(mountain.Position.X - 64f) > 0.5f || Mathf.Abs(mountain.Position.Y - 64f) > 0.5f)
        {
            throw new System.Exception($"Mountain (1,0) should project to (64,64); was {mountain.Position}.");
        }
    }
}
