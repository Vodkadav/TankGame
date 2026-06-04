using System;
using System.Collections.Generic;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class LevelMapTests
{
    [Fact]
    public void Parse_MapsCharactersToMaterials_AndRecordsTheSpawn()
    {
        var map = LevelMap.Parse(
            "#x.\n" +
            "@..");

        Assert.Equal(3, map.Width);
        Assert.Equal(2, map.Height);
        Assert.Equal(CellMaterial.Steel, map.Materials[0, 0]);
        Assert.Equal(CellMaterial.Brick, map.Materials[1, 0]);
        Assert.Equal(CellMaterial.Floor, map.Materials[2, 0]);
        Assert.Equal(0, map.SpawnX);
        Assert.Equal(1, map.SpawnY);
        Assert.Equal(CellMaterial.Floor, map.Materials[map.SpawnX, map.SpawnY]); // spawn is floor
    }

    [Fact]
    public void BuildGrid_ProducesAWallGridMatchingTheMap()
    {
        var grid = LevelMap.Parse(
            "##\n" +
            "x@").BuildGrid();

        Assert.True(grid.IsBlocked(0, 0));  // steel
        Assert.True(grid.IsBlocked(0, 1));  // brick
        Assert.False(grid.IsBlocked(1, 1)); // spawn floor
        Assert.Equal(WallGrid.DefaultBrickHp, grid.GetCell(0, 1).Hp);
    }

    [Theory]
    [InlineData("###\n#@")]           // ragged rows
    [InlineData("#@#\n#?#")]          // unknown character
    [InlineData("###\n#.#")]          // no spawn
    [InlineData("#@#\n#@#")]          // two spawns
    public void Parse_RejectsMalformedMaps(string text)
    {
        Assert.Throws<FormatException>(() => LevelMap.Parse(text));
    }

    [Fact]
    public void Parse_TreatsBushAsPassableFloorThatIsFlaggedConcealing()
    {
        var map = LevelMap.Parse(
            "##\n" +
            "@b");

        Assert.Equal(CellMaterial.Floor, map.Materials[1, 1]); // a bush does not block
        Assert.False(map.BuildGrid().IsBlocked(1, 1));         // …in the wall grid either
        Assert.True(map.Bushes[1, 1]);                          // but it is recorded as a bush
        Assert.False(map.Bushes[0, 1]);                         // plain floor is not
    }

    [Fact]
    public void FromCells_BuildsALevel_FromGeneratedData()
    {
        var materials = new CellMaterial[2, 2];
        materials[0, 0] = CellMaterial.Steel;
        materials[1, 0] = CellMaterial.Floor;
        materials[0, 1] = CellMaterial.Brick;
        materials[1, 1] = CellMaterial.Floor;
        var bushes = new bool[2, 2];
        bushes[1, 1] = true;

        var map = LevelMap.FromCells(materials, bushes, spawnX: 1, spawnY: 0);

        Assert.Equal(CellMaterial.Steel, map.Materials[0, 0]);
        Assert.True(map.Bushes[1, 1]);
        Assert.Equal(1, map.SpawnX);
        Assert.Equal(CellMaterial.Floor, map.Materials[map.SpawnX, map.SpawnY]);
    }

    [Fact]
    public void FromCells_RejectsASpawnThatIsNotFloor()
    {
        var materials = new CellMaterial[2, 1];
        materials[0, 0] = CellMaterial.Steel; // spawn target is a wall
        materials[1, 0] = CellMaterial.Floor;

        Assert.Throws<ArgumentException>(() => LevelMap.FromCells(materials, new bool[2, 1], spawnX: 0, spawnY: 0));
    }

    // --- Battlefield01 sanity (the hand-authored open arena the game ships) ---

    [Fact]
    public void Battlefield01_ParsesAndIsAReasonableSize()
    {
        var map = LevelMap.Parse(Battlefield01.Text);

        Assert.True(map.Width >= 20, $"battlefield too narrow: {map.Width}");
        Assert.True(map.Height >= 12, $"battlefield too short: {map.Height}");
    }

    [Fact]
    public void Battlefield01_IsEnclosedByASteelBorder()
    {
        var map = LevelMap.Parse(Battlefield01.Text);

        for (var x = 0; x < map.Width; x++)
        {
            Assert.Equal(CellMaterial.Steel, map.Materials[x, 0]);
            Assert.Equal(CellMaterial.Steel, map.Materials[x, map.Height - 1]);
        }

        for (var y = 0; y < map.Height; y++)
        {
            Assert.Equal(CellMaterial.Steel, map.Materials[0, y]);
            Assert.Equal(CellMaterial.Steel, map.Materials[map.Width - 1, y]);
        }
    }

    [Fact]
    public void Battlefield01_HasAFloorSpawnInsideTheBorder()
    {
        var map = LevelMap.Parse(Battlefield01.Text);

        Assert.InRange(map.SpawnX, 1, map.Width - 2);
        Assert.InRange(map.SpawnY, 1, map.Height - 2);
        Assert.Equal(CellMaterial.Floor, map.Materials[map.SpawnX, map.SpawnY]);
    }

    [Fact]
    public void Battlefield01_HasBushesToHideIn()
    {
        var map = LevelMap.Parse(Battlefield01.Text);

        var bushes = 0;
        foreach (var b in map.Bushes)
        {
            if (b)
            {
                bushes++;
            }
        }

        Assert.True(bushes >= 4, $"the battlefield should have hide-spots; found {bushes} bush cells");
    }

    [Fact]
    public void Battlefield01_HasBothDestructibleAndPermanentCover()
    {
        var map = LevelMap.Parse(Battlefield01.Text);

        var hasBrick = false;
        var hasInteriorSteel = false;
        for (var x = 1; x < map.Width - 1; x++)
        {
            for (var y = 1; y < map.Height - 1; y++)
            {
                if (map.Materials[x, y] == CellMaterial.Brick)
                {
                    hasBrick = true;
                }
                else if (map.Materials[x, y] == CellMaterial.Steel)
                {
                    hasInteriorSteel = true;
                }
            }
        }

        Assert.True(hasBrick, "the battlefield must have destructible brick cover");
        Assert.True(hasInteriorSteel, "the battlefield must have permanent steel cover");
    }

    // A battlefield is open, not a maze: the interior should be mostly floor so tanks have
    // room to move and flank. (The old maze was ~half walls; this guards against drifting
    // back toward a labyrinth.)
    [Fact]
    public void Battlefield01_InteriorIsMostlyOpenFloor()
    {
        var map = LevelMap.Parse(Battlefield01.Text);

        var floor = 0;
        var interior = 0;
        for (var x = 1; x < map.Width - 1; x++)
        {
            for (var y = 1; y < map.Height - 1; y++)
            {
                interior++;
                if (map.Materials[x, y] == CellMaterial.Floor)
                {
                    floor++;
                }
            }
        }

        Assert.True(floor / (double)interior >= 0.8,
            $"battlefield interior only {floor}/{interior} floor — too dense for an open arena");
    }

    // Scattered cover is fine (and wanted), but no floor pocket may be walled off: every
    // floor cell must be reachable from the spawn so no tank can be stranded.
    [Fact]
    public void Battlefield01_EveryFloorCellIsReachableFromTheSpawn()
    {
        var map = LevelMap.Parse(Battlefield01.Text);

        var totalFloor = 0;
        foreach (var m in map.Materials)
        {
            if (m == CellMaterial.Floor)
            {
                totalFloor++;
            }
        }

        var reached = FloodFillFloor(map);
        Assert.Equal(totalFloor, reached);
    }

    private static int FloodFillFloor(LevelMap map)
    {
        var seen = new bool[map.Width, map.Height];
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((map.SpawnX, map.SpawnY));
        seen[map.SpawnX, map.SpawnY] = true;
        var count = 0;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            count++;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= map.Width || ny >= map.Height)
                {
                    continue;
                }

                if (!seen[nx, ny] && map.Materials[nx, ny] == CellMaterial.Floor)
                {
                    seen[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return count;
    }
}
