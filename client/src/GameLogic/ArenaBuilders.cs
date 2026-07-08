using System.Collections.Generic;
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
}

// ── Stub builders ─────────────────────────────────────────────────────────────────────────────
// Each returns a small, valid, steel-ringed field with eight distinct spawns and a couple of pickups,
// so the arena plays and validates. The five content slices replace each Build body with a full
// themed layout; the ArenaLayout shape and the registry key stay fixed.

/// <summary>Forest Ambush — STUB (content slice S-G fills this in). Jungle-tinted holding arena.</summary>
public sealed class ForestArena : IArenaBuilder
{
    public ArenaLayout Build() => StubArena.Build(GroundTheme.Jungle);
}

/// <summary>Volcano Rim — STUB. Volcanic-tinted holding arena (lava layouts come with the content slice).</summary>
public sealed class VolcanoArena : IArenaBuilder
{
    public ArenaLayout Build() => StubArena.Build(GroundTheme.Volcano);
}

/// <summary>City Streets — STUB. Asphalt-tinted holding arena.</summary>
public sealed class CityArena : IArenaBuilder
{
    public ArenaLayout Build() => StubArena.Build(GroundTheme.ParkingLot);
}

/// <summary>Frozen Wastes — STUB. Pale holding arena (an ice theme can be added by the content slice).</summary>
public sealed class FrozenArena : IArenaBuilder
{
    public ArenaLayout Build() => StubArena.Build(GroundTheme.Sand);
}

/// <summary>Canyon Run — STUB. Rocky-tinted holding arena.</summary>
public sealed class CanyonArena : IArenaBuilder
{
    public ArenaLayout Build() => StubArena.Build(GroundTheme.Mars);
}

/// <summary>Builds the minimal valid arena the stubs share: a steel-bordered open floor field with
/// eight distinct spawns (from <see cref="SpawnTable"/>) and two pickups, so it passes
/// <c>MapValidator</c> and the game runs. Content slices stop calling this once they author a real map.</summary>
internal static class StubArena
{
    private const int Size = 20;

    public static ArenaLayout Build(GroundTheme theme)
    {
        var materials = new CellMaterial[Size, Size];
        var bushes = new bool[Size, Size];
        var sandbags = new bool[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                materials[x, y] = IsBorder(x, y) ? CellMaterial.Steel : CellMaterial.Floor;
            }
        }

        var spawns = SpawnTable.For(Size, Size, (1, 1), (Size - 2, Size - 2), IsBorder);
        var player = spawns[0];
        var enemies = new List<(int X, int Y)>(spawns.Count - 1);
        for (var i = 1; i < spawns.Count; i++)
        {
            enemies.Add(spawns[i]);
        }

        var powerups = new[]
        {
            (PowerupKind.Repair, Size / 2, Size / 2),
            (PowerupKind.Shield, Size / 2, 2),
        };

        var map = LevelMap.FromCells(materials, bushes, player.X, player.Y);
        return new ArenaLayout(map, player, enemies, powerups, sandbags,
            new List<TeleportPadLink>(), theme);
    }

    private static bool IsBorder(int x, int y) => x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
}
