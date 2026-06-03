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
    private const float EnemySpeed = 140f;
    private const float ProjectileSpeed = 600f;
    private const float FireInterval = 0.3f;
    private const float TileSize = 64f;
    private const float CombatHitRadius = 28f;
    private const int PlayerTeam = 0;
    private const int EnemyTeam = 1;

    private static readonly NVector2 GridOrigin = NVector2.Zero;

    // Floor cells in Maze01, spread away from the player spawn at (2,1).
    private static readonly (int X, int Y)[] EnemySpawns = { (25, 2), (25, 14), (13, 13) };

    private readonly Dictionary<Guid, Node2D> _views = new();
    private readonly MatchTracker _matchTracker = new();
    private KeyboardMouseInputSource _input = null!;
    private World _world = null!;
    private GridArena _arena = null!;
    private Camera2D _camera = null!;
    private ITank _player = null!;
    private bool _matchOver;

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

        // The camera lives on the scene (not the player's view) and follows the player each
        // frame, so it survives the player's death and the game-over screen stays put.
        _camera = new Camera2D { Name = "GameCamera", ProcessCallback = Camera2D.Camera2DProcessCallback.Physics };
        AddChild(_camera);

        // Spawn through the world so each tank reaches the screen by the same event path as
        // every other entity — no hand-wiring. EntitySpawned fires synchronously here.
        _player = new Tank(_input, _world, _arena, CellCentre(maze.SpawnX, maze.SpawnY),
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: PlayerTeam);
        _world.Spawn(_player);
        _camera.Position = new Vector2(_player.Position.X, _player.Position.Y);

        foreach (var (ex, ey) in EnemySpawns)
        {
            var ai = new AiInputSource(_world, _arena);
            var enemy = new Tank(ai, _world, _arena, CellCentre(ex, ey),
                EnemySpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: EnemyTeam);
            ai.Bind(enemy); // resolve the input-source ↔ tank construction cycle
            _world.Spawn(enemy);
        }
    }

    // The world is the single tick owner: it advances every entity (tank and projectiles) and
    // reaps the dead. The views hold no tick — they mirror their model each frame. Once the
    // match is decided the world freezes and the game-over screen shows.
    public override void _Process(double delta)
    {
        if (_matchOver)
        {
            return;
        }

        _world.Step((float)delta);

        if (_player.IsAlive)
        {
            _camera.Position = new Vector2(_player.Position.X, _player.Position.Y);
        }

        var result = _matchTracker.Evaluate(_world.Entities);
        if (result.Decided)
        {
            _matchOver = true;
            ShowGameOver(result);
        }
    }

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
        if (tank.Team != PlayerTeam)
        {
            view.Modulate = new Color(1f, 0.5f, 0.5f); // tint adversaries red
        }

        return view;
    }

    // Resolve the decided match into the player's perspective: their team winning is a win,
    // a draw or any other team surviving is a loss. Shows a screen-space overlay with a
    // restart button that reloads the scene for a fresh round.
    private void ShowGameOver(MatchResult result)
    {
        var key = result.WinningTeam == PlayerTeam ? "hud.you_win"
            : result.WinningTeam is null ? "hud.draw"
            : "hud.you_lose";

        var layer = new CanvasLayer { Name = "GameOverLayer" };
        var box = new VBoxContainer { Name = "GameOver" };
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        box.GrowHorizontal = Control.GrowDirection.Both;
        box.GrowVertical = Control.GrowDirection.Both;

        box.AddChild(new Label
        {
            Name = "Outcome",
            Text = key,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var restart = new Button { Name = "Restart", Text = "hud.restart" };
        restart.Pressed += () => GetTree().ReloadCurrentScene();
        box.AddChild(restart);

        layer.AddChild(box);
        AddChild(layer);
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
