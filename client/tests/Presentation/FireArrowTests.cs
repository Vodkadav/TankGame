using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class FireArrowTests : TestClass
{
    public FireArrowTests(Node testScene) : base(testScene) { }

    [Test]
    public void Show_PlacesAndAimsTheArrow()
    {
        var arrow = new FireArrow();
        TestScene.AddChild(arrow);
        try
        {
            arrow.Show(new Vector2(200f, 120f), angle: Mathf.Pi / 2f);

            if (Mathf.Abs(arrow.Position.X - 200f) > 0.01f || Mathf.Abs(arrow.Rotation - (Mathf.Pi / 2f)) > 0.01f)
            {
                throw new System.Exception($"Arrow should sit at and aim where told; pos {arrow.Position}, rot {arrow.Rotation}.");
            }

            if (arrow.GetNodeOrNull<Polygon2D>("Arrow") is null)
            {
                throw new System.Exception("FireArrow must build an 'Arrow' Polygon2D.");
            }
        }
        finally
        {
            arrow.QueueFree();
        }
    }

    [Test]
    public void Advance_Blinks_ThenFreesItselfAfterItsLifetime()
    {
        var arrow = new FireArrow();
        TestScene.AddChild(arrow);
        arrow.Show(Vector2.Zero, angle: 0f);

        arrow.Advance(0.1f);
        if (!arrow.Visible)
        {
            throw new System.Exception("The arrow should be visible at the start of a blink.");
        }

        arrow.Advance(0.3f); // crosses into the 'off' half of the blink
        if (arrow.Visible)
        {
            throw new System.Exception("The arrow should blink off partway through.");
        }

        arrow.Advance(2f); // past its 1.5 s life
        if (GodotObject.IsInstanceValid(arrow) && !arrow.IsQueuedForDeletion())
        {
            throw new System.Exception("The arrow should free itself once its lifetime elapses.");
        }
    }
}
