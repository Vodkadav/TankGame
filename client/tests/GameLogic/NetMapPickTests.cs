using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class NetMapPickTests
{
    [Fact]
    public void ExplicitBuiltInIds_ResolveToTheirMaps()
    {
        Assert.IsType<NetMapPick.Cliffs>(NetMapPick.Resolve("CliffsAndValleys", "ABC123"));
        Assert.IsType<NetMapPick.Desert>(NetMapPick.Resolve("DesertWar", "ABC123"));
    }

    [Fact]
    public void TheDesertSeed_DerivesFromTheLobbyCode_SoEveryMemberGeneratesTheSameArena()
    {
        var a = (NetMapPick.Desert)NetMapPick.Resolve("DesertWar", "ABC123");
        var b = (NetMapPick.Desert)NetMapPick.Resolve("DesertWar", "ABC123");
        var other = (NetMapPick.Desert)NetMapPick.Resolve("DesertWar", "XYZ789");

        Assert.Equal(a.Seed, b.Seed);
        Assert.NotEqual(a.Seed, other.Seed);
    }

    [Fact]
    public void Random_IsDeterministicPerLobbyCode()
    {
        Assert.Equal(NetMapPick.Resolve("", "ABC123"), NetMapPick.Resolve("", "ABC123"));
    }

    [Fact]
    public void RandomPool_IncludesTheThemedArenas_SoTheyAppearWithoutBeingPicked()
    {
        // Across many lobby codes the shared random draw must be able to land on a themed built-in, not
        // only Desert/Cliffs (ADD-7) — else the new maps are unreachable unless explicitly chosen.
        var sawThemed = false;
        for (var i = 0; i < 200 && !sawThemed; i++)
        {
            sawThemed = NetMapPick.Resolve("", $"CODE{i}") is NetMapPick.BuiltIn;
        }

        Assert.True(sawThemed, "the random map pool must include the themed built-in arenas");
    }

    [Fact]
    public void ACustomMapId_FallsBackToDesert_AGuestCannotBuildAMapItDoesNotHave()
    {
        Assert.IsType<NetMapPick.Desert>(NetMapPick.Resolve("custom:my-maze", "ABC123"));
    }

    [Theory]
    [InlineData("Forest")]
    [InlineData("Volcano")]
    [InlineData("City")]
    [InlineData("Frozen")]
    [InlineData("Canyon")]
    public void ARegisteredCodeArena_ResolvesToBuiltIn_SoEveryMemberBuildsIt(string arenaId)
    {
        var choice = Assert.IsType<NetMapPick.BuiltIn>(NetMapPick.Resolve(arenaId, "ABC123"));
        Assert.Equal(arenaId, choice.ArenaId);
    }
}
