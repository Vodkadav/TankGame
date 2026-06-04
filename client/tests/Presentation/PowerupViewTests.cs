using System;
using Godot;
using Chickensoft.GoDotTest;
using NVector2 = System.Numerics.Vector2;
using TankGame.Domain;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class PowerupViewTests : TestClass
{
    private sealed class StubPowerup : IPowerup
    {
        public StubPowerup(NVector2 position, PowerupKind kind) { Position = position; Kind = kind; Id = Guid.NewGuid(); }
        public Guid Id { get; }
        public NVector2 Position { get; }
        public PowerupKind Kind { get; }
        public bool IsAlive => true;
        public bool IsAvailable { get; set; } = true;
        public void Step(float deltaSeconds) { }
    }

    private PowerupView _view = default!;

    public PowerupViewTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _view = new PowerupView();
        TestScene.AddChild(_view);
    }

    [Cleanup]
    public void Cleanup() => _view.QueueFree();

    [Test]
    public void Bind_PlacesTheNode_AndDrawsTheKindsColouredShape()
    {
        var powerup = new StubPowerup(new NVector2(120f, 64f), PowerupKind.RapidFire);

        _view.Bind(powerup);

        if (Mathf.Abs(_view.Position.X - 120f) > 0.01f || Mathf.Abs(_view.Position.Y - 64f) > 0.01f)
        {
            throw new Exception($"View should sit at the powerup's position; was {_view.Position}.");
        }

        var shape = _view.GetNodeOrNull<Polygon2D>("Shape")
            ?? throw new Exception("PowerupView must draw a 'Shape' Polygon2D.");
        if (shape.Color != PowerupView.ColourFor(PowerupKind.RapidFire))
        {
            throw new Exception($"Shape colour should match the kind; was {shape.Color}.");
        }
    }

    [Test]
    public void View_HidesTheShape_WhileThePowerupIsUnavailable()
    {
        var powerup = new StubPowerup(new NVector2(0f, 0f), PowerupKind.SpeedBoost);
        _view.Bind(powerup);

        if (!_view.Visible)
        {
            throw new Exception("An available powerup's view should be visible.");
        }

        powerup.IsAvailable = false; // collected → dormant on its respawn cooldown
        _view.UpdateFromModel();
        if (_view.Visible)
        {
            throw new Exception("A dormant (respawning) powerup's view should be hidden.");
        }

        powerup.IsAvailable = true; // respawned
        _view.UpdateFromModel();
        if (!_view.Visible)
        {
            throw new Exception("A respawned powerup's view should be visible again.");
        }
    }

    [Test]
    public void HealthPickups_HaveDistinctColours()
    {
        var repair = PowerupView.ColourFor(PowerupKind.Repair);
        var shield = PowerupView.ColourFor(PowerupKind.Shield);

        if (repair == Colors.White || shield == Colors.White || repair == shield)
        {
            throw new Exception($"Repair ({repair}) and Shield ({shield}) need distinct, mapped colours.");
        }
    }
}
