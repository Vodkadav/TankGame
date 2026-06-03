using System;
using System.Collections.Generic;
using Godot;
using TankGame.Domain;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>M2 play scene root and gameplay composition root: loads the hand-authored
/// <see cref="Maze01"/> into a <see cref="WallGrid"/>, builds a <see cref="GridArena"/> over
/// it, renders it with a <see cref="WallGridView"/>, then spawns the player
/// <see cref="Tank"/> into the <see cref="World"/> at the maze's spawn cell. It subscribes
/// once to the world's spawn/despawn events and maps each entity to its Godot view via a
/// type-switch (the tank to a <see cref="TankView"/> with a following camera, a projectile
/// to a <see cref="ProjectileView"/>), freeing the view when the world reaps the entity. The
/// world is the single tick owner — <c>_Process</c> calls <see cref="World.Step"/> once and
/// the views are pure mirrors. Drawing a new kind of entity needs only a new switch arm.</summary>
public partial class ArenaScene : Node2D
{
    private const float TankSpeed = 200f;
    private const float ProjectileSpeed = 600f;
    private const float FireInterval = 0.3f;
    private const float TileSize = 64f;
    private const float CombatHitRadius = 28f;

    private static readonly NVector2 GridOrigin = NVector2.Zero;

    private readonly Dictionary<Guid, Node2D> _views = new();
    private KeyboardMouseInputSource _input = null!;
    private World _world = null!;
    private GridArena _arena = null!;

    public override void _Ready()
    {
        _input = new KeyboardMouseInputSource(GetViewport());

        var maze = MazeDefinition.Parse(Maze01.Text);
        var grid = maze.BuildGrid();
        _arena = new GridArena(grid, TileSize, GridOrigin);

        _world = new World(new CombatResolver(CombatHitRadius));
        _world.EntitySpawned += OnEntitySpawned;
        _world.EntityDespawned += OnEntityDespawned;

        var wallView = new WallGridView { Name = "WallGridView", RenderTileSize = (int)TileSize };
        AddChild(wallView);     // runs _Ready (builds the atlas TileSet)
        wallView.Bind(grid);    // then draw the maze and track damage
        AddChild(BuildInstructionsOverlay());

        var brickCounter = new BrickCounterOverlay { Name = "BrickCounterOverlay" };
        AddChild(brickCounter); // runs _Ready (builds the label)
        brickCounter.Bind(grid);

        // Spawn through the world so the tank reaches the screen by the same event path as
        // every other entity — no hand-wiring. EntitySpawned fires synchronously here.
        var spawn = CellCentre(maze.SpawnX, maze.SpawnY);
        _world.Spawn(new Tank(_input, _world, _arena, spawn, TankSpeed, FireInterval, ProjectileSpeed));
    }

    // The world is the single tick owner: it advances every entity (tank and projectiles)
    // and reaps the dead. The views hold no tick — they mirror their model each frame.
    public override void _Process(double delta) => _world.Step((float)delta);

    private void OnEntitySpawned(IEntity entity)
    {
        Node2D view = entity switch
        {
            ITank tank => BuildTankView(tank),
            IProjectile projectile => BuildProjectileView(projectile),
            _ => throw new NotSupportedException($"No view registered for {entity.GetType().Name}.")
        };

        AddChild(view);
        _views[entity.Id] = view;
    }

    private void OnEntityDespawned(IEntity entity)
    {
        if (_views.Remove(entity.Id, out var view))
        {
            view.QueueFree();
        }
    }

    private static TankView BuildTankView(ITank tank)
    {
        var view = GD.Load<PackedScene>("res://src/Presentation/Tank/TankView.tscn")
            .Instantiate<TankView>();
        view.Bind(tank);
        view.AddChild(new Camera2D { ProcessCallback = Camera2D.Camera2DProcessCallback.Physics });
        return view;
    }

    private static ProjectileView BuildProjectileView(IProjectile projectile)
    {
        var view = GD.Load<PackedScene>("res://src/Presentation/Projectile/ProjectileView.tscn")
            .Instantiate<ProjectileView>();
        view.Bind(projectile);
        return view;
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

    // World-space centre of cell (x, y) — where the tank starts and where shots land.
    private static NVector2 CellCentre(int x, int y) =>
        new(GridOrigin.X + ((x + 0.5f) * TileSize), GridOrigin.Y + ((y + 0.5f) * TileSize));
}
