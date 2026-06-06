using Godot;

namespace TankGame.Presentation;

/// <summary>Picks the directional frame for a facing angle from an N-way isometric sprite strip. The
/// iso tank's hull and turret are each pre-rendered as N frames evenly spaced around the compass; this
/// snaps a continuous facing (radians) to the nearest of those frames. Pure maths, wrapping cleanly for
/// any angle, so the chassis heading and the independent turret aim each select their own frame.</summary>
public static class IsoSpriteFacing
{
    /// <summary>The frame index in <c>[0, facings)</c> whose evenly-spaced direction is nearest the
    /// given angle. Frame 0 is angle 0; the index increases with the angle and wraps.</summary>
    public static int FrameIndex(float radians, int facings)
    {
        var step = Mathf.Tau / facings;
        var index = Mathf.RoundToInt(radians / step);
        return ((index % facings) + facings) % facings;
    }
}
