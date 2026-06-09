using Godot;
using Chickensoft.GoDotTest;
using TankGame.Infrastructure;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class MapSelectSceneTests : TestClass
{
    private Node _scene = default!;

    public MapSelectSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        TranslationLoader.EnsureLoaded();
        _scene = GD.Load<PackedScene>("res://src/Presentation/MapSelect.tscn").Instantiate();
        TestScene.AddChild(_scene); // runs _Ready, which builds the browser
    }

    [Cleanup]
    public void Cleanup()
    {
        GameSetup.CustomMap = null; // don't leak a custom map into other scenes' tests
        _scene.QueueFree();
    }

    [Test]
    public void MapSelect_ListsTheArenas_PlusPlayAndBack()
    {
        foreach (var name in new[] { "DesertWar", "CliffsAndValleys", "Play", "Back" })
        {
            if (_scene.FindChild(name, recursive: true, owned: false) is not Button)
            {
                throw new System.Exception($"Map select must offer a '{name}' button.");
            }
        }
    }

    [Test]
    public void DesertWar_IsPlayable_ByDefault()
    {
        if (Play().Disabled)
        {
            throw new System.Exception("Desert War is available, so Play should be enabled by default.");
        }
        if (ComingSoon().Visible)
        {
            throw new System.Exception("Desert War is available, so the coming-soon note should be hidden.");
        }
    }

    [Test]
    public void SelectingAnUnbuiltArena_DisablesPlay_AndShowsComingSoon()
    {
        Press("CliffsAndValleys");
        if (!Play().Disabled)
        {
            throw new System.Exception("Cliffs & Valleys is not built, so Play should be disabled.");
        }
        if (!ComingSoon().Visible)
        {
            throw new System.Exception("Cliffs & Valleys is not built, so the coming-soon note should show.");
        }

        Press("DesertWar"); // back to the playable arena
        if (Play().Disabled || ComingSoon().Visible)
        {
            throw new System.Exception("Re-selecting Desert War should re-enable Play and hide coming-soon.");
        }
    }

    [Test]
    public void MyMaps_OffersTheBundledSample_AndPlayingItLoadsACustomMap()
    {
        GameSetup.CustomMap = null;
        if (_scene.FindChild("Map_sample-skirmish", recursive: true, owned: false) is not Button)
        {
            throw new System.Exception("My Maps should list the bundled sample arena as a Play button.");
        }

        Press("Map_sample-skirmish");
        if (GameSetup.CustomMap is null)
        {
            throw new System.Exception("Playing a saved map should load it as the custom map.");
        }
    }

    private void Press(string name) =>
        (_scene.FindChild(name, recursive: true, owned: false) as Button
            ?? throw new System.Exception($"Missing '{name}' button.")).EmitSignal(Button.SignalName.Pressed);

    private Button Play() =>
        _scene.FindChild("Play", recursive: true, owned: false) as Button
            ?? throw new System.Exception("Missing 'Play' button.");

    private Label ComingSoon() =>
        _scene.FindChild("ComingSoon", recursive: true, owned: false) as Label
            ?? throw new System.Exception("Missing 'ComingSoon' label.");
}
