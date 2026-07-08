using System.Collections.Generic;
using System.Linq;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class CliffsArenaTests
{
    [Fact]
    public void Create_IsEnclosedByASteelBorder()
    {
        var map = CliffsArena.Create().Map;

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
    public void Create_HasARaisedPlateauOfLayerOneFloor()
    {
        var map = CliffsArena.Create().Map;

        var plateauCells = 0;
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                if (map.LayerAt(x, y) == 1)
                {
                    plateauCells++;
                    Assert.Equal(CellMaterial.Floor, map.Materials[x, y]); // a plateau you can drive on
                }
            }
        }

        Assert.True(plateauCells >= 9, $"expected a sizeable plateau; found {plateauCells} layer-1 cells");
    }

    [Fact]
    public void Create_HasRampsConnectingTheValleyToThePlateau()
    {
        var map = CliffsArena.Create().Map;

        var ramps = 0;
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                if (map.IsRamp(x, y))
                {
                    ramps++;
                    Assert.Equal(0, map.LayerAt(x, y));                    // a ramp sits on the low side
                    Assert.Equal(CellMaterial.Floor, map.Materials[x, y]); // and is drivable floor
                }
            }
        }

        Assert.True(ramps >= 2, $"the plateau must be reachable by ramps; found {ramps}");
    }

    [Fact]
    public void Create_PlacesValidReachableSpawnsAndPickups()
    {
        var layout = CliffsArena.Create();
        var map = layout.Map;

        Assert.Equal(CellMaterial.Floor, map.Materials[layout.PlayerSpawn.X, layout.PlayerSpawn.Y]);
        Assert.NotEmpty(layout.EnemySpawns);
        Assert.True(layout.Powerups.Count >= 3);

        var reachable = ReachableFromSpawn(map, layout.PlayerSpawn);
        Assert.Contains(layout.PlayerSpawn, reachable);
        foreach (var (ex, ey) in layout.EnemySpawns)
        {
            Assert.Equal(CellMaterial.Floor, map.Materials[ex, ey]);
            Assert.True(reachable.Contains((ex, ey)), $"enemy spawn ({ex},{ey}) is walled off");
        }

        foreach (var (_, px, py) in layout.Powerups)
        {
            Assert.True(reachable.Contains((px, py)), $"pickup ({px},{py}) is walled off");
        }
    }

    [Fact]
    public void Create_PlateauIsReachable_ClimbingViaARamp()
    {
        var layout = CliffsArena.Create();
        var map = layout.Map;

        var reachable = ReachableFromSpawn(map, layout.PlayerSpawn);

        var climbedOntoPlateau = false;
        foreach (var (x, y) in reachable)
        {
            if (map.LayerAt(x, y) == 1)
            {
                climbedOntoPlateau = true;
                break;
            }
        }

        Assert.True(climbedOntoPlateau, "a tank must be able to climb onto the plateau from the valley");
    }

    // ── Cross-layer teleport pads (T3) ──
    [Fact]
    public void Create_AuthorsACrossLayerTeleportPadPair()
    {
        var layout = CliffsArena.Create();
        var map = layout.Map;

        Assert.NotEmpty(layout.Pads);
        Assert.Contains(layout.Pads, p => map.LayerAt(p.AX, p.AY) != map.LayerAt(p.BX, p.BY));

        foreach (var pad in layout.Pads)
        {
            // Both ends drivable, off the ramps, and clear of every spawn so no tank starts on a pad.
            Assert.Equal(CellMaterial.Floor, map.Materials[pad.AX, pad.AY]);
            Assert.Equal(CellMaterial.Floor, map.Materials[pad.BX, pad.BY]);
            Assert.False(map.IsRamp(pad.AX, pad.AY));
            Assert.False(map.IsRamp(pad.BX, pad.BY));
            Assert.NotEqual(layout.PlayerSpawn, (pad.AX, pad.AY));
            Assert.NotEqual(layout.PlayerSpawn, (pad.BX, pad.BY));
            Assert.DoesNotContain((pad.AX, pad.AY), layout.EnemySpawns);
            Assert.DoesNotContain((pad.BX, pad.BY), layout.EnemySpawns);
        }
    }

    // ── Scaled to seat 8 players (issue #5, ADD-8) ──
    [Fact]
    public void Create_ScalesTheFieldToRoughlyDouble()
    {
        var map = CliffsArena.Create().Map;

        Assert.Equal(40, map.Width);
        Assert.Equal(32, map.Height);
    }

    [Fact]
    public void Create_ProvidesEightDistinctReachableSpawns()
    {
        var layout = CliffsArena.Create();
        var map = layout.Map;

        var starts = new List<(int X, int Y)> { layout.PlayerSpawn };
        starts.AddRange(layout.EnemySpawns);
        Assert.Equal(8, starts.Count);
        Assert.Equal(8, new HashSet<(int, int)>(starts).Count); // all distinct

        var reachable = ReachableFromSpawn(map, layout.PlayerSpawn);
        foreach (var (sx, sy) in starts)
        {
            Assert.Equal(CellMaterial.Floor, map.Materials[sx, sy]);
            Assert.Equal(0, map.LayerAt(sx, sy)); // every start on the valley floor
            Assert.True(reachable.Contains((sx, sy)), $"start ({sx},{sy}) is walled off");
        }
    }

    [Fact]
    public void Create_SpawnsArePointSymmetric_SoNoSideHasAnUnfairStart()
    {
        var layout = CliffsArena.Create();
        var map = layout.Map;

        var starts = new HashSet<(int, int)> { layout.PlayerSpawn };
        foreach (var e in layout.EnemySpawns)
        {
            starts.Add(e);
        }

        // The whole start set is invariant under a 180° rotation about the field centre.
        foreach (var (x, y) in starts)
        {
            Assert.Contains((map.Width - 1 - x, map.Height - 1 - y), starts);
        }

        // …and the raised plateau is centred: every layer-1 cell's rotation is also layer-1.
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                if (map.LayerAt(x, y) == 1)
                {
                    Assert.Equal(1, map.LayerAt(map.Width - 1 - x, map.Height - 1 - y));
                }
            }
        }
    }

    [Fact]
    public void Create_ProducesAMapThatPassesTheValidator()
    {
        var layout = CliffsArena.Create();
        var map = layout.Map;

        var layers = new int[map.Width, map.Height];
        var ramps = new bool[map.Width, map.Height];
        var sandbags = new bool[map.Width, map.Height];
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                layers[x, y] = map.LayerAt(x, y);
                ramps[x, y] = map.IsRamp(x, y);
            }
        }

        var definition = new MapDefinition(
            "Cliffs", map.Materials, map.Bushes, sandbags,
            layout.PlayerSpawn, layout.EnemySpawns,
            layout.Powerups.Select(p => new PowerupSpawn(p.Kind, p.X, p.Y)).ToList(),
            layout.Pads, layers, ramps);

        var result = MapValidator.Validate(definition);

        Assert.True(result.IsValid, string.Join(", ", result.Errors));
    }

    // Layer-aware flood fill: a tank moves between two adjacent floor cells only when their layers
    // match, or when one of them is a ramp bridging the two layers (mirrors GridArena's rules).
    private static HashSet<(int X, int Y)> ReachableFromSpawn(LevelMap map, (int X, int Y) spawn)
    {
        var seen = new HashSet<(int X, int Y)> { spawn };
        var queue = new Queue<(int X, int Y, int Layer)>();
        queue.Enqueue((spawn.X, spawn.Y, map.LayerAt(spawn.X, spawn.Y)));

        while (queue.Count > 0)
        {
            var (x, y, layer) = queue.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= map.Width || ny >= map.Height)
                {
                    continue;
                }

                if (map.Materials[nx, ny] != CellMaterial.Floor)
                {
                    continue; // a wall (steel/brick), or a cliff face
                }

                var nextLayer = NextLayer(map, layer, nx, ny);
                if (nextLayer is not { } reached)
                {
                    continue; // not connected to the tank's current layer
                }

                if (seen.Add((nx, ny)))
                {
                    queue.Enqueue((nx, ny, reached));
                }
            }
        }

        return seen;
    }

    // The layer a tank on `fromLayer` would be on after stepping onto (x,y), or null if that cell is on
    // a layer the tank cannot reach from here.
    private static int? NextLayer(LevelMap map, int fromLayer, int x, int y)
    {
        var cellLayer = map.LayerAt(x, y);
        if (map.IsRamp(x, y))
        {
            if (fromLayer == cellLayer)
            {
                return cellLayer + 1; // climbing up
            }

            if (fromLayer == cellLayer + 1)
            {
                return cellLayer; // descending
            }

            return null;
        }

        return fromLayer == cellLayer ? cellLayer : (int?)null;
    }
}
