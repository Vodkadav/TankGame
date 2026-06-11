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

    // The palette outgrew the screen (owner feedback 2026-06-11): it must live in a scroll container so
    // every tool stays reachable on any window size.
    [Test]
    public void Editor_PaletteScrolls_SoEveryToolStaysReachable()
    {
        var scroll = _editor.FindChild("PaletteScroll", recursive: true, owned: false) as ScrollContainer
            ?? throw new System.Exception("The tool palette must sit inside a 'PaletteScroll' ScrollContainer.");
        if (scroll.FindChild("Palette", recursive: true, owned: false) is null)
        {
            throw new System.Exception("The palette must be the scroll container's content.");
        }
    }

    // The Floor button expands a picker of whole-arena ground themes (owner feedback 2026-06-11):
    // jungle, mars, parking lot, plus the sandy default. Choosing one stamps it on the map.
    [Test]
    public void FloorButton_ExpandsTheThemePicker_AndChoosingOneThemesTheMap()
    {
        var themes = _editor.FindChild("FloorThemes", recursive: true, owned: false) as Control
            ?? throw new System.Exception("The editor must hold a 'FloorThemes' picker.");
        if (themes.Visible)
        {
            throw new System.Exception("The theme picker should stay collapsed until Floor is pressed.");
        }

        Press("Floor");
        if (!themes.Visible)
        {
            throw new System.Exception("Pressing Floor must expand the theme picker.");
        }

        Press("ThemeMars");
        if (_editor.CurrentMap().GroundTheme != GroundTheme.Mars)
        {
            throw new System.Exception("Choosing Mars must stamp the theme on the map.");
        }

        if (themes.Visible)
        {
            throw new System.Exception("Choosing a theme should collapse the picker again.");
        }
    }

    private void Press(string buttonName)
    {
        var button = _editor.FindChild(buttonName, recursive: true, owned: false) as Button
            ?? throw new System.Exception($"Missing '{buttonName}' button.");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    // The 3D build renders Brick as a fence and sandbags as oil spills — the tool labels must say what
    // the player actually sees (owner feedback 2026-06-11).
    [Test]
    public void Editor_LabelsTheTools_AsTheyRender()
    {
        var original = TranslationServer.GetLocale();
        try
        {
            TranslationServer.SetLocale("en");
            AssertToolLabel("Brick", "Fence");
            AssertToolLabel("Sandbag", "Oil Spill");
        }
        finally
        {
            TranslationServer.SetLocale(original);
        }
    }

    private void AssertToolLabel(string buttonName, string expected)
    {
        var button = _editor.FindChild(buttonName, recursive: true, owned: false) as Button
            ?? throw new System.Exception($"Missing '{buttonName}' button.");
        var rendered = button.Tr(button.Text).ToString();
        if (rendered != expected)
        {
            throw new System.Exception($"Tool '{buttonName}' must be labelled '{expected}'; rendered '{rendered}'.");
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
