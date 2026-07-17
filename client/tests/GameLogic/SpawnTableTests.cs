using System;
using System.Collections.Generic;
using System.Linq;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class SpawnTableTests
{
    private static int Chebyshev((int X, int Y) a, (int X, int Y) b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static int MinPairwise(IReadOnlyList<(int X, int Y)> spawns)
    {
        var min = int.MaxValue;
        for (var i = 0; i < spawns.Count; i++)
        {
            for (var j = i + 1; j < spawns.Count; j++)
            {
                min = Math.Min(min, Chebyshev(spawns[i], spawns[j]));
            }
        }

        return min;
    }

    [Fact]
    public void AnOpenField_MeetsTheTargetSeparation()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var spawns = SpawnTable.For(76, 46, SpawnTable.MaxSpawns, seed, (_, _) => false);

            Assert.Equal(8, spawns.Count);
            Assert.Equal(8, new HashSet<(int, int)>(spawns).Count);
            Assert.All(spawns, s => Assert.True(s.X is >= 0 and < 76 && s.Y is >= 0 and < 46));
            Assert.True(MinPairwise(spawns) >= SpawnTable.TargetSeparation,
                $"seed {seed}: pairwise separation {MinPairwise(spawns)} is under the target");
        }
    }

    [Fact]
    public void ACrampedField_RelaxesGradually_ButNeverUnderTheFloor()
    {
        // 12x12 cannot seat eight tanks ten apart; the placement must relax — yet never under three.
        for (var seed = 0; seed < 10; seed++)
        {
            var spawns = SpawnTable.For(12, 12, 8, seed, (_, _) => false);

            Assert.Equal(8, spawns.Count);
            Assert.Equal(8, new HashSet<(int, int)>(spawns).Count);
            Assert.True(MinPairwise(spawns) >= SpawnTable.FloorSeparation,
                $"seed {seed}: pairwise separation {MinPairwise(spawns)} is under the floor");
        }
    }

    [Fact]
    public void ATinyField_BestEffort_StillKeepsEverySpawnOnItsOwnCell()
    {
        // 3x3 can never give separation three, but its nine cells still seat eight tanks distinctly.
        var spawns = SpawnTable.For(3, 3, 8, seed: 5, (_, _) => false);

        Assert.Equal(8, spawns.Count);
        Assert.Equal(8, new HashSet<(int, int)>(spawns).Count);
        Assert.All(spawns, s => Assert.True(s.X is >= 0 and < 3 && s.Y is >= 0 and < 3));
    }

    [Fact]
    public void AFieldSmallerThanTheSpawnCount_IsTheOnlyTimeCellsAreShared()
    {
        var spawns = SpawnTable.For(2, 2, 8, seed: 5, (_, _) => false);

        Assert.Equal(8, spawns.Count);
        Assert.Equal(4, new HashSet<(int, int)>(spawns).Count); // all four cells used, then reused
        Assert.All(spawns, s => Assert.True(s.X is >= 0 and < 2 && s.Y is >= 0 and < 2));
    }

    [Fact]
    public void BlockedCells_AreNeverChosen_WhileOpenOnesRemain()
    {
        // The caller's predicate covers walls AND deadly terrain (lava/water); with the whole left
        // half blocked, every spawn must sit in the open right half.
        for (var seed = 0; seed < 10; seed++)
        {
            var spawns = SpawnTable.For(40, 40, 8, seed, (x, _) => x < 20);

            Assert.Equal(8, spawns.Count);
            Assert.All(spawns, s => Assert.True(s.X >= 20, $"seed {seed}: spawn {s} sits on a blocked cell"));
        }
    }

    [Fact]
    public void AFullyBlockedField_StillYieldsDistinctInBoundsSpawns()
    {
        // Nowhere is open: distinctness still beats openness — two tanks must never share a cell.
        var spawns = SpawnTable.For(8, 8, 8, seed: 3, (_, _) => true);

        Assert.Equal(8, spawns.Count);
        Assert.Equal(8, new HashSet<(int, int)>(spawns).Count);
        Assert.All(spawns, s => Assert.True(s.X is >= 0 and < 8 && s.Y is >= 0 and < 8));
    }

    [Fact]
    public void TheSameSeed_YieldsTheSameSpawns()
    {
        var a = SpawnTable.For(76, 46, 8, seed: 42, (_, _) => false);
        var b = SpawnTable.For(76, 46, 8, seed: 42, (_, _) => false);

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeeds_YieldDifferentSpawns()
    {
        var a = SpawnTable.For(76, 46, 8, seed: 1, (_, _) => false);
        var b = SpawnTable.For(76, 46, 8, seed: 2, (_, _) => false);

        Assert.NotEqual(a.ToList(), b.ToList());
    }
}
