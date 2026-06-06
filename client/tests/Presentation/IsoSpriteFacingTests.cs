using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// The pure quantiser that picks one of N directional sprite frames from a facing angle — the basis
// for the iso tank's directional hull and independently-aiming turret (Phase 3 of the iso pivot).
public class IsoSpriteFacingTests : TestClass
{
    public IsoSpriteFacingTests(Node testScene) : base(testScene) { }

    [Test]
    public void ZeroAngle_SelectsFrameZero()
    {
        if (IsoSpriteFacing.FrameIndex(0f, 16) != 0)
        {
            throw new System.Exception("Angle 0 must select frame 0.");
        }
    }

    [Test]
    public void OneStep_SelectsTheNextFrame()
    {
        if (IsoSpriteFacing.FrameIndex(Mathf.Tau / 16f, 16) != 1)
        {
            throw new System.Exception("One angular step must advance one frame.");
        }
    }

    [Test]
    public void NegativeAngle_WrapsToTheLastFrame()
    {
        if (IsoSpriteFacing.FrameIndex(-Mathf.Tau / 16f, 16) != 15)
        {
            throw new System.Exception("A step below zero must wrap to the last frame.");
        }
    }

    [Test]
    public void FullTurn_WrapsBackToZero()
    {
        if (IsoSpriteFacing.FrameIndex(Mathf.Tau, 16) != 0)
        {
            throw new System.Exception("A full turn must wrap back to frame 0.");
        }
    }

    [Test]
    public void RoundsToTheNearestDirection()
    {
        var step = Mathf.Tau / 16f;
        if (IsoSpriteFacing.FrameIndex(step * 0.4f, 16) != 0 || IsoSpriteFacing.FrameIndex(step * 0.6f, 16) != 1)
        {
            throw new System.Exception("The angle must snap to the nearest directional frame.");
        }
    }

    [Test]
    public void HalfTurn_SelectsTheOppositeFrame()
    {
        if (IsoSpriteFacing.FrameIndex(Mathf.Pi, 8) != 4)
        {
            throw new System.Exception("Half a turn must select the opposite frame.");
        }
    }
}
