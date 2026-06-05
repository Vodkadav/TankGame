using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The sandbags of a level as an <see cref="ITerrain"/>: a tank on a sandbag cell moves at
/// <see cref="SlowFactor"/> of its speed. Shares the tile size and origin of the
/// <see cref="GridArena"/> so cell lookups line up with the walls. Sandbags are passable — they only
/// slow, never block. Pure C# — no Godot.</summary>
public sealed class SandbagField : ITerrain
{
    /// <summary>Speed multiplier for a tank crossing sandbags — slow going.</summary>
    public const float SlowFactor = 0.5f;

    private readonly bool[,] _sandbags;
    private readonly int _width;
    private readonly int _height;
    private readonly float _tileSize;
    private readonly Vector2 _origin;

    /// <param name="sandbags">Sandbag flags indexed <c>[x, y]</c>.</param>
    /// <param name="tileSize">World-space size of one square tile.</param>
    /// <param name="origin">World position of cell (0,0)'s minimum corner.</param>
    public SandbagField(bool[,] sandbags, float tileSize, Vector2 origin)
    {
        _sandbags = sandbags;
        _width = sandbags.GetLength(0);
        _height = sandbags.GetLength(1);
        _tileSize = tileSize;
        _origin = origin;
    }

    public float SpeedFactorAt(Vector2 point)
    {
        var local = point - _origin;
        var cellX = (int)MathF.Floor(local.X / _tileSize);
        var cellY = (int)MathF.Floor(local.Y / _tileSize);
        if (cellX < 0 || cellY < 0 || cellX >= _width || cellY >= _height)
        {
            return 1f;
        }

        return _sandbags[cellX, cellY] ? SlowFactor : 1f;
    }
}
