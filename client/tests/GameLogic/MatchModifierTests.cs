using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class MatchModifierTests
{
    private sealed class ScriptedInput(TankInput value) : IInputSource
    {
        public TankInput Read() => value;
    }

    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    private static Tank ForwardTank() =>
        new(new ScriptedInput(new TankInput(new Vector2(1f, 0f), Aim: 0f, Fire: false)),
            new World(), new OpenArena(), Vector2.Zero, speed: 100f, fireInterval: 0.3f, projectileSpeed: 600f);

    [Fact]
    public void None_AppliesNothing()
    {
        var plain = ForwardTank();
        var unmodified = ForwardTank();
        MatchModifier.None.ApplyTo(unmodified);

        plain.Step(0.1f);
        unmodified.Step(0.1f);

        Assert.Equal(plain.Position.X, unmodified.Position.X, precision: 4);
    }

    [Fact]
    public void StartingSpeedEffect_MakesEveryTankFaster_ForTheWholeMatch()
    {
        var modifier = new MatchModifier(new[] { MatchModifier.Permanent(StatKind.Speed, mult: 2f) });
        var plain = ForwardTank();
        var boosted = ForwardTank();
        modifier.ApplyTo(boosted);

        // Step well past any ordinary powerup duration: a permanent effect must still be live.
        for (var i = 0; i < 100; i++)
        {
            plain.Step(0.1f);
            boosted.Step(0.1f);
        }

        Assert.Equal(plain.Position.X * 2f, boosted.Position.X, precision: 2);
    }

    [Fact]
    public void Blitz_IsARealPreset_ThatBuffsTanks()
    {
        var plain = ForwardTank();
        var blitzed = ForwardTank();
        MatchModifier.Blitz.ApplyTo(blitzed);

        plain.Step(0.1f);
        blitzed.Step(0.1f);

        Assert.True(blitzed.Position.X > plain.Position.X,
            $"Blitz should make tanks faster; plain={plain.Position.X}, blitz={blitzed.Position.X}.");
    }
}
