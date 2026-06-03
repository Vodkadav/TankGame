using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>An empty rectangular arena bounded by four walls. Pure C#: a ray cast from
/// inside the rectangle exits through the nearest wall (slab method). This is M1's
/// "empty room with placeholder walls"; M2 replaces it with a destructible wall grid.</summary>
public sealed class RectArena(Vector2 min, Vector2 max) : IArena
{
    public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance)
    {
        var dir = Vector2.Normalize(direction);
        var exit = float.PositiveInfinity;

        if (dir.X != 0f)
        {
            var t = ((dir.X > 0f ? max.X : min.X) - origin.X) / dir.X;
            if (t >= 0f)
            {
                exit = MathF.Min(exit, t);
            }
        }

        if (dir.Y != 0f)
        {
            var t = ((dir.Y > 0f ? max.Y : min.Y) - origin.Y) / dir.Y;
            if (t >= 0f)
            {
                exit = MathF.Min(exit, t);
            }
        }

        if (float.IsInfinity(exit) || exit > maxDistance)
        {
            return null;
        }

        return new RaycastHit(origin + (dir * exit), exit);
    }

    // The bounding walls are indestructible, so there is nothing to damage.
    public void DamageAt(Vector2 point, Vector2 direction, int amount)
    {
    }

    // Anything on or outside the rectangle's edges is wall.
    public bool IsBlocked(Vector2 point) =>
        point.X <= min.X || point.X >= max.X || point.Y <= min.Y || point.Y >= max.Y;
}
