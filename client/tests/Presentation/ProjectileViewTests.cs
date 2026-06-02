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
    public void Advance_MovesTheNodeWhileTheProjectileIsAlive()
    {
        var arena = new RectArena(new NVector2(0f, 0f), new NVector2(100f, 100f));
        _view.Bind(new Projectile(arena, new NVector2(50f, 50f), new NVector2(1f, 0f), speed: 200f));

        _view.Advance(0.1f); // 20 units along +X

        if (Mathf.Abs(_view.Position.X - 70f) > 0.01f)
        {
            throw new System.Exception($"View should follow the projectile to x=70; was {_view.Position.X}.");
        }
    }

    [Test]
    public void Advance_SnapsToTheWall_WhenTheProjectileHits()
    {
        var arena = new RectArena(new NVector2(0f, 0f), new NVector2(100f, 100f));
        _view.Bind(new Projectile(arena, new NVector2(50f, 50f), new NVector2(1f, 0f), speed: 2000f));

        _view.Advance(0.1f); // would travel 200 units, but the right wall is 50 away

        if (Mathf.Abs(_view.Position.X - 100f) > 0.01f)
        {
            throw new System.Exception($"View should snap to the wall at x=100; was {_view.Position.X}.");
        }
    }
}
