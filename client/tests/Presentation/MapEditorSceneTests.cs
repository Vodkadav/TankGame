using Godot;
using Chickensoft.GoDotTest;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class MapEditorSceneTests : TestClass
{
    private MapEditorScene _editor = default!;

    public MapEditorSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        TranslationLoader.EnsureLoaded();
        GameSetup.CustomMap = null;
        _editor = (MapEditorScene)GD.Load<PackedScene>("res://src/Presentation/MapEditor.tscn").Instantiate();
        TestScene.AddChild(_editor); // runs _Ready, which builds the palette and the starter arena
    }

    [Cleanup]
    public void Cleanup()
    {
        GameSetup.CustomMap = null;
        _editor.QueueFree();
    }

    [Test]
    public void Editor_BuildsThePaletteAndTheActionButtons()
    {
        foreach (var name in new[] { "Steel", "Bush", "Player", "Enemy", "TeleportPad", "Erase", "SizeMedium", "Validate", "TestPlay", "Save", "Back" })
        {
            if (_editor.FindChild(name, recursive: true, owned: false) is not Button)
            {
                throw new System.Exception($"The editor must offer a '{name}' button.");
            }
        }
    }

    [Test]
    public void PaintingAnEnemySpawn_MakesTheMapSaveableAndPlayable()
    {
        _editor.NewMap(28, 16);
        _editor.MapName = "Editor Test Map";
        _editor.SelectAction(EditorAction.ToggleEnemySpawn);
        _editor.Paint(20, 12);

        if (_editor.CurrentMap().EnemySpawns.Count == 0)
        {
            throw new System.Exception("Painting an enemy spawn should place it on the map.");
        }

        if (!_editor.Save())
        {
            throw new System.Exception("A map with a reachable enemy spawn should validate and save.");
        }

        _editor.TestPlay();
        if (GameSetup.CustomMap is null)
        {
            throw new System.Exception("Test Play should load the edited map as the custom map.");
        }
    }

    [Test]
    public void ABlankMap_FailsToSave_UntilAnEnemyIsPlaced()
    {
        _editor.NewMap(20, 12); // no enemy spawns yet

        if (_editor.Save())
        {
            throw new System.Exception("A map with no enemy spawns is not playable and must not save.");
        }
    }

    [Test]
    public void PlacingATeleportPadPair_CarriesItIntoTheMap_AndIntoTestPlay()
    {
        _editor.NewMap(28, 16);
        _editor.MapName = "Pad Test Map";
        _editor.SelectAction(EditorAction.ToggleEnemySpawn);
        _editor.Paint(20, 12); // make the map playable so TestPlay loads it

        _editor.SelectAction(EditorAction.PlaceTeleportPad);
        _editor.Paint(5, 5);  // pad A
        _editor.Paint(10, 8); // pad B completes the link

        var pads = _editor.CurrentMap().TeleportPads;
        if (pads.Count != 1 || pads[0] != new TeleportPadLink(5, 5, 10, 8))
        {
            throw new System.Exception("Placing a pad pair should author one teleport link.");
        }

        _editor.TestPlay();
        if (GameSetup.CustomMap is null || GameSetup.CustomMap.TeleportPads.Count != 1)
        {
            throw new System.Exception("Test Play must carry the authored teleport pads into play.");
        }
    }
}
