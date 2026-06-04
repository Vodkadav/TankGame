using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using TankGame.Domain;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>Play scene root and gameplay composition root: loads the hand-authored
/// <see cref="Battlefield01"/> into a <see cref="WallGrid"/>, builds a <see cref="GridArena"/>
/// over it, renders it with a <see cref="WallGridView"/>, then spawns the player
/// <see cref="Tank"/> into the <see cref="World"/> at the level's spawn cell. It subscribes
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

    // Total lives per tank: a fallen tank revives at its spawn after Tank.RespawnDelay until its
    // lives run out, then it stays dead. Tunable balance knob; uniform across modes for now.
    private const int StartingLives = 3;

    private static readonly NVector2 GridOrigin = NVector2.Zero;

    // Floor cells in Battlefield01, spread away from the player spawns.
    private static readonly (int X, int Y)[] EnemySpawns = { (25, 2), (25, 14), (13, 13) };
    private static readonly (int X, int Y) Player2Spawn = (25, 7);

    // Pickups: a tank driving over one collects its effect — a timed stat boost (S4, ADR-0012)
    // or an ammo crate that loads a special weapon for a few shots (S2, ADR-0013). Placed on open
    // floor near the middle so reaching one is a contested choice. Magnitudes/durations, shot
    // counts, and the pickup radius are tunable balance knobs.
    private const float PickupRadius = 28f;
    private const int AmmoShots = 5;
    private const int RepairAmount = 2;
    private const int ShieldAmount = 3;
    // Seconds before a collected pickup returns to the field — keeps the field stocked across a
    // long round instead of one-per-round. Tunable balance knob.
    private const float PickupRespawnDelay = 12f;
    private static readonly (int X, int Y, PowerupKind Kind, IPickupEffect Effect)[] PowerupSpawns =
    {
        (10, 8, PowerupKind.SpeedBoost, new StatusEffectPickup(new StatusEffect(StatKind.Speed, Mult: 1.6f, AddFlat: 0f, Seconds: 6f))),
        (18, 10, PowerupKind.RapidFire, new StatusEffectPickup(new StatusEffect(StatKind.FireInterval, Mult: 0.5f, AddFlat: 0f, Seconds: 6f))),
        (6, 10, PowerupKind.BouncingAmmo, new AmmoPickup(new BehaviourWeapon(() => new BouncingBehaviour(bounces: 3)), AmmoShots)),
        (21, 6, PowerupKind.SpreadAmmo, new AmmoPickup(new SpreadWeapon(count: 3, spreadRadians: 0.18f), AmmoShots)),
        (13, 13, PowerupKind.Repair, new RepairPickup(RepairAmount)),
        (14, 4, PowerupKind.Shield, new ShieldPickup(ShieldAmount)),
        (8, 6, PowerupKind.PiercingAmmo, new AmmoPickup(new PiercingWeapon(pierces: 1, TileSize), AmmoShots)),
    };

    // Two-player uses a static camera framing the whole field so both tanks stay on screen.
    private static readonly Vector2 ArenaCentre = new(GridOrigin.X + (28 * TileSize / 2f), GridOrigin.Y + (16 * TileSize / 2f));
    private static readonly Vector2 TwoPlayerZoom = new(0.55f, 0.55f);

    // Fog of war: a dark ambient the player's light cuts a hole in. Radius ≈ the AI fire range
    // so you can see roughly as far as a hidden enemy can hit you. No wall shadows yet — a soft
    // circular reveal. Off in versus (one shared screen can't fairly fog two rival views).
    private static readonly Color FogAmbient = new(0.16f, 0.16f, 0.22f);
    private const float FogLightRadius = 420f;
    private const int LightTextureSize = 256;

    private readonly Dictionary<Guid, Node2D> _views = new();
    private readonly List<(ITank Tank, PointLight2D Light)> _fogLights = new();
    private readonly MatchTracker _matchTracker = new();
    private readonly ScoreBoard _scoreBoard = new();
    private readonly MeterBoard _meterBoard = new();
    private World _world = null!;
    private GridArena _arena = null!;
    private BushField _bushes = null!;
    private Camera2D _camera = null!;
    private ITank _player = null!;
    private GameMode _mode;
    private bool _matchOver;

    public override void _Ready()
    {
        _mode = GameSetup.Mode;

        var level = LevelMap.Parse(Battlefield01.Text);
        var grid = level.BuildGrid();
        _arena = new GridArena(grid, TileSize, GridOrigin);
        _bushes = new BushField(level.Bushes, TileSize, GridOrigin);

        var combat = new CombatResolver(CombatHitRadius);
        combat.TankKilled += _scoreBoard.RecordKill; // credit each kill to the shooter's team
        combat.Hit += hit => _meterBoard.Record(hit.ShooterTeam, hit.VictimTeam, hit.Amount, hit.Killed);
        _world = new World(combat);
        _world.EntitySpawned += OnEntitySpawned;
        _world.EntityDespawned += OnEntityDespawned;

        var wallView = new WallGridView { Name = "WallGridView", RenderTileSize = (int)TileSize };
        AddChild(wallView);     // runs _Ready (builds the atlas TileSet)
        wallView.Bind(grid);    // then draw the walls and track damage

        var bushView = new BushOverlay { Name = "BushOverlay" };
        AddChild(bushView);     // beneath the tanks so a hidden tank shows over its bush
        bushView.Bind(level.Bushes, TileSize);

        AddChild(BuildInstructionsOverlay());

        var brickCounter = new BrickCounterOverlay { Name = "BrickCounterOverlay" };
        AddChild(brickCounter); // runs _Ready (builds the label)
        brickCounter.Bind(grid);

        var scoreOverlay = new ScoreOverlay { Name = "ScoreOverlay" };
        AddChild(scoreOverlay); // runs _Ready (builds the label)
        scoreOverlay.Bind(_scoreBoard);

        var metersOverlay = new MetersOverlay { Name = "MetersOverlay" };
        AddChild(metersOverlay); // runs _Ready (builds the label)
        metersOverlay.Bind(_meterBoard);

        _camera = new Camera2D { Name = "GameCamera", ProcessCallback = Camera2D.Camera2DProcessCallback.Physics };
        AddChild(_camera);

        SpawnPowerups(); // before the tanks so pickup diamonds render beneath them
        SpawnTanks(level);
    }

    // Spawn the field's pickups through the world so each reaches the screen by the same
    // spawn-event path as every other entity (a PowerupView via the type-switch).
    private void SpawnPowerups()
    {
        foreach (var (x, y, kind, effect) in PowerupSpawns)
        {
            _world.Spawn(new Powerup(_world, CellCentre(x, y), kind, effect, PickupRadius, PickupRespawnDelay));
        }
    }

    // Spawn through the world so each tank reaches the screen by the same event path as every
    // other entity — no hand-wiring. EntitySpawned fires synchronously. Player 1 is always
    // present; Player 2 and the AI depend on the mode.
    private void SpawnTanks(LevelMap level)
    {
        var twoPlayer = _mode != GameMode.OnePlayer;

        // In two-player the left mouse button is Player 2's fire, so Player 1 fires with space.
        var p1Input = new KeyboardMouseInputSource(GetViewport(), fireOnClick: !twoPlayer);
        _player = new Tank(p1Input, _world, _arena, CellCentre(level.SpawnX, level.SpawnY),
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: PlayerTeam, lives: StartingLives);
        _world.Spawn(_player);

        var viewers = new List<ITank> { _player };
        if (twoPlayer)
        {
            var p2Team = _mode == GameMode.TwoPlayerVersus ? EnemyTeam : PlayerTeam;
            var p2 = new Tank(new Player2InputSource(), _world, _arena, CellCentre(Player2Spawn.X, Player2Spawn.Y),
                TankSpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: p2Team, lives: StartingLives);
            _world.Spawn(p2);
            if (p2Team == PlayerTeam)
            {
                viewers.Add(p2); // co-op allies share the fog reveal (versus rivals do not)
            }
        }

        if (_mode != GameMode.TwoPlayerVersus)
        {
            foreach (var (ex, ey) in EnemySpawns)
            {
                var ai = new AiInputSource(_world, _arena, _bushes);
                var enemy = new Tank(ai, _world, _arena, CellCentre(ex, ey),
                    EnemySpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: EnemyTeam, lives: StartingLives);
                ai.Bind(enemy); // resolve the input-source ↔ tank construction cycle
                _world.Spawn(enemy);
            }
        }

        // One-player follows the tank; two-player frames the whole field so both stay on screen.
        if (twoPlayer)
        {
            _camera.Position = ArenaCentre;
            _camera.Zoom = TwoPlayerZoom;
        }
        else
        {
            _camera.Position = new Vector2(_player.Position.X, _player.Position.Y);
        }

        // Versus shares one screen between rivals, so fogging it would blind one of them; every
        // other mode limits the player team's sight to a lit circle around each ally.
        if (_mode != GameMode.TwoPlayerVersus)
        {
            SetUpFog(viewers);
        }
    }

    // A dark CanvasModulate over the field plus one PointLight2D per ally that follows them,
    // so the team sees only a lit circle. The light texture is a radial gradient built in code
    // (no art asset). Lights are tracked so _Process can keep them on their tanks.
    private void SetUpFog(List<ITank> viewers)
    {
        AddChild(new CanvasModulate { Name = "FogModulate", Color = FogAmbient });

        var texture = BuildLightTexture();
        foreach (var viewer in viewers)
        {
            var light = new PointLight2D
            {
                Name = "FogLight",
                Texture = texture,
                TextureScale = FogLightRadius / (LightTextureSize / 2f),
                Position = new Vector2(viewer.Position.X, viewer.Position.Y),
            };
            AddChild(light);
            _fogLights.Add((viewer, light));
        }
    }

    private static GradientTexture2D BuildLightTexture()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 1f, 1f, 1f)); // bright at the centre…
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0f)); // …fading to dark at the rim
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = LightTextureSize,
            Height = LightTextureSize,
        };
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

        // One-player keeps the camera on the player; two-player uses a fixed field-framing camera.
        // While the player is down (awaiting respawn) the camera holds its last position.
        if (_mode == GameMode.OnePlayer && _player.Hp > 0)
        {
            _camera.Position = new Vector2(_player.Position.X, _player.Position.Y);
        }

        // Each ally's vision light rides their tank; a downed (or fallen) ally's light goes dark.
        foreach (var (tank, light) in _fogLights)
        {
            light.Visible = tank.Hp > 0;
            if (tank.Hp > 0)
            {
                light.Position = new Vector2(tank.Position.X, tank.Position.Y);
            }
        }

        var result = _matchTracker.Evaluate(_world.Entities);
        if (result.Decided)
        {
            _matchOver = true;
            GameSetup.Series.RecordRound(result.WinningTeam); // tally this round toward best-of-N
            ShowRoundOver(result);
        }
    }

    private void OnEntitySpawned(IEntity entity)
    {
        Node2D view = entity switch
        {
            ITank tank => BuildTankView(tank),
            IProjectile projectile => BuildProjectileView(projectile),
            IPowerup powerup => BuildPowerupView(powerup),
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

    // A round was decided: show its outcome (from the player's perspective), this round's kill
    // score, and the running best-of-N round tally. When the match is over the button restarts
    // a fresh series; otherwise it loads the next round (the series carries across the reload).
    private void ShowRoundOver(MatchResult result)
    {
        var series = GameSetup.Series;

        var outcomeKey = _mode == GameMode.TwoPlayerVersus
            ? (result.WinningTeam is null ? "hud.draw" : result.WinningTeam == PlayerTeam ? "hud.p1_wins" : "hud.p2_wins")
            : (result.WinningTeam == PlayerTeam ? "hud.you_win" : result.WinningTeam is null ? "hud.draw" : "hud.you_lose");

        var layer = new CanvasLayer { Name = "GameOverLayer" };
        var box = new VBoxContainer { Name = "GameOver" };
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        box.GrowHorizontal = Control.GrowDirection.Both;
        box.GrowVertical = Control.GrowDirection.Both;

        box.AddChild(CentredLabel("Outcome", outcomeKey));
        box.AddChild(CentredLabel("FinalScore", Format(ScoreOverlay.Key,
            _scoreBoard.KillsFor(PlayerTeam), _scoreBoard.KillsFor(EnemyTeam))));
        box.AddChild(CentredLabel("RoundScore", Format("hud.rounds",
            series.WinsFor(PlayerTeam), series.WinsFor(EnemyTeam))));

        // Match over → "Play again" starts a brand-new series; otherwise → "Next round" reloads
        // the scene with the series intact so the tally continues.
        var button = new Button
        {
            Name = "Restart",
            Text = series.IsMatchOver ? "hud.restart" : "hud.next_round",
        };
        button.Pressed += () =>
        {
            if (series.IsMatchOver)
            {
                GameSetup.StartNewMatch(_mode);
            }

            GetTree().ReloadCurrentScene();
        };
        box.AddChild(button);

        layer.AddChild(box);
        AddChild(layer);
    }

    private static Label CentredLabel(string name, string text) => new()
    {
        Name = name,
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static string Format(string key, int a, int b) =>
        string.Format(CultureInfo.InvariantCulture, TranslationServer.Translate(key), a, b);

    private static ProjectileView BuildProjectileView(IProjectile projectile)
    {
        var view = GD.Load<PackedScene>("res://src/Presentation/Projectile/ProjectileView.tscn")
            .Instantiate<ProjectileView>();
        view.Bind(projectile);
        return view;
    }

    private static PowerupView BuildPowerupView(IPowerup powerup)
    {
        var view = new PowerupView();
        view.Bind(powerup);
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
