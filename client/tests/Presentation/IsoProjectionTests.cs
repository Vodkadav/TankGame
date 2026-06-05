using Godot;
using Chickensoft.GoDotTest;
using NVector2 = System.Numerics.Vector2;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Exercises the pure isometric projection (grid↔screen) the Phase-1 iso pivot is built on.
public class IsoProjectionTests : TestClass
{
    public IsoProjectionTests(Node testScene) : base(testScene) { }

    [Test]
    public void WorldToScreen_Maps2To1Dimetric()
    {
        // One world unit east shifts one across and a half down; one unit south shifts one left and
        // a half down — the 2:1 diamond at native tile scale.
        var screen = IsoProjection.WorldToScreen(new NVector2(130f, 64f));
        if (Mathf.Abs(screen.X - 66f) > 0.001f || Mathf.Abs(screen.Y - 97f) > 0.001f)
        {
            throw new System.Exception($"(130,64) should project to (66,97); was {screen}.");
        }
    }

    [Test]
    public void ScreenToWorld_IsTheInverseOfWorldToScreen()
    {
        var world = new NVector2(130f, 64f);
        var round = IsoProjection.ScreenToWorld(IsoProjection.WorldToScreen(world));
        if (Mathf.Abs(round.X - world.X) > 0.001f || Mathf.Abs(round.Y - world.Y) > 0.001f)
        {
            throw new System.Exception($"Round-trip should recover {world}; was {round}.");
        }
    }

    [Test]
    public void DepthOf_OrdersNearerOverFarther()
    {
        // Greater x+y is nearer the camera, so it must sort above a smaller x+y.
        if (IsoProjection.DepthOf(new NVector2(100f, 50f)) <= IsoProjection.DepthOf(new NVector2(10f, 10f)))
        {
            throw new System.Exception("A nearer (greater x+y) world point must carry a higher depth.");
        }
    }

    [Test]
    public void ScreenTransform_ShearsALocalPointToItsProjection()
    {
        // Applying the affine to a flat local position must equal WorldToScreen of that position.
        var point = new Vector2(130f, 64f);
        var sheared = IsoProjection.ScreenTransform * point;
        var expected = IsoProjection.WorldToScreen(new NVector2(point.X, point.Y));
        if (Mathf.Abs(sheared.X - expected.X) > 0.001f || Mathf.Abs(sheared.Y - expected.Y) > 0.001f)
        {
            throw new System.Exception($"ScreenTransform must match WorldToScreen; got {sheared} vs {expected}.");
        }
    }
}
