using System;

namespace TankGame.Domain;

/// <summary>What a wall cell is made of. <see cref="Floor"/> is passable empty space; a brick wall
/// and a <see cref="Crate"/> both take damage and break into floor; steel never breaks.</summary>
public enum CellMaterial
{
    Floor,
    Brick,
    Steel,

    /// <summary>A destructible crate — blocks movement and shots, breaks into floor after a couple of
    /// hits (fewer than brick).</summary>
    Crate,

    /// <summary>Water — blocks tank movement but lets shots fly over it; crossed only by a bridge.</summary>
    Water,

    /// <summary>A bridge over water — passable to both movement and shots, like floor.</summary>
    Bridge,

    /// <summary>A mountain — impassable and blocks shots (like steel), generated in clumps.</summary>
    Mountain,

    /// <summary>A building — a solid, indestructible block; generated as small rectangles.</summary>
    Building,
}

/// <summary>Per-material blocking rules. A cell can block movement, shots, both, or neither, so terrain
/// like water (blocks movement, not shots) and bridges (blocks neither) are possible alongside solid
/// walls. Pure data — no Godot.</summary>
public static class CellMaterials
{
    /// <summary>Whether a tank cannot drive onto this material.</summary>
    public static bool BlocksMovement(CellMaterial material) => material switch
    {
        CellMaterial.Floor or CellMaterial.Bridge => false,
        _ => true, // brick, steel, crate, water (and future solids) all stop a tank
    };

    /// <summary>Whether a shot cannot pass over this material.</summary>
    public static bool BlocksShots(CellMaterial material) => material switch
    {
        CellMaterial.Floor or CellMaterial.Bridge or CellMaterial.Water => false,
        _ => true, // brick, steel, crate (and future solids) stop a shot; water lets it fly over
    };
}

/// <summary>One cell of the wall grid: its material and remaining hit points. A
/// <see cref="CellMaterial.Floor"/> cell has no hp. Pure data — the Presentation layer maps
/// it to a tile/atlas frame.</summary>
/// <param name="Material">The cell's material.</param>
/// <param name="Hp">Remaining hit points (brick only; 0 once broken).</param>
public readonly record struct WallCell(CellMaterial Material, int Hp);

/// <summary>A cell whose state changed, broadcast so the view can re-render just that tile.</summary>
/// <param name="X">Column.</param>
/// <param name="Y">Row.</param>
/// <param name="Cell">The cell's new state.</param>
public readonly record struct WallCellChanged(int X, int Y, WallCell Cell);

/// <summary>A tile grid of walls and floor. The maze lives here in pure tile space (no
/// world coordinates, no Godot): cells block movement and shots, brick cells take damage and
/// turn to floor when destroyed, steel is indestructible. The arena maps world-space rays
/// onto this grid; the view subscribes to <see cref="CellChanged"/> to re-render damaged
/// tiles.</summary>
public interface IWallGrid
{
    /// <summary>Number of columns.</summary>
    int Width { get; }

    /// <summary>Number of rows.</summary>
    int Height { get; }

    /// <summary>The cell at (<paramref name="x"/>, <paramref name="y"/>). Out-of-bounds
    /// cells read as a blocking steel border so the maze is implicitly enclosed.</summary>
    WallCell GetCell(int x, int y);

    /// <summary>Whether the cell at (<paramref name="x"/>, <paramref name="y"/>) blocks tank
    /// movement (out-of-bounds reads as a blocking steel border). Water and walls block movement;
    /// floor and bridges do not.</summary>
    bool IsBlocked(int x, int y);

    /// <summary>Whether the cell at (<paramref name="x"/>, <paramref name="y"/>) blocks shots —
    /// walls do, water and bridges do not (shots fly over water).</summary>
    bool BlocksShots(int x, int y);

    /// <summary>Applies <paramref name="amount"/> damage to the cell. Brick loses hp and
    /// becomes <see cref="CellMaterial.Floor"/> at 0; steel and floor are unaffected. Raises
    /// <see cref="CellChanged"/> when the cell's state actually changes.</summary>
    void DamageCell(int x, int y, int amount);

    /// <summary>Raised when a cell's state changes (e.g. brick cracks or breaks).</summary>
    event Action<WallCellChanged> CellChanged;
}
