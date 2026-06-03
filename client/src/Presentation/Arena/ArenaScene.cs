using System;
using System.Collections.Generic;
using Godot;
using TankGame.Domain;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>M1 play scene root and gameplay composition root: builds the input source, a
/// <see cref="World"/>, a <see cref="Tank"/>, and a bounding <see cref="RectArena"/>;
/// renders the tank with a following camera; draws the arena walls. The tank owns the
/// fire/cooldown rule and spawns <see cref="Projectile"/>s into the world; the scene
/// subscribes to the world's spawn/despawn events and maps each spawned projectile to a
/// <see cref="ProjectileView"/>, freeing it when the world reaps the projectile. (Tick
/// ownership for the tank still lives in <see cref="TankView"/> until S1-T5.)</summary>
public partial class ArenaScene : Node2D
{
    private const float TankSpeed = 200f;
    private const float ProjectileSpeed = 600f;
    private const float FireInterval = 0.3f;

    private static readonly NVector2 ArenaMin = new(-400f, -300f);
    private static readonly NVector2 ArenaMax = new(400f, 300f);

    private readonly Dictionary<Guid, Node2D> _views = new();
    private KeyboardMouseInputSource _input = null!;
    private World _world = null!;
    private Tank _tank = null!;
    private RectArena _arena = null!;

    public override void _Ready()
    {
        _input = new KeyboardMouseInputSource(GetViewport());
        _arena = new RectArena(ArenaMin, ArenaMax);
        _world = new World();
        _world.EntitySpawned += OnEntitySpawned;
        _world.EntityDespawned += OnEntityDespawned;

        _tank = new Tank(_input, _world, _arena, NVector2.Zero, TankSpeed, FireInterval, ProjectileSpeed);

        AddChild(BuildWalls());
        AddChild(BuildInstructionsOverlay());

        var view = GD.Load<PackedScene>("res://src/Presentation/Tank/TankView.tscn")
            .Instantiate<TankView>();
        AddChild(view);
        view.Bind(_tank);
        view.AddChild(new Camera2D { ProcessCallback = Camera2D.Camera2DProcessCallback.Physics });
    }

    // The world advances and reaps the entities it owns (the spawned projectiles). The tank
    // is still hand-wired and stepped by its view; it is not in the world yet, so this does
    // not double-step it. S1-T5 moves the tank into the world and inverts the view ticks.
    public override void _Process(double delta) => _world.Step((float)delta);

    private void OnEntitySpawned(IEntity entity)
    {
        if (entity is not IProjectile projectile)
        {
            return;
        }

        var view = GD.Load<PackedScene>("res://src/Presentation/Projectile/ProjectileView.tscn")
            .Instantiate<ProjectileView>();
        AddChild(view);
        view.Bind(projectile);
        _views[entity.Id] = view;
    }

    private void OnEntityDespawned(IEntity entity)
    {
        if (_views.Remove(entity.Id, out var view))
        {
            view.QueueFree();
        }
    }

    // Screen-space overlay so the "how to play" line stays put while the camera tracks
    // the tank. The Label's text is the translation key; Godot auto-translates it via tr().
    private static CanvasLayer BuildInstructionsOverlay()
    {
        var layer = new CanvasLayer { Name = "InstructionsLayer" };
        var label = new Label { Name = "Instructions", Text = "m1.instructions" };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.GrowHorizontal = Control.GrowDirection.Both;
        layer.AddChild(label);
        return layer;
    }

    private static Line2D BuildWalls()
    {
        var line = new Line2D { Width = 4f, DefaultColor = new Color(0.5f, 0.5f, 0.55f) };
        line.AddPoint(new Vector2(ArenaMin.X, ArenaMin.Y));
        line.AddPoint(new Vector2(ArenaMax.X, ArenaMin.Y));
        line.AddPoint(new Vector2(ArenaMax.X, ArenaMax.Y));
        line.AddPoint(new Vector2(ArenaMin.X, ArenaMax.Y));
        line.AddPoint(new Vector2(ArenaMin.X, ArenaMin.Y));
        return line;
    }
}
