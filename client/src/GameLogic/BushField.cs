using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The bushes of a level as an <see cref="IConcealment"/>: a world-space point is
/// concealing when it falls on a bush cell. Shares the tile size and origin of the
/// <see cref="GridArena"/> so cell lookups line up with walls. Pure C# — no Godot.</summary>
public sealed class BushField : IConcealment
{
    private readonly bool[,] _bushes;
    private readonly int _width;
    private readonly int _height;
    private readonly float _tileSize;
    private readonly Vector2 _origin;

    /// <param name="bushes">Bush flags indexed <c>[x, y]</c> (e.g. <see cref="LevelMap.Bushes"/>).</param>
    /// <param name="tileSize">World-space size of one square tile.</param>
    /// <param name="origin">World position of cell (0,0)'s minimum corner.</param>
    public BushField(bool[,] bushes, float tileSize, Vector2 origin)
    {
        _bushes = bushes;
        _width = bushes.GetLength(0);
        _height = bushes.GetLength(1);
        _tileSize = tileSize;
        _origin = origin;
    }

    public bool ConcealsAt(Vector2 point)
    {
        var local = point - _origin;
        var cellX = (int)MathF.Floor(local.X / _tileSize);
        var cellY = (int)MathF.Floor(local.Y / _tileSize);
        if (cellX < 0 || cellY < 0 || cellX >= _width || cellY >= _height)
        {
            return false;
        }

        return _bushes[cellX, cellY];
    }
}
