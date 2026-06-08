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

    // An adversary lurking in grass is hidden from the player until a player tank is within about a
    // tile and a half — the same reveal range the AI uses to spot a bushed target.
    private const float BushRevealRange = 96f;

    // The player team's circle of vision: an enemy farther than this from every living player tank is
    // invisible (not just dimmed). Matches the AI's VisionRange and the lit fog circle so both sides
    // see about as far. A tunable balance knob.
    private const float PlayerVisionRadius = 640f;

    // Total lives per tank: a fallen tank revives at its spawn after Tank.RespawnDelay until its
    // lives run out, then it stays dead. Tunable balance knob; uniform across modes for now.
    private const int StartingLives = 3;
    private const int TankMaxHp = 8; // beefier tanks so fights last longer (below 40% HP a tank limps + smokes)

    private static readonly NVector2 GridOrigin = NVector2.Zero;

    // The procedural battlefield (S8) places its own spawns and pickups; the scene only says how
    // many adversaries to field. The map size comes from GameSetup so it is adjustable per match.
    private const int EnemyCount = 3;

    // Pickups: a tank driving over one collects its effect — a timed stat boost (S4, ADR-0012)
    // or an ammo crate that loads a special weapon for a few shots (S2, ADR-0013). The generator
    // chooses where each lands; this catalogue is just what to lay (kind + effect). Magnitudes,
    // shot counts, and the pickup radius are tunable balance knobs.
    private const float PickupRadius = 28f;
    private const int RepairAmount = 2;
    private const int ShieldAmount = 3;
    // Telephone airstrike: a wide blast on the caller's nearest foe after a short telegraph, so tanks
    // can scramble out. Tunable balance knobs.
    private const int PowerupCount = 9;
    private const float AirstrikeZoneRadius = 70f;
    private const float AirstrikeArmWindow = 3f; // all zones light up within 3s, expanding outward
    private const float AirstrikeDelay = 3f;     // each zone detonates 3s after it lit
    private const int AirstrikeDamage = 3;
    private const float AirstrikeCooldown = 120f; // the airstrike station refills every 2 minutes

    private (PowerupKind Kind, IPickupEffect Effect)[] _powerups = null!;

    // Field pickups grant their effect for as long as the collector lives (unlimited use), shed on
    // death — so the stat boosts are permanent (infinite duration), not the old 6-second timer.
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

    // Two-player frames the whole field; the zoom is computed per map so any size fits on screen.
    private const float TwoPlayerViewMargin = 1.08f;

    // Mouse-wheel zoom (a playtest aid): each wheel notch scales the camera by this factor, clamped.
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 4f;
    private const float ZoomStepFactor = 1.1f;

    // Fog of war: a dark ambient the player's light cuts a hole in. The lit circle matches the player's
    // vision radius so "lit" == "visible". The light is squashed to a 2:1 ellipse (Scale Y 0.5) so the
    // circular texture lands as the iso projection of a world circle. Off in versus (one shared screen
    // can't fairly fog two rival views).
    private static readonly Color FogAmbient = new(0.16f, 0.16f, 0.22f);
    private const float FogLightRadius = PlayerVisionRadius;
    private const int LightTextureSize = 256;

    private readonly Dictionary<Guid, Node2D> _views = new();
    private readonly List<(ITank Tank, PointLight2D Light)> _fogLights = new();
    private readonly MatchTracker _matchTracker = new();
    private readonly ScoreBoard _scoreBoard = new();
    private readonly MeterBoard _meterBoard = new();
    private World _world = null!;
    private GridArena _arena = null!;
    private BushField _bushes = null!;
    private SandbagField _sandbags = null!;
    private Camera2D _camera = null!;
    private CanvasLayer _fireArrows = null!;
    private ITank _player = null!;
    private GeneratedArena _layout = null!;
    private GameMode _mode;
    private bool _matchOver;

    public override void _Ready()
    {
        _mode = GameSetup.Mode;

        _layout = new ArenaGenerator().Generate(BuildArenaParams(GameSetup.ArenaSeed));
        var level = _layout.Map;
        _powerups = PowerupCatalogue(new NVector2(level.Width * TileSize, level.Height * TileSize));
        var grid = level.BuildGrid();
        _arena = new GridArena(grid, TileSize, GridOrigin);
        _bushes = new BushField(level.Bushes, TileSize, GridOrigin);
        _sandbags = new SandbagField(_layout.Sandbags, TileSize, GridOrigin);

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

        // Every non-floor cell — natural terrain (water/bridge/mountain) and the walls (brick/steel/
        // crate/building) — renders as a native iso tile that depth-sorts against the tanks.
        var terrainView = new IsoTerrainView { Name = "IsoTerrainView" };
        AddChild(terrainView);
        terrainView.Bind(grid, TileSize);

        var bushView = new BushOverlay { Name = "BushOverlay" };
        AddChild(bushView);     // beneath the tanks so a hidden tank shows over its bush
        bushView.Bind(level.Bushes, TileSize);

        var sandbagView = new SandbagOverlay { Name = "SandbagOverlay" };
        AddChild(sandbagView);  // beneath the tanks, marking the slow-going patches
        sandbagView.Bind(_layout.Sandbags, TileSize);

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

        _fireArrows = new CanvasLayer { Name = "FireArrows" }; // screen-space edge arrows for incoming fire
        AddChild(_fireArrows);

        SpawnPowerups(); // before the tanks so pickup diamonds render beneath them
        SpawnTanks();
    }

    // The procedural-arena recipe (S8): a battlefield of the match's size, from the match seed,
    // with the generator placing the spawns and one cell per pickup in the catalogue.
    private static ArenaGenParams BuildArenaParams(int seed) =>
        new(GameSetup.ArenaWidth, GameSetup.ArenaHeight, seed, EnemyCount, PowerupCount);

    // Lay each catalogue pickup at the cell the generator chose for it (same count by construction),
    // spawning it through the world so it reaches the screen by the same spawn-event path as every
    // other entity (a PowerupView via the type-switch).
    private void SpawnPowerups()
    {
        for (var i = 0; i < _powerups.Length; i++)
        {
            var (kind, effect) = _powerups[i];
            var (x, y) = _layout.PickupCells[i];

            // Every pickup is carried until its holder dies and drops it where it fell — except the
            // airstrike, a fixed station that stays at its spot and refills on a 2-minute cooldown.
            var powerup = kind == PowerupKind.Telephone
                ? new Powerup(_world, CellCentre(x, y), kind, effect, PickupRadius, respawnCooldown: AirstrikeCooldown)
                : new Powerup(_world, CellCentre(x, y), kind, effect, PickupRadius, dropOnCarrierDeath: true);
            _world.Spawn(powerup);
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
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: PlayerTeam, lives: StartingLives, terrain: _sandbags);
        _player = player;
        SpawnTank(player);

        var viewers = new List<ITank> { _player };
        if (twoPlayer)
        {
            var p2Team = _mode == GameMode.TwoPlayerVersus ? EnemyTeam : PlayerTeam;
            var p2 = new Tank(new Player2InputSource(), _world, _arena, CellCentre(_layout.Player2Spawn.X, _layout.Player2Spawn.Y),
                TankSpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: p2Team, lives: StartingLives, terrain: _sandbags);
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
                    EnemySpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: EnemyTeam, lives: StartingLives, terrain: _sandbags);
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
            _camera.Position = Screen(new NVector2(fieldW / 2f, fieldH / 2f));
            _camera.Zoom = FitZoom(fieldW, fieldH);
        }
        else
        {
            _camera.Position = Screen(_player.Position);
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
        // The field projects to an iso diamond: width (w+h)/2, height (w+h)/4 — fit that, not the square.
        var isoW = (fieldW + fieldH) * 0.5f;
        var isoH = (fieldW + fieldH) * 0.25f;
        var zoom = Mathf.Min(view.X / isoW, view.Y / isoH) / TwoPlayerViewMargin;
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
                Scale = new Vector2(1f, 0.5f), // squash to the iso 2:1 ellipse of a world vision circle
                Position = Screen(viewer.Position),
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
            _camera.Position = Screen(_player.Position);
        }

        // Each ally's vision light rides their tank; a downed (or fallen) ally's light goes dark.
        foreach (var (tank, light) in _fogLights)
        {
            light.Visible = tank.Hp > 0;
            if (tank.Hp > 0)
            {
                light.Position = Screen(tank.Position);
            }
        }

        UpdateConcealment();

        var result = _matchTracker.Evaluate(_world.Entities);
        if (result.Decided)
        {
            _matchOver = true;
            GameSetup.Series.RecordRound(result.WinningTeam); // tally this round toward best-of-N
            ShowRoundOver(result);
        }
    }

    // An enemy tank is visible to the player only inside the player team's circle of vision (and not
    // lurking unspotted in grass); beyond that circle it is invisible, not merely dimmed. The player's
    // own tank darkens while it sits in a bush, to signal it is in stealth cover. Versus has no AI
    // enemies and shares one screen, so it is left alone.
    private void UpdateConcealment()
    {
        if (_mode == GameMode.TwoPlayerVersus)
        {
            return;
        }

        foreach (var entity in _world.Entities)
        {
            if (entity is not ITank tank || !_views.TryGetValue(tank.Id, out var node) || node is not TankView view)
            {
                continue;
            }

            if (tank.Team == PlayerTeam)
            {
                view.Stealthed = tank.Hp > 0 && _bushes.ConcealsAt(tank.Position);
                continue;
            }

            var outsideVision = !AnyPlayerWithin(tank.Position, PlayerVisionRadius);
            var unspottedInGrass = _bushes.ConcealsAt(tank.Position) && !AnyPlayerWithin(tank.Position, BushRevealRange);
            view.Concealed = tank.Hp > 0 && (outsideVision || unspottedInGrass);
        }
    }

    private bool AnyPlayerWithin(NVector2 point, float range)
    {
        foreach (var entity in _world.Entities)
        {
            if (entity is ITank tank && tank.Team == PlayerTeam && tank.Hp > 0
                && NVector2.Distance(tank.Position, point) <= range)
            {
                return true;
            }
        }

        return false;
    }

    private void OnEntitySpawned(IEntity entity)
    {
        Node2D view = entity switch
        {
            ITank tank => BuildTankView(tank),
            IProjectile projectile => BuildProjectileView(projectile),
            IPowerup powerup => BuildPowerupView(powerup),
            IAirstrike strike => BuildAirstrikeView(strike),
            _ => throw new NotSupportedException($"No view registered for {entity.GetType().Name}.")
        };

        AddChild(view);
        _views[entity.Id] = view;

        // Pop a floating name where a pickup is collected, so the player learns what each one does.
        if (entity is IPowerup pickup)
        {
            pickup.Collected += kind => ShowPickupFloater(pickup.Position, kind);
        }

        // A new enemy shot — flash a screen-edge arrow toward the tank that fired it.
        if (entity is IProjectile shot && shot.Team != PlayerTeam)
        {
            ShowFireArrow(shot.Position);
        }
    }

    // Place a blinking arrow near the screen edge pointing toward where the shot came from. The
    // direction from the screen centre to the shooter is the same in world and screen space (the
    // camera is not rotated), so a normalised world delta gives the on-screen heading.
    private void ShowFireArrow(NVector2 shooterWorld)
    {
        var viewportCentre = GetViewportRect().Size * 0.5f;
        var toShooter = Screen(shooterWorld) - _camera.GetScreenCenterPosition();
        if (toShooter.LengthSquared() < 1f)
        {
            return;
        }

        var dir = toShooter.Normalized();
        var radius = Mathf.Min(viewportCentre.X, viewportCentre.Y) * 0.86f; // just inside the edge
        var arrow = new FireArrow();
        _fireArrows.AddChild(arrow);
        arrow.Show(viewportCentre + (dir * radius), dir.Angle());
    }

    private void ShowPickupFloater(NVector2 position, PowerupKind kind)
    {
        var floater = new PickupFloater { Name = "PickupFloater" };
        AddChild(floater);
        floater.Show(Screen(position), PickupFloater.LabelKeyFor(kind));
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
        view.ApplyTeamTint(tank.Team);
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

    private static AirstrikeView BuildAirstrikeView(IAirstrike strike)
    {
        var view = new AirstrikeView();
        view.Bind(strike);
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

    // The isometric ground (Phase 2): one PixVoxel diamond tile per cell laid through a real iso
    // TileMapLayer (Godot's TileShape = Isometric), tinted to the theme, drawn behind the walls and
    // tanks. The square flat-ground polygon is gone — this lays native 128×64 iso tiles that align
    // with the entity projection by construction.
    private const int GroundTileWidth = 128;          // the iso tile's diamond width
    private const int GroundTileHeight = 64;           // the iso tile's diamond (cell) height
    private const int GroundTileTextureHeight = 90;    // full texture height (diamond + bottom skirt)

    // Godot's iso TileMapLayer puts cell (0,0)'s centre at (tile, tile/2); the entity projection puts
    // it at (0, tile/2). The per-cell basis already matches (DiamondDown gives ex=(64,32) ey=(-64,32),
    // exactly WorldToScreen's deltas), so a constant −tile shift in x lands the layer on the same grid
    // as the sheared walls and the tanks. (Verified by Ground_AlignsWithTheEntityProjection.)
    private static readonly Vector2 GroundCellOffset = new(-TileSize, 0f);

    // The diamond's centre sits ~11 px below the 90-tall texture's centre; lift the art so a tank on
    // a cell reads as standing on its diamond. A cosmetic nudge — tune by eye (mouse-wheel zoom helps).
    private static readonly Vector2I GroundTextureOrigin = new(0, -11);

    private static TileMapLayer BuildGround(int widthCells, int heightCells, Color tint)
    {
        var source = new TileSetAtlasSource
        {
            Texture = GD.Load<Texture2D>(AssetCatalogue.Active.GroundTile),
            TextureRegionSize = new Vector2I(GroundTileWidth, GroundTileTextureHeight),
        };
        source.CreateTile(Vector2I.Zero);
        source.GetTileData(Vector2I.Zero, 0).TextureOrigin = GroundTextureOrigin;

        var tileSet = new TileSet
        {
            TileShape = TileSet.TileShapeEnum.Isometric,
            TileLayout = TileSet.TileLayoutEnum.DiamondDown, // cell (x,y) → ((x-y)·w/2, (x+y)·h/2)
            TileSize = new Vector2I(GroundTileWidth, GroundTileHeight),
        };
        var sourceId = tileSet.AddSource(source);

        var layer = new TileMapLayer
        {
            Name = "Ground",
            TileSet = tileSet,
            ZIndex = -10,             // behind the walls and tanks
            Position = GroundCellOffset,
            Modulate = tint,          // recolour the sand to the theme
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, // keep the pixel art crisp
        };
        for (var x = 0; x < widthCells; x++)
        {
            for (var y = 0; y < heightCells; y++)
            {
                layer.SetCell(new Vector2I(x, y), sourceId, Vector2I.Zero);
            }
        }

        return layer;
    }

    // Mouse-wheel zooms the camera in/out so the field can be inspected while playtesting. Wheel
    // events arrive unhandled (the input source polls state, it does not consume events).
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } click)
        {
            return;
        }

        if (click.ButtonIndex == MouseButton.WheelUp)
        {
            _camera.Zoom = SteppedZoom(_camera.Zoom, zoomIn: true);
        }
        else if (click.ButtonIndex == MouseButton.WheelDown)
        {
            _camera.Zoom = SteppedZoom(_camera.Zoom, zoomIn: false);
        }
    }

    /// <summary>One wheel-notch zoom step, clamped and uniform on both axes (so the view never
    /// skews). Public/static so a test can pin the curve without driving input events.</summary>
    public static Vector2 SteppedZoom(Vector2 current, bool zoomIn)
    {
        var z = Mathf.Clamp(current.X * (zoomIn ? ZoomStepFactor : 1f / ZoomStepFactor), MinZoom, MaxZoom);
        return new Vector2(z, z);
    }

    // Project a flat world position into isometric screen space for a node placed directly under the
    // scene (entities and the camera live in screen space; the static layers shear themselves instead).
    private static Vector2 Screen(NVector2 world)
    {
        var s = IsoProjection.WorldToScreen(world);
        return new Vector2(s.X, s.Y);
    }

    // World-space centre of cell (x, y) — where the tank starts and where shots land.
    private static NVector2 CellCentre(int x, int y) =>
        new(GridOrigin.X + ((x + 0.5f) * TileSize), GridOrigin.Y + ((y + 0.5f) * TileSize));
}
