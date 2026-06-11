using System.Linq;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class MapEditorTests
{
    private static MapEditor Medium() => new("Test Map", 28, 16);

    [Fact]
    public void New_HasASteelBorder_FloorInterior_AndADefaultPlayerSpawn()
    {
        var editor = Medium();

        Assert.Equal(CellMaterial.Steel, editor.MaterialAt(0, 0));
        Assert.Equal(CellMaterial.Floor, editor.MaterialAt(5, 5));
        Assert.Equal((1, 1), editor.PlayerSpawn);
    }

    [Fact]
    public void PaintMaterial_ChangesInteriorCells_ButLeavesTheBorderSteel()
    {
        var editor = Medium();
        editor.Action = EditorAction.PaintMaterial;
        editor.PaintMaterial = CellMaterial.Brick;

        editor.ApplyAt(4, 4);
        editor.ApplyAt(0, 0); // border — must stay steel

        Assert.Equal(CellMaterial.Brick, editor.MaterialAt(4, 4));
        Assert.Equal(CellMaterial.Steel, editor.MaterialAt(0, 0));
    }

    [Fact]
    public void PaintingASolidMaterial_ClearsBushAndSandbagOnThatCell()
    {
        var editor = Medium();
        editor.Action = EditorAction.ToggleBush;
        editor.ApplyAt(4, 4);
        Assert.True(editor.BushAt(4, 4));

        editor.Action = EditorAction.PaintMaterial;
        editor.PaintMaterial = CellMaterial.Steel;
        editor.ApplyAt(4, 4);

        Assert.False(editor.BushAt(4, 4));
    }

    [Fact]
    public void ToggleEnemySpawn_AddsThenRemovesTheCell()
    {
        var editor = Medium();
        editor.Action = EditorAction.ToggleEnemySpawn;

        editor.ApplyAt(6, 6);
        Assert.Contains((6, 6), editor.EnemySpawns);

        editor.ApplyAt(6, 6);
        Assert.DoesNotContain((6, 6), editor.EnemySpawns);
    }

    [Fact]
    public void TogglePowerup_PlacesByKind_ReplacesOnTheSameCell_AndRemovesWhenRepeated()
    {
        var editor = Medium();
        editor.Action = EditorAction.TogglePowerup;

        editor.PaintPowerup = PowerupKind.Repair;
        editor.ApplyAt(7, 7);
        Assert.Contains(editor.PowerupSpawns, p => p.Kind == PowerupKind.Repair && p.X == 7 && p.Y == 7);

        editor.PaintPowerup = PowerupKind.Shield;
        editor.ApplyAt(7, 7); // a different kind on the same cell replaces it
        Assert.Single(editor.PowerupSpawns);
        Assert.Equal(PowerupKind.Shield, editor.PowerupSpawns[0].Kind);

        editor.ApplyAt(7, 7); // same kind again removes it
        Assert.Empty(editor.PowerupSpawns);
    }

    [Fact]
    public void SetPlayerSpawn_MovesTheSingleSpawn()
    {
        var editor = Medium();
        editor.Action = EditorAction.SetPlayerSpawn;

        editor.ApplyAt(9, 5);

        Assert.Equal((9, 5), editor.PlayerSpawn);
    }

    [Fact]
    public void Erase_ReturnsACellToFloor_AndRemovesItsMarkers()
    {
        var editor = Medium();
        editor.Action = EditorAction.ToggleEnemySpawn;
        editor.ApplyAt(8, 8);
        editor.Action = EditorAction.PaintMaterial;
        editor.PaintMaterial = CellMaterial.Brick;
        editor.ApplyAt(8, 8);

        editor.Action = EditorAction.Erase;
        editor.ApplyAt(8, 8);

        Assert.Equal(CellMaterial.Floor, editor.MaterialAt(8, 8));
        Assert.DoesNotContain((8, 8), editor.EnemySpawns);
    }

    [Fact]
    public void ToMap_ProducesAValidMap_OnceAnEnemyIsPlaced()
    {
        var editor = Medium();
        editor.Action = EditorAction.ToggleEnemySpawn;
        editor.ApplyAt(20, 12);

        Assert.True(editor.Validate().IsValid);
        Assert.Equal("Test Map", editor.ToMap().Name);
    }

    [Fact]
    public void ToggleEnemySpawn_RefusesANinthTankSpawn()
    {
        var editor = Medium();
        editor.Action = EditorAction.ToggleEnemySpawn;
        for (var i = 0; i < 7; i++)
        {
            editor.ApplyAt(2 + i, 3); // player spawn + 7 enemies = the 8-tank cap
        }

        Assert.Equal(7, editor.EnemySpawns.Count);

        editor.ApplyAt(12, 3); // a ninth tank — refused
        Assert.Equal(7, editor.EnemySpawns.Count);

        editor.ApplyAt(2, 3); // removal always works
        Assert.Equal(6, editor.EnemySpawns.Count);
    }

    [Fact]
    public void ToMap_CarriesTheGroundTheme()
    {
        var editor = new MapEditor("Themed", 8, 6) { GroundTheme = GroundTheme.ParkingLot };

        Assert.Equal(GroundTheme.ParkingLot, editor.ToMap().GroundTheme);
    }

    [Fact]
    public void PlaceTeleportPad_FirstClickIsPending_SecondClickFormsALink()
    {
        var editor = Medium();
        editor.Action = EditorAction.PlaceTeleportPad;

        editor.ApplyAt(5, 5); // pad A — pending, no link yet
        Assert.Empty(editor.TeleportPads);
        Assert.Equal((5, 5), editor.PendingTeleportPad);

        editor.ApplyAt(10, 8); // pad B — completes the link
        Assert.Single(editor.TeleportPads);
        Assert.Equal(new TeleportPadLink(5, 5, 10, 8), editor.TeleportPads[0]);
        Assert.Null(editor.PendingTeleportPad);
    }

    [Fact]
    public void PlaceTeleportPad_ClickingAnExistingPad_RemovesItsLink()
    {
        var editor = Medium();
        editor.Action = EditorAction.PlaceTeleportPad;
        editor.ApplyAt(5, 5);
        editor.ApplyAt(10, 8);
        Assert.Single(editor.TeleportPads);

        editor.ApplyAt(10, 8); // clicking a placed pad clears its whole link
        Assert.Empty(editor.TeleportPads);
        Assert.Null(editor.PendingTeleportPad);
    }

    [Fact]
    public void PlaceTeleportPad_ClickingThePendingPadAgain_CancelsIt()
    {
        var editor = Medium();
        editor.Action = EditorAction.PlaceTeleportPad;
        editor.ApplyAt(5, 5);

        editor.ApplyAt(5, 5); // same cell again cancels the half-placed link
        Assert.Empty(editor.TeleportPads);
        Assert.Null(editor.PendingTeleportPad);
    }

    [Fact]
    public void Erase_RemovesATeleportPadLink_AndItsPartner()
    {
        var editor = Medium();
        editor.Action = EditorAction.PlaceTeleportPad;
        editor.ApplyAt(5, 5);
        editor.ApplyAt(10, 8);

        editor.Action = EditorAction.Erase;
        editor.ApplyAt(5, 5);

        Assert.Empty(editor.TeleportPads);
    }

    [Fact]
    public void ToMap_CarriesTeleportPads()
    {
        var editor = Medium();
        editor.Action = EditorAction.PlaceTeleportPad;
        editor.ApplyAt(5, 5);
        editor.ApplyAt(10, 8);

        Assert.Single(editor.ToMap().TeleportPads);
    }

    // ── Elevation tools (ADR-0020 Wave B step 5) ──

    [Fact]
    public void RaiseLayer_LiftsACell_AndClampsAtTheMaximum()
    {
        var editor = Medium();
        editor.Action = EditorAction.RaiseLayer;

        for (var i = 0; i < MapValidator.MaxLayer + 3; i++)
        {
            editor.ApplyAt(5, 5);
        }

        Assert.Equal(MapValidator.MaxLayer, editor.LayerAt(5, 5));
    }

    [Fact]
    public void LowerLayer_DropsACell_AndClampsAtTheGround()
    {
        var editor = Medium();
        editor.Action = EditorAction.RaiseLayer;
        editor.ApplyAt(5, 5);

        editor.Action = EditorAction.LowerLayer;
        editor.ApplyAt(5, 5);
        editor.ApplyAt(5, 5); // already at ground — stays 0

        Assert.Equal(0, editor.LayerAt(5, 5));
    }

    [Fact]
    public void RaiseLayer_IgnoresTheBorder()
    {
        var editor = Medium();
        editor.Action = EditorAction.RaiseLayer;

        editor.ApplyAt(0, 0);

        Assert.Equal(0, editor.LayerAt(0, 0));
    }

    [Fact]
    public void ToggleRamp_OnlyOnFloor_AndTogglesOff()
    {
        var editor = Medium();
        editor.Action = EditorAction.PaintMaterial;
        editor.PaintMaterial = CellMaterial.Steel;
        editor.ApplyAt(4, 4);

        editor.Action = EditorAction.ToggleRamp;
        editor.ApplyAt(4, 4); // steel — refused
        editor.ApplyAt(5, 5); // floor — placed
        Assert.False(editor.RampAt(4, 4));
        Assert.True(editor.RampAt(5, 5));

        editor.ApplyAt(5, 5); // second click clears it
        Assert.False(editor.RampAt(5, 5));
    }

    [Fact]
    public void Erase_FlattensTheCell()
    {
        var editor = Medium();
        editor.Action = EditorAction.RaiseLayer;
        editor.ApplyAt(5, 5);
        editor.Action = EditorAction.ToggleRamp;
        editor.ApplyAt(6, 5);

        editor.Action = EditorAction.Erase;
        editor.ApplyAt(5, 5);
        editor.ApplyAt(6, 5);

        Assert.Equal(0, editor.LayerAt(5, 5));
        Assert.False(editor.RampAt(6, 5));
    }

    [Fact]
    public void ToMap_CarriesElevation_OnlyWhenAuthored()
    {
        var flat = Medium().ToMap();
        Assert.Null(flat.Layers); // an untouched map keeps the lean flat document
        Assert.Null(flat.Ramps);

        var editor = Medium();
        editor.Action = EditorAction.RaiseLayer;
        editor.ApplyAt(5, 5);
        editor.Action = EditorAction.ToggleRamp;
        editor.ApplyAt(4, 5);

        var map = editor.ToMap();
        Assert.Equal(1, map.Layers![5, 5]);
        Assert.True(map.Ramps![4, 5]);
    }
}
