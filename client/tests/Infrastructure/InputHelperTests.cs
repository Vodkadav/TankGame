using System;
using TankGame.Infrastructure;
using Xunit;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Tests.Infrastructure;

// Exercises the pure helpers behind KeyboardMouseInputSource without a Godot runtime.
public class InputHelperTests
{
    [Fact]
    public void ReadMove_Up_IsNegativeY()
        => Assert.Equal(new NVector2(0f, -1f), KeyboardMouseInputSource.ReadMove(up: true, down: false, left: false, right: false));

    [Fact]
    public void ReadMove_DownAndRight_CombinesAxes()
        => Assert.Equal(new NVector2(1f, 1f), KeyboardMouseInputSource.ReadMove(up: false, down: true, left: false, right: true));

    [Fact]
    public void ReadMove_OpposingKeys_Cancel()
        => Assert.Equal(NVector2.Zero, KeyboardMouseInputSource.ReadMove(up: true, down: true, left: true, right: true));

    [Fact]
    public void ReadMove_NothingPressed_IsZero()
        => Assert.Equal(NVector2.Zero, KeyboardMouseInputSource.ReadMove(false, false, false, false));

    [Fact]
    public void ComputeAim_MouseRightOfCentre_PointsNorthEastInWorld()
    {
        // viewport 200x100 -> centre (100,50); mouse a screen-step right. Inverting the iso
        // projection, a purely rightward screen offset is the world direction (1,-1) -> -π/4.
        var aim = KeyboardMouseInputSource.ComputeAim(new NVector2(180f, 50f), new NVector2(200f, 100f));
        Assert.Equal(-MathF.PI / 4f, aim, precision: 4);
    }

    [Fact]
    public void ComputeAim_MouseBelowCentre_PointsSouthEastInWorld()
    {
        // A purely downward screen offset inverts to the world direction (1,1) -> +π/4.
        var aim = KeyboardMouseInputSource.ComputeAim(new NVector2(100f, 90f), new NVector2(200f, 100f));
        Assert.Equal(MathF.PI / 4f, aim, precision: 4);
    }

    [Fact]
    public void AimFromMovement_FacesTheDriveDirection()
    {
        // Driving +X aims at 0; driving down (+Y) aims at +π/2.
        Assert.Equal(0f, Player2InputSource.AimFromMovement(new NVector2(1f, 0f), lastAim: 3f), precision: 4);
        Assert.Equal(MathF.PI / 2f, Player2InputSource.AimFromMovement(new NVector2(0f, 1f), lastAim: 0f), precision: 4);
    }

    [Fact]
    public void AimFromMovement_KeepsLastAim_WhenIdle()
        => Assert.Equal(1.23f, Player2InputSource.AimFromMovement(NVector2.Zero, lastAim: 1.23f), precision: 4);
}
