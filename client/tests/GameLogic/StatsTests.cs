using System.Collections.Generic;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class StatsTests
{
    private static Stats SpeedStats(float baseSpeed = 100f) =>
        new(new Dictionary<StatKind, float> { [StatKind.Speed] = baseSpeed });

    [Fact]
    public void Current_WithNoEffects_IsTheBaseValue()
    {
        var stats = SpeedStats(120f);

        Assert.Equal(120f, stats.Current(StatKind.Speed));
    }

    [Fact]
    public void AMultiplierEffect_ScalesTheStat()
    {
        var stats = SpeedStats(100f);

        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 1.5f, AddFlat: 0f, Seconds: 5f));

        Assert.Equal(150f, stats.Current(StatKind.Speed));
    }

    [Fact]
    public void AFlatEffect_AddsToTheStat()
    {
        var stats = SpeedStats(100f);

        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 1f, AddFlat: 40f, Seconds: 5f));

        Assert.Equal(140f, stats.Current(StatKind.Speed));
    }

    [Fact]
    public void Clear_DropsEveryEffect_BackToTheBaseValue()
    {
        var stats = SpeedStats(100f);
        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: float.PositiveInfinity));

        stats.Clear();

        Assert.Equal(100f, stats.Current(StatKind.Speed)); // a permanent boost is gone after a clear
    }

    [Fact]
    public void FlatAndMultiplier_Compose_AsFlatThenScale()
    {
        var stats = SpeedStats(100f);

        // (100 + 20) * 1.5 = 180
        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 1.5f, AddFlat: 20f, Seconds: 5f));

        Assert.Equal(180f, stats.Current(StatKind.Speed));
    }

    [Fact]
    public void MultipleEffectsOnAStat_Stack()
    {
        var stats = SpeedStats(100f);

        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 5f));
        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 1.5f, AddFlat: 10f, Seconds: 5f));

        // (100 + 10) * (2 * 1.5) = 330
        Assert.Equal(330f, stats.Current(StatKind.Speed));
    }

    [Fact]
    public void AnEffectOnOneStat_DoesNotTouchAnother()
    {
        var stats = new Stats(new Dictionary<StatKind, float>
        {
            [StatKind.Speed] = 100f,
            [StatKind.FireInterval] = 0.3f,
        });

        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 5f));

        Assert.Equal(0.3f, stats.Current(StatKind.FireInterval));
    }

    [Fact]
    public void Apply_SameStatAndSource_Replaces_InsteadOfStacking()
    {
        var stats = SpeedStats(100f);

        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 1.6f, AddFlat: 0f, Seconds: 15f, Source: "SpeedBoost"));
        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 1.6f, AddFlat: 0f, Seconds: 15f, Source: "SpeedBoost"));

        Assert.Equal(160f, stats.Current(StatKind.Speed)); // 1.6x once, not 1.6 * 1.6
    }

    [Fact]
    public void Apply_SameSource_RefreshesTheDuration()
    {
        var stats = SpeedStats(100f);
        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 15f, Source: "SpeedBoost"));

        stats.Step(10f);
        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 15f, Source: "SpeedBoost"));

        stats.Step(10f); // 20s after the first apply — only alive because the re-apply reset the clock
        Assert.Equal(200f, stats.Current(StatKind.Speed));

        stats.Step(6f); // 16s after the refresh — now expired
        Assert.Equal(100f, stats.Current(StatKind.Speed));
    }

    [Fact]
    public void Apply_SameStat_DifferentSources_StillStack()
    {
        var stats = SpeedStats(100f);

        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 15f, Source: "SpeedBoost"));
        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 1.5f, AddFlat: 0f, Seconds: 15f, Source: "Blitz"));

        Assert.Equal(300f, stats.Current(StatKind.Speed));
    }

    [Fact]
    public void Apply_WithoutASource_KeepsTheOldAppendBehaviour()
    {
        var stats = SpeedStats(100f);

        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 5f));
        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 5f));

        Assert.Equal(400f, stats.Current(StatKind.Speed)); // sourceless effects still stack
    }

    [Fact]
    public void AnEffect_ExpiresAfterItsDuration_AndTheStatReturnsToBase()
    {
        var stats = SpeedStats(100f);
        stats.Apply(new StatusEffect(StatKind.Speed, Mult: 2f, AddFlat: 0f, Seconds: 1f));

        stats.Step(0.6f);
        Assert.Equal(200f, stats.Current(StatKind.Speed)); // still active mid-life

        stats.Step(0.6f); // total 1.2s > 1s duration
        Assert.Equal(100f, stats.Current(StatKind.Speed)); // expired
    }
}
