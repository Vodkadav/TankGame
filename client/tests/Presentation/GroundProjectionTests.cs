using Godot;
using Chickensoft.GoDotTest;
using NVector2 = System.Numerics.Vector2;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// The flat-plane -> 3D ground mapping the 3D presentation is built on (ADR-0017).
public class GroundProjectionTests : TestClass
{
    public GroundProjectionTests(Node testScene) : base(testScene) { }

    [Test]
    public void ToWorld_LaysTheGamePlaneOnTheGround()
    {
        var w = GroundProjection.ToWorld(new NVector2(10f, 7f));
        if (w.X != 10f || w.Y != 0f || w.Z != 7f)
        {
            throw new System.Exception($"Game (x,y) should map to world (x,0,y); was {w}.");
        }
    }

    [Test]
    public void ToWorld_LiftsToTheGivenHeight()
    {
        var w = GroundProjection.ToWorld(new NVector2(3f, 4f), height: 5f);
        if (w.Y != 5f)
        {
            throw new System.Exception($"Height should lift world Y; was {w.Y}.");
        }
    }
}
