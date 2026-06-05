using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class SandbagOverlayTests : TestClass
{
    private SandbagOverlay _overlay = default!;

    public SandbagOverlayTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _overlay = new SandbagOverlay();
        TestScene.AddChild(_overlay);
    }

    [Cleanup]
    public void Cleanup() => _overlay.QueueFree();

    [Test]
    public void Bind_DrawsAPatch_PerSandbagCell()
    {
        var sandbags = new bool[3, 3];
        sandbags[1, 1] = true;
        sandbags[2, 0] = true;

        _overlay.Bind(sandbags, tileSize: 64f);

        var patches = 0;
        foreach (var child in _overlay.GetChildren())
        {
            if (child is Sprite2D)
            {
                patches++;
            }
        }

        if (patches != 2)
        {
            throw new System.Exception($"Expected one iso cluster per sandbag cell (2); saw {patches}.");
        }
    }
}
