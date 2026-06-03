using System;
using Godot;
using TankGame.Domain;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Infrastructure;

/// <summary>Keyboard + mouse implementation of <see cref="IInputSource"/> for Player 1: WASD
/// to move, the mouse to aim, space (and optionally left-click) to fire. Aim is taken
/// relative to the viewport centre (the camera keeps the tank centred). The pure helpers
/// below carry the logic so it is unit-testable without a Godot runtime.</summary>
/// <param name="viewport">The viewport whose centre and mouse position drive aim.</param>
/// <param name="fireOnClick">Whether left-click also fires. Off in two-player, where the
/// left mouse button belongs to Player 2 — then Player 1 fires with space only.</param>
public sealed class KeyboardMouseInputSource(Viewport viewport, bool fireOnClick = true) : IInputSource
{
    public TankInput Read()
    {
        var move = ReadMove(
            up: Input.IsPhysicalKeyPressed(Key.W),
            down: Input.IsPhysicalKeyPressed(Key.S),
            left: Input.IsPhysicalKeyPressed(Key.A),
            right: Input.IsPhysicalKeyPressed(Key.D));

        var size = viewport.GetVisibleRect().Size;
        var mouse = viewport.GetMousePosition();
        var aim = ComputeAim(new NVector2(mouse.X, mouse.Y), new NVector2(size.X, size.Y));

        var fire = Input.IsPhysicalKeyPressed(Key.Space)
            || (fireOnClick && Input.IsMouseButtonPressed(MouseButton.Left));

        return new TankInput(move, aim, fire);
    }

    /// <summary>Movement vector in Godot screen space (+Y down): up is -Y, right is +X.
    /// Not normalised — the tank clamps diagonal speed.</summary>
    public static NVector2 ReadMove(bool up, bool down, bool left, bool right)
    {
        var move = NVector2.Zero;
        if (up) { move.Y -= 1f; }
        if (down) { move.Y += 1f; }
        if (left) { move.X -= 1f; }
        if (right) { move.X += 1f; }
        return move;
    }

    /// <summary>Aim angle (radians) from the viewport centre toward the mouse — valid while
    /// the camera keeps the tank centred.</summary>
    public static float ComputeAim(NVector2 mouse, NVector2 viewportSize)
    {
        var centre = viewportSize * 0.5f;
        return MathF.Atan2(mouse.Y - centre.Y, mouse.X - centre.X);
    }
}
