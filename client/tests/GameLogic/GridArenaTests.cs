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
    public void Water_BlocksMovement_ButShotsFlyOverIt_ToTheWallBehind()
    {
        // floor, water, floor, brick: a shot from column 0 should pass over the water (col 1) and
        // stop at the brick (col 3); a tank cannot stand on the water cell.
        var grid = WallGrid.FromMaterials(new[,]
        {
            { CellMaterial.Floor },
            { CellMaterial.Water },
            { CellMaterial.Floor },
            { CellMaterial.Brick },
        });
        var arena = new GridArena(grid, Tile, Vector2.Zero);

        var hit = arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), maxDistance: 100f);
        Assert.NotNull(hit);
        Assert.Equal(30f, hit!.Value.Point.X, precision: 3); // flew over the water, stopped at the brick

        Assert.True(arena.IsBlocked(new Vector2(15f, 5f)));   // a tank cannot drive onto the water
        Assert.False(arena.IsBlocked(new Vector2(5f, 5f)));   // but can on the floor
    }

    [Fact]
    public void Bridge_IsPassable_ToBothMovementAndShots()
    {
        var grid = WallGrid.FromMaterials(new[,]
        {
            { CellMaterial.Floor },
            { CellMaterial.Bridge },
            { CellMaterial.Brick },
        });
        var arena = new GridArena(grid, Tile, Vector2.Zero);

        Assert.False(arena.IsBlocked(new Vector2(15f, 5f))); // a tank can cross the bridge
        var hit = arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), maxDistance: 100f);
        Assert.Equal(20f, hit!.Value.Point.X, precision: 3); // a shot passes over the bridge to the brick
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

    // ── Elevation layers (ADR-0018 step 2b) ──
    // Columns 0-1 are ground (layer 0); columns 2-4 are a raised plateau (layer 1). The cells are
    // all floor — the only thing separating the two halves is their layer.
    private static GridArena GroundThenPlateau()
    {
        var grid = WallGrid.FromMaterials(
            new[,]
            {
                { CellMaterial.Floor },
                { CellMaterial.Floor },
                { CellMaterial.Floor },
                { CellMaterial.Floor },
                { CellMaterial.Floor },
            },
            layers: new[,] { { 0 }, { 0 }, { 1 }, { 1 }, { 1 } });
        return new GridArena(grid, Tile, Vector2.Zero);
    }

    [Fact]
    public void IsBlocked_ARaisedCellIsAWall_ToATankOnTheGround_AndFloor_ToATankOnTheLevel()
    {
        var arena = GroundThenPlateau();
        var onThePlateau = new Vector2(25f, 5f); // column 2, layer 1
        var onTheGround = new Vector2(5f, 5f);    // column 0, layer 0

        Assert.True(arena.IsBlocked(onThePlateau, layer: 0));   // a ground tank can't drive up the cliff
        Assert.False(arena.IsBlocked(onThePlateau, layer: 1));  // a tank already up there stands on it
        Assert.True(arena.IsBlocked(onTheGround, layer: 1));    // and can't step off the edge to the ground
        Assert.False(arena.IsBlocked(onTheGround, layer: 0));   // a ground tank stands on the ground
    }

    [Fact]
    public void RaycastFirstHit_AShotOnTheGround_StopsAtTheCliffFace_Permanently()
    {
        var arena = GroundThenPlateau();

        // Fired along the ground (+X, layer 0) from column 0; the plateau's west face is x=20.
        var hit = arena.RaycastFirstHit(new Vector2(5f, 5f), new Vector2(1f, 0f), 100f, layer: 0);

        Assert.NotNull(hit);
        Assert.Equal(20f, hit!.Value.Point.X, precision: 3);
        Assert.False(hit.Value.Destructible); // a cliff face is permanent, not a brick
    }

    [Fact]
    public void RaycastFirstHit_AShotOnThePlateau_TravelsAcrossItsOwnLevel()
    {
        var arena = GroundThenPlateau();

        // Fired across the plateau (+X, layer 1) from column 2; nothing on layer 1 stops it until
        // the steel border at x=50.
        var hit = arena.RaycastFirstHit(new Vector2(25f, 5f), new Vector2(1f, 0f), 100f, layer: 1);

        Assert.NotNull(hit);
        Assert.Equal(50f, hit!.Value.Point.X, precision: 3);
    }

    [Fact]
    public void DamageAt_IgnoresGeometryOnAnotherLayer()
    {
        // Column 2 is a brick that sits on the raised layer 1; a shot travelling on the ground must
        // not chip it (it is hitting the cliff face, not the brick).
        var grid = WallGrid.FromMaterials(
            new[,] { { CellMaterial.Floor }, { CellMaterial.Floor }, { CellMaterial.Brick } },
            layers: new[,] { { 0 }, { 0 }, { 1 } });
        var arena = new GridArena(grid, Tile, Vector2.Zero);

        arena.DamageAt(new Vector2(20f, 5f), new Vector2(1f, 0f), 1, layer: 0);
        Assert.Equal(WallGrid.DefaultBrickHp, grid.GetCell(2, 0).Hp); // untouched from below

        arena.DamageAt(new Vector2(20f, 5f), new Vector2(1f, 0f), 1, layer: 1);
        Assert.Equal(WallGrid.DefaultBrickHp - 1, grid.GetCell(2, 0).Hp); // a same-layer shot chips it
    }

    [Fact]
    public void LayerAwareQueries_OnAFlatGrid_MatchTheFlatQuery()
    {
        var (arena, _) = RowWithBrickAtColumn3(); // every cell defaults to layer 0

        for (var x = 0; x < 5; x++)
        {
            var point = new Vector2((x * Tile) + 5f, 5f);
            Assert.Equal(arena.IsBlocked(point), arena.IsBlocked(point, layer: 0));
        }
    }
}
