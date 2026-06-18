using Godot;

namespace TankGame.Presentation;

/// <summary>The start screen. Offers the two play modes (Solo, and Team vs Team via the networked
/// lobby — ADR-0019 step 2), a Select Map browser, and Exit. Solo launches the 3D arena — the game's
/// main presentation (ADR-0017). Button labels are translation keys (Godot auto-translates).</summary>
public partial class TitleScene : Control
{
    public const string ArenaScenePath = "res://src/Presentation/Arena/Arena3D.tscn";
    public const string MapSelectScenePath = "res://src/Presentation/MapSelect.tscn";
    public const string LobbyScenePath = "res://src/Presentation/Lobby.tscn";

    private const string SettingsPath = "user://settings.cfg";

    private Control _namePrompt = null!;
    private LineEdit _nameEntry = null!;
    private System.Action? _afterNamePrompt;
    private SfxPool _sfx = null!;
    private SettingsOverlay _settingsOverlay = null!;

    public override void _Ready()
    {
        LoadRememberedName();
        GameSetup.LoadSettings();

        _sfx = new SfxPool { Name = "SfxPool" };
        AddChild(_sfx);
        _sfx.SetVolumeDb(GameSetup.SfxVolumeDb);

        BuildBackdrop(); // behind the menu — added before it so the menu draws on top

        var menu = new VBoxContainer { Name = "Menu" };
        menu.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        menu.GrowHorizontal = GrowDirection.Both;
        menu.GrowVertical = GrowDirection.Both;
        menu.AddThemeConstantOverride("separation", 12);

        menu.AddChild(new Label
        {
            Name = "Title",
            Text = "title.heading",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // Every way into a game asks for the player's battle name first (owner feedback 2026-06-11);
        // the prompt pre-fills the remembered name, so a returning player just confirms.
        var solo = Button("Solo", "title.solo");
        solo.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); PromptForName(StartSolo); };
        solo.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(solo);

        var team = Button("TeamVsTeam", "title.team_vs_team");
        team.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); PromptForName(() => Go(LobbyScenePath)); };
        team.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(team);

        var selectMap = Button("SelectMap", "title.select_map");
        selectMap.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); Go(MapSelectScenePath); };
        selectMap.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(selectMap);

        // Authoring is its own activity, not a step of choosing what to play — the editor gets its
        // own menu entry (owner feedback 2026-06-11).
        var editor = Button("Editor", "title.editor");
        editor.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); Go(MapEditorScene.MapEditorScenePath); };
        editor.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(editor);

        var settings = Button("Settings", "title.settings");
        settings.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); _settingsOverlay.Open(_sfx); };
        settings.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(settings);

        var exit = Button("Exit", "title.exit");
        exit.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); GetTree().Quit(); };
        exit.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(exit);

        AddChild(menu);
        BuildNamePrompt();
        _settingsOverlay = new SettingsOverlay { Name = "SettingsOverlay" };
        AddChild(_settingsOverlay);
        GameSetup.SettingsChanged += () => _sfx.SetVolumeDb(GameSetup.SfxVolumeDb);
    }

    private void BuildNamePrompt()
    {
        _namePrompt = new PanelContainer { Name = "NamePrompt", Visible = false };
        _namePrompt.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        _namePrompt.GrowHorizontal = GrowDirection.Both;
        _namePrompt.GrowVertical = GrowDirection.Both;

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        box.AddChild(new Label
        {
            Text = "title.name_prompt",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        _nameEntry = new LineEdit
        {
            Name = "NameEntry",
            Alignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(260f, 0f),
        };
        box.AddChild(_nameEntry);

        // Cancel + OK side by side so the player can always back out to the menu (owner feedback
        // 2026-06-18: clicking Solo trapped you in the name prompt with no way back). Escape also
        // cancels — see _UnhandledInput.
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);

        var cancel = Button("NameCancel", "map.back");
        cancel.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); CancelNamePrompt(); };
        cancel.MouseEntered += () => _sfx.PlayHover();
        buttons.AddChild(cancel);

        var ok = Button("NameOk", "title.name_ok");
        ok.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); ConfirmName(); };
        ok.MouseEntered += () => _sfx.PlayHover();
        buttons.AddChild(ok);
        box.AddChild(buttons);
        _nameEntry.TextSubmitted += _ => ConfirmName(); // Enter confirms too

        _namePrompt.AddChild(box);
        AddChild(_namePrompt);
    }

    // Escape backs out of the name prompt; consumed so it does not propagate further.
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_namePrompt is { Visible: true } && @event.IsActionPressed("ui_cancel"))
        {
            CancelNamePrompt();
            GetViewport().SetInputAsHandled();
        }
    }

    private void CancelNamePrompt()
    {
        _afterNamePrompt = null;
        _namePrompt.Visible = false;
    }

    private void PromptForName(System.Action proceed)
    {
        _afterNamePrompt = proceed;
        _nameEntry.Text = GameSetup.PlayerName;
        _namePrompt.Visible = true;
        _nameEntry.GrabFocus();
    }

    private void ConfirmName()
    {
        var name = _nameEntry.Text.Trim();
        GameSetup.PlayerName = name.Length > 0 ? name : "Player";
        SaveRememberedName();
        _namePrompt.Visible = false;
        var proceed = _afterNamePrompt;
        _afterNamePrompt = null;
        proceed?.Invoke();
    }

    private static void LoadRememberedName()
    {
        if (GameSetup.PlayerName.Length > 0)
        {
            return; // already chosen this session
        }

        var config = new ConfigFile();
        if (config.Load(SettingsPath) == Error.Ok)
        {
            GameSetup.PlayerName = (string)config.GetValue("player", "name", "");
        }
    }

    private static void SaveRememberedName()
    {
        var config = new ConfigFile();
        config.Load(SettingsPath); // keep any other settings; a missing file is fine
        config.SetValue("player", "name", GameSetup.PlayerName);
        config.Save(SettingsPath);
    }

    private void StartSolo()
    {
        GameSetup.StartNewMatch(GameMode.OnePlayer); // fresh series at 0 - 0
        Go(ArenaScenePath);
    }

    // Guarded so the GoDotTest click-path (which adds the title as a child, not the active scene) can
    // assert the wiring without the runner swapping its whole scene out from under it.
    private void Go(string scenePath)
    {
        if (GetTree().CurrentScene == this)
        {
            GetTree().ChangeSceneToFile(scenePath);
        }
    }

    private const string BackdropPath = "res://src/Presentation/Title/ui/title_bg.png";

    // A cool cartoon tank-battle backdrop behind the menu (owner ask 2026-06-18). Loaded from the raw
    // PNG at runtime — like SfxPool's audio — so it works straight after a git pull with no editor
    // import step; absent art is simply skipped. A faint scrim keeps the menu text legible over it.
    private void BuildBackdrop()
    {
        var tex = LoadPng(BackdropPath);
        if (tex is null)
        {
            return;
        }

        var backdrop = new TextureRect
        {
            Name = "Backdrop",
            Texture = tex,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var scrim = new ColorRect
        {
            Name = "BackdropScrim",
            Color = new Color(0f, 0f, 0f, 0.35f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        scrim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scrim);
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

    private static Button Button(string name, string textKey) => new() { Name = name, Text = textKey };
}
