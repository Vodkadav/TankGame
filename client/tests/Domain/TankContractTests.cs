using System;
using System.Numerics;
using TankGame.Domain;
using Xunit;

namespace TankGame.Tests.Domain;

public class TankContractTests
{
    // Minimal tank that drives along its input's Move vector and points its
    // turret at the input's Aim — just enough to exercise the contract.
    private sealed class StubTank(IInputSource input, float speed) : ITank
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Vector2 Position { get; private set; }
        public float Rotation { get; private set; }
        public float TurretRotation { get; private set; }
        public int Hp { get; private set; } = 1;
        public int MaxHp => 1;
        public int Team => 0;
        public int Shield => 0;
        public bool IsAlive => Hp > 0;
        public void TakeDamage(int amount) => Hp = System.Math.Max(0, Hp - amount);

        public void Step(float deltaSeconds)
        {
            var i = input.Read();
            Position += i.Move * speed * deltaSeconds;
            if (i.Move != Vector2.Zero)
            {
                Rotation = MathF.Atan2(i.Move.Y, i.Move.X);
            }

            TurretRotation = i.Aim;
        }
    }

    private sealed class FixedInput(TankInput value) : IInputSource
    {
        public TankInput Read() => value;
    }

    [Fact]
    public void Step_AdvancesPositionAlongMoveInput()
    {
        var input = new FixedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false));
        ITank tank = new StubTank(input, speed: 100f);

        tank.Step(0.5f);

        Assert.Equal(new Vector2(50f, 0f), tank.Position);
    }

    [Fact]
    public void Altitude_DefaultsToTheLayer_AndGrounded()
    {
        // A tank that predates ledge drops (ADR-0020 Wave B) is always grounded: its altitude is
        // exactly its layer and it is never airborne — so every existing fake keeps working untouched.
        ITank tank = new StubTank(new FixedInput(new TankInput(Vector2.Zero, 0f, false)), speed: 0f);

        Assert.Equal(0f, tank.Altitude);
        Assert.False(tank.IsAirborne);
    }

    [Fact]
    public void Step_PointsTurretAtAim()
    {
        var input = new FixedInput(new TankInput(Vector2.Zero, Aim: 1.25f, Fire: false));
        ITank tank = new StubTank(input, speed: 100f);

        tank.Step(0.016f);

        Assert.Equal(1.25f, tank.TurretRotation);
        Assert.Equal(Vector2.Zero, tank.Position);
    }
}
