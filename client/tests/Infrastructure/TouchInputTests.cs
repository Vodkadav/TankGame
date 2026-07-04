using System;
using TankGame.Infrastructure;
using Xunit;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Tests.Infrastructure;

// Exercises the pure twin-stick maths behind TouchInput3DSource without a Godot runtime. The camera
// basis is the identity ground frame (forward = world +Z, right = world +X) so the mapping is direct.
public class TouchInputTests
{
    private static readonly NVector2 Forward = new(0f, 1f); // game (x,y) → world (x,z); +Z
    private static readonly NVector2 Right = new(1f, 0f);    // +X

    private static (NVector2 Move, float Aim, bool Fire) Resolve(NVector2 move, NVector2 aim, float lastAim = 0f)
        => TouchInput3DSource.Resolve(move, aim, Forward, Right, lastAim,
            TouchInput3DSource.MoveDeadzone, TouchInput3DSource.FireThreshold);

    [Fact]
    public void Move_PushRight_DrivesAlongWorldX()
        => Assert.Equal(new NVector2(1f, 0f), Resolve(new NVector2(1f, 0f), NVector2.Zero).Move);

    [Fact]
    public void Move_PushUpScreen_DrivesForward()
    {
        // Screen up is −Y; it must map to the camera forward (world +Z), i.e. game (0, 1).
        var move = Resolve(new NVector2(0f, -1f), NVector2.Zero).Move;
        Assert.Equal(0f, move.X, precision: 4);
        Assert.Equal(1f, move.Y, precision: 4);
    }

    [Fact]
    public void Move_WithinDeadzone_ReadsIdle()
        => Assert.Equal(NVector2.Zero, Resolve(new NVector2(0.1f, 0f), NVector2.Zero).Move);

    [Fact]
    public void Aim_PushRight_FiresPointingAlongPositiveX()
    {
        var (_, aim, fire) = Resolve(NVector2.Zero, new NVector2(1f, 0f));
        Assert.True(fire);
        Assert.Equal(0f, aim, precision: 4);
    }

    [Fact]
    public void Aim_PushUpScreen_PointsForward()
    {
        // Up-screen aim (−Y) → world +Z → atan2(1, 0) = +π/2.
        var (_, aim, fire) = Resolve(NVector2.Zero, new NVector2(0f, -1f));
        Assert.True(fire);
        Assert.Equal(MathF.PI / 2f, aim, precision: 4);
    }

    [Fact]
    public void Aim_StickIdle_HoldsLastAimAndDoesNotFire()
    {
        var (_, aim, fire) = Resolve(NVector2.Zero, new NVector2(0.1f, 0f), lastAim: 1.23f);
        Assert.False(fire);
        Assert.Equal(1.23f, aim, precision: 4);
    }

    [Fact]
    public void StickOutput_WithinRadius_ScalesProportionally()
        => Assert.Equal(new NVector2(0.25f, 0f),
            TouchInput3DSource.StickOutput(new NVector2(110f, 100f), new NVector2(100f, 100f), radius: 40f));

    [Fact]
    public void StickOutput_BeyondRadius_SaturatesToUnit()
    {
        var output = TouchInput3DSource.StickOutput(new NVector2(300f, 100f), new NVector2(100f, 100f), radius: 40f);
        Assert.Equal(1f, output.Length(), precision: 4);
        Assert.Equal(0f, output.Y, precision: 4);
    }

    [Fact]
    public void StickOutput_AtBase_IsZero()
        => Assert.Equal(NVector2.Zero,
            TouchInput3DSource.StickOutput(new NVector2(100f, 100f), new NVector2(100f, 100f), radius: 40f));
}
