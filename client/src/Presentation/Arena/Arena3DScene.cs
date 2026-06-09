using System;
using System.Collections.Generic;
using System.Linq;
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
    private const int TankMaxHp = 8; // beefier tanks so fights last longer (below 40% HP a tank limps + smokes)

    // Fog of war (the 3D port of the iso fog): the player sees only a lit circle around their tank. An
    // enemy farther than PlayerVisionRadius from every living player tank is invisible (not just dimmed);
    // an enemy lurking in grass is hidden until a player is within BushRevealRange. The same radii the iso
    // scene and the AI use, so both sides see about as far. Tunable balance knobs.
    private const float PlayerVisionRadius = 640f;
    private const float BushRevealRange = 96f;

    private const float PickupRadius = 28f;
    private const int RepairAmount = 2;
    private const int ShieldAmount = 3;
    private const int PowerupCount = 9;
    private const float AirstrikeZoneRadius = 70f;
    private const float AirstrikeArmWindow = 3f; // all zones light up within 3s, expanding outward
    private const float AirstrikeDelay = 3f;     // each zone detonates 3s after it lit
    private const int AirstrikeDamage = 3;
    private const float AirstrikeCooldown = 120f; // the airstrike station refills every 2 minutes

    private IReadOnlyDictionary<PowerupKind, IPickupEffect> _powerupEffects = null!;
    private IReadOnlyList<(PowerupKind Kind, int X, int Y)> _powerupPlacements = Array.Empty<(PowerupKind, int, int)>();

    // The catalogue's kind order — the generator's PickupCells line up with it one-for-one. Keep in sync
    // with PowerupCatalogue below.
    private static readonly PowerupKind[] PowerupOrder =
    {
        PowerupKind.SpeedBoost, PowerupKind.RapidFire, PowerupKind.BouncingAmmo, PowerupKind.SpreadAmmo,
        PowerupKind.Repair, PowerupKind.Shield, PowerupKind.PiercingAmmo, PowerupKind.Missile, PowerupKind.Telephone,
    };

    // Field pickups grant their effect for as long as the collector lives (unlimited use), shed on
    // death — so the stat boosts are permanent (infinite duration), not the old 6-second timer. Built
    // in _Ready once the field size is known (the airstrike needs the field bounds for its swathe).
    private (PowerupKind Kind, IPickupEffect Effect)[] PowerupCatalogue(NVector2 fieldMax) => new[]
    {
        (PowerupKind.SpeedBoost, (IPickupEffect)new StatusEffectPickup(new StatusEffect(StatKind.Speed, Mult: 1.6f, AddFlat: 0f, Seconds: float.PositiveInfinity))),
        (PowerupKind.RapidFire, new StatusEffectPickup(new StatusEffect(StatKind.FireInterval, Mult: 0.5f, AddFlat: 0f, Seconds: float.PositiveInfinity))),
        (PowerupKind.BouncingAmmo, new AmmoPickup(new BouncingAmmo(bounces: 3))),
        (PowerupKind.SpreadAmmo, new AmmoPickup(new SpreadAmmo(count: 3, radians: 0.18f))),
        (PowerupKind.Repair, new RepairPickup(RepairAmount)),
        (PowerupKind.Shield, new ShieldPickup(ShieldAmount)),
        (PowerupKind.PiercingAmmo, new AmmoPickup(new PiercingAmmo(pierces: 1, TileSize))),
        (PowerupKind.Missile, new AmmoPickup(new MissileAmmo(TileSize))),
        (PowerupKind.Telephone, new AirstrikePickup(GridOrigin, fieldMax, AirstrikeZoneRadius, AirstrikeArmWindow, AirstrikeDelay, AirstrikeDamage)),
    };

    // Orthographic ¾ camera. Eyeball-gated on playtest.
    private const float CamPitchDeg = 52f;
    private const float CamYawDeg = 45f;
    private const float CamDistance = 2500f;
    private const float CamOrthoSize = 820f; // world units shown vertically (~13 cells)
    private const float MinOrthoSize = 200f;
    private const float MaxOrthoSize = 2400f;
    private const float ZoomStep = 1.12f;

    private static readonly NVector2 GridOrigin = NVector2.Zero;

    private readonly Dictionary<Guid, Node3D> _views = new();
    private World _world = null!;
    private GridArena _arena = null!;
    private BushField _bushes = null!;
    private SandbagField _sandbags = null!;
    private (int X, int Y) _playerSpawn;
    private IReadOnlyList<(int X, int Y)> _enemySpawns = Array.Empty<(int, int)>();
    private Camera3D _camera = null!;
    private ITank _player = null!;
    private DirectionalLight3D _sun = null!;
    private Godot.Environment _environment = null!;

    // The fog spotlight rides the player and lights roughly a vision-radius circle on the ground; the
    // viewers are the tanks whose sight reveals the field (just the player today; co-op allies would join
    // this list so the team shares one lit field). A dark ambient — set when fog is on — is what the
    // lights cut a hole in.
    private SpotLight3D _fogLight = null!;
    private readonly List<ITank> _viewers = new();

    public override void _Ready()
    {
        LevelMap level;
        bool[,] sandbags;

        // Build from a user-made map when one is chosen ("My Maps"), otherwise generate a Desert War.
        if (GameSetup.CustomMap is { } custom)
        {
            level = MapLoader.ToLevel(custom);
            sandbags = custom.Sandbags;
            _playerSpawn = custom.PlayerSpawn;
            _enemySpawns = custom.EnemySpawns;
            _powerupPlacements = custom.PowerupSpawns.Select(s => (s.Kind, s.X, s.Y)).ToList();
        }
        else
        {
            var dim = Mathf.Max(GameSetup.ArenaWidth, GameSetup.ArenaHeight); // a square arena, not oblong
            var layout = new ArenaGenerator().Generate(
                new ArenaGenParams(dim, dim, GameSetup.ArenaSeed, EnemyCount, PowerupCount));
            level = layout.Map;
            sandbags = layout.Sandbags;
            _playerSpawn = layout.PlayerSpawn;
            _enemySpawns = layout.EnemySpawns;
            _powerupPlacements = ZipCatalogue(layout.PickupCells);
        }

        var fieldMax = new NVector2(level.Width * TileSize, level.Height * TileSize);
        _powerupEffects = PowerupCatalogue(fieldMax).ToDictionary(p => p.Kind, p => p.Effect);

        var grid = level.BuildGrid();
        _arena = new GridArena(grid, TileSize, GridOrigin);
        _bushes = new BushField(level.Bushes, TileSize, GridOrigin);
        _sandbags = new SandbagField(sandbags, TileSize, GridOrigin);

        var combat = new CombatResolver(CombatHitRadius, alliedTeam: PlayerTeam);
        _world = new World(combat);
        _world.EntitySpawned += OnEntitySpawned;
        _world.EntityDespawned += OnEntityDespawned;

        BuildEnvironment();
        BuildGround(level.Width, level.Height);

        var terrain = new Terrain3DView { Name = "Terrain3DView" };
        AddChild(terrain);
        terrain.Bind(grid, level.Bushes, sandbags, TileSize);

        SpawnPowerups();
        SpawnTanks();
        BuildPreviewHud();
        BuildPauseMenu();
    }

    // Pairs each catalogue kind with the cell the generator chose for it (one pickup per kind, in order).
    private static IReadOnlyList<(PowerupKind Kind, int X, int Y)> ZipCatalogue(IReadOnlyList<(int X, int Y)> cells)
    {
        var placements = new List<(PowerupKind, int, int)>();
        for (var i = 0; i < PowerupOrder.Length && i < cells.Count; i++)
        {
            placements.Add((PowerupOrder[i], cells[i].X, cells[i].Y));
        }

        return placements;
    }

    private CanvasLayer _pauseLayer = null!;
    private bool _paused;

    /// <summary>Whether the game is paused (the Escape menu is up). Exposed for tests.</summary>
    public bool IsPaused => _paused;

    /// <summary>The player tank's world position on the ground plane. Exposed so a test can assert the
    /// fog light is centred on the player.</summary>
    public Vector3 PlayerWorldPosition => GroundProjection.ToWorld(_player.Position);

    // Escape opens a pause menu (single-player, so freezing the world is fair): the world stops stepping
    // and an overlay offers Resume, Main Menu, or Exit. Public so a test can drive it.
    public void TogglePause()
    {
        _paused = !_paused;
        _pauseLayer.Visible = _paused;
    }

    private void BuildPauseMenu()
    {
        _pauseLayer = new CanvasLayer { Name = "PauseMenu", Visible = false };

        var dim = new ColorRect { Name = "Dim", Color = new Color(0f, 0f, 0f, 0.55f) };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseLayer.AddChild(dim);

        var menu = new VBoxContainer { Name = "PauseBox" };
        menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        menu.GrowHorizontal = Control.GrowDirection.Both;
        menu.GrowVertical = Control.GrowDirection.Both;
        menu.AddThemeConstantOverride("separation", 12);
        menu.AddChild(new Label { Text = "pause.heading", HorizontalAlignment = HorizontalAlignment.Center });

        var resume = new Button { Name = "Resume", Text = "pause.resume" };
        resume.Pressed += TogglePause;
        menu.AddChild(resume);

        var mainMenu = new Button { Name = "MainMenu", Text = "pause.main_menu" };
        mainMenu.Pressed += () => GetTree().ChangeSceneToFile("res://src/Presentation/Title.tscn");
        menu.AddChild(mainMenu);

        var exit = new Button { Name = "ExitGame", Text = "pause.exit" };
        exit.Pressed += () => GetTree().Quit();
        menu.AddChild(exit);

        _pauseLayer.AddChild(menu);
        AddChild(_pauseLayer);
    }

    private bool _labelsShown = true;

    // Mouse wheel zooms the orthographic camera (smaller size = closer); the L key toggles the debug
    // name tags. Preview aids while the 3D port is built up.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } click)
        {
            if (click.ButtonIndex == MouseButton.WheelUp)
            {
                _camera.Size = Mathf.Clamp(_camera.Size / ZoomStep, MinOrthoSize, MaxOrthoSize);
            }
            else if (click.ButtonIndex == MouseButton.WheelDown)
            {
                _camera.Size = Mathf.Clamp(_camera.Size * ZoomStep, MinOrthoSize, MaxOrthoSize);
            }
        }
        else if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            TogglePause();
        }
        else if (@event is InputEventKey { Pressed: true, Keycode: Key.L })
        {
            _labelsShown = !_labelsShown;
            foreach (var node in GetTree().GetNodesInGroup(DebugLabel.Group))
            {
                if (node is Label3D label)
                {
                    label.Visible = _labelsShown;
                }
            }
        }
    }

    // A screen-space "Replay" button (reloads the scene) so the owner can re-watch the match while
    // inspecting the 3D assets.
    private void BuildPreviewHud()
    {
        var layer = new CanvasLayer { Name = "PreviewHud" };
        var replay = new Button { Name = "Replay", Text = "hud.replay" };
        replay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        replay.Position += new Vector2(16f, -16f);
        replay.Pressed += () => GetTree().ReloadCurrentScene();
        layer.AddChild(replay);
        AddChild(layer);
    }

    public override void _Process(double delta)
    {
        if (_paused)
        {
            return; // the Escape menu is up — freeze the match
        }

        _world.Step((float)delta);
        if (_player.Hp > 0)
        {
            var target = GroundProjection.ToWorld(_player.Position);
            _camera.Position = target + CameraOffset();
            _camera.LookAt(target, Vector3.Up);
        }

        PositionFogLight();   // the lit circle rides the player (goes dark while the player is down)
        UpdateConcealment();  // hide enemies outside the player's vision; darken the player in cover
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
            // The scene steps and re-aims the camera from _Process, not _PhysicsProcess; opting it out of
            // physics interpolation avoids Godot's "Interpolated Camera3D triggered from outside physics
            // process" warning (the move is already smooth at frame rate).
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
        };
        AddChild(_camera);

        _sun = new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-55f, -40f, 0f),
            LightEnergy = 1.4f,
            ShadowEnabled = true,
        };
        AddChild(_sun);

        _environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.45f, 0.62f, 0.78f), // sky
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.7f, 0.72f, 0.78f),
            AmbientLightEnergy = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Aces, // compress highlights so light colours don't blow out to white
        };
        AddChild(new WorldEnvironment { Environment = _environment });
    }

    // Fog of war: a dark world (the sun dimmed to near-dusk, a near-black ambient and sky) that the
    // player's spotlight cuts a lit circle into. The spotlight hangs high over the player and points
    // straight down, its cone sized so the lit ground disc is about a vision-radius across — so "lit"
    // reads as "visible", matching the iso fog. Structured so co-op could add an ally light later
    // (one per viewer); single-player needs just the one. Versus (shared screen) gets no fog at all.
    private static readonly Color FogAmbient = new(0.10f, 0.10f, 0.14f);
    private const float FogLightHeight = 1400f;   // how high over the player the spotlight hangs
    private const float FogLightEnergy = 6f;
    private const float FogSunEnergy = 0.18f;      // the sun dimmed to a faint dusk under fog

    private void SetUpFog()
    {
        _sun.LightEnergy = FogSunEnergy;
        _environment.AmbientLightColor = FogAmbient;
        _environment.AmbientLightEnergy = 1f;
        _environment.BackgroundColor = FogAmbient; // a dark horizon, not a bright sky

        // The cone half-angle whose lit ground disc (at FogLightHeight) is ~PlayerVisionRadius across.
        var coneDeg = Mathf.RadToDeg(Mathf.Atan2(PlayerVisionRadius, FogLightHeight));
        _fogLight = new SpotLight3D
        {
            Name = "FogLight",
            RotationDegrees = new Vector3(-90f, 0f, 0f), // point straight down
            SpotRange = FogLightHeight * 2.4f,
            SpotAngle = coneDeg,
            SpotAngleAttenuation = 1.4f, // softer rim so the circle fades rather than hard-edges
            LightEnergy = FogLightEnergy,
            ShadowEnabled = false,
        };
        AddChild(_fogLight);
        PositionFogLight();
    }

    private void PositionFogLight()
    {
        if (_fogLight is null)
        {
            return;
        }

        var on = _player.Hp > 0;
        _fogLight.Visible = on;
        if (on)
        {
            _fogLight.Position = GroundProjection.ToWorld(_player.Position) + new Vector3(0f, FogLightHeight, 0f);
        }
    }

    // An enemy tank is shown only inside the player team's circle of vision (and not lurking unspotted
    // in grass); beyond that it is hidden, not merely dimmed. The player's own tank darkens while it
    // sits in a bush, to signal it is in stealth cover. The 3D port of ArenaScene.UpdateConcealment.
    private void UpdateConcealment()
    {
        foreach (var entity in _world.Entities)
        {
            if (entity is not ITank tank || !_views.TryGetValue(tank.Id, out var node) || node is not Tank3DView view)
            {
                continue;
            }

            if (tank.Team == PlayerTeam)
            {
                view.Stealthed = tank.Hp > 0 && _bushes.ConcealsAt(tank.Position);
                continue;
            }

            var outsideVision = !AnyViewerWithin(tank.Position, PlayerVisionRadius);
            var unspottedInGrass = _bushes.ConcealsAt(tank.Position) && !AnyViewerWithin(tank.Position, BushRevealRange);
            view.Concealed = tank.Hp > 0 && (outsideVision || unspottedInGrass);
        }
    }

    private bool AnyViewerWithin(NVector2 point, float range)
    {
        foreach (var viewer in _viewers)
        {
            if (viewer.Hp > 0 && NVector2.Distance(viewer.Position, point) <= range)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildGround(int widthCells, int heightCells)
    {
        var w = widthCells * TileSize;
        var h = heightCells * TileSize;

        // A noise-mottled sand so the ground reads dusty rather than a flat shiny slab.
        var noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.05f };
        // A patchwork of brown, yellow, grey and green so the ground reads as varied dusty terrain.
        var ramp = new Gradient();
        ramp.Offsets = new[] { 0f, 0.34f, 0.67f, 1f };
        ramp.Colors = new[]
        {
            new Color(0.40f, 0.47f, 0.30f), // green
            new Color(0.52f, 0.40f, 0.26f), // brown
            new Color(0.78f, 0.67f, 0.42f), // yellow sand
            new Color(0.54f, 0.54f, 0.52f), // grey
        };
        var sand = new NoiseTexture2D { Noise = noise, Width = 256, Height = 256, Seamless = true, ColorRamp = ramp };

        var ground = new MeshInstance3D
        {
            Name = "Ground",
            Mesh = new PlaneMesh { Size = new Vector2(w, h) },
            Position = new Vector3(w / 2f, 0f, h / 2f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = sand,
                Uv1Scale = new Vector3(widthCells / 4f, heightCells / 4f, 1f), // tile every ~4 cells
                Roughness = 1f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            },
        };
        AddChild(ground);

        ScatterDirt(w, h);
    }

    // A sprinkle of darker dirt patches across the field for extra texture. Deterministic positions.
    private void ScatterDirt(float w, float h)
    {
        var patch = new CylinderMesh { TopRadius = 26f, BottomRadius = 26f, Height = 0.5f };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.46f, 0.37f, 0.24f, 0.6f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 1f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        };
        for (var i = 1; i <= 32; i++)
        {
            var px = ((i * 2654435761u) % (uint)Mathf.Max(1f, w));
            var pz = ((i * 40503u * 7919u) % (uint)Mathf.Max(1f, h));
            AddChild(new MeshInstance3D
            {
                Name = $"Dirt_{i}",
                Mesh = patch,
                Position = new Vector3(px, 0.6f, pz),
                MaterialOverride = material,
            });
        }
    }

    private void SpawnTanks()
    {
        var p1Input = new KeyboardMouse3DInputSource(_camera, fireOnClick: true);
        var player = new Tank(p1Input, _world, _arena, CellCentre(_playerSpawn.X, _playerSpawn.Y),
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: PlayerTeam, lives: StartingLives, terrain: _sandbags);
        _player = player;
        _viewers.Add(player); // the player's sight reveals the field; co-op allies would join this list
        SpawnTank(player);

        var enemyIndex = 0;
        foreach (var (ex, ey) in _enemySpawns)
        {
            var ambusher = enemyIndex % 2 == 1;
            var ai = new AiInputSource(_world, _arena, _bushes, ambusher);
            var enemy = new Tank(ai, _world, _arena, CellCentre(ex, ey),
                EnemySpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: EnemyTeam, lives: StartingLives, terrain: _sandbags);
            ai.Bind(enemy);
            SpawnTank(enemy);
            enemyIndex++;
        }

        SetUpFog(); // single-player → fog the field with a lit circle around the player
    }

    private void SpawnTank(Tank tank)
    {
        GameSetup.Modifier.ApplyTo(tank);
        _world.Spawn(tank);
    }

    // Lay each placed pickup (from the generator or the chosen custom map) at its cell, spawned through
    // the world so it reaches the scene by the same spawn-event path as every other entity.
    private void SpawnPowerups()
    {
        foreach (var (kind, x, y) in _powerupPlacements)
        {
            if (!_powerupEffects.TryGetValue(kind, out var effect))
            {
                continue;
            }

            // Every pickup is carried until its holder dies and drops it where it fell — except the
            // airstrike, a fixed station that stays at its spot and refills on a 2-minute cooldown.
            var powerup = kind == PowerupKind.Telephone
                ? new Powerup(_world, CellCentre(x, y), kind, effect, PickupRadius, respawnCooldown: AirstrikeCooldown)
                : new Powerup(_world, CellCentre(x, y), kind, effect, PickupRadius, dropOnCarrierDeath: true);
            _world.Spawn(powerup);
        }
    }

    private void OnEntitySpawned(IEntity entity)
    {
        Node3D? view = entity switch
        {
            ITank tank => BuildTankView(tank),
            IProjectile projectile => BuildProjectileView(projectile),
            IPowerup powerup => BuildPowerupView(powerup),
            IAirstrike strike => BuildAirstrikeView(strike),
            _ => null,
        };

        if (view is null)
        {
            return;
        }

        AddChild(view);
        _views[entity.Id] = view;

        if (entity is IPowerup pickup)
        {
            pickup.Collected += kind => ShowPickupFloater(pickup.Position, kind);
        }
    }

    private void ShowPickupFloater(NVector2 position, PowerupKind kind)
    {
        var floater = new PickupFloater3D { Name = "PickupFloater" };
        AddChild(floater);
        floater.Show(GroundProjection.ToWorld(position, 40f), PickupFloater.LabelKeyFor(kind));
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

    private static Powerup3DView BuildPowerupView(IPowerup powerup)
    {
        var view = new Powerup3DView();
        view.Bind(powerup);
        return view;
    }

    private static Airstrike3DView BuildAirstrikeView(IAirstrike strike)
    {
        var view = new Airstrike3DView();
        view.Bind(strike);
        return view;
    }

    private static NVector2 CellCentre(int x, int y) =>
        new(GridOrigin.X + ((x + 0.5f) * TileSize), GridOrigin.Y + ((y + 0.5f) * TileSize));
}
