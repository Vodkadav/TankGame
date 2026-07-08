using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class ArenaBuildersTests
{
    // The five ArenaId names (Presentation) that must resolve to a code builder — the dispatch in
    // Arena3DScene keys the registry by ArenaId.ToString(), so these strings are that seam.
    [Theory]
    [InlineData("Forest")]
    [InlineData("Volcano")]
    [InlineData("City")]
    [InlineData("Frozen")]
    [InlineData("Canyon")]
    public void EachRegisteredArena_BuildsAValidPlayableArena(string arenaId)
    {
        Assert.True(ArenaBuilders.TryGet(arenaId, out var builder), $"{arenaId} must have a builder");

        var layout = builder.Build();
        var grid = layout.Map.BuildGrid();

        // Eight starts (player + seven enemies), all distinct and standing on open, non-blocked ground.
        Assert.Equal(7, layout.EnemySpawns.Count);
        var starts = new System.Collections.Generic.List<(int X, int Y)> { layout.PlayerSpawn };
        starts.AddRange(layout.EnemySpawns);
        Assert.Equal(8, new System.Collections.Generic.HashSet<(int, int)>(starts).Count);
        Assert.All(starts, s => Assert.False(grid.IsBlocked(s.X, s.Y), $"spawn {s} sits in a wall"));

        // A theme is carried so the ground renders themed, and pickups are placed.
        Assert.NotEmpty(layout.Powerups);
    }

    [Fact]
    public void ClassicBuiltIns_AndCustomMaps_AreNotRegistered_TheyHaveTheirOwnPaths()
    {
        Assert.False(ArenaBuilders.Has("DesertWar"));
        Assert.False(ArenaBuilders.Has("CliffsAndValleys"));
        Assert.False(ArenaBuilders.Has("custom:whatever"));
    }
}
