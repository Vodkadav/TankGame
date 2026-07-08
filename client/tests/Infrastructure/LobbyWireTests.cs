using TankGame.Domain.Net;
using TankGame.Infrastructure.Net;
using Xunit;

namespace TankGame.Tests.Infrastructure;

// The wire parser shared by the .NET and Godot lobby clients — one reading of the Worker's JSON.
public class LobbyWireTests
{
    [Fact]
    public void ParseCreatedCode_ReadsTheMintedCode() =>
        Assert.Equal("CDEFGH", LobbyWire.ParseCreatedCode("{\"code\":\"CDEFGH\",\"wsUrl\":\"/room/CDEFGH\"}"));

    [Fact]
    public void ParseCreatedCode_ReturnsNull_OnGarbage() =>
        Assert.Null(LobbyWire.ParseCreatedCode("boom"));

    [Fact]
    public void ParseCreatedCode_ReturnsNull_WhenTheCodeIsMissing() =>
        Assert.Null(LobbyWire.ParseCreatedCode("{\"wsUrl\":\"/room/X\"}"));

    [Fact]
    public void ParseOpenLobbies_ReadsRows_IncludingAMissingMap()
    {
        var open = LobbyWire.ParseOpenLobbies(
            "[{\"code\":\"ABC123\",\"mode\":\"team\",\"players\":2,\"map\":\"DesertWar\"}," +
            "{\"code\":\"DEF456\",\"mode\":\"ffa\",\"players\":1}]");

        Assert.NotNull(open);
        Assert.Equal(new OpenLobbyInfo("ABC123", GameMode.Team, 2, "DesertWar"), open![0]);
        Assert.Equal(new OpenLobbyInfo("DEF456", GameMode.Ffa, 1, ""), open[1]);
    }

    [Fact]
    public void ParseOpenLobbies_ReturnsNull_OnGarbage() =>
        Assert.Null(LobbyWire.ParseOpenLobbies("boom"));

    [Fact]
    public void ParseOpenLobbies_ReturnsNull_WhenTheBodyIsNotAList() =>
        Assert.Null(LobbyWire.ParseOpenLobbies("{\"code\":\"X\"}"));
}
