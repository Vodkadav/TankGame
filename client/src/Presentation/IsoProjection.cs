using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>The isometric projection shared by every view — the Phase-1 step of the flat top-down →
/// 2D isometric pivot. Pure grid↔screen maths over a flat world position: GameLogic stays a flat grid
/// and is untouched; isometric is entirely a Presentation concern. A 2:1 dimetric diamond — one world
/// tile maps to a cell twice as wide as it is tall. The core round-trip carries no Godot dependency so
/// it is unit-testable; <see cref="ScreenTransform"/> is the same map as a Godot affine for shearing a
/// whole square-laid layer at once.</summary>
public static class IsoProjection
{
    // Half a tile across in screen-X per world unit, a quarter in screen-Y — the 2:1 dimetric ratio.
    private const float AxisX = 0.5f;
    private const float AxisY = 0.25f;

    // Godot's canvas Z range; depth keys are clamped into it so a huge map can't overflow ZIndex.
    private const int ZMax = 4096;

    /// <summary>Projects a flat world position to its isometric screen position.</summary>
    public static NVector2 WorldToScreen(NVector2 world) =>
        new((world.X - world.Y) * AxisX, (world.X + world.Y) * AxisY);

    /// <summary>The inverse of <see cref="WorldToScreen"/> — the flat world point under a screen
    /// position (used to aim the mouse at a world location). Being linear, it maps deltas too.</summary>
    public static NVector2 ScreenToWorld(NVector2 screen) =>
        new(screen.X + (2f * screen.Y), (2f * screen.Y) - screen.X);

    /// <summary>Back-to-front depth key: greater <c>x+y</c> is nearer the camera and draws over
    /// farther things. Fed to a view's <c>ZIndex</c> each frame.</summary>
    public static int DepthOf(NVector2 world) =>
        Mathf.Clamp((int)(world.X + world.Y), -ZMax, ZMax);

    /// <summary>The same projection as a Godot affine transform, for shearing a whole square-laid
    /// layer (the wall tilemap, the ground, the terrain overlays) into iso in one step — its children
    /// keep their flat local positions and render projected.</summary>
    public static Transform2D ScreenTransform =>
        new(new Vector2(AxisX, AxisY), new Vector2(-AxisX, AxisY), Vector2.Zero);
}
