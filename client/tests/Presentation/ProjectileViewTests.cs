using Godot;
using Chickensoft.GoDotTest;
using TankGame.GameLogic;
using TankGame.Presentation;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Tests.Presentation;

public class ProjectileViewTests : TestClass
{
    private ProjectileView _view = default!;

    public ProjectileViewTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _view = GD.Load<PackedScene>("res://src/Presentation/Projectile/ProjectileView.tscn")
            .Instantiate<ProjectileView>();
        TestScene.AddChild(_view);
    }

    [Cleanup]
    public void Cleanup()
    {
        if (GodotObject.IsInstanceValid(_view))
        {
            _view.QueueFree();
        }
    }

    [Test]
    public void UpdateFromModel_MirrorsTheProjectilePosition()
    {
        var arena = new RectArena(new NVector2(0f, 0f), new NVector2(100f, 100f));
        var projectile = new Projectile(arena, new NVector2(50f, 50f), new NVector2(1f, 0f), speed: 200f);
        _view.Bind(projectile);

        projectile.Step(0.1f); // the model advances 20 units; the view only mirrors
        _view.UpdateFromModel();

        if (Mathf.Abs(_view.Position.X - 70f) > 0.01f)
        {
            throw new System.Exception($"View should mirror the projectile to x=70; was {_view.Position.X}.");
        }
    }

    [Test]
    public void UpdateFromModel_MirrorsTheSnappedHitPosition()
    {
        var arena = new RectArena(new NVector2(0f, 0f), new NVector2(100f, 100f));
        var projectile = new Projectile(arena, new NVector2(50f, 50f), new NVector2(1f, 0f), speed: 2000f);
        _view.Bind(projectile);

        projectile.Step(0.1f); // would travel 200 units, but the right wall snaps it to x=100
        _view.UpdateFromModel();

        if (Mathf.Abs(_view.Position.X - 100f) > 0.01f)
        {
            throw new System.Exception($"View should mirror the snapped hit at x=100; was {_view.Position.X}.");
        }
    }
}
