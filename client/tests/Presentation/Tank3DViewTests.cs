using System;
using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain;
using TankGame.Presentation;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Tests.Presentation;

// Drop-off-ledge rendering (ADR-0020 Wave B step 4): the tank view must seat at the tank's
// CONTINUOUS Altitude (layer units × LayerHeight), not snap to its discrete Layer — a falling
// tank sweeps down the cliff face instead of teleporting to the floor.
public class Tank3DViewTests : TestClass
{
    private sealed class FakeTank : ITank
    {
        public Guid Id { get; } = Guid.NewGuid();
        public NVector2 Position { get; set; }
        public float Rotation => 0f;
        public float TurretRotation => 0f;
        public int Hp => 1;
        public int MaxHp => 1;
        public int Team => 0;
        public int Shield => 0;
        public int Layer { get; set; }
        public float Altitude { get; set; }
        public bool IsAirborne { get; set; }
        public bool IsAlive => true;
        public void TakeDamage(int amount) { }
        public void Step(float deltaSeconds) { }
    }

    private Tank3DView _view = default!;

    public Tank3DViewTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _view = new Tank3DView();
        TestScene.AddChild(_view); // runs _Ready: loads the GLB, builds bars/smoke/dust
    }

    // Free immediately (not deferred) and force a GC so no managed wrapper to a freed Godot
    // resource lingers to engine shutdown — the teardown crash pattern from Arena3DSceneTests.
    [Cleanup]
    public void Cleanup()
    {
        if (GodotObject.IsInstanceValid(_view))
        {
            _view.Free();
        }

        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    [Test]
    public void AFallingTank_RendersAtItsAltitude_NotItsLayer()
    {
        var tank = new FakeTank
        {
            Position = new NVector2(100f, 200f),
            Layer = 1,              // combat still treats it as up top...
            Altitude = 0.5f,        // ...but it is halfway down the cliff
            IsAirborne = true,
        };
        _view.Bind(tank);

        _view.UpdateFromModel();

        var expectedY = 0.5f * GroundProjection.LayerHeight;
        if (Mathf.Abs(_view.Position.Y - expectedY) > 0.001f)
        {
            throw new Exception($"a falling tank must render at Altitude×LayerHeight ({expectedY}); was {_view.Position.Y}.");
        }

        if (Mathf.Abs(_view.Position.X - 100f) > 0.001f || Mathf.Abs(_view.Position.Z - 200f) > 0.001f)
        {
            throw new Exception("the ground-plane mapping (x, y) → (x, ·, y) must be unchanged.");
        }
    }

    [Test]
    public void AGroundedTank_SitsExactlyOnItsLayer()
    {
        var tank = new FakeTank { Position = NVector2.Zero, Layer = 2, Altitude = 2f, IsAirborne = false };
        _view.Bind(tank);

        _view.UpdateFromModel();

        var expectedY = 2f * GroundProjection.LayerHeight;
        if (Mathf.Abs(_view.Position.Y - expectedY) > 0.001f)
        {
            throw new Exception($"a grounded tank must sit on its layer ({expectedY}); was {_view.Position.Y}.");
        }
    }
}
