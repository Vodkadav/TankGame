using System;
using Godot;
using TankGame.Domain;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Infrastructure;

/// <summary>Keyboard + mouse input for the 3D world (ADR-0017): WASD to move, the mouse to aim, space
/// (and optionally left-click) to fire. Aim is a ray from the camera through the mouse onto the ground
/// plane — the turret points at the world point under the cursor, exactly, with no projection fudge.
/// Movement is rotated by the camera yaw so "W" drives up-screen.</summary>
/// <param name="camera">The 3D camera the aim ray is cast from; it follows and centres the player.</param>
/// <param name="fireOnClick">Whether left-click also fires (off in two-player, where it is Player 2's).</param>
public sealed class KeyboardMouse3DInputSource(Camera3D camera, bool fireOnClick = true) : IInputSource
{
    // Rotates WASD into the world so the controls read relative to the angled camera. Eyeball-tunable.
    private const float MoveYawDeg = 45f;

    public TankInput Read()
    {
        var raw = KeyboardMouseInputSource.ReadMove(
            up: Input.IsPhysicalKeyPressed(Key.W),
            down: Input.IsPhysicalKeyPressed(Key.S),
            left: Input.IsPhysicalKeyPressed(Key.A),
            right: Input.IsPhysicalKeyPressed(Key.D));
        var move = Rotate(raw, Mathf.DegToRad(MoveYawDeg));

        var fire = Input.IsPhysicalKeyPressed(Key.Space)
            || (fireOnClick && Input.IsMouseButtonPressed(MouseButton.Left));

        return new TankInput(move, ComputeAim(), fire);
    }

    // The aim angle (game radians) toward the ground point under the cursor, relative to the ground point
    // under the screen centre (where the camera keeps the player). Game (x,y) maps to world (x,z), so the
    // angle is atan2(dz, dx).
    private float ComputeAim()
    {
        var viewport = camera.GetViewport();
        var size = viewport.GetVisibleRect().Size;
        var underMouse = GroundHit(viewport.GetMousePosition());
        var underCentre = GroundHit(size * 0.5f);
        if (underMouse is not { } hit || underCentre is not { } centre)
        {
            return 0f;
        }

        var d = hit - centre;
        return MathF.Atan2(d.Z, d.X);
    }

    private Vector3? GroundHit(Vector2 screen)
    {
        var origin = camera.ProjectRayOrigin(screen);
        var normal = camera.ProjectRayNormal(screen);
        if (Mathf.Abs(normal.Y) < 1e-5f)
        {
            return null; // ray parallel to the ground
        }

        var t = -origin.Y / normal.Y;
        return origin + (normal * t);
    }

    private static NVector2 Rotate(NVector2 v, float radians)
    {
        var (s, c) = (MathF.Sin(radians), MathF.Cos(radians));
        return new NVector2((v.X * c) - (v.Y * s), (v.X * s) + (v.Y * c));
    }
}
