using Godot;
using Chickensoft.GoDotTest;
using NVector2 = System.Numerics.Vector2;
using TankGame.Domain;
using TankGame.GameLogic;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class TankViewTests : TestClass
{
    private sealed class FixedInput(TankInput value) : IInputSource
    {
        public TankInput Read() => value;
    }

    private TankView _view = default!;

    public TankViewTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        // Loading the scene also imports/loads the placeholder textures (M1-T2).
        _view = GD.Load<PackedScene>("res://src/Presentation/Tank/TankView.tscn")
            .Instantiate<TankView>();
        TestScene.AddChild(_view);
    }

    [Cleanup]
    public void Cleanup() => _view.QueueFree();

    [Test]
    public void Scene_HasBodyAndTurretSprites()
    {
        var body = _view.GetNode<Sprite2D>("Body");
        var turret = _view.GetNode<Sprite2D>("Turret");

        if (body.Texture is null || turret.Texture is null)
        {
            throw new System.Exception("Body and Turret sprites must have textures loaded.");
        }
    }

    [Test]
    public void UpdateFromModel_MovesNodeAndRotatesSpritesToMatchTank()
    {
        var input = new FixedInput(new TankInput(new NVector2(1f, 0f), Aim: 0.75f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f, projectileSpeed: 600f);
        _view.Bind(tank);

        _view.UpdateFromModel(0.1f); // 100 u/s * 0.1s = 10 units along +X

        if (Mathf.Abs(_view.Position.X - 10f) > 0.01f)
        {
            throw new System.Exception($"View should follow the tank to x=10; was {_view.Position.X}.");
        }

        var turret = _view.GetNode<Sprite2D>("Turret");
        if (Mathf.Abs(turret.Rotation - 0.75f) > 0.001f)
        {
            throw new System.Exception($"Turret should aim at 0.75 rad; was {turret.Rotation}.");
        }
    }
}
