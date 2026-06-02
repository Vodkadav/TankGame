using System.Numerics;

namespace TankGame.Domain;

/// <summary>One frame of player intent, decoupled from any input device or engine.</summary>
/// <param name="Move">Desired movement direction; length 0..1 (zero = no movement).</param>
/// <param name="Aim">Turret aim angle in radians, world-space.</param>
/// <param name="Fire">True on a frame where firing is requested.</param>
public readonly record struct TankInput(Vector2 Move, float Aim, bool Fire);

/// <summary>Supplies the current <see cref="TankInput"/>. Implemented by keyboard/mouse
/// (Infrastructure) now and by a network source later, without touching tank logic.</summary>
public interface IInputSource
{
    TankInput Read();
}
