using Godot;
using TankGame.GameLogic;
using TankGame.Infrastructure;

namespace TankGame.Presentation;

/// <summary>The Select Map browser: a list of battle arenas on the left and a preview image plus the name
/// of the highlighted arena on the right, so a player can browse the map pool before playing (and so the
/// developer can test each arena quickly). Selecting an available arena and pressing Play launches it on
/// the 3D scene; an unavailable arena (the upcoming Cliffs &amp; Valleys, ADR-0018) shows "coming soon"
/// and cannot be played. Labels are translation keys (Godot auto-translates).</summary>
public partial class MapSelectScene : Control
{
    public const string TitleScenePath = "res://src/Presentation/Title.tscn";
    private const string PreviewDir = "res://src/Presentation/Arena/previews/";
    private const string MapsDir = "user://maps";

    private MapRepository _maps = null!;

    private readonly record struct ArenaEntry(ArenaId Id, string Node, string NameKey, string Preview, bool Available);

    private static readonly ArenaEntry[] Arenas =
    {
        new(ArenaId.DesertWar, "DesertWar", "map.desert_war", PreviewDir + "DesertWar.png", true),
        new(ArenaId.CliffsAndValleys, "CliffsAndValleys", "map.cliffs_and_valleys", PreviewDir + "CliffsAndValleys.png", true),
    };

    private TextureRect _preview = null!;
    private Label _name = null!;
    private Label _comingSoon = null!;
    private Button _play = null!;
    private ArenaEntry _selected;

    public override void _Ready()
    {
        var root = new HBoxContainer { Name = "Root" };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        root.GrowHorizontal = GrowDirection.Both;
        root.GrowVertical = GrowDirection.Both;
        root.AddThemeConstantOverride("separation", 28);
        AddChild(root);

        var left = new VBoxContainer { Name = "List", CustomMinimumSize = new Vector2(240f, 0f) };
        left.AddThemeConstantOverride("separation", 10);
        left.AddChild(new Label { Name = "Heading", Text = "map.heading" });
        foreach (var arena in Arenas)
        {
            var entry = arena;
            var button = new Button { Name = entry.Node, Text = entry.NameKey };
            button.Pressed += () => Select(entry);
            left.AddChild(button);
        }

        BuildMyMaps(left);

        var back = new Button { Name = "Back", Text = "map.back" };
        back.Pressed += () => Go(TitleScenePath);
        left.AddChild(back);
        root.AddChild(left);

        var detail = new VBoxContainer { Name = "Detail" };
        detail.AddThemeConstantOverride("separation", 8);
        _preview = new TextureRect
        {
            Name = "Preview",
            CustomMinimumSize = new Vector2(360f, 270f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
        };
        detail.AddChild(_preview);
        _name = new Label { Name = "SelectedName" };
        detail.AddChild(_name);
        _comingSoon = new Label { Name = "ComingSoon", Text = "map.coming_soon", Visible = false };
        detail.AddChild(_comingSoon);
        _play = new Button { Name = "Play", Text = "map.play" };
        _play.Pressed += () => Play();
        detail.AddChild(_play);
        root.AddChild(detail);

        Select(Arenas[0]); // Desert War highlighted by default
    }

    // Lists the player's saved maps under their own heading, seeding a bundled sample so there is always
    // one to play (and so the save/list/load round trip is exercised). Each map gets a Play button.
    private void BuildMyMaps(VBoxContainer left)
    {
        _maps = new MapRepository(ProjectSettings.GlobalizePath(MapsDir));
        _maps.Save(SampleArena.Build()); // keep the bundled sample present (overwrites its own slot)

        left.AddChild(new Label { Name = "MyMapsHeading", Text = "map.my_maps" });

        foreach (var stored in _maps.List())
        {
            var id = stored.Id;
            var button = new Button { Name = "Map_" + id, Text = stored.Name };
            button.Pressed += () => PlayCustom(id);
            left.AddChild(button);
        }
    }

    private void PlayCustom(string id)
    {
        MapDefinition map;
        try
        {
            map = _maps.Load(id);
        }
        catch (MapFormatException)
        {
            return; // a corrupt file: ignore the click rather than crash
        }

        GameSetup.StartNewMatch(GameMode.OnePlayer);
        GameSetup.CustomMap = map; // after StartNewMatch, which clears it for built-in launches
        Go(TitleScene.ArenaScenePath);
    }

    // Highlights an arena: shows its preview and name, and only enables Play for a playable arena. Driven
    // by the per-arena buttons (a test presses those to exercise it).
    private void Select(ArenaEntry arena)
    {
        _selected = arena;
        _name.Text = arena.NameKey;
        _comingSoon.Visible = !arena.Available;
        _play.Disabled = !arena.Available;
        _preview.Texture = GD.Load<Texture2D>(arena.Preview); // tolerated null if the texture is not imported
    }

    private void Play()
    {
        if (!_selected.Available)
        {
            return;
        }

        GameSetup.Arena = _selected.Id;
        GameSetup.StartNewMatch(GameMode.OnePlayer);
        Go(TitleScene.ArenaScenePath);
    }

    // Guarded so the GoDotTest click-path (title added as a child, not the active scene) can assert the
    // wiring without the runner swapping its whole scene out from under it.
    private void Go(string scenePath)
    {
        if (GetTree().CurrentScene == this)
        {
            GetTree().ChangeSceneToFile(scenePath);
        }
    }
}
