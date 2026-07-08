using System.Collections.Generic;
using System.Linq;
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

    // The hand-authored themed maps: each is a large (~76x46) field with eight spread spawns, several
    // pickups, and passes the same MapValidator the editor holds custom maps to.
    [Theory]
    [InlineData("Forest")]
    [InlineData("Volcano")]
    [InlineData("City")]
    [InlineData("Frozen")]
    [InlineData("Canyon")]
    public void ThemedArena_IsLargeEightSpawn_AndValidates(string arenaId)
    {
        var layout = BuildOrFail(arenaId);

        Assert.InRange(layout.Map.Width, 72, 80);
        Assert.InRange(layout.Map.Height, 44, 48);

        // Eight distinct starts, all on real floor cells (not lava, bridge, water, or a wall).
        var starts = Starts(layout);
        Assert.Equal(8, starts.Count);
        Assert.Equal(8, new HashSet<(int, int)>(starts).Count);
        Assert.All(starts, s => Assert.Equal(CellMaterial.Floor, layout.Map.Materials[s.X, s.Y]));

        Assert.InRange(layout.Powerups.Count, 4, 8);

        var result = MapValidator.Validate(ToDefinition(layout, arenaId));
        Assert.True(result.IsValid, $"{arenaId} failed validation: {Describe(result)}");
    }

    [Fact]
    public void Forest_HasBushConcealment_AndMountainHills()
    {
        var layout = BuildOrFail("Forest");
        Assert.True(AnyCell(layout.Map.Bushes), "forest must have bush copses to hide in");
        Assert.True(AnyMaterial(layout, CellMaterial.Mountain), "forest must have Mountain hills");
    }

    [Fact]
    public void Volcano_HasLava_CrossedByABridgeOverLava()
    {
        var layout = BuildOrFail("Volcano");
        Assert.True(AnyMaterial(layout, CellMaterial.Lava), "volcano must have lava rivers");

        // At least one bridge must actually span lava (a lava cell on either horizontal side), else it
        // is not a crossing.
        var mats = layout.Map.Materials;
        var bridgeOverLava = false;
        for (var x = 1; x < layout.Map.Width - 1 && !bridgeOverLava; x++)
        {
            for (var y = 0; y < layout.Map.Height; y++)
            {
                if (mats[x, y] == CellMaterial.Bridge
                    && (mats[x - 1, y] == CellMaterial.Lava || mats[x + 1, y] == CellMaterial.Lava))
                {
                    bridgeOverLava = true;
                    break;
                }
            }
        }

        Assert.True(bridgeOverLava, "volcano must have at least one Bridge crossing lava");
    }

    [Fact]
    public void City_IsAGridOfBuildingBlocks()
    {
        var layout = BuildOrFail("City");
        Assert.True(AnyMaterial(layout, CellMaterial.Building), "city must have Building blocks");
    }

    [Fact]
    public void Frozen_HasWaterPonds_CrossedByBridges()
    {
        var layout = BuildOrFail("Frozen");
        Assert.True(AnyMaterial(layout, CellMaterial.Water), "frozen must have water ponds");
        Assert.True(AnyMaterial(layout, CellMaterial.Bridge), "frozen ponds must have bridge crossings");
    }

    [Fact]
    public void Canyon_HasMountainWalls_AndARampedPlateau()
    {
        var layout = BuildOrFail("Canyon");
        Assert.True(AnyMaterial(layout, CellMaterial.Mountain), "canyon must have Mountain walls");

        var (hasRamp, hasHighGround) = (false, false);
        for (var x = 0; x < layout.Map.Width; x++)
        {
            for (var y = 0; y < layout.Map.Height; y++)
            {
                hasRamp |= layout.Map.IsRamp(x, y);
                hasHighGround |= layout.Map.LayerAt(x, y) > 0;
            }
        }

        Assert.True(hasHighGround, "canyon must have a raised plateau");
        Assert.True(hasRamp, "canyon plateau must be reachable by a ramp");
    }

    // Net sync: host and guest build independently, so two builds must be byte-identical terrain.
    [Theory]
    [InlineData("Forest")]
    [InlineData("Volcano")]
    [InlineData("City")]
    [InlineData("Frozen")]
    [InlineData("Canyon")]
    public void ThemedArena_IsDeterministic(string arenaId)
    {
        var a = BuildOrFail(arenaId);
        var b = BuildOrFail(arenaId);

        Assert.Equal(a.PlayerSpawn, b.PlayerSpawn);
        Assert.Equal(Starts(a), Starts(b));
        for (var x = 0; x < a.Map.Width; x++)
        {
            for (var y = 0; y < a.Map.Height; y++)
            {
                Assert.Equal(a.Map.Materials[x, y], b.Map.Materials[x, y]);
                Assert.Equal(a.Map.Bushes[x, y], b.Map.Bushes[x, y]);
            }
        }
    }

    private static ArenaLayout BuildOrFail(string arenaId)
    {
        Assert.True(ArenaBuilders.TryGet(arenaId, out var builder), $"{arenaId} must have a builder");
        return builder.Build();
    }

    private static List<(int X, int Y)> Starts(ArenaLayout layout)
    {
        var starts = new List<(int X, int Y)> { layout.PlayerSpawn };
        starts.AddRange(layout.EnemySpawns);
        return starts;
    }

    private static bool AnyCell(bool[,] grid)
    {
        foreach (var v in grid)
        {
            if (v)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnyMaterial(ArenaLayout layout, CellMaterial material)
    {
        foreach (var m in layout.Map.Materials)
        {
            if (m == material)
            {
                return true;
            }
        }

        return false;
    }

    // Reconstructs the MapDefinition the validator checks from the built layout, pulling elevation back
    // out of the LevelMap so a ramped map (Canyon) is validated with its real layers.
    private static MapDefinition ToDefinition(ArenaLayout layout, string name)
    {
        var w = layout.Map.Width;
        var h = layout.Map.Height;
        var layers = new int[w, h];
        var ramps = new bool[w, h];
        var anyLayer = false;
        var anyRamp = false;
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                layers[x, y] = layout.Map.LayerAt(x, y);
                ramps[x, y] = layout.Map.IsRamp(x, y);
                anyLayer |= layers[x, y] != 0;
                anyRamp |= ramps[x, y];
            }
        }

        var powerups = layout.Powerups.Select(p => new PowerupSpawn(p.Kind, p.X, p.Y)).ToList();
        return new MapDefinition(
            name, layout.Map.Materials, layout.Map.Bushes, layout.Sandbags,
            layout.PlayerSpawn, layout.EnemySpawns, powerups, layout.Pads,
            anyLayer ? layers : null, anyRamp ? ramps : null, layout.GroundTheme);
    }

    private static string Describe(MapValidationResult result) =>
        string.Join(", ", result.Errors.Select(e => $"{e.Code}@({e.X},{e.Y})"));
}
