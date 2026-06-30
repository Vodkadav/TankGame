using System;
using Godot;
using TankGame.Domain;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Infrastructure;

/// <summary>Touch input for the 3D world (owner ask 2026-06-30): a twin-stick scheme for phones.
/// The left thumbstick drives, the right thumbstick aims the turret, and the tank auto-fires while the
/// right stick is held past a small threshold — so a child needs only two thumbs and no buttons. The
/// raw stick outputs are screen-space (Godot +Y down, magnitude 0..1) and come from the on-screen
/// <c>TouchControls</c> overlay through the two suppliers; both move and aim are mapped through the
/// camera basis so "push up" always means "up-screen" regardless of the camera yaw, exactly like
/// <see cref="KeyboardMouse3DInputSource"/>. The pure helpers below carry the maths so they are
/// unit-testable without a Godot runtime.</summary>
/// <param name="camera">The 3D camera whose yaw orients the sticks (it follows the player).</param>
/// <param name="moveStick">Supplies the left (drive) stick output; screen-space, magnitude 0..1.</param>
/// <param name="aimStick">Supplies the right (aim+fire) stick output; screen-space, magnitude 0..1.</param>
public sealed class TouchInput3DSource(Camera3D camera, Func<NVector2> moveStick, Func<NVector2> aimStick)
    : IInputSource
{
    // Below this the drive stick reads as idle, so a resting thumb does not creep the tank.
    public const float MoveDeadzone = 0.15f;
    // The right stick must be pushed at least this far to count as aiming-and-firing.
    public const float FireThreshold = 0.25f;

    private float _lastAim;

    public TankInput Read()
    {
        var basis = camera.GlobalTransform.Basis;
        var forward = Flatten(-basis.Z);
        var right = Flatten(basis.X);
        var (move, aim, fire) = Resolve(
            moveStick(), aimStick(),
            new NVector2(forward.X, forward.Z), new NVector2(right.X, right.Z),
            _lastAim, MoveDeadzone, FireThreshold);
        _lastAim = aim;
        return new TankInput(move, aim, fire);
    }

    /// <summary>Maps the two screen-space stick outputs onto a <see cref="TankInput"/> in game space.
    /// <paramref name="camForward"/>/<paramref name="camRight"/> are the camera's flattened forward/right
    /// on the ground plane (game x = world x, game y = world z). Screen up (−Y) drives forward. The tank
    /// auto-fires while the aim stick is past <paramref name="fireThreshold"/>; when it is idle the aim
    /// holds its last angle so the turret does not snap back.</summary>
    public static (NVector2 Move, float Aim, bool Fire) Resolve(
        NVector2 moveStick, NVector2 aimStick, NVector2 camForward, NVector2 camRight,
        float lastAim, float moveDeadzone, float fireThreshold)
    {
        var move = moveStick.Length() < moveDeadzone
            ? NVector2.Zero
            : (camRight * moveStick.X) + (camForward * -moveStick.Y);

        var fire = aimStick.Length() >= fireThreshold;
        var aim = lastAim;
        if (fire)
        {
            var dir = (camRight * aimStick.X) + (camForward * -aimStick.Y);
            aim = MathF.Atan2(dir.Y, dir.X); // game (x,y) → world (x,z); matches KeyboardMouse3D ComputeAim
        }

        return (move, aim, fire);
    }

    /// <summary>A floating thumbstick's output: the touch offset from the stick's base, clamped to a
    /// unit circle (magnitude 0..1, screen-space). A touch within <paramref name="radius"/> reads
    /// proportionally; beyond it saturates to a full-deflection unit vector.</summary>
    public static NVector2 StickOutput(NVector2 touch, NVector2 basePos, float radius)
    {
        var delta = touch - basePos;
        var length = delta.Length();
        if (length <= 1e-4f)
        {
            return NVector2.Zero;
        }

        return length <= radius ? delta / radius : delta / length;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.Y = 0f;
        return v.LengthSquared() > 1e-6f ? v.Normalized() : Vector3.Zero;
    }
}
