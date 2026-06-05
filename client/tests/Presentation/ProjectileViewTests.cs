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
    public void Scene_LoadsTheBulletTexture_FromTheCatalogue()
    {
        var bullet = _view.GetNode<Sprite2D>("Bullet");
        if (bullet.Texture != GD.Load<Texture2D>(AssetCatalogue.Active.Bullet))
        {
            throw new System.Exception("The Bullet sprite must load its texture from the asset catalogue.");
        }
    }

    [Test]
    public void UpdateFromModel_RotatesTheBullet_ToFaceTravelDirection()
    {
        var arena = new RectArena(new NVector2(0f, 0f), new NVector2(1000f, 1000f));
        var projectile = new Projectile(arena, new NVector2(50f, 50f), new NVector2(0f, 1f), speed: 200f);
        _view.Bind(projectile);

        _view.UpdateFromModel();

        // The bullet art faces east at rotation 0; a shot heading +Y must point the sprite down.
        if (Mathf.Abs(_view.Rotation - (Mathf.Pi / 2f)) > 0.01f)
        {
            throw new System.Exception($"View should face the travel direction (π/2); was {_view.Rotation}.");
        }
    }

    [Test]
    public void UpdateFromModel_MirrorsTheProjectilePosition()
    {
        var arena = new RectArena(new NVector2(0f, 0f), new NVector2(100f, 100f));
        var projectile = new Projectile(arena, new NVector2(50f, 50f), new NVector2(1f, 0f), speed: 200f);
        _view.Bind(projectile);

        projectile.Step(0.1f); // the model advances 20 units; the view mirrors it, projected to iso
        _view.UpdateFromModel();

        // World (70,50) projects to iso ((70-50)*0.5, (70+50)*0.25) = (10, 30).
        if (Mathf.Abs(_view.Position.X - 10f) > 0.01f || Mathf.Abs(_view.Position.Y - 30f) > 0.01f)
        {
            throw new System.Exception($"View should mirror the projectile to iso (10,30); was {_view.Position}.");
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

        // Snapped world (100,50) projects to iso ((100-50)*0.5, (100+50)*0.25) = (25, 37.5).
        if (Mathf.Abs(_view.Position.X - 25f) > 0.01f || Mathf.Abs(_view.Position.Y - 37.5f) > 0.01f)
        {
            throw new System.Exception($"View should mirror the snapped hit at iso (25,37.5); was {_view.Position}.");
        }
    }
}
