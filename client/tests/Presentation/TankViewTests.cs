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
    public void UpdateFromModel_MirrorsTheTankPositionAndRotations()
    {
        var input = new FixedInput(new TankInput(new NVector2(1f, 0f), Aim: 0.75f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f, projectileSpeed: 600f);
        _view.Bind(tank);

        tank.Step(0.1f); // the model advances 10 units along +X; the view only mirrors
        _view.UpdateFromModel();

        if (Mathf.Abs(_view.Position.X - 10f) > 0.01f)
        {
            throw new System.Exception($"View should mirror the tank to x=10; was {_view.Position.X}.");
        }

        var turret = _view.GetNode<Sprite2D>("Turret");
        if (Mathf.Abs(turret.Rotation - 0.75f) > 0.001f)
        {
            throw new System.Exception($"Turret should aim at 0.75 rad; was {turret.Rotation}.");
        }
    }

    [Test]
    public void UpdateFromModel_ShrinksTheHealthBar_AsTheTankTakesDamage()
    {
        var input = new FixedInput(new TankInput(NVector2.Zero, Aim: 0f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 4);
        _view.Bind(tank);

        _view.UpdateFromModel();
        var bar = _view.GetNode<ColorRect>("HealthBar");
        var fullWidth = bar.Size.X;

        tank.TakeDamage(2); // half health
        _view.UpdateFromModel();

        if (Mathf.Abs(bar.Size.X - (fullWidth / 2f)) > 0.5f)
        {
            throw new System.Exception($"Health bar should halve to {fullWidth / 2f}; was {bar.Size.X}.");
        }
    }

    [Test]
    public void ShieldBar_IsHiddenWhenUnshielded_AndShowsWhenShielded()
    {
        var input = new FixedInput(new TankInput(NVector2.Zero, Aim: 0f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 3);
        _view.Bind(tank);

        _view.UpdateFromModel();
        var shieldBar = _view.GetNode<ColorRect>("ShieldBar");
        if (shieldBar.Visible)
        {
            throw new System.Exception("Shield bar should be hidden when the tank has no shield.");
        }

        tank.AddShield(3);
        _view.UpdateFromModel();
        if (!shieldBar.Visible || shieldBar.Size.X <= 0f)
        {
            throw new System.Exception($"Shield bar should show with a positive width; visible={shieldBar.Visible}, w={shieldBar.Size.X}.");
        }
    }

    [Test]
    public void ApplyTeamTint_TintsEnemies_AndLeavesAlliesUntinted()
    {
        _view.ApplyTeamTint(isEnemy: true);
        if (_view.Modulate != TeamPalette.TintFor(isEnemy: true))
        {
            throw new System.Exception($"An enemy view should carry the enemy tint; was {_view.Modulate}.");
        }

        _view.ApplyTeamTint(isEnemy: false);
        if (_view.Modulate != Colors.White)
        {
            throw new System.Exception($"A friendly view should be untinted; was {_view.Modulate}.");
        }
    }

    [Test]
    public void DownedTank_IsHidden_AndReappearsOnRespawn()
    {
        var input = new FixedInput(new TankInput(NVector2.Zero, Aim: 0f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 1, team: 0, lives: 2);
        _view.Bind(tank);

        tank.TakeDamage(1); // downed, awaiting respawn
        _view.UpdateFromModel();
        if (_view.Visible)
        {
            throw new System.Exception("A downed tank's view should be hidden while it awaits respawn.");
        }

        tank.Step(Tank.RespawnDelay + 0.1f); // revive
        _view.UpdateFromModel();
        if (!_view.Visible)
        {
            throw new System.Exception("A respawned tank's view should be visible again.");
        }
    }
}
