using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain;
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
        foreach (var name in new[] { "Steel", "Bush", "Spawn", "TeleportPad", "RaiseLayer", "LowerLayer", "Ramp", "Erase", "SizeMedium", "Validate", "TestPlay", "Save", "Back" })
        {
            if (_editor.FindChild(name, recursive: true, owned: false) is not Button)
            {
                throw new System.Exception($"The editor must offer a '{name}' button.");
            }
        }

        // One unified Spawn tool replaced the per-role buttons (owner follow-up 2026-06-11).
        foreach (var retired in new[] { "Player", "Enemy" })
        {
            if (_editor.FindChild(retired, recursive: true, owned: false) is Button)
            {
                throw new System.Exception($"The '{retired}' button is retired — spawns are one numbered pool.");
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

    // The asset browser (owner ask 2026-06-11): a fly-out with search + collapsible categories over
    // the library. CI has no external library, so the committed sample under models/imported/ is the
    // catalogue — picking it arms the decoration tool, placing renders the real model in the preview.
    [Test]
    public void AssetBrowser_SearchFindsTheImportedSample_AndPlacingRendersIt()
    {
        var browser = _editor.FindChild("AssetBrowser", recursive: true, owned: false) as Control
            ?? throw new System.Exception("The editor must hold the 'AssetBrowser' fly-out.");
        if (browser.Visible)
        {
            throw new System.Exception("The browser should stay closed until Assets is pressed.");
        }

        Press("Assets");
        if (!browser.Visible)
        {
            throw new System.Exception("Pressing Assets must open the browser.");
        }

        var search = _editor.FindChild("AssetSearch", recursive: true, owned: false) as LineEdit
            ?? throw new System.Exception("The browser must offer a search field.");
        search.Text = "crate";
        search.EmitSignal(LineEdit.SignalName.TextChanged, "crate");

        var tree = _editor.FindChild("AssetTree", recursive: true, owned: false) as Tree
            ?? throw new System.Exception("The browser must list assets in a tree.");
        if (FindTreeItem(tree.GetRoot(), "crate medium") is null)
        {
            throw new System.Exception("Searching 'crate' must surface the imported sample crate.");
        }

        _editor.NewMap(28, 16);
        _editor.PickAsset("kenney_blaster-kit/crate-medium");
        _editor.Paint(6, 6);

        if (_editor.CurrentMap().Decorations.Count != 1)
        {
            throw new System.Exception("Placing the picked asset must add a decoration to the map.");
        }

        var view = _editor.FindChild("Decoration_6_6", recursive: true, owned: false) as DecorationView
            ?? throw new System.Exception("The placed prop must render in the preview.");
        if (view.GetChildCount() == 0)
        {
            throw new System.Exception("The decoration must load its real model.");
        }

        // Decorations pose like any placed item: select, turn, scale (the gizmo flow).
        _editor.SelectCell(6, 6);
        if (_editor.SelectedCell != (6, 6))
        {
            throw new System.Exception("A decoration on floor must be selectable for posing.");
        }

        _editor.ScaleSelected(1.6f);
        var rescaled = _editor.FindChild("Decoration_6_6", recursive: true, owned: false) as DecorationView
            ?? throw new System.Exception("The reposed prop must re-render.");
        if (Mathf.Abs(rescaled.Scale.X - 1.6f) > 0.01f)
        {
            throw new System.Exception($"The size bar must scale the prop; scale was {rescaled.Scale.X}.");
        }
    }

    private static TreeItem? FindTreeItem(TreeItem? item, string text)
    {
        if (item is null)
        {
            return null;
        }

        if (item.GetText(0) == text)
        {
            return item;
        }

        for (var child = item.GetFirstChild(); child is not null; child = child.GetNext())
        {
            if (FindTreeItem(child, text) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // Every placed prop obeys the gizmo (owner feedback 2026-06-11): mountains had an early-return
    // render branch that skipped the pose, so size scaling "worked on steel but not mountains".
    [Test]
    public void ScalingAMountain_ScalesItsMesh_LikeAnyOtherProp()
    {
        _editor.NewMap(28, 16);
        _editor.SelectMaterial(CellMaterial.Mountain);
        _editor.Paint(5, 5);
        _editor.SelectCell(5, 5);
        _editor.ScaleSelected(2f);

        var rock = _editor.FindChild("Mountain_5_5", recursive: true, owned: false) as Node3D
            ?? throw new System.Exception("The painted mountain must render.");
        if (Mathf.Abs(rock.Scale.X - 2f) > 0.01f)
        {
            throw new System.Exception($"The size bar must scale the mountain; scale was {rock.Scale.X}.");
        }
    }

    // The editor views the map from the game's ¾ isometric angle (owner follow-up 2026-06-11) —
    // straight-down made walls read as flat squares and placed items near-impossible to tell apart.
    [Test]
    public void EditorCamera_UsesTheIsometricAngle_NotTopDown()
    {
        var camera = _editor.FindChild("EditorCamera", recursive: true, owned: false) as Camera3D
            ?? throw new System.Exception("Missing the editor camera.");
        var pitch = Mathf.RadToDeg(camera.Rotation.X);
        if (pitch <= -80f)
        {
            throw new System.Exception($"The editor must use the ¾ view, not top-down; pitch was {pitch}°.");
        }

        if (Mathf.Abs(Mathf.RadToDeg(camera.Rotation.Y)) < 5f)
        {
            throw new System.Exception("The ¾ view must be turned to the isometric yaw, not facing flat north.");
        }
    }

    // The theme picker flies out BESIDE the palette (owner follow-up 2026-06-11), not inline where
    // it shoves every tool below it down.
    [Test]
    public void FloorThemes_FlyOutBesideThePalette_NotInsideIt()
    {
        var themes = _editor.FindChild("FloorThemes", recursive: true, owned: false) as Control
            ?? throw new System.Exception("Missing the 'FloorThemes' picker.");
        if (themes.GetParent()?.Name.ToString() == "Palette")
        {
            throw new System.Exception("The theme picker must fly out beside the palette, not expand inside it.");
        }
    }

    // The bottom action bar grew DOWN from the bottom edge, so its buttons stuck outside the window
    // (owner follow-up 2026-06-11) — it must grow upward instead.
    [Test]
    public void BottomActionBar_GrowsUpward_SoItStaysOnScreen()
    {
        var bar = _editor.FindChild("Actions", recursive: true, owned: false) as Control
            ?? throw new System.Exception("Missing the bottom action bar.");
        if (bar.GrowVertical != Control.GrowDirection.Begin)
        {
            throw new System.Exception("The bottom bar must grow upward from the bottom edge.");
        }
    }

    // The selection gizmo (owner follow-up 2026-06-11): right-clicking a placed item selects it —
    // three axis rings rotate it freely and the size bar scales it, WYSIWYG in the preview mesh.
    [Test]
    public void SelectingAPlacedFence_ShowsTheGizmo_AndPosingItMovesTheMesh()
    {
        _editor.NewMap(28, 16);
        _editor.SelectMaterial(CellMaterial.Brick);
        _editor.Paint(5, 5);

        _editor.SelectCell(5, 5);
        if (_editor.SelectedCell != (5, 5))
        {
            throw new System.Exception("Right-click selection must remember the picked cell.");
        }

        var gizmo = _editor.FindChild("SelectionGizmo", recursive: true, owned: false) as RotationGizmo3D
            ?? throw new System.Exception("Selecting a placed item must show the axis-ring gizmo.");
        if (!gizmo.Visible || gizmo.GetChildCount() < 3)
        {
            throw new System.Exception("The gizmo must present its three axis rings.");
        }

        var scalePanel = _editor.FindChild("ScalePanel", recursive: true, owned: false) as Control
            ?? throw new System.Exception("Selecting must reveal the scale panel.");
        if (!scalePanel.Visible)
        {
            throw new System.Exception("The size bar must appear with the selection.");
        }

        _editor.RotateSelected(RotationGizmo3D.AxisY, 90f);
        _editor.ScaleSelected(1.5f);

        var wall = _editor.FindChild("Wall_5_5", recursive: true, owned: false) as Node3D
            ?? throw new System.Exception("The painted fence must render as a wall node.");
        if (Mathf.Abs(Mathf.Abs(Mathf.RadToDeg(wall.Rotation.Y)) - 90f) > 0.1f)
        {
            throw new System.Exception($"The yaw ring must turn the mesh; yaw was {Mathf.RadToDeg(wall.Rotation.Y)}°.");
        }

        if (Mathf.Abs(wall.Scale.X - 1.5f) > 0.01f)
        {
            throw new System.Exception($"The size bar must scale the mesh; scale was {wall.Scale.X}.");
        }

        _editor.SelectCell(6, 6); // empty floor — nothing to pose
        if (_editor.SelectedCell is not null || gizmo.Visible)
        {
            throw new System.Exception("Clicking empty floor must deselect and hide the gizmo.");
        }
    }

    // A placed teleport pair shows a pulsing dotted line between its two ends (owner feedback
    // 2026-06-11); a half-placed pad (awaiting its partner) has no line yet.
    [Test]
    public void TeleportPadPair_DrawsAFadingDottedLink_OnceBothEndsExist()
    {
        _editor.NewMap(28, 16);
        _editor.SelectAction(EditorAction.PlaceTeleportPad);
        _editor.Paint(5, 5); // first end — pending, no link line yet

        if (_editor.FindChild("PadLink0", recursive: true, owned: false) is not null)
        {
            throw new System.Exception("A half-placed pad must not draw a link line yet.");
        }

        _editor.Paint(10, 8); // the partner — the pair forms

        var link = _editor.FindChild("PadLink0", recursive: true, owned: false) as TeleportLinkLine
            ?? throw new System.Exception("A completed pad pair must draw its dotted link line.");
        if (link.DotCount < 3)
        {
            throw new System.Exception($"The link must be dotted along the span; saw {link.DotCount} dots.");
        }
    }

    // Spawn markers are one numbered pool drawn as the ringed-disc gizmo (owner follow-up
    // 2026-06-11): a red disc with two white rings and the marker number, easy to spot while editing.
    [Test]
    public void SpawnMarkers_AreRingedDiscs_NumberedFromOne()
    {
        _editor.NewMap(28, 16);
        _editor.SelectAction(EditorAction.ToggleSpawn);
        _editor.Paint(20, 12);
        _editor.Paint(21, 12);

        var gizmos = _editor.FindChild("Gizmos", recursive: true, owned: false)
            ?? throw new System.Exception("The editor must render its spawn gizmos.");
        foreach (var expected in new[] { "Spawn1", "Spawn2", "Spawn3" })
        {
            if (gizmos.FindChild(expected, recursive: true, owned: false) is not SpawnMarker3D)
            {
                throw new System.Exception($"Spawn markers must be numbered ringed discs; missing '{expected}'.");
            }
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
