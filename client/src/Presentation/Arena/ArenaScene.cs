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

    // The procedural battlefield (S8) places its own spawns and pickups; the scene only says how
    // many adversaries to field. The map size comes from GameSetup so it is adjustable per match.
    private const int EnemyCount = 3;

    // Pickups: a tank driving over one collects its effect — a timed stat boost (S4, ADR-0012)
    // or an ammo crate that loads a special weapon for a few shots (S2, ADR-0013). The generator
    // chooses where each lands; this catalogue is just what to lay (kind + effect). Magnitudes,
    // shot counts, and the pickup radius are tunable balance knobs.
    private const float PickupRadius = 28f;
    private const int AmmoShots = 5;
    private const int RepairAmount = 2;
    private const int ShieldAmount = 3;
    // Seconds before a collected pickup returns to the field — keeps the field stocked across a
    // long round instead of one-per-round. Tunable balance knob.
    private const float PickupRespawnDelay = 12f;
    private static readonly (PowerupKind Kind, IPickupEffect Effect)[] PowerupCatalogue =
    {
        (PowerupKind.SpeedBoost, new StatusEffectPickup(new StatusEffect(StatKind.Speed, Mult: 1.6f, AddFlat: 0f, Seconds: 6f))),
        (PowerupKind.RapidFire, new StatusEffectPickup(new StatusEffect(StatKind.FireInterval, Mult: 0.5f, AddFlat: 0f, Seconds: 6f))),
        (PowerupKind.BouncingAmmo, new AmmoPickup(new BouncingAmmo(bounces: 3), AmmoShots)),
        (PowerupKind.SpreadAmmo, new AmmoPickup(new SpreadAmmo(count: 3, radians: 0.18f), AmmoShots)),
        (PowerupKind.Repair, new RepairPickup(RepairAmount)),
        (PowerupKind.Shield, new ShieldPickup(ShieldAmount)),
        (PowerupKind.PiercingAmmo, new AmmoPickup(new PiercingAmmo(pierces: 1, TileSize), AmmoShots)),
    };

    // Two-player frames the whole field; the zoom is computed per map so any size fits on screen.
    private const float TwoPlayerViewMargin = 1.08f;

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
    private GeneratedArena _layout = null!;
    private GameMode _mode;
    private bool _matchOver;

    public override void _Ready()
    {
        _mode = GameSetup.Mode;

        _layout = new ArenaGenerator().Generate(BuildArenaParams(GameSetup.ArenaSeed));
        var level = _layout.Map;
        var grid = level.BuildGrid();
        _arena = new GridArena(grid, TileSize, GridOrigin);
        _bushes = new BushField(level.Bushes, TileSize, GridOrigin);

        // The player team never friendly-fires (co-op stays friendly); the AI tanks are free-for-all,
        // so they fight each other as well as the player.
        var combat = new CombatResolver(CombatHitRadius, alliedTeam: PlayerTeam);
        combat.TankKilled += _scoreBoard.RecordKill; // credit each kill to the shooter's team
        combat.Hit += hit => _meterBoard.Record(hit.ShooterTeam, hit.VictimTeam, hit.Amount, hit.Killed);
        _world = new World(combat);
        _world.EntitySpawned += OnEntitySpawned;
        _world.EntityDespawned += OnEntityDespawned;

        var theme = GameSetup.Theme;
        AddChild(BuildGround(level.Width, level.Height, theme.Ground)); // behind everything

        var wallView = new WallGridView { Name = "WallGridView", RenderTileSize = (int)TileSize };
        wallView.Modulate = theme.WallTint; // recolour the wall sprites to the theme
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
        SpawnTanks();
    }

    // The procedural-arena recipe (S8): a battlefield of the match's size, from the match seed,
    // with the generator placing the spawns and one cell per pickup in the catalogue.
    private static ArenaGenParams BuildArenaParams(int seed) =>
        new(GameSetup.ArenaWidth, GameSetup.ArenaHeight, seed, EnemyCount, PowerupCatalogue.Length);

    // Lay each catalogue pickup at the cell the generator chose for it (same count by construction),
    // spawning it through the world so it reaches the screen by the same spawn-event path as every
    // other entity (a PowerupView via the type-switch).
    private void SpawnPowerups()
    {
        for (var i = 0; i < PowerupCatalogue.Length; i++)
        {
            var (kind, effect) = PowerupCatalogue[i];
            var (x, y) = _layout.PickupCells[i];
            _world.Spawn(new Powerup(_world, CellCentre(x, y), kind, effect, PickupRadius, PickupRespawnDelay));
        }
    }

    // Spawn through the world so each tank reaches the screen by the same event path as every
    // other entity — no hand-wiring. EntitySpawned fires synchronously. Player 1 is always
    // present; Player 2 and the AI depend on the mode.
    private void SpawnTanks()
    {
        var twoPlayer = _mode != GameMode.OnePlayer;

        // In two-player the left mouse button is Player 2's fire, so Player 1 fires with space.
        var p1Input = new KeyboardMouseInputSource(GetViewport(), fireOnClick: !twoPlayer);
        var player = new Tank(p1Input, _world, _arena, CellCentre(_layout.PlayerSpawn.X, _layout.PlayerSpawn.Y),
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: PlayerTeam, lives: StartingLives);
        _player = player;
        SpawnTank(player);

        var viewers = new List<ITank> { _player };
        if (twoPlayer)
        {
            var p2Team = _mode == GameMode.TwoPlayerVersus ? EnemyTeam : PlayerTeam;
            var p2 = new Tank(new Player2InputSource(), _world, _arena, CellCentre(_layout.Player2Spawn.X, _layout.Player2Spawn.Y),
                TankSpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: p2Team, lives: StartingLives);
            SpawnTank(p2);
            if (p2Team == PlayerTeam)
            {
                viewers.Add(p2); // co-op allies share the fog reveal (versus rivals do not)
            }
        }

        if (_mode != GameMode.TwoPlayerVersus)
        {
            var enemyIndex = 0;
            foreach (var (ex, ey) in _layout.EnemySpawns)
            {
                var ambusher = enemyIndex % 2 == 1; // every other enemy lies in wait in the grass
                var ai = new AiInputSource(_world, _arena, _bushes, ambusher);
                var enemy = new Tank(ai, _world, _arena, CellCentre(ex, ey),
                    EnemySpeed, FireInterval, ProjectileSpeed, maxHp: 3, team: EnemyTeam, lives: StartingLives);
                ai.Bind(enemy); // resolve the input-source ↔ tank construction cycle
                SpawnTank(enemy);
                enemyIndex++;
            }
        }

        // One-player follows the tank; two-player frames the whole field so both stay on screen —
        // centred on the arena and zoomed to fit whatever size this match was generated at.
        if (twoPlayer)
        {
            var fieldW = _layout.Map.Width * TileSize;
            var fieldH = _layout.Map.Height * TileSize;
            _camera.Position = new Vector2(GridOrigin.X + (fieldW / 2f), GridOrigin.Y + (fieldH / 2f));
            _camera.Zoom = FitZoom(fieldW, fieldH);
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

    // Apply the active match modifier (S9: "everyone starts with effect X") to the tank, then spawn
    // it through the world so it reaches the screen by the same event path as every other entity.
    private void SpawnTank(Tank tank)
    {
        GameSetup.Modifier.ApplyTo(tank);
        _world.Spawn(tank);
    }

    // Zoom that fits the whole field (plus a small margin) in the viewport, so the two-player
    // whole-field view works at any generated map size rather than a hand-tuned constant.
    private Vector2 FitZoom(float fieldW, float fieldH)
    {
        var view = GetViewportRect().Size;
        var zoom = Mathf.Min(view.X / fieldW, view.Y / fieldH) / TwoPlayerViewMargin;
        return new Vector2(zoom, zoom);
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

        // Pop a floating name where a pickup is collected, so the player learns what each one does.
        if (entity is IPowerup pickup)
        {
            pickup.Collected += kind => ShowPickupFloater(pickup.Position, kind);
        }
    }

    private void ShowPickupFloater(NVector2 position, PowerupKind kind)
    {
        var floater = new PickupFloater { Name = "PickupFloater" };
        AddChild(floater);
        floater.Show(new Vector2(position.X, position.Y), PickupFloater.LabelKeyFor(kind));
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
        view.ApplyTeamTint(tank.Team != PlayerTeam);
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

    // A ground rectangle covering the whole field, drawn behind the walls and tanks (negative
    // ZIndex). World-space Polygon2D so it tracks the camera; it tiles the themed ground texture
    // (UV in pixels + texture-repeat → one tile per TileSize) tinted by the theme's ground colour.
    private static Polygon2D BuildGround(int widthCells, int heightCells, Color colour)
    {
        var w = widthCells * TileSize;
        var h = heightCells * TileSize;
        var corners = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(w, 0f),
            new Vector2(w, h),
            new Vector2(0f, h),
        };
        return new Polygon2D
        {
            Name = "Ground",
            Color = colour,
            ZIndex = -10,
            Position = new Vector2(GridOrigin.X, GridOrigin.Y),
            Polygon = corners,
            Texture = GD.Load<Texture2D>(AssetCatalogue.Active.GroundTile),
            UV = corners, // UV in texture pixels; with repeat this tiles once per ground-tile width
            TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
        };
    }

    // World-space centre of cell (x, y) — where the tank starts and where shots land.
    private static NVector2 CellCentre(int x, int y) =>
        new(GridOrigin.X + ((x + 0.5f) * TileSize), GridOrigin.Y + ((y + 0.5f) * TileSize));
}
