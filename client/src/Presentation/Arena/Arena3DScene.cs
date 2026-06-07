using System;
using System.Collections.Generic;
using Godot;
using TankGame.Domain;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>The 3D play scene (ADR-0017), a parallel of <see cref="ArenaScene"/> while the port is built
/// up. It composes the same pure gameplay — a procedurally generated <see cref="GeneratedArena"/>, a
/// <see cref="World"/> with a <see cref="CombatResolver"/>, a player tank and AI adversaries — but renders
/// it in a real 3D world: a ground plane, a box per wall cell, 3D tank and projectile views, under a
/// fixed orthographic ¾ camera that follows the player. Mouse aims via a ground raycast. One-player only
/// for now; powerups/terrain detail/fog/two-player/net land in later phases. The world is the single tick
/// owner; the views are pure mirrors.</summary>
public partial class Arena3DScene : Node3D
{
    private const float TankSpeed = 200f;
    private const float EnemySpeed = 140f;
    private const float ProjectileSpeed = 600f;
    private const float FireInterval = 0.3f;
    private const float TileSize = 64f;
    private const float CombatHitRadius = 28f;
    private const int PlayerTeam = 0;
    private const int EnemyTeam = 1;
    private const int EnemyCount = 3;
    private const int StartingLives = 3;
    private const float WallHeight = 64f;

    // Orthographic ¾ camera. Eyeball-gated on playtest.
    private const float CamPitchDeg = 52f;
    private const float CamYawDeg = 45f;
    private const float CamDistance = 2500f;
    private const float CamOrthoSize = 820f; // world units shown vertically (~13 cells)

    private static readonly NVector2 GridOrigin = NVector2.Zero;

    private readonly Dictionary<Guid, Node3D> _views = new();
    private World _world = null!;
    private GridArena _arena = null!;
    private BushField _bushes = null!;
    private SandbagField _sandbags = null!;
    private GeneratedArena _layout = null!;
    private Camera3D _camera = null!;
    private ITank _player = null!;

    public override void _Ready()
    {
        _layout = new ArenaGenerator().Generate(
            new ArenaGenParams(GameSetup.ArenaWidth, GameSetup.ArenaHeight, GameSetup.ArenaSeed, EnemyCount, 0));
        var level = _layout.Map;
        var grid = level.BuildGrid();
        _arena = new GridArena(grid, TileSize, GridOrigin);
        _bushes = new BushField(level.Bushes, TileSize, GridOrigin);
        _sandbags = new SandbagField(_layout.Sandbags, TileSize, GridOrigin);

        var combat = new CombatResolver(CombatHitRadius, alliedTeam: PlayerTeam);
        _world = new World(combat);
        _world.EntitySpawned += OnEntitySpawned;
        _world.EntityDespawned += OnEntityDespawned;

        BuildEnvironment();
        BuildGround(level.Width, level.Height);
        BuildWalls(grid);
        SpawnTanks();
    }

    public override void _Process(double delta)
    {
        _world.Step((float)delta);
        if (_player.Hp > 0)
        {
            var target = GroundProjection.ToWorld(_player.Position);
            _camera.Position = target + CameraOffset();
            _camera.LookAt(target, Vector3.Up);
        }
    }

    private static Vector3 CameraOffset()
    {
        var pitch = Mathf.DegToRad(CamPitchDeg);
        var yaw = Mathf.DegToRad(CamYawDeg);
        var dir = new Vector3(Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Cos(yaw));
        return dir * CamDistance;
    }

    private void BuildEnvironment()
    {
        _camera = new Camera3D
        {
            Name = "GameCamera",
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = CamOrthoSize,
            Far = 12000f,
            Near = 1f,
        };
        AddChild(_camera);

        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-55f, -40f, 0f),
            LightEnergy = 1.4f,
            ShadowEnabled = true,
        };
        AddChild(sun);

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.45f, 0.62f, 0.78f), // sky
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.7f, 0.72f, 0.78f),
                AmbientLightEnergy = 1.1f,
            },
        });
    }

    private void BuildGround(int widthCells, int heightCells)
    {
        var w = widthCells * TileSize;
        var h = heightCells * TileSize;
        var ground = new MeshInstance3D
        {
            Name = "Ground",
            Mesh = new PlaneMesh { Size = new Vector2(w, h) },
            Position = new Vector3(w / 2f, 0f, h / 2f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.82f, 0.71f, 0.50f), Roughness = 1f },
        };
        AddChild(ground);
    }

    private void BuildWalls(IWallGrid grid)
    {
        var box = new BoxMesh { Size = new Vector3(TileSize, WallHeight, TileSize) };
        for (var x = 0; x < grid.Width; x++)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                var material = grid.GetCell(x, y).Material;
                if (material is CellMaterial.Floor or CellMaterial.Bridge or CellMaterial.Water)
                {
                    continue; // PR2 gives water/bridge their own meshes; floor is the ground plane
                }

                var centre = CellCentre(x, y);
                AddChild(new MeshInstance3D
                {
                    Mesh = box,
                    Position = new Vector3(centre.X, WallHeight / 2f, centre.Y),
                    MaterialOverride = new StandardMaterial3D { AlbedoColor = WallColour(material), Roughness = 1f },
                });
            }
        }
    }

    private static Color WallColour(CellMaterial material) => material switch
    {
        CellMaterial.Brick => new Color(0.78f, 0.36f, 0.24f),
        CellMaterial.Crate => new Color(0.60f, 0.44f, 0.24f),
        CellMaterial.Steel => new Color(0.55f, 0.57f, 0.60f),
        CellMaterial.Mountain => new Color(0.40f, 0.42f, 0.40f),
        CellMaterial.Building => new Color(0.72f, 0.72f, 0.70f),
        _ => new Color(0.5f, 0.5f, 0.5f),
    };

    private void SpawnTanks()
    {
        var p1Input = new KeyboardMouse3DInputSource(_camera, fireOnClick: true);
        var player = new Tank(p1Input, _world, _arena, CellCentre(_layout.PlayerSpawn.X, _layout.PlayerSpawn.Y),
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: PlayerTeam, lives: StartingLives, terrain: _sandbags);
        _player = player;
        SpawnTank(player);

        var enemyIndex = 0;
        foreach (var (ex, ey) in _layout.EnemySpawns)
        {
            var ambusher = enemyIndex % 2 == 1;
            var ai = new AiInputSource(_world, _arena, _bushes, ambusher);
            var enemy = new Tank(ai, _world, _arena, CellCentre(ex, ey),
                EnemySpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: EnemyTeam, lives: StartingLives, terrain: _sandbags);
            ai.Bind(enemy);
            SpawnTank(enemy);
            enemyIndex++;
        }
    }

    private void SpawnTank(Tank tank)
    {
        GameSetup.Modifier.ApplyTo(tank);
        _world.Spawn(tank);
    }

    private void OnEntitySpawned(IEntity entity)
    {
        Node3D? view = entity switch
        {
            ITank tank => BuildTankView(tank),
            IProjectile projectile => BuildProjectileView(projectile),
            _ => null, // powerups/airstrike are added in a later phase
        };

        if (view is null)
        {
            return;
        }

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

    private static Tank3DView BuildTankView(ITank tank)
    {
        var view = new Tank3DView();
        view.Bind(tank);
        view.ApplyTeamTint(tank.Team);
        return view;
    }

    private static Projectile3DView BuildProjectileView(IProjectile projectile)
    {
        var view = new Projectile3DView();
        view.Bind(projectile);
        return view;
    }

    private static NVector2 CellCentre(int x, int y) =>
        new(GridOrigin.X + ((x + 0.5f) * TileSize), GridOrigin.Y + ((y + 0.5f) * TileSize));
}
