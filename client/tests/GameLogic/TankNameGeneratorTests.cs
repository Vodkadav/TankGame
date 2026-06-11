using System.Collections.Generic;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class TankNameGeneratorTests
{
    [Fact]
    public void Next_IsDeterministic_ForASeed()
    {
        var a = new TankNameGenerator(seed: 7);
        var b = new TankNameGenerator(seed: 7);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(a.Next(), b.Next());
        }
    }

    [Fact]
    public void Next_NeverRepeats_UntilThePoolIsExhausted()
    {
        var generator = new TankNameGenerator(seed: 3);
        var seen = new HashSet<string>();

        for (var i = 0; i < TankNameGenerator.PoolSize; i++)
        {
            var name = generator.Next();
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.True(seen.Add(name), $"'{name}' was handed out twice before the pool ran dry.");
        }
    }

    [Fact]
    public void Next_KeepsServingNames_AfterThePoolRunsDry()
    {
        var generator = new TankNameGenerator(seed: 11);
        for (var i = 0; i < TankNameGenerator.PoolSize; i++)
        {
            generator.Next();
        }

        Assert.False(string.IsNullOrWhiteSpace(generator.Next())); // an 8-tank match can never starve it
    }

    [Fact]
    public void DifferentSeeds_ShuffleTheOrder()
    {
        var a = new TankNameGenerator(seed: 1);
        var b = new TankNameGenerator(seed: 2);

        var anyDifference = false;
        for (var i = 0; i < 6; i++)
        {
            anyDifference |= a.Next() != b.Next();
        }

        Assert.True(anyDifference, "different seeds should deal the names in a different order");
    }
}
