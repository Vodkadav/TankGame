using Godot;

namespace TankGame.Presentation;

/// <summary>The shared cartoon look for the menu/lobby screens (owner ask 2026-07-08: the flat gray
/// default Godot controls looked "100% basic"). One <see cref="Theme"/> is set on each menu scene's
/// root <see cref="Control"/> and cascades to every child — rounded sunny buttons with a chunky brown
/// border and outlined white text, opaque framed panels (which also fixes modals rendering
/// see-through over the scene behind), and outlined labels. <see cref="AttachHover"/> adds the
/// "buttons lift a little when you mouse over" bounce. Palette matches the orange-tank desert
/// backdrop so the whole screen reads as one piece.</summary>
public static class MenuStyle
{
    // Cartoon desert palette (harmonises with title_bg.png).
    private static readonly Color Ink = new(0.24f, 0.15f, 0.09f);      // borders + text outline
    private static readonly Color Cream = new(0.99f, 0.95f, 0.86f);    // panel fill
    private static readonly Color Orange = new(0.95f, 0.58f, 0.18f);   // button
    private static readonly Color OrangeHi = new(1.00f, 0.72f, 0.28f); // button hover/focus
    private static readonly Color OrangeLo = new(0.80f, 0.46f, 0.13f); // button pressed
    private static readonly Color White = new(1f, 1f, 1f);

    private static Theme? _shared;

    /// <summary>The one theme instance every menu scene sets on its root.</summary>
    public static Theme Shared => _shared ??= Build();

    private static Theme Build()
    {
        var theme = new Theme();

        // Buttons: sunny rounded pill, chunky ink border, outlined white text; hover/pressed shift.
        theme.SetStylebox("normal", "Button", Pill(Orange));
        theme.SetStylebox("hover", "Button", Pill(OrangeHi));
        theme.SetStylebox("pressed", "Button", Pill(OrangeLo));
        theme.SetStylebox("focus", "Button", Pill(OrangeHi));
        theme.SetStylebox("disabled", "Button", Pill(Orange));
        foreach (var slot in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            theme.SetColor(slot, "Button", White);
        }

        theme.SetColor("font_outline_color", "Button", Ink);
        theme.SetConstant("outline_size", "Button", 6);
        theme.SetFontSize("font_size", "Button", 22);

        // OptionButton (create panel's mode/map picks) shares the button look.
        theme.SetStylebox("normal", "OptionButton", Pill(Orange));
        theme.SetStylebox("hover", "OptionButton", Pill(OrangeHi));
        theme.SetStylebox("pressed", "OptionButton", Pill(OrangeLo));
        theme.SetStylebox("focus", "OptionButton", Pill(OrangeHi));
        theme.SetColor("font_color", "OptionButton", White);
        theme.SetColor("font_outline_color", "OptionButton", Ink);
        theme.SetConstant("outline_size", "OptionButton", 6);

        // Panels: opaque cream card with a thick ink frame — this is what stops a modal from showing
        // the scene through it, and gives the "frames" the menu was missing.
        var panel = new StyleBoxFlat { BgColor = Cream };
        panel.SetBorderWidthAll(5);
        panel.BorderColor = Ink;
        panel.SetCornerRadiusAll(18);
        panel.SetContentMarginAll(22f);
        theme.SetStylebox("panel", "PanelContainer", panel);

        // Labels: outlined so they stay legible over the busy backdrop.
        theme.SetColor("font_color", "Label", White);
        theme.SetColor("font_outline_color", "Label", Ink);
        theme.SetConstant("outline_size", "Label", 5);
        theme.SetFontSize("font_size", "Label", 20);

        // Name entry field: cream inset with an ink border.
        var field = new StyleBoxFlat { BgColor = White };
        field.SetBorderWidthAll(3);
        field.BorderColor = Ink;
        field.SetCornerRadiusAll(10);
        field.SetContentMarginAll(10f);
        theme.SetStylebox("normal", "LineEdit", field);
        theme.SetStylebox("focus", "LineEdit", field);
        theme.SetColor("font_color", "LineEdit", Ink);
        theme.SetColor("caret_color", "LineEdit", Ink);

        return theme;
    }

    private static StyleBoxFlat Pill(Color fill)
    {
        var box = new StyleBoxFlat { BgColor = fill };
        box.SetBorderWidthAll(3);
        box.BorderColor = Ink;
        box.SetCornerRadiusAll(14);
        box.ContentMarginLeft = 20f;
        box.ContentMarginRight = 20f;
        box.ContentMarginTop = 10f;
        box.ContentMarginBottom = 10f;
        return box;
    }

    /// <summary>Make a control "lift" a little on hover/focus — the shift other games do. Pointer
    /// hover covers desktop; focus covers keyboard/controller and gives touch a press response via
    /// the theme's pressed style. Scales about the control's centre, so it grows in place.</summary>
    public static void AttachHover(Control control)
    {
        control.MouseEntered += () => Lift(control, 1.06f);
        control.MouseExited += () => Lift(control, 1f);
        control.FocusEntered += () => Lift(control, 1.06f);
        control.FocusExited += () => Lift(control, 1f);
    }

    /// <summary>Attach the hover lift to every Button/OptionButton under a node — one call per scene
    /// instead of wiring each button.</summary>
    public static void AttachHoverRecursive(Node root)
    {
        if (root is Button or OptionButton)
        {
            AttachHover((Control)root);
        }

        foreach (var child in root.GetChildren())
        {
            AttachHoverRecursive(child);
        }
    }

    private const string LiftTweenMeta = "menu_lift_tween";

    private static void Lift(Control control, float to)
    {
        control.PivotOffset = control.Size / 2f;
        // Kill any in-flight lift on this control first, so a fast enter/exit doesn't run competing
        // scale tweens that fight over the final size.
        if (control.HasMeta(LiftTweenMeta) && control.GetMeta(LiftTweenMeta).As<Tween>() is { } running
            && GodotObject.IsInstanceValid(running))
        {
            running.Kill();
        }

        var tween = control.CreateTween();
        tween.TweenProperty(control, "scale", new Vector2(to, to), 0.12)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        control.SetMeta(LiftTweenMeta, tween);
    }

    private const string BackdropPath = "res://src/Presentation/Title/ui/title_bg.png";

    /// <summary>The cartoon tank-battle backdrop behind a menu, with a faint scrim so text stays
    /// legible. Shared by all three menu scenes so they look like one place. Added before the menu so
    /// the menu draws on top.</summary>
    public static void AddBackdrop(Control scene)
    {
        var tex = LoadTexture(BackdropPath);
        if (tex is null)
        {
            return; // absent art is simply skipped (dev before an import, or a stripped build)
        }

        var backdrop = new TextureRect
        {
            Name = "Backdrop",
            Texture = tex,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        scene.AddChild(backdrop);

        var scrim = new ColorRect
        {
            Name = "BackdropScrim",
            Color = new Color(0f, 0f, 0f, 0.38f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        scrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        scene.AddChild(scrim);
    }

    // Prefer the imported texture (present in the web .pck); fall back to the raw PNG for a dev
    // checkout that has not run the editor import step. The old title-only path used FileAccess only,
    // which is why the backdrop never showed in the web export — the raw PNG isn't bundled there.
    private static Texture2D? LoadTexture(string resPath)
    {
        if (ResourceLoader.Exists(resPath))
        {
            return GD.Load<Texture2D>(resPath);
        }

        return LoadPng(resPath);
    }

    private static Texture2D? LoadPng(string resPath)
    {
        using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            return null;
        }

        var img = new Image();
        return img.LoadPngFromBuffer(file.GetBuffer((long)file.GetLength())) == Error.Ok
            ? ImageTexture.CreateFromImage(img)
            : null;
    }
}
