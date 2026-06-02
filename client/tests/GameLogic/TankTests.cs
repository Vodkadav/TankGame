using System;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class TankTests
{
    private sealed class ScriptedInput(TankInput value) : IInputSource
    {
        public TankInput Value { get; set; } = value;
        public TankInput Read() => Value;
    }

    private const float Speed = 100f;

    [Fact]
    public void Step_MovesAlongInput_AtSpeedOverTime()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        var tank = new Tank(input, Vector2.Zero, Speed);

        for (var i = 0; i < 10; i++)
        {
            tank.Step(0.1f); // 10 ticks * 0.1s = 1s at 100 u/s = 100 units
        }

        Assert.Equal(100f, tank.Position.X, precision: 3);
        Assert.Equal(0f, tank.Position.Y, precision: 3);
    }

    [Fact]
    public void Step_NormalisesDiagonalInput_SoSpeedIsConstant()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 1f), Aim: 0f, Fire: false));
        var tank = new Tank(input, Vector2.Zero, Speed);

        tank.Step(1f);

        // A raw (1,1) would travel ~141 units; normalised it travels exactly 100.
        Assert.Equal(100f, tank.Position.Length(), precision: 3);
    }

    [Fact]
    public void Step_SetsChassisRotationFromMovementDirection()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(0f, 1f), Aim: 0f, Fire: false));
        var tank = new Tank(input, Vector2.Zero, Speed);

        tank.Step(0.1f);

        Assert.Equal(MathF.Atan2(1f, 0f), tank.Rotation, precision: 4);
    }

    [Fact]
    public void Step_SetsTurretRotationFromAim()
    {
        var input = new ScriptedInput(new TankInput(Vector2.Zero, Aim: 2.0f, Fire: false));
        var tank = new Tank(input, Vector2.Zero, Speed);

        tank.Step(0.1f);

        Assert.Equal(2.0f, tank.TurretRotation, precision: 4);
    }

    [Fact]
    public void Step_WithNoMovement_KeepsPositionAndChassisRotation()
    {
        var input = new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        var tank = new Tank(input, Vector2.Zero, Speed);
        tank.Step(0.1f);
        var rotationAfterMove = tank.Rotation;
        var positionAfterMove = tank.Position;

        input.Value = new TankInput(Vector2.Zero, Aim: 0f, Fire: false);
        tank.Step(0.1f);

        Assert.Equal(positionAfterMove, tank.Position);
        Assert.Equal(rotationAfterMove, tank.Rotation); // chassis keeps last facing when idle
    }
}
