using System.Collections.Generic;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class ArenaGeneratorTests
{
    private const int EnemyCount = 3;
    private const int PickupCount = 7;

    private static ArenaGenParams Params(int seed, int width = 28, int height = 16) =>
        new(width, height, seed, EnemyCount, PickupCount);

    private static IEnumerable<(int X, int Y)> AllAnchors(GeneratedArena a)
    {
        yield return a.PlayerSpawn;
        yield return a.Player2Spawn;
        foreach (var e in a.EnemySpawns)
        {
            yield return e;
        }

        foreach (var pk in a.PickupCells)
        {
            yield return pk;
        }
    }

    [Fact]
    public void Generate_ClustersLikeCells_FormingClumps_NotSaltAndPepper()
    {
        // With neighbour-weighted placement, obstacles clump: somewhere there is a run of three or
        // more identical obstacle cells in a row, which independent salt-and-pepper rarely produces.
        var map = new ArenaGenerator().Generate(Params(seed: 11)).Map;
        var longestRun = 0;

        for (var y = 1; y < map.Height - 1; y++)
        {
            var run = 0;
            var prev = CellMaterial.Floor;
            for (var x = 1; x < map.Width - 1; x++)
            {
                var m = map.Materials[x, y];
                run = (m != CellMaterial.Floor && m == prev) ? run + 1 : 1;
                prev = m;
                if (m != CellMaterial.Floor && run > longestRun)
                {
                    longestRun = run;
                }
            }
        }

        Assert.True(longestRun >= 3, $"clustering should produce a run of >= 3 like obstacles; longest was {longestRun}.");
    }

    [Fact]
    public void Generate_PlacesSandbags_OnlyOnFloorCells()
    {
        var arena = new ArenaGenerator().Generate(Params(seed: 3));
        var sandbags = arena.Sandbags;

        Assert.Equal(arena.Map.Width, sandbags.GetLength(0));
        Assert.Equal(arena.Map.Height, sandbags.GetLength(1));
        for (var x = 0; x < arena.Map.Width; x++)
        {
            for (var y = 0; y < arena.Map.Height; y++)
            {
                if (sandbags[x, y])
                {
                    Assert.Equal(CellMaterial.Floor, arena.Map.Materials[x, y]); // passable floor only
                }
            }
        }
    }

    [Fact]
    public void Generate_ProducesTheRequestedSize_EnclosedByASteelBorder()
    {
        var map = new ArenaGenerator().Generate(Params(seed: 7)).Map;

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
    public void Generate_PlacesTheRequestedCountOfSpawnsAndPickups()
    {
        var arena = new ArenaGenerator().Generate(Params(seed: 1));

        Assert.Equal(EnemyCount, arena.EnemySpawns.Count);
        Assert.Equal(PickupCount, arena.PickupCells.Count);
        Assert.Equal(arena.PlayerSpawn, (arena.Map.SpawnX, arena.Map.SpawnY));
    }

    [Fact]
    public void Generate_EveryAnchorIsReachableOpenFloor()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var arena = new ArenaGenerator().Generate(Params(seed));
            var reached = ReachableSet(arena.Map);

            foreach (var (x, y) in AllAnchors(arena))
            {
                Assert.Equal(CellMaterial.Floor, arena.Map.Materials[x, y]);
                Assert.True(reached[x, y], $"seed {seed}: anchor ({x},{y}) is not reachable from spawn");
            }
        }
    }

    [Fact]
    public void Generate_LeavesNoFloorCellWalledOffFromTheSpawn()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var map = new ArenaGenerator().Generate(Params(seed)).Map;
            var reached = ReachableSet(map);

            var totalFloor = 0;
            var reachedFloor = 0;
            for (var x = 0; x < map.Width; x++)
            {
                for (var y = 0; y < map.Height; y++)
                {
                    if (map.Materials[x, y] != CellMaterial.Floor)
                    {
                        continue;
                    }

                    totalFloor++;
                    if (reached[x, y])
                    {
                        reachedFloor++;
                    }
                }
            }

            Assert.Equal(totalFloor, reachedFloor);
        }
    }

    [Fact]
    public void Generate_InteriorStaysMostlyOpenFloor()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var map = new ArenaGenerator().Generate(Params(seed)).Map;

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

    [Theory]
    [InlineData(20, 12)]
    [InlineData(36, 22)]
    [InlineData(44, 28)]
    public void Generate_WorksAtDifferentSizes(int width, int height)
    {
        var arena = new ArenaGenerator().Generate(Params(seed: 5, width, height));

        Assert.Equal(width, arena.Map.Width);
        Assert.Equal(height, arena.Map.Height);
        var reached = ReachableSet(arena.Map);
        foreach (var (x, y) in AllAnchors(arena))
        {
            Assert.True(reached[x, y], $"{width}x{height}: anchor ({x},{y}) unreachable");
        }
    }

    [Fact]
    public void Generate_IsDeterministic_ForAGivenSeed()
    {
        var a = new ArenaGenerator().Generate(Params(seed: 42));
        var b = new ArenaGenerator().Generate(Params(seed: 42));

        Assert.Equal(a.PlayerSpawn, b.PlayerSpawn);
        Assert.Equal(a.EnemySpawns, b.EnemySpawns);
        Assert.Equal(a.PickupCells, b.PickupCells);
        for (var x = 0; x < a.Map.Width; x++)
        {
            for (var y = 0; y < a.Map.Height; y++)
            {
                Assert.Equal(a.Map.Materials[x, y], b.Map.Materials[x, y]);
            }
        }
    }

    [Fact]
    public void Generate_ScattersBothDestructibleAndPermanentCover()
    {
        var map = new ArenaGenerator().Generate(Params(seed: 3)).Map;

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

    private static bool[,] ReachableSet(LevelMap map)
    {
        var seen = new bool[map.Width, map.Height];
        var queue = new Queue<(int X, int Y)>();
        seen[map.SpawnX, map.SpawnY] = true;
        queue.Enqueue((map.SpawnX, map.SpawnY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
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

        return seen;
    }
}
