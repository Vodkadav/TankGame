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

    public override void _Ready()
    {
        LoadRememberedName();
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
        solo.Pressed += () => PromptForName(StartSolo);
        menu.AddChild(solo);

        var team = Button("TeamVsTeam", "title.team_vs_team");
        team.Pressed += () => PromptForName(() => Go(LobbyScenePath));
        menu.AddChild(team);

        var selectMap = Button("SelectMap", "title.select_map");
        selectMap.Pressed += () => Go(MapSelectScenePath);
        menu.AddChild(selectMap);

        // Authoring is its own activity, not a step of choosing what to play — the editor gets its
        // own menu entry (owner feedback 2026-06-11).
        var editor = Button("Editor", "title.editor");
        editor.Pressed += () => Go(MapEditorScene.MapEditorScenePath);
        menu.AddChild(editor);

        var exit = Button("Exit", "title.exit");
        exit.Pressed += () => GetTree().Quit();
        menu.AddChild(exit);

        AddChild(menu);
        BuildNamePrompt();
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

        var ok = Button("NameOk", "title.name_ok");
        ok.Pressed += ConfirmName;
        box.AddChild(ok);
        _nameEntry.TextSubmitted += _ => ConfirmName(); // Enter confirms too

        _namePrompt.AddChild(box);
        AddChild(_namePrompt);
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

    private static Button Button(string name, string textKey) => new() { Name = name, Text = textKey };
}
