using System;
using Godot;
using TankGame.Domain;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Infrastructure;

/// <summary>Player 2's couch-co-op controls: the arrow keys to move, the turret aims where
/// the tank drives (no shared mouse for aim), and left-click or Enter to fire. Reuses Player
/// 1's pure move helper; the aim-from-movement helper is unit-testable without Godot.</summary>
public sealed class Player2InputSource : IInputSource
{
    private float _lastAim;

    public TankInput Read()
    {
        var move = KeyboardMouseInputSource.ReadMove(
            up: Input.IsPhysicalKeyPressed(Key.Up),
            down: Input.IsPhysicalKeyPressed(Key.Down),
            left: Input.IsPhysicalKeyPressed(Key.Left),
            right: Input.IsPhysicalKeyPressed(Key.Right));

        _lastAim = AimFromMovement(move, _lastAim);

        var fire = Input.IsMouseButtonPressed(MouseButton.Left)
            || Input.IsPhysicalKeyPressed(Key.Enter);

        return new TankInput(move, _lastAim, fire);
    }

    /// <summary>Turret angle from the movement direction; keeps the previous aim while idle so
    /// the turret does not snap back to zero when the player stops.</summary>
    public static float AimFromMovement(NVector2 move, float lastAim) =>
        move == NVector2.Zero ? lastAim : MathF.Atan2(move.Y, move.X);
}
