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

    private const float TeleportPadRadius = 40f; // a tank centred within this of a pad warps to its partner
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
    private IWallGrid _grid = null!;
    private BushField _bushes = null!;
    private SandbagField _sandbags = null!;
    private Teleporter _teleporter = null!;
    private readonly List<TeleportPad3DView> _padViews = new();
    private IReadOnlyList<TeleportPadLink> _authoredPads = Array.Empty<TeleportPadLink>();
    /// <summary>Per-tank combat stats for this match — the end-of-match screen reads it.</summary>
    public BattleStats Stats { get; private set; } = null!;

    private readonly MatchTracker _matchTracker = new();

    /// <summary>True once the victory screen is up: the world stops stepping.</summary>
    public bool IsMatchOver { get; private set; }

    private const string BannerArtPath = "res://src/Presentation/Arena/ui/victory_banner.png";
    private const int PlateCount = 8; // the artwork's numbered plates = the 4v4 tank cap

    // The leaderboard's ranking views, panned with the arrows (owner ask 2026-06-11). Victory v3 keeps
    // the single cycling view (owner choice) and re-anchors it onto the eight-plate template2 artwork.
    private static readonly (string TitleKey, Func<BattleStats.TankTally, int> Value)[] LeaderboardViews =
    {
        ("stats.kills", t => t.Kills),
        ("stats.deaths", t => t.Deaths),
        ("stats.dealt", t => t.DamageDealt),
        ("stats.taken", t => t.DamageTaken),
        ("stats.healing", t => t.HealingTaken),
        ("stats.assists", t => t.Assists),
    };

    // Anchors measured from victory_banner.png (1024x1471, prepared by scripts/prep_victory_banner.py):
    // the wood ribbon (champion name), the two header pills and the arrow buttons flanking them (view
    // nav), the eight numbered plates (leaderboard rows), and a bottom strip for the menu buttons. All
    // are fractions of the banner's drawn rectangle, recomputed on resize so they track the art at any
    // window size (the portrait banner fits height on a landscape window, with dim bands at the sides).
    private static readonly Rect2 RibbonFrac = new(0.205f, 0.150f, 0.580f, 0.082f);
    private static readonly Rect2 ViewTitleFrac = new(0.265f, 0.436f, 0.455f, 0.050f);
    private static readonly Rect2 PrevArrowFrac = new(0.150f, 0.432f, 0.075f, 0.058f);
    private static readonly Rect2 NextArrowFrac = new(0.775f, 0.432f, 0.075f, 0.058f);
    private static readonly Rect2 ButtonBarFrac = new(0.110f, 0.952f, 0.780f, 0.044f);
    private static readonly float[] PlateMidFrac = { 0.558f, 0.611f, 0.666f, 0.718f, 0.771f, 0.823f, 0.873f, 0.926f };
    private const float PlateLeftFrac = 0.175f; // text starts past the baked number badge
    private const float PlateRightFrac = 0.945f;
    private const float PlateHeightFrac = 0.046f;

    private static readonly Color PlateInk = new(0.20f, 0.13f, 0.05f);   // dark text on the gold plaques
    private static readonly Color AwardRed = new(0.55f, 0.10f, 0.08f);   // the art's deep red, for award tags
    private static readonly Color RibbonGold = new(1f, 0.84f, 0.30f);    // the ribbon/pill gold

    private int _viewIndex;
    private Texture2D _bannerArt = null!;
    private Label _winnerName = null!;
    private Label _viewTitle = null!;
    private Button _prevView = null!;
    private Button _nextView = null!;
    private Control _leaderboardRows = null!;
    private HBoxContainer _buttonBar = null!;

    /// <summary>The end of the match, victory screen v3 (owner ask 2026-06-11/14): freeze the world
    /// under the celebration banner artwork — the champion's name carved into its ribbon, a fixed
    /// eight-plate leaderboard anchored onto the art's numbered plaques, the current ranking view
    /// named over the header pills with the baked arrows panning it, and styled New Game / Back to
    /// Menu / Exit on the bottom strip. Everything anchors to the banner's drawn rectangle and
    /// re-lays-out on resize. Public so a test can drive it without fighting a whole battle.</summary>
    public void ShowMatchOver(MatchResult result)
    {
        if (IsMatchOver)
        {
            return;
        }

        IsMatchOver = true;

        var layer = new CanvasLayer { Name = "GameOverLayer" };
        var dim = new ColorRect { Name = "Dim", Color = new Color(0f, 0f, 0f, 0.75f) };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(dim);

        _bannerArt = GD.Load<Texture2D>(BannerArtPath);
        var banner = new TextureRect
        {
            Name = "Banner",
            Texture = _bannerArt,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore, // the buttons above it take the clicks
        };
        banner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(banner);

        _winnerName = BuildWinnerName(result);
        _viewTitle = BuildViewTitle();
        _prevView = BuildArrowButton("PrevView", () => SwitchView(-1));
        _nextView = BuildArrowButton("NextView", () => SwitchView(1));
        _leaderboardRows = new Control { Name = "LeaderboardRows", MouseFilter = Control.MouseFilterEnum.Ignore };
        _buttonBar = BuildGameOverButtons();
        layer.AddChild(_leaderboardRows);
        layer.AddChild(_winnerName);
        layer.AddChild(_viewTitle);
        layer.AddChild(_prevView);
        layer.AddChild(_nextView);
        layer.AddChild(_buttonBar);

        AddChild(layer);

        _viewIndex = 0;
        LayoutVictory();
        GetViewport().SizeChanged += LayoutVictory; // re-anchor when the window resizes
    }

    // The banner's on-screen rectangle under KeepAspectCentered — the portrait art fits the smaller
    // of the two viewport ratios and centres, so on a landscape window it fills the height with dim
    // bands at the sides. Every overlay is a fraction of this rect.
    private Rect2 BannerDrawnRect()
    {
        var viewport = GetViewport().GetVisibleRect().Size;
        var fit = Mathf.Min(viewport.X / _bannerArt.GetWidth(), viewport.Y / _bannerArt.GetHeight());
        var size = new Vector2(_bannerArt.GetWidth(), _bannerArt.GetHeight()) * fit;
        return new Rect2((viewport - size) * 0.5f, size);
    }

    private static void Place(Control control, Rect2 frac, Rect2 drawn)
    {
        control.Position = drawn.Position + (drawn.Size * frac.Position);
        control.Size = drawn.Size * frac.Size;
    }

    // Positions every victory overlay against the current drawn rect and (re)builds the leaderboard.
    // Called once at show time and again on every viewport resize.
    private void LayoutVictory()
    {
        var drawn = BannerDrawnRect();
        Place(_winnerName, RibbonFrac, drawn);
        Place(_viewTitle, ViewTitleFrac, drawn);
        Place(_prevView, PrevArrowFrac, drawn);
        Place(_nextView, NextArrowFrac, drawn);
        Place(_buttonBar, ButtonBarFrac, drawn);
        _leaderboardRows.Position = drawn.Position;
        _leaderboardRows.Size = drawn.Size;

        _winnerName.AddThemeFontSizeOverride("font_size", (int)(drawn.Size.Y * 0.036f));
        _winnerName.AddThemeConstantOverride("outline_size", Mathf.Max(1, (int)(drawn.Size.Y * 0.0045f)));
        _viewTitle.AddThemeFontSizeOverride("font_size", (int)(drawn.Size.Y * 0.026f));
        RebuildLeaderboard(drawn.Size);
    }

    // The champion's name carved into the artwork's wood ribbon (the baked "[PLACEHOLDER]" is erased
    // by the prep script). Gold with a dark outline and drop shadow so it reads on the plank. A draw
    // shows the draw line instead.
    private Label BuildWinnerName(MatchResult result)
    {
        var champion = BattleAwards.Champion(Stats.Tallies, result.WinningTeam);
        var label = new Label
        {
            Name = "WinnerName",
            Text = champion?.Name ?? TranslationServer.Translate("hud.draw"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeColorOverride("font_color", RibbonGold);
        label.AddThemeColorOverride("font_outline_color", new Color(0.18f, 0.09f, 0.02f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.5f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    // The current ranking view's name, centred over the two header pills; the baked arrows pan it.
    private static Label BuildViewTitle()
    {
        var title = new Label
        {
            Name = "ViewTitle",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        title.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.72f));
        title.AddThemeColorOverride("font_outline_color", new Color(0.10f, 0.06f, 0.02f));
        title.AddThemeConstantOverride("outline_size", 4);
        return title;
    }

    // A transparent clickable hotspot over one of the artwork's baked arrow buttons — no chrome of
    // its own, the arrow is part of the art.
    private static Button BuildArrowButton(string name, Action onPressed)
    {
        var button = new Button { Name = name, Flat = true };
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += onPressed;
        return button;
    }

    /// <summary>Pans the leaderboard <paramref name="delta"/> views along the ring (the arrows call
    /// it with ±1). Public so tests can drive the panning.</summary>
    public void SwitchView(int delta)
    {
        var count = LeaderboardViews.Length;
        _viewIndex = ((_viewIndex + delta) % count + count) % count;
        RebuildLeaderboard(BannerDrawnRect().Size);
    }

    // Builds one row per tank, each anchored onto its numbered plate (rank 1 = the top plate). With
    // fewer than eight tanks the lower plates simply show as empty art. Coordinates are relative to
    // _leaderboardRows, which spans the whole drawn rect.
    private void RebuildLeaderboard(Vector2 drawnSize)
    {
        _viewTitle.Text = LeaderboardViews[_viewIndex].TitleKey;
        var value = LeaderboardViews[_viewIndex].Value;

        foreach (var child in _leaderboardRows.GetChildren())
        {
            child.Free(); // immediate, so a re-pan in the same frame rebuilds a clean list
        }

        var awards = BattleAwards.Compute(Stats.Tallies);
        var fontSize = Mathf.Max(1, (int)(drawnSize.Y * 0.023f));
        var rank = 0;
        foreach (var tally in Stats.Tallies.OrderByDescending(value))
        {
            if (rank >= PlateCount)
            {
                break; // only the artwork's eight plates
            }

            var row = new HBoxContainer
            {
                Name = $"Row{rank + 1}",
                Position = new Vector2(
                    drawnSize.X * PlateLeftFrac,
                    drawnSize.Y * (PlateMidFrac[rank] - (PlateHeightFrac / 2f))),
                Size = new Vector2(drawnSize.X * (PlateRightFrac - PlateLeftFrac), drawnSize.Y * PlateHeightFrac),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            row.AddThemeConstantOverride("separation", (int)(drawnSize.X * 0.01f));

            var name = new Label
            {
                Text = tally.Name, // the plate carries the rank number already
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                VerticalAlignment = VerticalAlignment.Center,
            };
            name.AddThemeFontSizeOverride("font_size", fontSize);
            name.AddThemeColorOverride("font_color", PlateInk);
            row.AddChild(name);

            var tags = string.Join("  ", awards
                .Where(a => ReferenceEquals(a.Winner, tally))
                .Select(a => TranslationServer.Translate(AwardKey(a.Kind)).ToString()));
            if (tags.Length > 0)
            {
                var honours = new Label { Text = tags, VerticalAlignment = VerticalAlignment.Center };
                honours.AddThemeFontSizeOverride("font_size", (int)(fontSize * 0.8f));
                honours.AddThemeColorOverride("font_color", AwardRed);
                row.AddChild(honours);
            }

            var amount = new Label
            {
                Text = value(tally).ToString(System.Globalization.CultureInfo.InvariantCulture),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(drawnSize.X * 0.10f, 0f),
            };
            amount.AddThemeFontSizeOverride("font_size", fontSize);
            amount.AddThemeColorOverride("font_color", PlateInk);
            row.AddChild(amount);

            _leaderboardRows.AddChild(row);
            rank++;
        }
    }

    private static string AwardKey(AwardKind kind) => kind switch
    {
        AwardKind.MostDeadly => "award.most_deadly",
        AwardKind.MostEvasive => "award.most_evasive",
        AwardKind.Sharpshooter => "award.sharpshooter",
        _ => "award.bullet_sponge",
    };

    private HBoxContainer BuildGameOverButtons()
    {
        var bar = new HBoxContainer { Name = "GameOverButtons", Alignment = BoxContainer.AlignmentMode.Center };
        bar.AddThemeConstantOverride("separation", 16);

        var newGame = StyledButton("NewGame", "gameover.new_game");
        newGame.Pressed += () =>
        {
            var custom = GameSetup.CustomMap; // StartNewMatch clears it; a custom-map rematch keeps its map
            GameSetup.StartNewMatch(GameSetup.Mode);
            GameSetup.CustomMap = custom;
            GetTree().ReloadCurrentScene();
        };
        bar.AddChild(newGame);

        var menu = StyledButton("BackToMenu", "pause.main_menu");
        menu.Pressed += () => GetTree().ChangeSceneToFile("res://src/Presentation/Title.tscn");
        bar.AddChild(menu);

        var exit = StyledButton("ExitGame", "pause.exit");
        exit.Pressed += () => GetTree().Quit();
        bar.AddChild(exit);
        return bar;
    }

    // A wood-and-gold button matching the artwork: gold border, dark fill, drop shadow. Text is a
    // locale key — Godot's Control auto-translation resolves it through the TranslationServer.
    private static Button StyledButton(string name, string textKey)
    {
        var button = new Button { Name = name, Text = textKey };
        button.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.80f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 0.92f));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.30f, 0.18f, 0.08f)));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.40f, 0.25f, 0.11f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.22f, 0.13f, 0.05f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return button;
    }

    private static StyleBoxFlat ButtonStyle(Color fill) => new()
    {
        BgColor = fill,
        BorderColor = RibbonGold,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
        ShadowColor = new Color(0f, 0f, 0f, 0.5f),
        ShadowSize = 4,
        ContentMarginLeft = 18,
        ContentMarginRight = 18,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };

    private (int X, int Y) _playerSpawn;
    private IReadOnlyList<(int X, int Y)> _enemySpawns = Array.Empty<(int, int)>();
    private Camera3D _camera = null!;
    private ITank _player = null!;

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

        // Build from a user-made map when one is chosen ("My Maps"); else the hand-authored multi-level
        // Cliffs & Valleys map when it is selected (ADR-0018); otherwise generate a Desert War.
        if (GameSetup.CustomMap is { } custom)
        {
            level = MapLoader.ToLevel(custom);
            sandbags = custom.Sandbags;
            _playerSpawn = custom.PlayerSpawn;
            _enemySpawns = custom.EnemySpawns;
            _powerupPlacements = custom.PowerupSpawns.Select(s => (s.Kind, s.X, s.Y)).ToList();
            _authoredPads = custom.TeleportPads;
        }
        else if (GameSetup.Arena == ArenaId.CliffsAndValleys)
        {
            var cliffs = CliffsArena.Create();
            level = cliffs.Map;
            sandbags = cliffs.Sandbags;
            _playerSpawn = cliffs.PlayerSpawn;
            _enemySpawns = cliffs.EnemySpawns;
            _powerupPlacements = cliffs.Powerups;
            _authoredPads = cliffs.Pads; // the cross-layer valley↔plateau pair (teleport pads T3)
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
        _grid = grid;
        _arena = new GridArena(grid, TileSize, GridOrigin);
        _bushes = new BushField(level.Bushes, TileSize, GridOrigin);
        _sandbags = new SandbagField(sandbags, TileSize, GridOrigin);

        var combat = new CombatResolver(CombatHitRadius, alliedTeam: PlayerTeam);
        _world = new World(combat);
        Stats = new BattleStats(_world, combat); // before any spawn, so it sees every tank and shot
        _world.EntitySpawned += OnEntitySpawned;
        _world.EntityDespawned += OnEntityDespawned;

        BuildEnvironment();
        BuildGround(level.Width, level.Height);

        var terrain = new Terrain3DView { Name = "Terrain3DView" };
        AddChild(terrain);
        terrain.Bind(grid, level.Bushes, sandbags, TileSize);

        // A custom map's library props (owner ask 2026-06-11): cosmetic scenery, posed as authored.
        if (GameSetup.CustomMap is { } decorated)
        {
            foreach (var decoration in decorated.Decorations)
            {
                var prop = new DecorationView { Name = $"Decoration_{decoration.X}_{decoration.Y}" };
                var pose = decorated.Transforms is { } poses && poses.TryGetValue((decoration.X, decoration.Y), out var t)
                    ? t
                    : PropTransform.Identity;
                prop.Configure(decoration.AssetId, pose, TileSize);
                prop.Position = new Vector3((decoration.X + 0.5f) * TileSize, 0f, (decoration.Y + 0.5f) * TileSize);
                AddChild(prop);
            }
        }

        BuildTeleporter(level.Width, level.Height); // before SpawnTanks — the tanks consult it
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
        if (_paused || IsMatchOver)
        {
            return; // the Escape menu or the victory screen is up — freeze the match
        }

        _world.Step((float)delta);
        _teleporter.Step((float)delta); // age pad cooldowns once per frame (tanks warp inside world.Step)

        var result = _matchTracker.Evaluate(_world.Entities);
        if (result.Decided)
        {
            ShowMatchOver(result); // only the player team (or nobody) is left standing
        }
        if (_player.Hp > 0)
        {
            var target = GroundProjection.ToWorld(_player.Position);
            _camera.Position = target + CameraOffset();
            _camera.LookAt(target, Vector3.Up);
        }

        PositionFogLight();   // the lit circle rides the player (goes dark while the player is down)
        UpdateConcealment();  // hide enemies outside the player's vision; darken the player in cover
        UpdatePadViews();     // pulse ready pads, dim ones on cooldown
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

        // Fog of war bakes the world dark at creation (single-player is always fogged): the sun is dimmed
        // to a faint dusk and the ambient/sky are near-black, so only the player's fog spotlight relights a
        // circle. Built as locals — holding the Environment resource in a field leaks an unsafe reference
        // at engine shutdown (Godot .NET fatal); the WorldEnvironment node owns it and frees it cleanly.
        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-55f, -40f, 0f),
            LightEnergy = FogSunEnergy,
            ShadowEnabled = true,
        };
        AddChild(sun);

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = FogAmbient, // a dark horizon, not a bright sky
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = FogAmbient,
                AmbientLightEnergy = 1f,
                TonemapMode = Godot.Environment.ToneMapper.Aces, // compress highlights so light colours don't blow out to white
            },
        });
    }

    // Fog of war: a dark world (the sun dimmed to near-dusk, a near-black ambient and sky, baked in
    // BuildEnvironment) that the player's spotlight cuts a lit circle into. The spotlight hangs high over
    // the player and points straight down, its cone sized so the lit ground disc is about a vision-radius
    // across — so "lit" reads as "visible", matching the iso fog. Structured so co-op could add an ally
    // light later (one per viewer); single-player needs just the one. Versus would get no fog at all.
    private static readonly Color FogAmbient = new(0.10f, 0.10f, 0.14f);
    private const float FogLightHeight = 1400f;   // how high over the player the spotlight hangs
    private const float FogLightEnergy = 6f;
    private const float FogSunEnergy = 0.18f;      // the sun dimmed to a faint dusk under fog

    private void SetUpFog()
    {
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

        // A custom map brings its authored ground tileset; the built-in arenas keep the sandy look.
        var theme = GameSetup.CustomMap?.GroundTheme ?? GroundTheme.Sand;
        var ground = new MeshInstance3D
        {
            Name = "Ground",
            Mesh = new PlaneMesh { Size = new Vector2(w, h) },
            Position = new Vector3(w / 2f, 0f, h / 2f),
            MaterialOverride = GroundThemes.Material(theme, widthCells, heightCells),
        };
        AddChild(ground);

        if (theme == GroundTheme.Sand)
        {
            ScatterDirt(w, h); // the dirt patches belong to the dusty look only
        }
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
        // An authored map deals its spawn markers randomly (owner feedback): the player and every AI
        // draw a distinct cell from the combined marker pool, and each respawn re-rolls from the same
        // pool. Seeded by the arena so a best-of-N series is reproducible; the built-in arenas keep
        // their fixed spawns.
        var playerCell = _playerSpawn;
        var enemyCells = _enemySpawns;
        Func<NVector2>? respawnPoint = null;
        if (GameSetup.CustomMap is not null)
        {
            var pool = new List<(int X, int Y)> { _playerSpawn };
            pool.AddRange(_enemySpawns);
            var rng = new Random(GameSetup.ArenaSeed);
            for (var i = pool.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            playerCell = pool[0];
            enemyCells = pool.Skip(1).ToList();
            respawnPoint = () =>
            {
                var cell = pool[rng.Next(pool.Count)];
                return CellCentre(cell.X, cell.Y);
            };
        }

        var p1Input = new KeyboardMouse3DInputSource(_camera, fireOnClick: true);
        var playerName = GameSetup.PlayerName.Length > 0 ? GameSetup.PlayerName : "Player";
        var player = new Tank(p1Input, _world, _arena, CellCentre(playerCell.X, playerCell.Y),
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: PlayerTeam, lives: StartingLives,
            terrain: _sandbags, teleporter: _teleporter, displayName: playerName, respawnPoint: respawnPoint);
        _player = player;
        _viewers.Add(player); // the player's sight reveals the field; co-op allies would join this list
        SpawnTank(player);

        // Seeded by the arena so a best-of-N series keeps its cast of derpy adversaries.
        var names = new TankNameGenerator(GameSetup.ArenaSeed);
        var enemyIndex = 0;
        foreach (var (ex, ey) in enemyCells)
        {
            var ambusher = enemyIndex % 2 == 1;
            var ai = new AiInputSource(_world, _arena, _bushes, ambusher, _grid, TileSize, GridOrigin);
            var enemy = new Tank(ai, _world, _arena, CellCentre(ex, ey),
                EnemySpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: EnemyTeam, lives: StartingLives,
                terrain: _sandbags, teleporter: _teleporter, displayName: names.Next(), respawnPoint: respawnPoint);
            ai.Bind(enemy);
            SpawnTank(enemy);
            enemyIndex++;
        }

        SetUpFog(); // single-player → fog the field with a lit circle around the player
    }

    // Place one linked pair of teleport pads on the two best-separated floor cells (near opposite quarters),
    // skipping the spawn cells so no tank starts on a pad. The Teleporter is the deterministic owner; the
    // rings are pure views of its state.
    private void BuildTeleporter(int widthCells, int heightCells)
    {
        // Authored pads (a custom map's, or Cliffs' cross-layer pair) override the auto-placement.
        if (_authoredPads.Count > 0)
        {
            BuildAuthoredTeleporter();
            return;
        }

        var taken = new HashSet<(int, int)>(_enemySpawns.Select(s => (s.X, s.Y))) { (_playerSpawn.X, _playerSpawn.Y) };
        var a = ClosestFloor(widthCells / 4, heightCells / 4, taken);
        var b = ClosestFloor(widthCells - (widthCells / 4), heightCells - (heightCells / 4), taken);

        if (a is null || b is null || a.Value == b.Value)
        {
            _teleporter = new Teleporter(Array.Empty<(TeleportPad, TeleportPad)>(), TeleportPadRadius);
            return;
        }

        var padA = PadAt(a.Value.X, a.Value.Y);
        var padB = PadAt(b.Value.X, b.Value.Y);
        _teleporter = new Teleporter(new[] { (padA, padB) }, TeleportPadRadius);

        AddPadView(padA);
        AddPadView(padB);
    }

    // Build the teleporter from the authored pad pairs (cells → world centres). The rings are added in
    // the same link order the Teleporter holds, so the scene can mirror pad state to them by index.
    private void BuildAuthoredTeleporter()
    {
        var links = new List<(TeleportPad, TeleportPad)>(_authoredPads.Count);
        var pads = new List<TeleportPad>(_authoredPads.Count * 2);
        foreach (var link in _authoredPads)
        {
            var padA = PadAt(link.AX, link.AY);
            var padB = PadAt(link.BX, link.BY);
            links.Add((padA, padB));
            pads.Add(padA);
            pads.Add(padB);
        }

        _teleporter = new Teleporter(links, TeleportPadRadius);
        foreach (var pad in pads)
        {
            AddPadView(pad);
        }
    }

    // A pad sits on whatever elevation layer its cell has (teleport pads T3): the layer is derived from
    // the grid, never authored separately, so pad data stays plain cells and cannot disagree with the map.
    private TeleportPad PadAt(int x, int y) => new(CellCentre(x, y), _grid.LayerAt(x, y));

    private void AddPadView(TeleportPad pad)
    {
        var view = new TeleportPad3DView { Name = "TeleportPad" };
        view.Configure(GroundProjection.ToWorld(pad.Position, pad.Layer), TeleportPadRadius);
        AddChild(view);
        _padViews.Add(view);
    }

    // The floor cell nearest a target cell, excluding spawn cells — a guaranteed-passable spot for a pad.
    private (int X, int Y)? ClosestFloor(int targetX, int targetY, ISet<(int, int)> exclude)
    {
        (int X, int Y)? best = null;
        var bestDistance = int.MaxValue;
        for (var x = 0; x < _grid.Width; x++)
        {
            for (var y = 0; y < _grid.Height; y++)
            {
                if (_grid.IsBlocked(x, y) || exclude.Contains((x, y)))
                {
                    continue;
                }

                var dx = x - targetX;
                var dy = y - targetY;
                var distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (x, y);
                }
            }
        }

        return best;
    }

    private void UpdatePadViews()
    {
        if (_padViews.Count == 0)
        {
            return;
        }

        var statuses = _teleporter.PadStatuses();
        for (var i = 0; i < _padViews.Count && i < statuses.Count; i++)
        {
            _padViews[i].SetState(statuses[i].Ready, statuses[i].CooldownFraction);
        }
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
