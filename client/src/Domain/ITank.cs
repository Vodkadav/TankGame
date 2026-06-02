using System.Numerics;

namespace TankGame.Domain;

/// <summary>A controllable tank. The implementation (GameLogic) is constructed with an
/// <see cref="IInputSource"/> and advances itself on <see cref="Step"/> — pure C#, no engine.</summary>
public interface ITank
{
    /// <summary>World-space position of the chassis centre.</summary>
    Vector2 Position { get; }

    /// <summary>Chassis facing, in radians (follows movement direction).</summary>
    float Rotation { get; }

    /// <summary>Turret facing, in radians (follows the aim input).</summary>
    float TurretRotation { get; }

    /// <summary>Advances the tank by <paramref name="deltaSeconds"/>, consuming its input source.</summary>
    void Step(float deltaSeconds);
}
