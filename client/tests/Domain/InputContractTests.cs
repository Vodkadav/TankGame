using System.Numerics;
using TankGame.Domain;
using Xunit;

namespace TankGame.Tests.Domain;

public class InputContractTests
{
    private sealed class StubInputSource(TankInput input) : IInputSource
    {
        public TankInput Read() => input;
    }

    [Fact]
    public void Read_ReturnsTheProvidedInput()
    {
        var input = new TankInput(Move: new Vector2(1f, 0f), Aim: 1.5f, Fire: true);

        IInputSource source = new StubInputSource(input);

        Assert.Equal(input, source.Read());
    }

    [Fact]
    public void TankInput_CarriesMoveAimAndFire()
    {
        var input = new TankInput(new Vector2(0f, -1f), 3.14f, Fire: false);

        Assert.Equal(new Vector2(0f, -1f), input.Move);
        Assert.Equal(3.14f, input.Aim);
        Assert.False(input.Fire);
    }

    [Fact]
    public void TankInput_HasValueEquality()
    {
        var a = new TankInput(new Vector2(1f, 2f), 0.5f, true);
        var b = new TankInput(new Vector2(1f, 2f), 0.5f, true);

        Assert.Equal(a, b);
    }
}
