using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A locally-driven tank. Pure C#: reads an <see cref="IInputSource"/> each
/// <see cref="Step"/>, moves at a constant speed, faces its movement direction, and aims
/// its turret at the input. No Godot — the Presentation layer renders this state.</summary>
public sealed class Tank : ITank
{
    private readonly IInputSource _input;
    private readonly float _speed;

    /// <param name="input">Per-frame intent source.</param>
    /// <param name="startPosition">Initial world-space position.</param>
    /// <param name="speed">Movement speed in units per second.</param>
    public Tank(IInputSource input, Vector2 startPosition, float speed)
    {
        _input = input;
        Position = startPosition;
        _speed = speed;
    }

    public Vector2 Position { get; private set; }
    public float Rotation { get; private set; }
    public float TurretRotation { get; private set; }

    public void Step(float deltaSeconds)
    {
        var input = _input.Read();

        var move = input.Move;
        if (move.LengthSquared() > 1f)
        {
            move = Vector2.Normalize(move); // a (1,1) keyboard diagonal must not be faster
        }

        Position += move * _speed * deltaSeconds;

        if (move != Vector2.Zero)
        {
            Rotation = MathF.Atan2(move.Y, move.X); // chassis keeps its last facing when idle
        }

        TurretRotation = input.Aim;
    }
}
