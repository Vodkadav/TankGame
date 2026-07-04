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
    private const int StartingLives = 6; // 1 current life + 5 respawns (owner ask); HUD shows respawns left
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

    private const string BackdropPath = "res://src/Presentation/Arena/ui/victory_bg.png";
    private const int MaxRows = 8; // the 4v4 tank cap — up to eight ranked rows, one per tank

    // The ranking views, switched with the nav arrows (owner ask 2026-06-11/14): each is a full
    // ranking of every tank by one metric.
    // LowerIsBetter flips the ranking: deaths and damage taken rank ascending (fewer/less is better),
    // so the tank that died least or took the least damage sits at the top.
    private static readonly (string TitleKey, Func<BattleStats.TankTally, int> Value, bool LowerIsBetter)[] LeaderboardViews =
    {
        ("stats.kills", t => t.Kills, false),
        ("stats.deaths", t => t.Deaths, true),
        ("stats.dealt", t => t.DamageDealt, false),
        ("stats.taken", t => t.DamageTaken, true),
        ("stats.repairs", t => t.HealingTaken, false),
        ("stats.assists", t => t.Assists, false),
    };

    // The victory screen is composed from real UI controls over a generated celebration backdrop
    // (owner feedback 2026-06-14: the old screen floated text over a baked mock-up, which fought the
    // live content). These colours echo the backdrop so the built ribbon/plaque/plates/badges match it.
    private static readonly Color RibbonWood = new(0.45f, 0.27f, 0.12f);
    private static readonly Color Gold = new(1f, 0.82f, 0.28f);
    private static readonly Color PlaqueMetal = new(0.15f, 0.16f, 0.19f, 0.95f);
    private static readonly Color PlateGold = new(0.84f, 0.62f, 0.16f);
    private static readonly Color PlateSilver = new(0.57f, 0.60f, 0.63f);
    private static readonly Color PlateInk = new(0.13f, 0.08f, 0.02f);
    private static readonly Color AwardRed = new(0.62f, 0.10f, 0.08f);
    private static readonly Color[] BadgeColours = { new(0.18f, 0.45f, 0.86f), new(0.80f, 0.17f, 0.15f) };

    private int _viewIndex;
    private Texture2D _bgArt = null!;
    private Label _winnerName = null!;
    private Label _viewTitle = null!;
    private VBoxContainer _leaderboardRows = null!;

    /// <summary>The end of the match (owner ask 2026-06-11/14, rebuilt): freeze the world under a
    /// generated celebration backdrop and compose the victory screen from real UI controls — a wood
    /// ribbon with the champion's name and a big "IS VICTORIOUS!", a nav row whose arrows switch the
    /// ranking sheet shown on a pill, a metal plaque of up to eight numbered gold/silver rows (one per
    /// tank: badge, name, award tags, value), and styled New Game / Main Menu / Exit. Native
    /// containers centre and resize it; nothing is anchored to baked artwork. Public so a test can
    /// drive it without fighting a whole battle.</summary>
    public void ShowMatchOver(MatchResult result)
    {
        if (IsMatchOver)
        {
            return;
        }

        IsMatchOver = true;
        _sfx.PlayUi(SfxKind.Victory);

        var viewport = GetViewport().GetVisibleRect().Size;
        var cardWidth = Mathf.Min(viewport.X * 0.94f, 760f);

        var layer = new CanvasLayer { Name = "GameOverLayer" };

        _bgArt = GD.Load<Texture2D>(BackdropPath);
        var backdrop = new TextureRect
        {
            Name = "Backdrop",
            Texture = _bgArt,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(backdrop);

        var scrim = new ColorRect
        {
            Name = "Scrim",
            Color = new Color(0f, 0f, 0f, 0.30f), // darken the busy art so the plaque text reads
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        scrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(scrim);

        // A centred portrait card: the container hierarchy positions and re-centres everything, so a
        // window resize needs no manual maths (the font sizes, picked from the viewport, stay put).
        var holder = new CenterContainer { Name = "CardHolder", MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(holder);

        var card = new VBoxContainer { Name = "VictoryCard" };
        card.AddThemeConstantOverride("separation", (int)(viewport.Y * 0.012f));
        holder.AddChild(card);

        card.AddChild(BuildTitleBlock(result, viewport));
        card.AddChild(BuildNavRow(viewport));
        card.AddChild(BuildPlaque(viewport, cardWidth));
        card.AddChild(BuildGameOverButtons(viewport));

        AddChild(layer);

        _viewIndex = 0;
        RebuildLeaderboard(viewport);
    }

    // The wood ribbon carrying the winner's name, with a big gold "IS VICTORIOUS!" beneath it (hidden
    // on a draw). Real controls, so the text never collides with baked art.
    private Control BuildTitleBlock(MatchResult result, Vector2 viewport)
    {
        var champion = BattleAwards.Champion(Stats.Tallies, result.WinningTeam);

        var block = new VBoxContainer { Name = "TitleBlock", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        block.AddThemeConstantOverride("separation", (int)(viewport.Y * 0.006f));

        var ribbon = new PanelContainer { Name = "Ribbon", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        ribbon.AddThemeStyleboxOverride("panel", RibbonStyle());
        _winnerName = new Label
        {
            Name = "WinnerName",
            Text = champion?.Name ?? TranslationServer.Translate("hud.draw"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        ApplyFont(_winnerName, (int)(viewport.Y * 0.032f), Gold, outline: (int)(viewport.Y * 0.004f));
        ribbon.AddChild(_winnerName);
        block.AddChild(ribbon);

        var victorious = new Label
        {
            Name = "Victorious",
            Text = TranslationServer.Translate("hud.victorious"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = champion is not null,
        };
        ApplyFont(victorious, (int)(viewport.Y * 0.058f), new Color(1f, 0.78f, 0.16f), outline: (int)(viewport.Y * 0.006f));
        block.AddChild(victorious);
        return block;
    }

    // The ranking-sheet navigator: < [current sheet name] > — the arrows switch the sheet.
    private Control BuildNavRow(Vector2 viewport)
    {
        var nav = new HBoxContainer { Name = "NavRow", Alignment = BoxContainer.AlignmentMode.Center };
        nav.AddThemeConstantOverride("separation", (int)(viewport.X * 0.02f));
        nav.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        var prevBtn = NavButton("PrevView", "<", viewport, () => { _sfx.PlayUi(SfxKind.UiClick); SwitchView(-1); });
        prevBtn.MouseEntered += () => _sfx.PlayHover();
        nav.AddChild(prevBtn);

        var pill = new PanelContainer { Name = "ViewPill" };
        pill.AddThemeStyleboxOverride("panel", PillStyle());
        _viewTitle = new Label
        {
            Name = "ViewTitle",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(viewport.X * 0.34f, 0f), // stable width so the arrows do not shift
        };
        ApplyFont(_viewTitle, (int)(viewport.Y * 0.028f), new Color(1f, 0.96f, 0.80f), outline: (int)(viewport.Y * 0.003f));
        pill.AddChild(_viewTitle);
        nav.AddChild(pill);

        var nextBtn = NavButton("NextView", ">", viewport, () => { _sfx.PlayUi(SfxKind.UiClick); SwitchView(1); });
        nextBtn.MouseEntered += () => _sfx.PlayHover();
        nav.AddChild(nextBtn);
        return nav;
    }

    private static Button NavButton(string name, string glyph, Vector2 viewport, Action onPressed)
    {
        var button = new Button { Name = name, Text = glyph };
        button.AddThemeFontSizeOverride("font_size", (int)(viewport.Y * 0.030f));
        button.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        button.AddThemeStyleboxOverride("normal", NavStyle(new Color(0.16f, 0.40f, 0.80f)));
        button.AddThemeStyleboxOverride("hover", NavStyle(new Color(0.24f, 0.50f, 0.92f)));
        button.AddThemeStyleboxOverride("pressed", NavStyle(new Color(0.12f, 0.30f, 0.62f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += onPressed;
        return button;
    }

    // The plaque: a dark metal panel holding the ranked rows (filled by RebuildLeaderboard) inside a
    // fixed-height scroll window — about four rows show at once, and at the 4v4 cap of eight tanks the
    // rest scroll into view (owner ask 2026-06-14).
    private Control BuildPlaque(Vector2 viewport, float cardWidth)
    {
        var plaque = new PanelContainer { Name = "Plaque", CustomMinimumSize = new Vector2(cardWidth, 0f) };
        plaque.AddThemeStyleboxOverride("panel", PlaqueStyle());

        var scroll = new ScrollContainer
        {
            Name = "LeaderboardScroll",
            CustomMinimumSize = new Vector2(0f, (viewport.Y * 0.205f) + 44f), // ~four rows tall
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        _leaderboardRows = new VBoxContainer { Name = "LeaderboardRows", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _leaderboardRows.AddThemeConstantOverride("separation", (int)(viewport.Y * 0.008f));
        scroll.AddChild(_leaderboardRows);
        plaque.AddChild(scroll);
        return plaque;
    }

    private static void ApplyFont(Label label, int size, Color colour, int outline = 0, bool shadow = true)
    {
        label.AddThemeFontSizeOverride("font_size", Mathf.Max(1, size));
        label.AddThemeColorOverride("font_color", colour);
        if (outline > 0)
        {
            label.AddThemeColorOverride("font_outline_color", new Color(0.12f, 0.07f, 0.02f));
            label.AddThemeConstantOverride("outline_size", outline);
        }

        if (shadow)
        {
            label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.45f));
            label.AddThemeConstantOverride("shadow_offset_x", 2);
            label.AddThemeConstantOverride("shadow_offset_y", 2);
        }
    }

    private static StyleBoxFlat Rounded(Color fill, Color border, int borderWidth, int radius)
    {
        return new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
        };
    }

    private static StyleBoxFlat RibbonStyle()
    {
        var s = Rounded(RibbonWood, new Color(0.64f, 0.41f, 0.18f), 4, 16);
        s.ContentMarginLeft = 44;
        s.ContentMarginRight = 44;
        s.ContentMarginTop = 8;
        s.ContentMarginBottom = 10;
        s.ShadowColor = new Color(0f, 0f, 0f, 0.5f);
        s.ShadowSize = 6;
        return s;
    }

    private static StyleBoxFlat PillStyle()
    {
        var s = Rounded(new Color(0.12f, 0.30f, 0.62f), Gold, 3, 24);
        s.ContentMarginLeft = 24;
        s.ContentMarginRight = 24;
        s.ContentMarginTop = 8;
        s.ContentMarginBottom = 8;
        return s;
    }

    private static StyleBoxFlat NavStyle(Color fill)
    {
        var s = Rounded(fill, Gold, 3, 14);
        s.ContentMarginLeft = 20;
        s.ContentMarginRight = 20;
        s.ContentMarginTop = 4;
        s.ContentMarginBottom = 6;
        s.ShadowColor = new Color(0f, 0f, 0f, 0.4f);
        s.ShadowSize = 3;
        return s;
    }

    private static StyleBoxFlat PlaqueStyle()
    {
        var s = Rounded(PlaqueMetal, new Color(0.46f, 0.48f, 0.52f), 4, 16);
        s.ContentMarginLeft = 14;
        s.ContentMarginRight = 14;
        s.ContentMarginTop = 14;
        s.ContentMarginBottom = 14;
        s.ShadowColor = new Color(0f, 0f, 0f, 0.55f);
        s.ShadowSize = 8;
        return s;
    }

    /// <summary>Switches the leaderboard <paramref name="delta"/> sheets along the ring (the arrows
    /// call it with ±1). Public so tests can drive the navigation.</summary>
    public void SwitchView(int delta)
    {
        var count = LeaderboardViews.Length;
        _viewIndex = ((_viewIndex + delta) % count + count) % count;
        RebuildLeaderboard(GetViewport().GetVisibleRect().Size);
    }

    // Fills the plaque with one ranked row per tank (rank 1 at the top), up to the eight-tank cap.
    // Each row is a gold/silver plate carrying a coloured number badge, the tank name, any award tags
    // and the metric value — all real controls in an HBox, so names and numbers never clip.
    private void RebuildLeaderboard(Vector2 viewport)
    {
        var view = LeaderboardViews[_viewIndex];
        _viewTitle.Text = view.TitleKey;
        var value = view.Value;

        foreach (var child in _leaderboardRows.GetChildren())
        {
            child.Free(); // immediate, so a same-frame re-switch rebuilds a clean list
        }

        var awards = BattleAwards.Compute(Stats.Tallies);
        var rank = 0;
        foreach (var tally in LeaderboardOrder.Rank(Stats.Tallies, value, view.LowerIsBetter))
        {
            if (rank >= MaxRows)
            {
                break;
            }

            var plate = new PanelContainer { Name = $"Row{rank + 1}" };
            plate.AddThemeStyleboxOverride("panel", PlateStyle(rank % 2 == 0 ? PlateGold : PlateSilver));

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", (int)(viewport.X * 0.015f));

            var badge = new PanelContainer { Name = "Badge" };
            badge.AddThemeStyleboxOverride("panel", BadgeStyle(BadgeColours[rank % 2]));
            var badgeSize = (int)(viewport.Y * 0.044f);
            badge.CustomMinimumSize = new Vector2(badgeSize, badgeSize);
            var number = new Label
            {
                Text = (rank + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ApplyFont(number, (int)(viewport.Y * 0.026f), new Color(1f, 1f, 1f), outline: 2);
            badge.AddChild(number);
            row.AddChild(badge);

            var name = new Label
            {
                Text = tally.Name,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            ApplyFont(name, (int)(viewport.Y * 0.024f), PlateInk, shadow: false);
            row.AddChild(name);

            var tags = string.Join("  ", awards
                .Where(a => ReferenceEquals(a.Winner, tally))
                .Select(a => TranslationServer.Translate(AwardKey(a.Kind)).ToString()));
            if (tags.Length > 0)
            {
                var honours = new Label { Text = tags, VerticalAlignment = VerticalAlignment.Center };
                ApplyFont(honours, (int)(viewport.Y * 0.019f), AwardRed, shadow: false);
                row.AddChild(honours);
            }

            var amountLabel = new Label
            {
                Text = value(tally).ToString(System.Globalization.CultureInfo.InvariantCulture),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(viewport.X * 0.10f, 0f),
            };
            ApplyFont(amountLabel, (int)(viewport.Y * 0.024f), PlateInk, shadow: false);
            row.AddChild(amountLabel);

            plate.AddChild(row);
            _leaderboardRows.AddChild(plate);
            rank++;
        }
    }

    private static StyleBoxFlat PlateStyle(Color fill)
    {
        var s = Rounded(fill, new Color(0f, 0f, 0f, 0.25f), 2, 8);
        s.ContentMarginLeft = 12;
        s.ContentMarginRight = 14;
        s.ContentMarginTop = 5;
        s.ContentMarginBottom = 5;
        return s;
    }

    // A high corner radius clamps to a circle at the badge's square size.
    private static StyleBoxFlat BadgeStyle(Color fill) => Rounded(fill, new Color(1f, 1f, 1f, 0.9f), 3, 100);

    private static string AwardKey(AwardKind kind) => kind switch
    {
        AwardKind.MostDeadly => "award.most_deadly",
        AwardKind.MostEvasive => "award.most_evasive",
        AwardKind.Sharpshooter => "award.sharpshooter",
        _ => "award.bullet_sponge",
    };

    private HBoxContainer BuildGameOverButtons(Vector2 viewport)
    {
        var bar = new HBoxContainer { Name = "GameOverButtons", Alignment = BoxContainer.AlignmentMode.Center };
        bar.AddThemeConstantOverride("separation", (int)(viewport.X * 0.015f));

        var newGame = StyledButton("NewGame", "gameover.new_game", viewport);
        newGame.Pressed += () =>
        {
            _sfx.PlayUi(SfxKind.UiClick);
            var custom = GameSetup.CustomMap; // StartNewMatch clears it; a custom-map rematch keeps its map
            GameSetup.StartNewMatch(GameSetup.Mode);
            GameSetup.CustomMap = custom;
            GetTree().ReloadCurrentScene();
        };
        newGame.MouseEntered += () => _sfx.PlayHover();
        bar.AddChild(newGame);

        var menu = StyledButton("BackToMenu", "pause.main_menu", viewport);
        menu.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); GetTree().ChangeSceneToFile("res://src/Presentation/Title.tscn"); };
        menu.MouseEntered += () => _sfx.PlayHover();
        bar.AddChild(menu);

        var exit = StyledButton("ExitGame", "pause.exit", viewport);
        exit.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); PlatformExit.Run(GetTree()); };
        exit.MouseEntered += () => _sfx.PlayHover();
        bar.AddChild(exit);
        return bar;
    }

    // A wood-and-gold button matching the celebration art: gold border, dark fill, drop shadow. Text
    // is a locale key — Godot's Control auto-translation resolves it through the TranslationServer.
    private static Button StyledButton(string name, string textKey, Vector2 viewport)
    {
        var button = new Button { Name = name, Text = textKey, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        button.AddThemeFontSizeOverride("font_size", (int)(viewport.Y * 0.026f));
        button.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.80f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 0.92f));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.30f, 0.18f, 0.08f)));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.40f, 0.25f, 0.11f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.22f, 0.13f, 0.05f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return button;
    }

    private static StyleBoxFlat ButtonStyle(Color fill)
    {
        var s = Rounded(fill, Gold, 2, 8);
        s.ShadowColor = new Color(0f, 0f, 0f, 0.5f);
        s.ShadowSize = 4;
        s.ContentMarginLeft = 18;
        s.ContentMarginRight = 18;
        s.ContentMarginTop = 10;
        s.ContentMarginBottom = 12;
        return s;
    }

    private (int X, int Y) _playerSpawn;
    private IReadOnlyList<(int X, int Y)> _enemySpawns = Array.Empty<(int, int)>();
    private Camera3D _camera = null!;
    private ITank _player = null!;
    private RespawnHud _respawnHud = null!;

    // The fog spotlight rides the player and lights roughly a vision-radius circle on the ground; the
    // viewers are the tanks whose sight reveals the field (just the player today; co-op allies would join
    // this list so the team shares one lit field). A dark ambient — set when fog is on — is what the
    // lights cut a hole in.
    private SpotLight3D _fogLight = null!;
    private readonly List<ITank> _viewers = new();
    private SfxPool _sfx = null!;
    private WorldEnvironment _worldEnv = null!;
    private DirectionalLight3D _sun = null!;
    private SettingsOverlay _settingsOverlay = null!;
    private TouchControls? _touch;

    // On a touch device (the shipped Android APK) drive with the on-screen twin sticks; otherwise the
    // desktop keyboard/mouse. Headless tests report no touchscreen, so they keep the keyboard path.
    private IInputSource BuildPlayerInput()
    {
        if (!DisplayServer.IsTouchscreenAvailable())
        {
            return new KeyboardMouse3DInputSource(_camera, fireOnClick: true);
        }

        _touch = new TouchControls { Name = "TouchControls" };
        var layer = new CanvasLayer { Name = "TouchControlsLayer" };
        layer.AddChild(_touch);
        AddChild(layer);
        return new TouchInput3DSource(_camera, () => _touch.MoveOutput, () => _touch.AimOutput);
    }

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
        BuildTouchPauseButton();

        // SFX pool — must come after _grid and _world are assigned (we subscribe to their events).
        _sfx = new SfxPool { Name = "SfxPool" };
        AddChild(_sfx);
        _sfx.SetVolumeDb(GameSetup.SfxVolumeDb);
        ApplyBrightness();
        _settingsOverlay = new SettingsOverlay { Name = "SettingsOverlay" };
        AddChild(_settingsOverlay);
        GameSetup.SettingsChanged += OnSettingsChanged;
        _grid.CellChanged += OnGridCellChangedSfx;
        combat.Hit += OnCombatHitSfx; // announce the local player's kills + streaks
    }

    // Play a wall-break sound whenever a destructible cell crumbles to floor. Floor tiles never
    // receive damage, so the only way CellChanged fires with Material=Floor is a brick/crate
    // reaching HP 0.
    private void OnGridCellChangedSfx(WallCellChanged change)
    {
        if (change.Cell.Material != CellMaterial.Floor) return;
        var centre = new NVector2((change.X + 0.5f) * TileSize, (change.Y + 0.5f) * TileSize);
        if (!WithinEarshot(centre)) return; // a wall crumbling across the map is ambient noise
        _sfx.PlayAt(SfxKind.WallBreak, GroundProjection.ToWorld(centre));
    }

    // The local player's kill announcer + streak callouts (owner ask 2026-06-18). Only the player's
    // own kills are announced; the player's own death resets their streak.
    private void OnCombatHitSfx(CombatResolver.CombatHit hit)
    {
        if (!hit.Killed) return;
        if (hit.Victim == _player.Id) _streak.Reset();
        if (hit.Shooter != _player.Id) return;

        var voice = _streak.RegisterKill(_matchClock) switch
        {
            StreakTier.Double => SfxKind.StreakDouble,
            StreakTier.Triple => SfxKind.StreakTriple,
            StreakTier.Multi  => SfxKind.StreakMulti,
            _                 => SfxKind.KillEnemy,
        };
        _sfx.PlayVoice(voice);
    }

    // True when a game-coords point is close enough to the player to be worth hearing, so distant
    // tank fire and far-off pickups are culled (~7 cells). Kept relative to the player tank, not the
    // camera, so it matches what the player should care about.
    private bool WithinEarshot(NVector2 gamePos) =>
        NVector2.Distance(gamePos, _player.Position) <= EarshotRadius;

    private void OnSettingsChanged()
    {
        _sfx.SetVolumeDb(GameSetup.SfxVolumeDb);
        ApplyBrightness();
    }

    private void ApplyBrightness()
    {
        if (_worldEnv?.Environment is not { } env) return;
        var b = GameSetup.BrightnessMultiplier;
        env.AdjustmentEnabled = true;
        env.AdjustmentBrightness = b;
        if (_sun is not null)
            _sun.LightEnergy = FogSunEnergy * b;
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

    // The local player's kill-streak announcer + the monotonic match clock that feeds it (owner ask
    // 2026-06-18). Distant tank fire / far-off pickups beyond ~7 cells of the player are not heard.
    private readonly KillStreakTracker _streak = new();
    private float _matchClock;
    private const float EarshotRadius = 7f * TileSize;

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
        resume.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); TogglePause(); };
        resume.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(resume);

        var settingsBtn = new Button { Name = "Settings", Text = "title.settings" };
        settingsBtn.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); _settingsOverlay.Open(_sfx); };
        settingsBtn.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(settingsBtn);

        var mainMenu = new Button { Name = "MainMenu", Text = "pause.main_menu" };
        mainMenu.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); GetTree().ChangeSceneToFile("res://src/Presentation/Title.tscn"); };
        mainMenu.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(mainMenu);

        var exit = new Button { Name = "ExitGame", Text = "pause.exit" };
        exit.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); PlatformExit.Run(GetTree()); };
        exit.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(exit);

        _pauseLayer.AddChild(menu);
        AddChild(_pauseLayer);
    }

    // Escape opens the pause menu on desktop, but a phone has no Escape — without an on-screen button a
    // touch player can never pause, and so can never reach Settings, Main Menu, or Exit mid-match (owner
    // ask 2026-07-04). A corner button (touch devices only) opens the same menu. A high layer keeps it
    // above the twin-stick overlay; the Button consumes its own taps, so tapping it doesn't also aim.
    private void BuildTouchPauseButton()
    {
        if (!DisplayServer.IsTouchscreenAvailable())
        {
            return;
        }

        var layer = new CanvasLayer { Name = "TouchPauseLayer", Layer = 3 };
        var pause = new Button { Name = "TouchPause", Text = "pause.open" };
        pause.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        pause.Position += new Vector2(-16f, 16f);
        pause.CustomMinimumSize = new Vector2(112f, 64f); // a comfortable thumb target
        pause.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); if (!_paused) TogglePause(); };
        layer.AddChild(pause);
        AddChild(layer);
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
        _matchClock += (float)delta; // monotonic clock for the kill-streak window
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
        _respawnHud.Show(_player.LivesRemaining - 1); // dim a tank icon as each respawn is spent
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
        // circle. Stored as fields so brightness adjustments can reach them via ApplyBrightness().
        _sun = new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-55f, -40f, 0f),
            LightEnergy = FogSunEnergy,
            ShadowEnabled = true,
        };
        AddChild(_sun);

        _worldEnv = new WorldEnvironment
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
        };
        AddChild(_worldEnv);
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

        var p1Input = BuildPlayerInput();
        var playerName = GameSetup.PlayerName.Length > 0 ? GameSetup.PlayerName : "Player";
        var player = new Tank(p1Input, _world, _arena, CellCentre(playerCell.X, playerCell.Y),
            TankSpeed, FireInterval, ProjectileSpeed, maxHp: TankMaxHp, team: PlayerTeam, lives: StartingLives,
            terrain: _sandbags, teleporter: _teleporter, displayName: playerName, respawnPoint: respawnPoint);
        _player = player;
        _viewers.Add(player); // the player's sight reveals the field; co-op allies would join this list
        SpawnTank(player);

        _respawnHud = new RespawnHud { Name = "RespawnHud" };
        AddChild(_respawnHud);
        _respawnHud.Show(player.LivesRemaining - 1); // respawns in hand (one life is the tank fighting now)

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

        // SFX: fire sound when a projectile is born; a per-kind pick-up sound when it is collected.
        // Both are culled to the player's earshot so distant tanks firing across the map stay quiet.
        if (entity is IProjectile proj && WithinEarshot(proj.Position))
        {
            _sfx.PlayAt(SfxKind.Fire, GroundProjection.ToWorld(proj.Position));
        }

        if (entity is IPowerup pickup)
        {
            pickup.Collected += kind => ShowPickupFloater(pickup.Position, kind);
            pickup.Collected += kind =>
            {
                if (WithinEarshot(pickup.Position))
                    _sfx.PlayAt(PickupSfx(kind), GroundProjection.ToWorld(pickup.Position));
            };
        }
    }

    // Each powerup gets its own pickup cue (owner ask 2026-06-18); unmapped kinds fall back to the
    // generic pickup chime.
    private static SfxKind PickupSfx(PowerupKind kind) => kind switch
    {
        PowerupKind.SpeedBoost   => SfxKind.PowerupSpeed,
        PowerupKind.RapidFire    => SfxKind.PowerupRapidFire,
        PowerupKind.BouncingAmmo => SfxKind.PowerupBouncing,
        PowerupKind.SpreadAmmo   => SfxKind.PowerupSpread,
        PowerupKind.PiercingAmmo => SfxKind.PowerupPiercing,
        PowerupKind.Repair       => SfxKind.PowerupRepair,
        PowerupKind.Shield       => SfxKind.PowerupShield,
        PowerupKind.Missile      => SfxKind.PowerupMissile,
        PowerupKind.Telephone    => SfxKind.PowerupAirstrike,
        _                        => SfxKind.Pickup,
    };

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

    private Tank3DView BuildTankView(ITank tank)
    {
        var view = new Tank3DView();
        view.Bind(tank);
        view.ApplyTeamTint(tank.Team);
        view.SetSfx(_sfx); // inject pool so Tank3DView plays explosion on death
        view.IsFriendly = (tank.Team == PlayerTeam);
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
