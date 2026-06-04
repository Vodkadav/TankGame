using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A scattergun: fires <c>count</c> straight pellets fanned symmetrically about the aim,
/// <c>spreadRadians</c> apart. Spread is a firing *pattern*, not a projectile behaviour — each
/// pellet is an ordinary straight shot, just aimed at its own angle.</summary>
public sealed class SpreadWeapon : IWeapon
{
    private readonly int _count;
    private readonly float _spreadRadians;

    public SpreadWeapon(int count, float spreadRadians)
    {
        _count = count;
        _spreadRadians = spreadRadians;
    }

    public void Fire(IWorld world, IArena arena, Vector2 muzzle, Vector2 direction, float speed, int team)
    {
        var baseAngle = MathF.Atan2(direction.Y, direction.X);
        var firstOffset = -_spreadRadians * (_count - 1) / 2f; // centre the fan on the aim

        for (var i = 0; i < _count; i++)
        {
            var angle = baseAngle + firstOffset + (_spreadRadians * i);
            var pelletDir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            world.Spawn(new Projectile(arena, muzzle, pelletDir, speed, team: team));
        }
    }
}
