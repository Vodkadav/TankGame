using Godot;

namespace TankGame.Presentation;

/// <summary>The start screen: a title and one button per <see cref="GameMode"/>. Pressing a
/// button records the mode in <see cref="GameSetup"/> and loads the play scene. Button labels
/// are translation keys (Godot auto-translates them).</summary>
public partial class TitleScene : Control
{
    public override void _Ready()
    {
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
        menu.AddChild(ModeButton("OnePlayer", "title.one_player", GameMode.OnePlayer));
        menu.AddChild(ModeButton("Coop", "title.two_player_coop", GameMode.TwoPlayerCoop));
        menu.AddChild(ModeButton("Versus", "title.two_player_versus", GameMode.TwoPlayerVersus));
        menu.AddChild(JoinTestButton());

        AddChild(menu);
    }

    private Button JoinTestButton()
    {
        var button = new Button { Name = "JoinTest", Text = "title.join_test" };
        button.Pressed += () =>
        {
            NetworkSession.Join(NetworkSession.TestLobbyCode);
            button.Disabled = true; // one shot — connecting; networked play scene wiring is M3-T7.
        };
        return button;
    }

    private Button ModeButton(string name, string textKey, GameMode mode)
    {
        var button = new Button { Name = name, Text = textKey };
        button.Pressed += () => Start(mode);
        return button;
    }

    private void Start(GameMode mode)
    {
        GameSetup.StartNewMatch(mode); // fresh series at 0 - 0
        GetTree().ChangeSceneToFile("res://src/Presentation/Arena/Arena.tscn");
    }
}
