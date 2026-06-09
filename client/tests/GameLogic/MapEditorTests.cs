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
}
