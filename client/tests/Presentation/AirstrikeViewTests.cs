using System;
using Godot;
using Chickensoft.GoDotTest;
using NVector2 = System.Numerics.Vector2;
using TankGame.Domain;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class AirstrikeViewTests : TestClass
{
    private sealed class StubStrike : IAirstrike
    {
        public StubStrike(NVector2 position, float radius) { Position = position; Radius = radius; Id = Guid.NewGuid(); }
        public Guid Id { get; }
        public NVector2 Position { get; }
        public float Radius { get; }
        public bool IsAlive => true;
        public void Step(float deltaSeconds) { }
    }

    private AirstrikeView _view = default!;

    public AirstrikeViewTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _view = new AirstrikeView();
        TestScene.AddChild(_view);
    }

    [Cleanup]
    public void Cleanup() => _view.QueueFree();

    [Test]
    public void Bind_PlacesTheMarker_AtTheStrike()
    {
        _view.Bind(new StubStrike(new NVector2(96f, 48f), radius: 100f));

        // World (96,48) projects to iso ((96-48)*1, (96+48)*0.5) = (48, 72).
        if (Mathf.Abs(_view.Position.X - 48f) > 0.01f || Mathf.Abs(_view.Position.Y - 72f) > 0.01f)
        {
            throw new Exception($"Marker should sit at the strike's projected position; was {_view.Position}.");
        }

        if (_view.GetNodeOrNull<Polygon2D>("BlastMarker") is null)
        {
            throw new Exception("AirstrikeView must draw a 'BlastMarker' Polygon2D.");
        }
    }
}
