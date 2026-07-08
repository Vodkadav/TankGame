using System.Collections.Generic;
using System.Linq;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The result of building a built-in arena: the <see cref="LevelMap"/> (carrying terrain,
/// elevation, and ramps), where its spawns and pickups sit, its ground theme, and any teleport pads —
/// everything the play scene needs to wire an arena the same way it does a <see cref="CliffsLayout"/>.
/// This is the seam the five themed content slices fill in: each <see cref="IArenaBuilder"/> returns
/// one of these.</summary>
public sealed record ArenaLayout(
    LevelMap Map,
    (int X, int Y) PlayerSpawn,
    IReadOnlyList<(int X, int Y)> EnemySpawns,
    IReadOnlyList<(PowerupKind Kind, int X, int Y)> Powerups,
    bool[,] Sandbags,
    IReadOnlyList<TeleportPadLink> Pads,
    GroundTheme GroundTheme);

/// <summary>Builds one built-in arena. A content slice implements this per map (a full themed layout
/// of ~2x size with eight spawns and powerup pads); the stubs here return a small valid arena so the
/// game runs and tests pass until then.</summary>
public interface IArenaBuilder
{
    ArenaLayout Build();
}

/// <summary>Registry of the code-built arenas, keyed by arena id name (matching the Presentation
/// <c>ArenaId</c> value's name, e.g. "Forest"), so both the single-player scene and the net path
/// resolve the same builder from a wire id without Presentation leaking into this layer. Custom and
/// editor maps are not registered here — only net-synced built-ins are.</summary>
public static class ArenaBuilders
{
    private static readonly IReadOnlyDictionary<string, IArenaBuilder> Registry =
        new Dictionary<string, IArenaBuilder>
        {
            ["Forest"] = new ForestArena(),
            ["Volcano"] = new VolcanoArena(),
            ["City"] = new CityArena(),
            ["Frozen"] = new FrozenArena(),
            ["Canyon"] = new CanyonArena(),
        };

    /// <summary>The builder registered under <paramref name="arenaId"/>, or false when none is (a
    /// built-in with its own scene path like Desert/Cliffs, or a custom map).</summary>
    public static bool TryGet(string arenaId, out IArenaBuilder builder)
    {
        if (Registry.TryGetValue(arenaId, out var found))
        {
            builder = found;
            return true;
        }

        builder = null!;
        return false;
    }

    /// <summary>Whether <paramref name="arenaId"/> names a registered code arena.</summary>
    public static bool Has(string arenaId) => Registry.ContainsKey(arenaId);

    /// <summary>The ids of every registered code arena — the themed built-ins that join Desert and Cliffs
    /// in the shared random-map pool.</summary>
    public static IReadOnlyCollection<string> Ids => Registry.Keys.ToArray();
}

// ── Hand-authored themed arenas ───────────────────────────────────────────────────────────────
// Each map is a ~76x46 steel-ringed field authored with loops and constants only — no Random, Guid, or
// time — so host and guest calling Build() independently get byte-identical layouts (net-synced).
// Spawns come from SpawnTable (eight symmetric, well-separated starts, each nudged to open floor); the
// shared ArenaAuthor.Assemble stitches materials + spawns + pickups into an ArenaLayout.

/// <summary>Forest Ambush: open clearings dotted with Mountain hills and bush copses that let an
/// ambusher lie hidden between the trees.</summary>
public sealed class ForestArena : IArenaBuilder
{
    public ArenaLayout Build()
    {
        var (materials, bushes) = ArenaAuthor.Field();

        // Rocky hills (impassable Mountain clumps) scattered as cover between the clearings.
        foreach (var (cx, cy) in new[] { (14, 10), (26, 34), (50, 12), (62, 33), (38, 23), (20, 38), (58, 22) })
        {
            ArenaAuthor.Diamond(materials, cx, cy, 2, CellMaterial.Mountain);
        }

        // Bush copses (concealment) — passable floor a tank vanishes into.
        foreach (var (cx, cy) in new[] { (10, 24), (34, 8), (46, 36), (66, 14), (30, 26), (54, 30), (22, 16) })
        {
            ArenaAuthor.BushPatch(bushes, cx, cy, 3, 2);
        }

        var powerups = new[]
        {
            (PowerupKind.Repair, 38, 12),
            (PowerupKind.Shield, 12, 33),
            (PowerupKind.SpeedBoost, 64, 24),
            (PowerupKind.RapidFire, 42, 34),
            (PowerupKind.Missile, 6, 6),
        };

        return ArenaAuthor.Assemble(materials, bushes, powerups, GroundTheme.Jungle, (2, 2), (73, 43));
    }
}

/// <summary>Volcano Rim: rivers of Lava snaking across the field, crossed by Bridge cells. Lava lets
/// shots fly over it but destroys any tank that drives onto it — the bridges are the only safe crossings.</summary>
public sealed class VolcanoArena : IArenaBuilder
{
    public ArenaLayout Build()
    {
        var (materials, bushes) = ArenaAuthor.Field();

        // Two horizontal lava rivers spanning the field, each with four bridge crossings.
        foreach (var riverY in new[] { 13, 32 })
        {
            for (var x = 3; x <= 72; x++)
            {
                materials[x, riverY] = CellMaterial.Lava;
            }

            foreach (var bridgeX in new[] { 12, 28, 46, 62 })
            {
                materials[bridgeX, riverY] = CellMaterial.Bridge;
            }
        }

        // A short vertical lava channel joining the two rivers, bridged in the middle.
        for (var y = 13; y <= 32; y++)
        {
            materials[38, y] = CellMaterial.Lava;
        }

        materials[38, 22] = CellMaterial.Bridge;

        // Basalt outcrops (steel) for hard cover on the rims.
        foreach (var (cx, cy) in new[] { (18, 6), (58, 6), (18, 39), (58, 39), (38, 6), (38, 39) })
        {
            ArenaAuthor.Diamond(materials, cx, cy, 1, CellMaterial.Steel);
        }

        var powerups = new[]
        {
            (PowerupKind.Repair, 12, 6),
            (PowerupKind.Shield, 62, 39),
            (PowerupKind.Missile, 38, 4),
            (PowerupKind.RapidFire, 6, 22),
            (PowerupKind.SpeedBoost, 70, 22),
        };

        return ArenaAuthor.Assemble(materials, bushes, powerups, GroundTheme.Volcano, (2, 2), (73, 43));
    }
}

/// <summary>City Streets: a regular grid of solid Building blocks separated by floor roads and
/// cross-street intersections, laid out like real city planning.</summary>
public sealed class CityArena : IArenaBuilder
{
    private const int Period = 12; // one city block (9 wide) plus its 3-wide road
    private const int RoadWidth = 3;

    public ArenaLayout Build()
    {
        var (materials, bushes) = ArenaAuthor.Field();

        // A cell is road (floor) when it lies on a vertical OR horizontal street; otherwise it is a
        // Building. The streets form a connected grid, so every road cell stays reachable.
        for (var x = 1; x < ArenaAuthor.Width - 1; x++)
        {
            for (var y = 1; y < ArenaAuthor.Height - 1; y++)
            {
                var onStreet = x % Period < RoadWidth || y % (RoadWidth + 5) < RoadWidth;
                materials[x, y] = onStreet ? CellMaterial.Floor : CellMaterial.Building;
            }
        }

        var powerups = new[]
        {
            (PowerupKind.Repair, 13, 7),
            (PowerupKind.Shield, 37, 19),
            (PowerupKind.SpeedBoost, 61, 31),
            (PowerupKind.RapidFire, 25, 37),
            (PowerupKind.Missile, 49, 13),
            (PowerupKind.Telephone, 1, 23),
        };

        return ArenaAuthor.Assemble(materials, bushes, powerups, GroundTheme.ParkingLot, (1, 1), (73, 43));
    }
}

/// <summary>Frozen Wastes: an open ice field with scattered Steel and Brick cover and a few Water
/// ponds, each crossed by a Bridge so no pocket is walled off. Long sightlines between the cover.</summary>
public sealed class FrozenArena : IArenaBuilder
{
    public ArenaLayout Build()
    {
        var (materials, bushes) = ArenaAuthor.Field();

        // Ice ridges: steel blocks for hard cover, brick rubble for destructible cover — scattered so
        // sightlines stay long and open between them.
        foreach (var (cx, cy) in new[] { (16, 11), (60, 11), (16, 34), (60, 34), (38, 22) })
        {
            ArenaAuthor.Diamond(materials, cx, cy, 1, CellMaterial.Steel);
        }

        foreach (var (cx, cy) in new[] { (30, 16), (46, 16), (30, 30), (46, 30), (10, 22), (66, 22) })
        {
            ArenaAuthor.Diamond(materials, cx, cy, 1, CellMaterial.Brick);
        }

        // Frozen ponds: Water blocks tanks but shots skim over it; a Bridge lane crosses each so no
        // floor is sealed away.
        foreach (var (cx, cy) in new[] { (24, 23), (52, 23) })
        {
            ArenaAuthor.Diamond(materials, cx, cy, 2, CellMaterial.Water);
            materials[cx, cy] = CellMaterial.Bridge;
        }

        var powerups = new[]
        {
            (PowerupKind.Repair, 38, 8),
            (PowerupKind.Shield, 38, 37),
            (PowerupKind.SpeedBoost, 8, 22),
            (PowerupKind.RapidFire, 68, 22),
            (PowerupKind.Missile, 14, 14),
        };

        return ArenaAuthor.Assemble(materials, bushes, powerups, GroundTheme.Sand, (2, 2), (73, 43));
    }
}

/// <summary>Canyon Run: winding corridors carved by Mountain walls between two open arenas, with a
/// raised central mesa (elevation layer 1) reached only by ramps on its four sides.</summary>
public sealed class CanyonArena : IArenaBuilder
{
    public ArenaLayout Build()
    {
        var (materials, bushes) = ArenaAuthor.Field();
        var layers = new int[ArenaAuthor.Width, ArenaAuthor.Height];
        var ramps = new bool[ArenaAuthor.Width, ArenaAuthor.Height];

        // Two mountain walls split the canyon into three bands, each wall pierced by a single gap so a
        // tank must snake through — top band → middle (gap near the right), middle → bottom (gap left).
        for (var x = 3; x <= 72; x++)
        {
            materials[x, 15] = CellMaterial.Mountain;
            materials[x, 30] = CellMaterial.Mountain;
        }

        for (var x = 36; x <= 40; x++)
        {
            materials[x, 15] = CellMaterial.Floor; // upper pass
        }

        for (var x = 12; x <= 16; x++)
        {
            materials[x, 30] = CellMaterial.Floor; // lower pass
        }

        // Mesa buttes (mountain clumps) breaking up the open bands.
        foreach (var (cx, cy) in new[] { (58, 7), (18, 7), (58, 38), (18, 38) })
        {
            ArenaAuthor.Diamond(materials, cx, cy, 1, CellMaterial.Mountain);
        }

        // A raised central mesa in the middle band: a layer-1 plateau with a ramp on each mid-edge, so
        // holding the high ground means controlling the ramps (mirrors Cliffs & Valleys).
        const int minX = 33, maxX = 42, minY = 18, maxY = 27;
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                layers[x, y] = 1;
            }
        }

        ArenaAuthor.Ramp(materials, layers, ramps, (minX + maxX) / 2, minY - 1);
        ArenaAuthor.Ramp(materials, layers, ramps, (minX + maxX) / 2, maxY + 1);
        ArenaAuthor.Ramp(materials, layers, ramps, minX - 1, (minY + maxY) / 2);
        ArenaAuthor.Ramp(materials, layers, ramps, maxX + 1, (minY + maxY) / 2);

        var powerups = new[]
        {
            (PowerupKind.Repair, (minX + maxX) / 2, (minY + maxY) / 2), // atop the mesa
            (PowerupKind.Shield, 6, 7),
            (PowerupKind.Missile, 69, 7),
            (PowerupKind.SpeedBoost, 6, 38),
            (PowerupKind.RapidFire, 69, 38),
        };

        return ArenaAuthor.Assemble(materials, bushes, powerups, GroundTheme.Mars, (2, 2), (73, 43), layers, ramps);
    }
}

/// <summary>Shared authoring helpers for the built-in themed arenas: a steel-ringed floor field of the
/// common size, primitive terrain stampers, and the assembler that turns authored terrain into an
/// <see cref="ArenaLayout"/> with eight <see cref="SpawnTable"/> spawns. Deterministic — loops and
/// constants only, so a net host and guest build byte-identical maps.</summary>
internal static class ArenaAuthor
{
    public const int Width = 76;
    public const int Height = 46;

    /// <summary>A fresh steel-bordered, all-floor field plus its (empty) bush grid.</summary>
    public static (CellMaterial[,] Materials, bool[,] Bushes) Field()
    {
        var materials = new CellMaterial[Width, Height];
        var bushes = new bool[Width, Height];
        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                materials[x, y] = IsBorder(x, y) ? CellMaterial.Steel : CellMaterial.Floor;
            }
        }

        return (materials, bushes);
    }

    /// <summary>Stamps a solid diamond (L1 radius) of <paramref name="material"/> centred at
    /// (<paramref name="cx"/>, <paramref name="cy"/>), never overwriting the border.</summary>
    public static void Diamond(CellMaterial[,] materials, int cx, int cy, int radius, CellMaterial material)
    {
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (System.Math.Abs(dx) + System.Math.Abs(dy) > radius)
                {
                    continue;
                }

                var x = cx + dx;
                var y = cy + dy;
                if (!IsBorder(x, y) && InBounds(x, y))
                {
                    materials[x, y] = material;
                }
            }
        }
    }

    /// <summary>Marks a <paramref name="halfW"/>×<paramref name="halfH"/> rectangle of bush cells
    /// (concealing floor) centred at (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    public static void BushPatch(bool[,] bushes, int cx, int cy, int halfW, int halfH)
    {
        for (var x = cx - halfW; x <= cx + halfW; x++)
        {
            for (var y = cy - halfH; y <= cy + halfH; y++)
            {
                if (x > 0 && y > 0 && x < Width - 1 && y < Height - 1)
                {
                    bushes[x, y] = true;
                }
            }
        }
    }

    /// <summary>Marks (<paramref name="x"/>, <paramref name="y"/>) a layer-0 floor ramp connecting the
    /// valley to the layer-1 plateau beside it.</summary>
    public static void Ramp(CellMaterial[,] materials, int[,] layers, bool[,] ramps, int x, int y)
    {
        materials[x, y] = CellMaterial.Floor;
        layers[x, y] = 0;
        ramps[x, y] = true;
    }

    /// <summary>Stitches authored terrain into a playable <see cref="ArenaLayout"/>: eight symmetric
    /// spawns from <see cref="SpawnTable"/> (each nudged onto open floor), the player first, the rest as
    /// enemies. Spawn placement treats any non-floor cell as blocked, so no start lands in a wall, on
    /// lava, or on a bridge.</summary>
    public static ArenaLayout Assemble(
        CellMaterial[,] materials, bool[,] bushes,
        (PowerupKind Kind, int X, int Y)[] powerups, GroundTheme theme,
        (int X, int Y) primary, (int X, int Y) secondary,
        int[,]? layers = null, bool[,]? ramps = null)
    {
        bool Blocked(int x, int y) => materials[x, y] != CellMaterial.Floor;

        var spawns = SpawnTable.For(Width, Height, primary, secondary, Blocked);
        var player = spawns[0];
        var enemies = spawns.Skip(1).ToList();
        var sandbags = new bool[Width, Height];
        var map = LevelMap.FromCells(materials, bushes, player.X, player.Y, layers, ramps);
        return new ArenaLayout(map, player, enemies, powerups, sandbags, new List<TeleportPadLink>(), theme);
    }

    private static bool IsBorder(int x, int y) => x == 0 || y == 0 || x == Width - 1 || y == Height - 1;

    private static bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
}
