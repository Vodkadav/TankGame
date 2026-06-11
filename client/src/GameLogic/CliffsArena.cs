using System.Collections.Generic;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The result of building the Cliffs &amp; Valleys map (ADR-0018): the <see cref="LevelMap"/>
/// (which carries the per-cell elevation layers and ramps) plus where its spawns and pickups sit, so
/// the scene wires it the same way it does a <see cref="GeneratedArena"/>.</summary>
public sealed record CliffsLayout(
    LevelMap Map,
    (int X, int Y) PlayerSpawn,
    IReadOnlyList<(int X, int Y)> EnemySpawns,
    IReadOnlyList<(PowerupKind Kind, int X, int Y)> Powerups,
    bool[,] Sandbags,
    IReadOnlyList<TeleportPadLink> Pads);

/// <summary>The hand-authored "Cliffs &amp; Valleys" themed map (ADR-0018 step 3): a steel-ringed
/// field whose centre is a raised layer-1 plateau, reached by ramps on each side. Tanks on the valley
/// floor (layer 0) cannot shoot or drive onto the plateau except across a ramp; tanks up top hold the
/// high ground. Symmetric so neither side starts with the height advantage. Pure C# — produces a
/// <see cref="LevelMap"/> the arena and 3D view consume; the elevation engine (layer-aware
/// <see cref="GridArena"/>, ramp transitions in <see cref="Tank"/>) does the rest.</summary>
public static class CliffsArena
{
    private const int Width = 20;
    private const int Height = 16;

    // The raised plateau is a rectangle of layer-1 floor in the middle of the field. Ramps sit on its
    // four mid-edges (at layer 0) so a tank can climb up from any side.
    private const int PlateauMinX = 7;
    private const int PlateauMaxX = 12; // inclusive
    private const int PlateauMinY = 5;
    private const int PlateauMaxY = 10; // inclusive

    public static CliffsLayout Create()
    {
        var materials = new CellMaterial[Width, Height];
        var bushes = new bool[Width, Height];
        var sandbags = new bool[Width, Height];
        var layers = new int[Width, Height];
        var ramps = new bool[Width, Height];

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                materials[x, y] = IsBorder(x, y) ? CellMaterial.Steel : CellMaterial.Floor;
            }
        }

        // Raise the plateau interior to layer 1 (floor up top). Its edge cells become a cliff to any
        // tank below — a wall the engine derives from the layer mismatch, no material needed.
        for (var x = PlateauMinX; x <= PlateauMaxX; x++)
        {
            for (var y = PlateauMinY; y <= PlateauMaxY; y++)
            {
                layers[x, y] = 1;
            }
        }

        // A ramp on the middle of each plateau edge: a layer-0 floor cell flagged as a ramp, so it
        // connects the valley (0) and the plateau (1) — drive onto it to change layer. Placed just
        // outside the plateau, in line with a plateau floor cell, so the climb is continuous.
        AddRamp(layers, ramps, (PlateauMinX + PlateauMaxX) / 2, PlateauMinY - 1); // north
        AddRamp(layers, ramps, (PlateauMinX + PlateauMaxX) / 2, PlateauMaxY + 1); // south
        AddRamp(layers, ramps, PlateauMinX - 1, (PlateauMinY + PlateauMaxY) / 2); // west
        AddRamp(layers, ramps, PlateauMaxX + 1, (PlateauMinY + PlateauMaxY) / 2); // east

        // A little destructible cover in the valley corners and a bush hide-spot each side — none of it
        // seals a region (every floor cell stays reachable, asserted in the tests).
        materials[3, 3] = CellMaterial.Brick;
        materials[3, 4] = CellMaterial.Brick;
        materials[Width - 4, Height - 4] = CellMaterial.Brick;
        materials[Width - 4, Height - 5] = CellMaterial.Brick;
        bushes[3, Height - 3] = true;
        bushes[Width - 4, 2] = true;

        var playerSpawn = (1, 1);
        var enemySpawns = new[] { (Width - 2, Height - 2), (Width - 2, 1), (1, Height - 2) };

        // One pickup on the high ground (a prize for taking the plateau) and the rest spread round the
        // valley so the fight flows between floors.
        var powerups = new[]
        {
            (PowerupKind.Repair, (PlateauMinX + PlateauMaxX) / 2, (PlateauMinY + PlateauMaxY) / 2), // atop the plateau
            (PowerupKind.Shield, 2, Height / 2),
            (PowerupKind.Missile, Width - 3, Height / 2),
            (PowerupKind.SpeedBoost, Width / 2, 2),
            (PowerupKind.RapidFire, Width / 2, Height - 3),
        };

        // A cross-layer teleport pad pair (T3): a valley corner pad warps straight up onto the plateau
        // (and back), a flanking route past the defended ramps. Each pad's layer is derived from the
        // cell it sits on, so the link data stays plain cells.
        var pads = new[] { new TeleportPadLink(2, 2, 11, 9) };

        var map = LevelMap.FromCells(materials, bushes, playerSpawn.Item1, playerSpawn.Item2, layers, ramps);
        return new CliffsLayout(map, playerSpawn, enemySpawns, powerups, sandbags, pads);
    }

    private static void AddRamp(int[,] layers, bool[,] ramps, int x, int y)
    {
        layers[x, y] = 0; // a ramp sits on the low side and connects up to LayerAt + 1
        ramps[x, y] = true;
    }

    private static bool IsBorder(int x, int y) => x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
}
