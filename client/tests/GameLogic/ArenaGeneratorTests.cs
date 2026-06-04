using System.Collections.Generic;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class ArenaGeneratorTests
{
    private static readonly (int X, int Y)[] Reserved = { (10, 8), (18, 10), (6, 10), (21, 6), (25, 7), (25, 2), (25, 14) };

    private static ArenaGenParams Params(int seed, int width = 28, int height = 16) =>
        new(width, height, seed, SpawnX: 2, SpawnY: 1, ReservedFloor: Reserved);

    [Fact]
    public void Generate_ProducesTheRequestedSize_EnclosedByASteelBorder()
    {
        var map = new ArenaGenerator().Generate(Params(seed: 7));

        Assert.Equal(28, map.Width);
        Assert.Equal(16, map.Height);
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
    public void Generate_KeepsTheSpawnAndEveryReservedCellAsFloor()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var map = new ArenaGenerator().Generate(Params(seed));

            Assert.Equal(CellMaterial.Floor, map.Materials[map.SpawnX, map.SpawnY]);
            foreach (var (x, y) in Reserved)
            {
                Assert.Equal(CellMaterial.Floor, map.Materials[x, y]);
            }
        }
    }

    [Fact]
    public void Generate_LeavesNoFloorCellWalledOffFromTheSpawn()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var map = new ArenaGenerator().Generate(Params(seed));

            var totalFloor = 0;
            foreach (var m in map.Materials)
            {
                if (m == CellMaterial.Floor)
                {
                    totalFloor++;
                }
            }

            Assert.Equal(totalFloor, ReachableFloor(map));
        }
    }

    [Fact]
    public void Generate_InteriorStaysMostlyOpenFloor()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var map = new ArenaGenerator().Generate(Params(seed));

            var interior = 0;
            var floor = 0;
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

            Assert.True(floor / (double)interior >= 0.8, $"seed {seed}: only {floor}/{interior} open floor");
        }
    }

    [Fact]
    public void Generate_IsDeterministic_ForAGivenSeed()
    {
        var a = new ArenaGenerator().Generate(Params(seed: 42));
        var b = new ArenaGenerator().Generate(Params(seed: 42));

        for (var x = 0; x < a.Width; x++)
        {
            for (var y = 0; y < a.Height; y++)
            {
                Assert.Equal(a.Materials[x, y], b.Materials[x, y]);
                Assert.Equal(a.Bushes[x, y], b.Bushes[x, y]);
            }
        }
    }

    [Fact]
    public void Generate_DifferentSeeds_ProduceDifferentLayouts()
    {
        var a = new ArenaGenerator().Generate(Params(seed: 1));
        var b = new ArenaGenerator().Generate(Params(seed: 2));

        var identical = true;
        for (var x = 0; x < a.Width && identical; x++)
        {
            for (var y = 0; y < a.Height; y++)
            {
                if (a.Materials[x, y] != b.Materials[x, y])
                {
                    identical = false;
                    break;
                }
            }
        }

        Assert.False(identical, "two seeds should scatter cover differently");
    }

    [Fact]
    public void Generate_ScattersBothDestructibleAndPermanentCover()
    {
        var map = new ArenaGenerator().Generate(Params(seed: 3));

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

        Assert.True(hasBrick, "a generated arena should have destructible brick cover");
        Assert.True(hasInteriorSteel, "a generated arena should have permanent steel cover");
    }

    private static int ReachableFloor(LevelMap map)
    {
        var seen = new bool[map.Width, map.Height];
        var queue = new Queue<(int X, int Y)>();
        seen[map.SpawnX, map.SpawnY] = true;
        queue.Enqueue((map.SpawnX, map.SpawnY));
        var count = 0;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            count++;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < map.Width && ny < map.Height &&
                    !seen[nx, ny] && map.Materials[nx, ny] == CellMaterial.Floor)
                {
                    seen[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return count;
    }
}
