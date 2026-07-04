using Godot;

namespace TankGame.Presentation;

/// <summary>A full-screen settings overlay that can be opened from any scene. Displays controls
/// for SFX volume, brightness, and name-tag visibility. Call <see cref="Open"/> to show it,
/// passing the scene's <see cref="SfxPool"/> so volume changes are heard live.</summary>
public partial class SettingsOverlay : Node
{
    private CanvasLayer _layer = null!;
    private SfxPool? _sfx;

    /// <summary>Open the settings panel. Pass the scene's SFX pool so volume can be previewed
    /// live (null is safe — volume preview is skipped).</summary>
    public void Open(SfxPool? sfx = null)
    {
        _sfx = sfx;
        if (_layer is null)
        {
            Build();
        }

        _layer.Visible = true;
    }

    public void Close() => _layer.Visible = false;

    public bool IsOpen => _layer?.Visible ?? false;

    // Escape closes the panel from anywhere, and is consumed so it does not also toggle the arena's
    // pause menu underneath. Without this the panel could only be dismissed via the small ✕, which
    // the owner found flaky.
    public override void _Input(InputEvent @event)
    {
        if (!IsOpen) return;
        if (@event.IsActionPressed("ui_cancel"))
        {
            GameSetup.ApplySettings();
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Build()
    {
        _layer = new CanvasLayer { Name = "SettingsLayer", Visible = false };
        AddChild(_layer);

        // Dark scrim
        var scrim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.65f) };
        scrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        scrim.MouseFilter = Control.MouseFilterEnum.Stop; // block clicks through
        // Clicking the dark area outside the panel dismisses the overlay (the panel itself absorbs
        // its own clicks, so only off-panel clicks reach the scrim).
        scrim.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true })
            {
                GameSetup.ApplySettings();
                Close();
            }
        };
        _layer.AddChild(scrim);

        // Centred panel
        var holder = new CenterContainer();
        holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        holder.MouseFilter = Control.MouseFilterEnum.Ignore;
        _layer.AddChild(holder);

        var panel = new PanelContainer { Name = "SettingsPanel" };
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        holder.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        vbox.CustomMinimumSize = new Vector2(380f, 0f);
        panel.AddChild(vbox);

        // Header row
        var header = new HBoxContainer();
        var title = new Label { Text = "settings.heading", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        ApplyFont(title, 22, new Color(1f, 0.90f, 0.65f));
        header.AddChild(title);
        var closeBtn = CloseButton();
        closeBtn.Pressed += Close;
        header.AddChild(closeBtn);
        vbox.AddChild(header);

        // Separator
        vbox.AddChild(new HSeparator());

        // SFX Volume
        vbox.AddChild(RowLabel("settings.sfx_volume"));
        var volRow = new HBoxContainer();
        var volLabel = new Label { CustomMinimumSize = new Vector2(52f, 0f), HorizontalAlignment = HorizontalAlignment.Right };
        UpdateDbLabel(volLabel, GameSetup.SfxVolumeDb);
        var volSlider = Slider(-30f, 0f, 1f, GameSetup.SfxVolumeDb);
        volSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        volSlider.ValueChanged += v =>
        {
            GameSetup.SfxVolumeDb = (float)v;
            UpdateDbLabel(volLabel, (float)v);
            _sfx?.SetVolumeDb((float)v);
        };
        volRow.AddChild(volSlider);
        volRow.AddChild(volLabel);
        vbox.AddChild(volRow);

        // Brightness
        vbox.AddChild(RowLabel("settings.brightness"));
        var brightRow = new HBoxContainer();
        var brightLabel = new Label { CustomMinimumSize = new Vector2(52f, 0f), HorizontalAlignment = HorizontalAlignment.Right };
        brightLabel.Text = SettingsFormat.Brightness(GameSetup.BrightnessMultiplier);
        var brightSlider = Slider(0.5f, 2.0f, 0.05f, GameSetup.BrightnessMultiplier);
        brightSlider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        brightSlider.ValueChanged += v =>
        {
            GameSetup.BrightnessMultiplier = (float)v;
            brightLabel.Text = SettingsFormat.Brightness((float)v);
            GameSetup.ApplySettings(); // live brightness update via SettingsChanged event
        };
        brightRow.AddChild(brightSlider);
        brightRow.AddChild(brightLabel);
        vbox.AddChild(brightRow);

        // Friendly names toggle
        vbox.AddChild(RowLabel("settings.show_friendly_names"));
        var friendlyToggle = Toggle(GameSetup.ShowFriendlyNames);
        friendlyToggle.Toggled += on =>
        {
            GameSetup.ShowFriendlyNames = on;
            GameSetup.ApplySettings();
        };
        vbox.AddChild(friendlyToggle);

        // Enemy names toggle
        vbox.AddChild(RowLabel("settings.show_enemy_names"));
        var enemyToggle = Toggle(GameSetup.ShowEnemyNames);
        enemyToggle.Toggled += on =>
        {
            GameSetup.ShowEnemyNames = on;
            GameSetup.ApplySettings();
        };
        vbox.AddChild(enemyToggle);

        // Bottom separator + close
        vbox.AddChild(new HSeparator());
        var saveBtn = new Button { Text = "settings.close" };
        saveBtn.AddThemeFontSizeOverride("font_size", 16);
        saveBtn.Pressed += () =>
        {
            GameSetup.ApplySettings();
            Close();
        };
        vbox.AddChild(saveBtn);
    }

    private static void UpdateDbLabel(Label label, float db) =>
        label.Text = db <= -29f ? TranslationServer.Translate("settings.mute") : SettingsFormat.Db((int)db);

    private static HSlider Slider(float min, float max, float step, float value)
    {
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value };
        s.CustomMinimumSize = new Vector2(220f, 0f);
        return s;
    }

    private static CheckButton Toggle(bool value) =>
        new CheckButton { ButtonPressed = value };

    private static Label RowLabel(string key)
    {
        var l = new Label { Text = key };
        l.AddThemeFontSizeOverride("font_size", 15);
        l.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        return l;
    }

    private static Button CloseButton()
    {
        var b = new Button { Text = "✕", Flat = true };
        b.AddThemeFontSizeOverride("font_size", 20);
        b.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        return b;
    }

    private static void ApplyFont(Label label, int size, Color colour)
    {
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);
    }

    private static StyleBoxFlat PanelStyle()
    {
        var s = new StyleBoxFlat
        {
            BgColor = new Color(0.13f, 0.13f, 0.18f, 0.97f),
            BorderColor = new Color(0.40f, 0.25f, 0.10f),
            BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        };
        s.ContentMarginLeft = s.ContentMarginRight = s.ContentMarginTop = s.ContentMarginBottom = 24f;
        return s;
    }
}
