using System;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class MazeDefinitionTests
{
    [Fact]
    public void Parse_MapsCharactersToMaterials_AndRecordsTheSpawn()
    {
        var maze = MazeDefinition.Parse(
            "#x.\n" +
            "@..");

        Assert.Equal(3, maze.Width);
        Assert.Equal(2, maze.Height);
        Assert.Equal(CellMaterial.Steel, maze.Materials[0, 0]);
        Assert.Equal(CellMaterial.Brick, maze.Materials[1, 0]);
        Assert.Equal(CellMaterial.Floor, maze.Materials[2, 0]);
        Assert.Equal(0, maze.SpawnX);
        Assert.Equal(1, maze.SpawnY);
        Assert.Equal(CellMaterial.Floor, maze.Materials[maze.SpawnX, maze.SpawnY]); // spawn is floor
    }

    [Fact]
    public void BuildGrid_ProducesAWallGridMatchingTheMaze()
    {
        var grid = MazeDefinition.Parse(
            "##\n" +
            "x@").BuildGrid();

        Assert.True(grid.IsBlocked(0, 0));  // steel
        Assert.True(grid.IsBlocked(0, 1));  // brick
        Assert.False(grid.IsBlocked(1, 1)); // spawn floor
        Assert.Equal(WallGrid.DefaultBrickHp, grid.GetCell(0, 1).Hp);
    }

    [Theory]
    [InlineData("###\n#@")]          // ragged rows
    [InlineData("#@#\n#?#")]          // unknown character
    [InlineData("###\n#.#")]          // no spawn
    [InlineData("#@#\n#@#")]          // two spawns
    public void Parse_RejectsMalformedMazes(string text)
    {
        Assert.Throws<FormatException>(() => MazeDefinition.Parse(text));
    }

    // --- Maze01 sanity (the hand-authored labyrinth M2 ships) ---

    [Fact]
    public void Maze01_ParsesAndIsAReasonableSize()
    {
        var maze = MazeDefinition.Parse(Maze01.Text);

        Assert.True(maze.Width >= 20, $"maze too narrow: {maze.Width}");
        Assert.True(maze.Height >= 12, $"maze too short: {maze.Height}");
    }

    [Fact]
    public void Maze01_IsEnclosedByASteelBorder()
    {
        var maze = MazeDefinition.Parse(Maze01.Text);

        for (var x = 0; x < maze.Width; x++)
        {
            Assert.Equal(CellMaterial.Steel, maze.Materials[x, 0]);
            Assert.Equal(CellMaterial.Steel, maze.Materials[x, maze.Height - 1]);
        }

        for (var y = 0; y < maze.Height; y++)
        {
            Assert.Equal(CellMaterial.Steel, maze.Materials[0, y]);
            Assert.Equal(CellMaterial.Steel, maze.Materials[maze.Width - 1, y]);
        }
    }

    [Fact]
    public void Maze01_HasAFloorSpawnInsideTheBorder_AndShootableBrick()
    {
        var maze = MazeDefinition.Parse(Maze01.Text);

        Assert.InRange(maze.SpawnX, 1, maze.Width - 2);
        Assert.InRange(maze.SpawnY, 1, maze.Height - 2);
        Assert.Equal(CellMaterial.Floor, maze.Materials[maze.SpawnX, maze.SpawnY]);

        var hasBrick = false;
        foreach (var m in maze.Materials)
        {
            if (m == CellMaterial.Brick)
            {
                hasBrick = true;
            }
        }

        Assert.True(hasBrick, "the maze must contain destructible brick");
    }

    [Fact]
    public void Maze01_HasNoOrphanWall_FullySurroundedByFloor()
    {
        var maze = MazeDefinition.Parse(Maze01.Text);

        // An interior wall cell ringed by floor on all four sides is a stray tile — a likely
        // authoring slip. The border is steel so it is never an orphan.
        for (var x = 1; x < maze.Width - 1; x++)
        {
            for (var y = 1; y < maze.Height - 1; y++)
            {
                if (maze.Materials[x, y] == CellMaterial.Floor)
                {
                    continue;
                }

                var surrounded =
                    maze.Materials[x - 1, y] == CellMaterial.Floor &&
                    maze.Materials[x + 1, y] == CellMaterial.Floor &&
                    maze.Materials[x, y - 1] == CellMaterial.Floor &&
                    maze.Materials[x, y + 1] == CellMaterial.Floor;

                Assert.False(surrounded, $"orphan wall tile at ({x},{y})");
            }
        }
    }
}
