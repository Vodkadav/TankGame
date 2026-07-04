using System;
using Godot;
using Chickensoft.GoDotTest;
using NVector2 = System.Numerics.Vector2;
using TankGame.Domain;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class Powerup3DViewTests : TestClass
{
    private sealed class StubPowerup : IPowerup
    {
        public StubPowerup(NVector2 position, PowerupKind kind) { Position = position; Kind = kind; Id = Guid.NewGuid(); }
        public Guid Id { get; }
        public NVector2 Position { get; }
        public PowerupKind Kind { get; }
        public bool IsAlive => true;
        public bool IsAvailable { get; set; } = true;
        public event System.Action<PowerupKind>? Collected { add { } remove { } }
        public void Step(float deltaSeconds) { }
    }

    private Powerup3DView _view = default!;

    public Powerup3DViewTests(Node testScene) : base(testScene) { }

    [Cleanup]
    public void Cleanup() => _view?.QueueFree();

    [Test]
    public void View_LabelsThePowerupWithItsLocalizedName_SoItReadsAtAGlance()
    {
        var powerup = new StubPowerup(new NVector2(120f, 64f), PowerupKind.Shield);
        _view = new Powerup3DView();
        _view.Bind(powerup);
        TestScene.AddChild(_view); // adding to the tree runs _Ready, which builds the label

        var label = _view.GetNodeOrNull<Label3D>("NameLabel")
            ?? throw new Exception("Powerup3DView must float a 'NameLabel' so the powerup is identifiable.");

        var expected = TranslationServer.Translate(PickupFloater.LabelKeyFor(PowerupKind.Shield));
        if (label.Text != expected)
        {
            throw new Exception($"The label should show the powerup's localized name '{expected}'; was '{label.Text}'.");
        }
        if (label.Text.Length == 0)
        {
            throw new Exception("The powerup name label must not be empty.");
        }
        if (label.Position.Y <= 0f)
        {
            throw new Exception($"The name label should float above the pickup; Y was {label.Position.Y}.");
        }
    }

    [Test]
    public void View_UsesTheKindsOwnName_NotAFixedOne()
    {
        var powerup = new StubPowerup(new NVector2(0f, 0f), PowerupKind.Missile);
        _view = new Powerup3DView();
        _view.Bind(powerup);
        TestScene.AddChild(_view);

        var label = _view.GetNodeOrNull<Label3D>("NameLabel")
            ?? throw new Exception("Powerup3DView must float a 'NameLabel'.");
        var expected = TranslationServer.Translate(PickupFloater.LabelKeyFor(PowerupKind.Missile));
        if (label.Text != expected)
        {
            throw new Exception($"A Missile pickup should be labelled '{expected}'; was '{label.Text}'.");
        }
    }
}
