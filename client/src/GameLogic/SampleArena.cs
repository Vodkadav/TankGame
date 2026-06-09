using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A small hand-built arena bundled so the map browser always has one custom map to play (and
/// so the save/list/load round trip is exercised end-to-end). A steel-ringed 16×12 field with a little
/// brick cover, two enemy spawns, and a couple of powerups — all on reachable open floor. Pure C#.
/// </summary>
public static class SampleArena
{
    public static MapDefinition Build()
    {
        var map = MapDefinition.CreateBlank("Sample Skirmish", 16, 12);

        // A few brick clumps for cover — none seal off a region (validation enforces reachability).
        map.Materials[6, 4] = CellMaterial.Brick;
        map.Materials[6, 5] = CellMaterial.Brick;
        map.Materials[9, 6] = CellMaterial.Brick;
        map.Materials[9, 7] = CellMaterial.Brick;
        map.Bushes[3, 8] = true;
        map.Bushes[12, 3] = true;

        return new MapDefinition(
            map.Name,
            map.Materials,
            map.Bushes,
            map.Sandbags,
            (1, 1),
            new (int X, int Y)[] { (14, 10), (14, 1) },
            new[]
            {
                new PowerupSpawn(PowerupKind.Repair, 8, 6),
                new PowerupSpawn(PowerupKind.Shield, 3, 4),
            });
    }
}
