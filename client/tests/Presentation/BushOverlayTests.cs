using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class BushOverlayTests : TestClass
{
    private BushOverlay _overlay = default!;

    public BushOverlayTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _overlay = new BushOverlay();
        TestScene.AddChild(_overlay);
    }

    [Cleanup]
    public void Cleanup() => _overlay.QueueFree();

    [Test]
    public void Bind_DrawsOnePatchPerBushCell_PositionedOnItsTile()
    {
        // [x, y]: bushes at (1,0) and (1,1); everything else clear.
        var bushes = new bool[2, 2];
        bushes[1, 0] = true;
        bushes[1, 1] = true;

        _overlay.Bind(bushes, tileSize: 64f);

        if (_overlay.GetChildCount() != 2)
        {
            throw new System.Exception($"Expected 2 bush patches, got {_overlay.GetChildCount()}.");
        }

        var patch = _overlay.GetNode<Polygon2D>("Bush_1_0");
        if (patch.Position != new Vector2(64f, 0f))
        {
            throw new System.Exception($"Bush (1,0) should sit on its tile; was {patch.Position}.");
        }
    }

    [Test]
    public void Bind_DrawsNothing_WhenThereAreNoBushes()
    {
        _overlay.Bind(new bool[3, 3], tileSize: 64f);

        if (_overlay.GetChildCount() != 0)
        {
            throw new System.Exception("A bushless level must draw no patches.");
        }
    }
}
