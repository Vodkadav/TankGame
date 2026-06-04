using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class GridArenaTests
{
    private const float Tile = 10f;

    // A 5x1 row of floor with a brick at column 3, origin at world (0,0):
    // cells span x in [0,10),[10,20),[20,30),[30,40),[40,50); the brick is [30,40).
    private static (GridArena arena, WallGrid grid) RowWithBrickAtColumn3()
    {
        var grid = WallGrid.FromMaterials(new[,]
        {
            { CellMaterial.Floor },
            { CellMaterial.Floor },
            { CellMaterial.Floor },
            { CellMaterial.Brick },
            { CellMaterial.Floor },
        });
        return (new GridArena(grid, Tile, Vector2.Zero), grid);
    }

    [Fact]
    public void RaycastFirstHit_StopsAtTheFirstBlockedCellFace()
    {
        var (arena, _) = RowWithBrickAtColumn3();

        // Fired from the middle of column 0 (x=5) along +X; the brick's near face is x=30.
        var hit = arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), maxDistance: 100f);

        Assert.NotNull(hit);
        Assert.Equal(30f, hit!.Value.Point.X, precision: 3);
        Assert.Equal(25f, hit.Value.Distance, precision: 3);
    }

    [Fact]
    public void RaycastFirstHit_ReportsTheStruckFaceNormal_PerDirection()
    {
        var (arena, _) = RowWithBrickAtColumn3();

        // +X meets the brick's west face → normal points back along -X.
        Assert.Equal(new Vector2(-1f, 0f),
            arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), 100f)!.Value.Normal);
        // -X from the right meets the east face → +X.
        Assert.Equal(new Vector2(1f, 0f),
            arena.RaycastFirstHit(new Vector2(45f, 5f), new Vector2(-1f, 0f), 100f)!.Value.Normal);
        // +Y meets the bottom steel-border face → -Y.
        Assert.Equal(new Vector2(0f, -1f),
            arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(0f, 1f), 100f)!.Value.Normal);
        // -Y meets the top steel-border face → +Y.
        Assert.Equal(new Vector2(0f, 1f),
            arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(0f, -1f), 100f)!.Value.Normal);
    }

    [Fact]
    public void RaycastFirstHit_ReturnsNull_WhenTheWallIsBeyondRange()
    {
        var (arena, _) = RowWithBrickAtColumn3();

        var hit = arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), maxDistance: 10f);

        Assert.Null(hit);
    }

    [Fact]
    public void RaycastFirstHit_HitsTheSteelBorder_WhenLeavingTheGrid()
    {
        // An all-floor 2x1 row; firing +X must still stop at the implicit steel border (x=20).
        var grid = WallGrid.FromMaterials(new[,] { { CellMaterial.Floor }, { CellMaterial.Floor } });
        var arena = new GridArena(grid, Tile, Vector2.Zero);

        var hit = arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), maxDistance: 100f);

        Assert.NotNull(hit);
        Assert.Equal(20f, hit!.Value.Point.X, precision: 3);
    }

    [Fact]
    public void DamageAt_DamagesTheStruckBrickCell()
    {
        var (arena, grid) = RowWithBrickAtColumn3();
        var hit = arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), 100f)!.Value;

        arena.DamageAt(hit.Point, new Vector2(1f, 0f), 1);

        Assert.Equal(WallGrid.DefaultBrickHp - 1, grid.GetCell(3, 0).Hp);
    }

    [Fact]
    public void DamageAt_LeavesTheSteelBorderUntouched()
    {
        var grid = WallGrid.FromMaterials(new[,] { { CellMaterial.Floor }, { CellMaterial.Floor } });
        var arena = new GridArena(grid, Tile, Vector2.Zero);
        var hit = arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), 100f)!.Value;

        arena.DamageAt(hit.Point, new Vector2(1f, 0f), 5); // border is out-of-bounds steel

        Assert.False(grid.GetCell(0, 0).Material == CellMaterial.Brick);
        Assert.True(arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), 100f).HasValue);
    }
}
