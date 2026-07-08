using System;
using Godot;
using TankGame.GameLogic;
using TankGame.Infrastructure;

namespace TankGame.Presentation;

/// <summary>The start screen — a slim menu: Solo, Multiplayer (→ the lobby browser, where map
/// picking and the desktop editor now live), Settings, and Exit (labelled "Back to Arcade" on web,
/// where it returns to the arcade site instead of quitting). Solo launches the 3D arena — the game's
/// main presentation (ADR-0017). Button labels are translation keys (Godot auto-translates).</summary>
public partial class TitleScene : Control
{
    public const string ArenaScenePath = "res://src/Presentation/Arena/Arena3D.tscn";
    public const string MapSelectScenePath = "res://src/Presentation/MapSelect.tscn";
    public const string LobbyBrowserScenePath = "res://src/Presentation/LobbyBrowser.tscn";

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
        Theme = MenuStyle.Shared;

        _sfx = new SfxPool { Name = "SfxPool" };
        AddChild(_sfx);
        _sfx.SetVolumeDb(GameSetup.SfxVolumeDb);

        MenuStyle.AddBackdrop(this); // behind the menu — added before it so the menu draws on top

        var menu = new VBoxContainer { Name = "Menu" };
        menu.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        menu.GrowHorizontal = GrowDirection.Both;
        menu.GrowVertical = GrowDirection.Both;
        menu.AddThemeConstantOverride("separation", 12);

        var title = new Label
        {
            Name = "Title",
            Text = "title.heading",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 44); // the heading looms larger than the buttons
        menu.AddChild(title);

        // Every way into a game asks for the player's battle name first (owner feedback 2026-06-11);
        // the prompt pre-fills the remembered name, so a returning player just confirms.
        var solo = Button("Solo", "title.solo");
        solo.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); PromptForName(StartSolo); };
        solo.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(solo);

        var multiplayer = Button("Multiplayer", "title.multiplayer");
        multiplayer.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); PromptForName(() => Go(LobbyBrowserScenePath)); };
        multiplayer.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(multiplayer);

        var settings = Button("Settings", "title.settings");
        settings.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); _settingsOverlay.Open(_sfx); };
        settings.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(settings);

        // Same node name and behaviour everywhere (PlatformExit already quits on desktop and returns
        // to the arcade on web) — only the visible label says which one will happen.
        var exit = Button("Exit", OS.HasFeature("web") ? "title.back_to_arcade" : "title.exit");
        exit.Pressed += () => { _sfx.PlayUi(SfxKind.UiClick); PlatformExit.Run(GetTree()); };
        exit.MouseEntered += () => _sfx.PlayHover();
        menu.AddChild(exit);

        AddChild(menu);
        BuildNamePrompt();
        _settingsOverlay = new SettingsOverlay { Name = "SettingsOverlay" };
        AddChild(_settingsOverlay);
        GameSetup.SettingsChanged += () => _sfx.SetVolumeDb(GameSetup.SfxVolumeDb);
        MenuStyle.AttachHoverRecursive(this); // every button lifts a little on hover/focus
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

        // iPad Safari never raises a soft keyboard for a focused canvas LineEdit, so the in-game
        // panel is un-typeable there — the browser's native prompt is the only way to a keyboard.
        if (WebTextEntry.NeedsNativePrompt(OS.HasFeature("web"), DisplayServer.IsTouchscreenAvailable()))
        {
            var name = WebTextEntry.Prompt(Tr("title.name_prompt"), GameSetup.PlayerName);
            if (name is null)
            {
                CancelNamePrompt();
                return;
            }

            _nameEntry.Text = name;
            ConfirmName();
            return;
        }

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

    private const string MapsDir = "user://maps";
    private static readonly ArenaId[] BuiltInArenas =
    {
        ArenaId.DesertWar, ArenaId.CliffsAndValleys,
        ArenaId.Forest, ArenaId.Volcano, ArenaId.City, ArenaId.Frozen, ArenaId.Canyon,
    };
    private static readonly Random SoloMapRng = new();

    private void StartSolo()
    {
        GameSetup.StartNewMatch(GameMode.OnePlayer); // fresh series at 0 - 0
        ChooseRandomMap(); // no specific map chosen → drop into a random one of all available maps (owner ask)
        Go(ArenaScenePath);
    }

    // The player reached Solo without picking a map. Play a random one of ALL available maps — the
    // built-in arenas plus any created maps — instead of always the built-in Desert War. A created map
    // that turns out to be corrupt falls back to the built-in arena, so one bad file never blocks a game.
    private static void ChooseRandomMap()
    {
        var repo = new MapRepository(ProjectSettings.GlobalizePath(MapsDir));
        switch (SoloMapSelection.Pick(BuiltInArenas, repo.List(), SoloMapRng))
        {
            case SoloMapSelection.BuiltIn builtIn:
                GameSetup.Arena = builtIn.Arena; // CustomMap already cleared by StartNewMatch
                break;
            case SoloMapSelection.Created created:
                try
                {
                    GameSetup.CustomMap = repo.Load(created.MapId);
                }
                catch (MapFormatException)
                {
                    // corrupt map file → leave CustomMap null and fall back to the built-in arena
                }

                break;
        }
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

    private static Button Button(string name, string textKey) => new() { Name = name, Text = textKey };
}
