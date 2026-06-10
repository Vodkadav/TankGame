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
        foreach (var name in new[] { "Steel", "Bush", "Player", "Enemy", "TeleportPad", "RaiseLayer", "LowerLayer", "Ramp", "Erase", "SizeMedium", "Validate", "TestPlay", "Save", "Back" })
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

    [Test]
    public void RaisingGroundAndPlacingARamp_ShowsRealElevationMeshes_AndCarriesIntoTheMap()
    {
        _editor.NewMap(28, 16);
        _editor.MapName = "Cliff Test Map";

        // A 2x2 plateau at (10-11, 5-6) with a ramp leading up to it from the west.
        _editor.SelectAction(EditorAction.RaiseLayer);
        _editor.Paint(10, 5);
        _editor.Paint(11, 5);
        _editor.Paint(10, 6);
        _editor.Paint(11, 6);
        _editor.SelectAction(EditorAction.ToggleRamp);
        _editor.Paint(9, 5);

        var map = _editor.CurrentMap();
        if (map.Layers is null || map.Layers[10, 5] != 1 || map.Ramps is null || !map.Ramps[9, 5])
        {
            throw new System.Exception("The elevation tools must author layers and ramps into the map.");
        }

        // WYSIWYG: the editor terrain must render the same plateau blocks and ramp wedges the match uses.
        var terrain = _editor.FindChild("Terrain3DView", recursive: true, owned: false)
            ?? throw new System.Exception("The editor must render its map through a Terrain3DView.");
        var plateaus = 0;
        var ramps = 0;
        foreach (var node in terrain.GetChildren())
        {
            var name = node.Name.ToString();
            if (name.StartsWith("Plateau_"))
            {
                plateaus++;
            }
            else if (name.StartsWith("Ramp_"))
            {
                ramps++;
            }
        }

        if (plateaus == 0 || ramps == 0)
        {
            throw new System.Exception($"Raised cells must show as plateau/ramp meshes (saw {plateaus} plateaus, {ramps} ramps).");
        }
    }
}
