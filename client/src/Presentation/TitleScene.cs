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

        var solo = Button("Solo", "title.solo");
        solo.Pressed += () => StartSolo();
        menu.AddChild(solo);

        var team = Button("TeamVsTeam", "title.team_vs_team");
        team.Pressed += () => Go(LobbyScenePath);
        menu.AddChild(team);

        var selectMap = Button("SelectMap", "title.select_map");
        selectMap.Pressed += () => Go(MapSelectScenePath);
        menu.AddChild(selectMap);

        var exit = Button("Exit", "title.exit");
        exit.Pressed += () => GetTree().Quit();
        menu.AddChild(exit);

        AddChild(menu);
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
