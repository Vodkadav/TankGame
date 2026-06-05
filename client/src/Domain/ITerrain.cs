using System.Numerics;

namespace TankGame.Domain;

/// <summary>How the ground underfoot affects a tank's movement. A tank multiplies its speed by
/// <see cref="SpeedFactorAt"/> for its current position, so terrain like sandbags can slow a tank
/// crossing it. 1 is normal ground; below 1 is slow going. Pure query — terrain never blocks
/// movement or shots, it only changes how fast a tank moves. Runs identically client- and
/// server-side later.</summary>
public interface ITerrain
{
    /// <summary>The speed multiplier for a tank at <paramref name="point"/> (1 = normal).</summary>
    float SpeedFactorAt(Vector2 point);
}
