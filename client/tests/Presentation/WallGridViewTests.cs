using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain;
using TankGame.GameLogic;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class WallGridViewTests : TestClass
{
    private WallGridView _view = default!;

    public WallGridViewTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _view = new WallGridView();
        TestScene.AddChild(_view); // runs _Ready, which builds the atlas TileSet
    }

    [Cleanup]
    public void Cleanup() => _view.QueueFree();

    [Test]
    public void Bind_PlacesTheRightAtlasFramePerMaterial()
    {
        // [x, y]: (0,0) brick, (1,0) steel, (2,0) floor.
        var grid = WallGrid.FromMaterials(new[,]
        {
            { CellMaterial.Brick },
            { CellMaterial.Steel },
            { CellMaterial.Floor },
        });

        _view.Bind(grid);

        AssertFrame(0, 0, 0); // intact brick -> frame 0
        AssertFrame(1, 0, 3); // steel -> frame 3
        if (_view.GetCellSourceId(new Vector2I(2, 0)) != -1)
        {
            throw new System.Exception("Floor cells must render no tile.");
        }
    }

    [Test]
    public void CellChanged_UpdatesTheFrame_AsBrickTakesDamageAndBreaks()
    {
        var grid = WallGrid.FromMaterials(new[,] { { CellMaterial.Brick } });
        _view.Bind(grid);
        AssertFrame(0, 0, 0); // intact

        grid.DamageCell(0, 0, 1); // hp 2 -> cracked
        AssertFrame(0, 0, 1);

        grid.DamageCell(0, 0, 1); // hp 1 -> rubble
        AssertFrame(0, 0, 2);

        grid.DamageCell(0, 0, 1); // breaks -> floor -> no tile
        if (_view.GetCellSourceId(new Vector2I(0, 0)) != -1)
        {
            throw new System.Exception("A broken brick must render no tile.");
        }
    }

    private void AssertFrame(int x, int y, int expectedFrameX)
    {
        var coords = _view.GetCellAtlasCoords(new Vector2I(x, y));
        if (coords != new Vector2I(expectedFrameX, 0))
        {
            throw new System.Exception($"Cell ({x},{y}) should show atlas frame {expectedFrameX}; was {coords.X}.");
        }
    }
}
