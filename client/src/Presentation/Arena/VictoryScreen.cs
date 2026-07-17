using System;
using System.Collections.Generic;
using Godot;

namespace TankGame.Presentation;

/// <summary>The victory screen v2 (owner ask 2026-06-11/14), extracted so the solo arena and the
/// networked match share one screen: real UI controls composed over the generated celebration
/// backdrop — a wood ribbon with the champion's name and a big "IS VICTORIOUS!", a nav row whose
/// arrows switch the ranking sheet shown on a pill, a metal plaque of up to eight numbered
/// gold/silver rows, and a styled button bar. The caller supplies the data: the champion's name
/// (null on a draw), the pre-ranked sheets, and the buttons with their actions — the screen owns
/// only presentation. Native containers centre and resize everything; nothing anchors to baked
/// artwork.</summary>
public partial class VictoryScreen : CanvasLayer
{
    /// <summary>One ranked plate: the tank's name, the metric value already formatted, and any
    /// award tags ("" for none).</summary>
    public sealed record Row(string Name, string Value, string Tags = "");

    /// <summary>One ranking sheet: its pill title (a locale key) and its rows, best first.</summary>
    public sealed record Sheet(string TitleKey, IReadOnlyList<Row> Rows);

    /// <summary>A bottom-bar button: node name, label locale key, and the press action.</summary>
    public sealed record ButtonSpec(string Name, string TextKey, Action Pressed);

    private const string BackdropPath = "res://src/Presentation/Arena/ui/victory_bg.png";
    private const int MaxRows = 8; // the 4v4 tank cap — up to eight ranked rows, one per tank

    // These colours echo the generated backdrop so the built ribbon/plaque/plates/badges match it.
    private static readonly Color RibbonWood = new(0.45f, 0.27f, 0.12f);
    private static readonly Color Gold = new(1f, 0.82f, 0.28f);
    private static readonly Color PlaqueMetal = new(0.15f, 0.16f, 0.19f, 0.95f);
    private static readonly Color PlateGold = new(0.84f, 0.62f, 0.16f);
    private static readonly Color PlateSilver = new(0.57f, 0.60f, 0.63f);
    private static readonly Color PlateInk = new(0.13f, 0.08f, 0.02f);
    private static readonly Color AwardRed = new(0.62f, 0.10f, 0.08f);
    private static readonly Color[] BadgeColours = { new(0.18f, 0.45f, 0.86f), new(0.80f, 0.17f, 0.15f) };

    private IReadOnlyList<Sheet> _sheets = Array.Empty<Sheet>();
    private Action? _onClick;
    private Action? _onHover;
    private int _viewIndex;
    private Vector2 _viewport;
    private Texture2D _bgArt = null!;
    private Label _viewTitle = null!;
    private VBoxContainer _leaderboardRows = null!;

    /// <summary>Builds the whole screen. <paramref name="championName"/> null = a draw (the ribbon
    /// says so and the "IS VICTORIOUS!" line hides). <paramref name="onClick"/>/<paramref name="onHover"/>
    /// are optional UI-sound hooks — a scene without a sound pool passes null.</summary>
    public static VictoryScreen Build(
        Vector2 viewport,
        string? championName,
        IReadOnlyList<Sheet> sheets,
        IReadOnlyList<ButtonSpec> buttons,
        Action? onClick = null,
        Action? onHover = null)
    {
        var screen = new VictoryScreen
        {
            Name = "GameOverLayer",
            _sheets = sheets,
            _onClick = onClick,
            _onHover = onHover,
            _viewport = viewport,
        };

        screen._bgArt = GD.Load<Texture2D>(BackdropPath);
        var backdrop = new TextureRect
        {
            Name = "Backdrop",
            Texture = screen._bgArt,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(backdrop);

        var scrim = new ColorRect
        {
            Name = "Scrim",
            Color = new Color(0f, 0f, 0f, 0.30f), // darken the busy art so the plaque text reads
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        scrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(scrim);

        // A centred portrait card: the container hierarchy positions and re-centres everything, so a
        // window resize needs no manual maths (the font sizes, picked from the viewport, stay put).
        var holder = new CenterContainer { Name = "CardHolder", MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.AddChild(holder);

        var card = new VBoxContainer { Name = "VictoryCard" };
        card.AddThemeConstantOverride("separation", (int)(viewport.Y * 0.012f));
        holder.AddChild(card);

        var cardWidth = Mathf.Min(viewport.X * 0.94f, 760f);
        card.AddChild(screen.BuildTitleBlock(championName));
        card.AddChild(screen.BuildNavRow());
        card.AddChild(screen.BuildPlaque(cardWidth));
        card.AddChild(screen.BuildButtons(buttons));

        screen._viewIndex = 0;
        screen.RebuildLeaderboard();
        return screen;
    }

    // The wood ribbon carrying the winner's name, with a big gold "IS VICTORIOUS!" beneath it (hidden
    // on a draw). Real controls, so the text never collides with baked art.
    private Control BuildTitleBlock(string? championName)
    {
        var block = new VBoxContainer { Name = "TitleBlock", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        block.AddThemeConstantOverride("separation", (int)(_viewport.Y * 0.006f));

        var ribbon = new PanelContainer { Name = "Ribbon", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        ribbon.AddThemeStyleboxOverride("panel", RibbonStyle());
        var winnerName = new Label
        {
            Name = "WinnerName",
            Text = championName ?? TranslationServer.Translate("hud.draw"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        ApplyFont(winnerName, (int)(_viewport.Y * 0.032f), Gold, outline: (int)(_viewport.Y * 0.004f));
        ribbon.AddChild(winnerName);
        block.AddChild(ribbon);

        var victorious = new Label
        {
            Name = "Victorious",
            Text = TranslationServer.Translate("hud.victorious"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = championName is not null,
        };
        ApplyFont(victorious, (int)(_viewport.Y * 0.058f), new Color(1f, 0.78f, 0.16f), outline: (int)(_viewport.Y * 0.006f));
        block.AddChild(victorious);
        return block;
    }

    // The ranking-sheet navigator: < [current sheet name] > — the arrows switch the sheet.
    private Control BuildNavRow()
    {
        var nav = new HBoxContainer { Name = "NavRow", Alignment = BoxContainer.AlignmentMode.Center };
        nav.AddThemeConstantOverride("separation", (int)(_viewport.X * 0.02f));
        nav.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        nav.AddChild(NavButton("PrevView", "<", () => { _onClick?.Invoke(); SwitchSheet(-1); }));

        var pill = new PanelContainer { Name = "ViewPill" };
        pill.AddThemeStyleboxOverride("panel", PillStyle());
        _viewTitle = new Label
        {
            Name = "ViewTitle",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(_viewport.X * 0.34f, 0f), // stable width so the arrows do not shift
        };
        ApplyFont(_viewTitle, (int)(_viewport.Y * 0.028f), new Color(1f, 0.96f, 0.80f), outline: (int)(_viewport.Y * 0.003f));
        pill.AddChild(_viewTitle);
        nav.AddChild(pill);

        nav.AddChild(NavButton("NextView", ">", () => { _onClick?.Invoke(); SwitchSheet(1); }));
        return nav;
    }

    private Button NavButton(string name, string glyph, Action onPressed)
    {
        var button = new Button { Name = name, Text = glyph };
        button.AddThemeFontSizeOverride("font_size", (int)(_viewport.Y * 0.030f));
        button.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        button.AddThemeStyleboxOverride("normal", NavStyle(new Color(0.16f, 0.40f, 0.80f)));
        button.AddThemeStyleboxOverride("hover", NavStyle(new Color(0.24f, 0.50f, 0.92f)));
        button.AddThemeStyleboxOverride("pressed", NavStyle(new Color(0.12f, 0.30f, 0.62f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Pressed += onPressed;
        if (_onHover is { } hover)
        {
            button.MouseEntered += () => hover();
        }

        return button;
    }

    // The plaque: a dark metal panel holding the ranked rows (filled by RebuildLeaderboard) inside a
    // fixed-height scroll window — about four rows show at once, and at the 4v4 cap of eight tanks the
    // rest scroll into view (owner ask 2026-06-14).
    private Control BuildPlaque(float cardWidth)
    {
        var plaque = new PanelContainer { Name = "Plaque", CustomMinimumSize = new Vector2(cardWidth, 0f) };
        plaque.AddThemeStyleboxOverride("panel", PlaqueStyle());

        var scroll = new ScrollContainer
        {
            Name = "LeaderboardScroll",
            CustomMinimumSize = new Vector2(0f, (_viewport.Y * 0.205f) + 44f), // ~four rows tall
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        _leaderboardRows = new VBoxContainer { Name = "LeaderboardRows", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _leaderboardRows.AddThemeConstantOverride("separation", (int)(_viewport.Y * 0.008f));
        scroll.AddChild(_leaderboardRows);
        plaque.AddChild(scroll);
        return plaque;
    }

    /// <summary>Switches the leaderboard <paramref name="delta"/> sheets along the ring (the arrows
    /// call it with ±1). Public so tests and the owning scene can drive the navigation.</summary>
    public void SwitchSheet(int delta)
    {
        var count = _sheets.Count;
        if (count == 0)
        {
            return;
        }

        _viewIndex = ((_viewIndex + delta) % count + count) % count;
        RebuildLeaderboard();
    }

    // Fills the plaque with one ranked row per tank (rank 1 at the top), up to the eight-tank cap.
    // Each row is a gold/silver plate carrying a coloured number badge, the tank name, any award tags
    // and the value — all real controls in an HBox, so names and numbers never clip.
    private void RebuildLeaderboard()
    {
        var sheet = _sheets[_viewIndex];
        _viewTitle.Text = sheet.TitleKey;

        foreach (var child in _leaderboardRows.GetChildren())
        {
            child.Free(); // immediate, so a same-frame re-switch rebuilds a clean list
        }

        var rank = 0;
        foreach (var entry in sheet.Rows)
        {
            if (rank >= MaxRows)
            {
                break;
            }

            var plate = new PanelContainer { Name = $"Row{rank + 1}" };
            plate.AddThemeStyleboxOverride("panel", PlateStyle(rank % 2 == 0 ? PlateGold : PlateSilver));

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", (int)(_viewport.X * 0.015f));

            var badge = new PanelContainer { Name = "Badge" };
            badge.AddThemeStyleboxOverride("panel", BadgeStyle(BadgeColours[rank % 2]));
            var badgeSize = (int)(_viewport.Y * 0.044f);
            badge.CustomMinimumSize = new Vector2(badgeSize, badgeSize);
            var number = new Label
            {
                Text = (rank + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ApplyFont(number, (int)(_viewport.Y * 0.026f), new Color(1f, 1f, 1f), outline: 2);
            badge.AddChild(number);
            row.AddChild(badge);

            var name = new Label
            {
                Text = entry.Name,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            ApplyFont(name, (int)(_viewport.Y * 0.024f), PlateInk, shadow: false);
            row.AddChild(name);

            if (entry.Tags.Length > 0)
            {
                var honours = new Label { Text = entry.Tags, VerticalAlignment = VerticalAlignment.Center };
                ApplyFont(honours, (int)(_viewport.Y * 0.019f), AwardRed, shadow: false);
                row.AddChild(honours);
            }

            var amountLabel = new Label
            {
                Text = entry.Value,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(_viewport.X * 0.10f, 0f),
            };
            ApplyFont(amountLabel, (int)(_viewport.Y * 0.024f), PlateInk, shadow: false);
            row.AddChild(amountLabel);

            plate.AddChild(row);
            _leaderboardRows.AddChild(plate);
            rank++;
        }
    }

    private HBoxContainer BuildButtons(IReadOnlyList<ButtonSpec> buttons)
    {
        var bar = new HBoxContainer { Name = "GameOverButtons", Alignment = BoxContainer.AlignmentMode.Center };
        bar.AddThemeConstantOverride("separation", (int)(_viewport.X * 0.015f));

        foreach (var spec in buttons)
        {
            var button = StyledButton(spec.Name, spec.TextKey);
            var pressed = spec.Pressed;
            button.Pressed += () => { _onClick?.Invoke(); pressed(); };
            if (_onHover is { } hover)
            {
                button.MouseEntered += () => hover();
            }

            bar.AddChild(button);
        }

        return bar;
    }

    // A wood-and-gold button matching the celebration art: gold border, dark fill, drop shadow. Text
    // is a locale key — Godot's Control auto-translation resolves it through the TranslationServer.
    private Button StyledButton(string name, string textKey)
    {
        var button = new Button { Name = name, Text = textKey, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        button.AddThemeFontSizeOverride("font_size", (int)(_viewport.Y * 0.026f));
        button.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.80f));
        button.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 0.92f));
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.30f, 0.18f, 0.08f)));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.40f, 0.25f, 0.11f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.22f, 0.13f, 0.05f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return button;
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
}
