using System.Collections.Generic;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class SpawnTableTests
{
    [Fact]
    public void AnOpenField_UsesTheDeclaredCells_AndTheirMirrors()
    {
        var spawns = SpawnTable.For(30, 16, primary: (2, 7), secondary: (25, 7), (_, _) => false);

        Assert.Equal(new List<(int X, int Y)> { (2, 7), (25, 7), (27, 8), (4, 8) }, spawns);
    }

    [Fact]
    public void ABlockedCandidate_NudgesToTheNearestOpenCell()
    {
        var blocked = new HashSet<(int, int)> { (2, 7) };

        var spawns = SpawnTable.For(30, 16, primary: (2, 7), secondary: (25, 7),
            (x, y) => blocked.Contains((x, y)));

        Assert.NotEqual((2, 7), spawns[0]);
        var (dx, dy) = (spawns[0].X - 2, spawns[0].Y - 7);
        Assert.True(System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) == 1,
            $"the nudge must land on an adjacent cell; landed {spawns[0]}");
        Assert.Equal((25, 7), spawns[1]); // the open candidates never move
    }

    [Fact]
    public void ACandidateOutsideTheGrid_IsPulledInBounds()
    {
        // A mirrored spawn can land out of bounds on an asymmetric level; the ring search only
        // accepts in-bounds cells.
        var spawns = SpawnTable.For(10, 10, primary: (0, 0), secondary: (12, 5), (_, _) => false);

        Assert.All(spawns, s => Assert.True(
            s.X is >= 0 and < 10 && s.Y is >= 0 and < 10, $"spawn {s} must be in bounds"));
    }
}
