using System;
using System.Text;
using System.Text.Json;
using TankGame.Domain.Net;
using Xunit;

namespace TankGame.Tests.Domain;

// Parity for the JSON lobby channel: these byte/JSON shapes must match the worker's codec.ts +
// lobbyState.ts. The literals below are exactly what the worker's JSON.stringify(LobbyState) emits.
public class LobbyProtocolTests
{
    private static byte[] StateMessage(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var message = new byte[payload.Length + 1];
        message[0] = LobbyProtocol.MsgLobbyState;
        payload.CopyTo(message, 1);
        return message;
    }

    [Fact]
    public void ParseState_ReadsAWaitingFfaLobby()
    {
        var message = StateMessage(
            "{\"mode\":\"ffa\",\"phase\":\"waiting\",\"hostSlot\":0,\"countdown\":0," +
            "\"players\":[{\"slot\":0,\"name\":\"Ada\",\"team\":0,\"ready\":false}," +
            "{\"slot\":1,\"name\":\"Bea\",\"team\":1,\"ready\":true}]}");

        var view = LobbyProtocol.ParseState(message);

        Assert.Equal(GameMode.Ffa, view.Mode);
        Assert.Equal(LobbyPhase.Waiting, view.Phase);
        Assert.Equal(0, view.HostSlot);
        Assert.Equal(2, view.Players.Count);
        Assert.Equal(new LobbyPlayer(1, "Bea", 1, true), view.Players[1]);
    }

    [Fact]
    public void ParseState_ReadsTeamModeAndCountdown()
    {
        var message = StateMessage(
            "{\"mode\":\"team\",\"phase\":\"countdown\",\"hostSlot\":2,\"countdown\":3,\"players\":[]}");

        var view = LobbyProtocol.ParseState(message);

        Assert.Equal(GameMode.Team, view.Mode);
        Assert.Equal(LobbyPhase.Countdown, view.Phase);
        Assert.Equal(2, view.HostSlot);
        Assert.Equal(3, view.Countdown);
        Assert.Empty(view.Players);
    }

    [Fact]
    public void EncodeSetReady_TagsAndEmitsTheCommandJson()
    {
        var message = LobbyProtocol.EncodeSetReady(true);

        Assert.Equal(LobbyProtocol.MsgLobbyCmd, message[0]);
        using var doc = JsonDocument.Parse(message.AsSpan(1).ToArray());
        Assert.Equal("setReady", doc.RootElement.GetProperty("type").GetString());
        Assert.True(doc.RootElement.GetProperty("ready").GetBoolean());
    }

    [Fact]
    public void EncodeSetMode_UsesTheWireModeStrings()
    {
        using var team = JsonDocument.Parse(LobbyProtocol.EncodeSetMode(GameMode.Team).AsSpan(1).ToArray());
        Assert.Equal("team", team.RootElement.GetProperty("mode").GetString());

        using var ffa = JsonDocument.Parse(LobbyProtocol.EncodeSetMode(GameMode.Ffa).AsSpan(1).ToArray());
        Assert.Equal("ffa", ffa.RootElement.GetProperty("mode").GetString());
    }

    [Fact]
    public void EncodeSetName_EscapesTheName()
    {
        var message = LobbyProtocol.EncodeSetName("A\"B");

        using var doc = JsonDocument.Parse(message.AsSpan(1).ToArray());
        Assert.Equal("setName", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("A\"B", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void ParseState_ReadsThePickedMap_AndDefaultsToRandomWhenAbsent()
    {
        var picked = LobbyProtocol.ParseState(StateMessage(
            "{\"mode\":\"ffa\",\"phase\":\"waiting\",\"hostSlot\":0,\"countdown\":0," +
            "\"map\":\"CliffsAndValleys\",\"players\":[]}"));
        Assert.Equal("CliffsAndValleys", picked.Map);

        var legacy = LobbyProtocol.ParseState(StateMessage(
            "{\"mode\":\"ffa\",\"phase\":\"waiting\",\"hostSlot\":0,\"countdown\":0,\"players\":[]}"));
        Assert.Equal("", legacy.Map); // an older server without the field means "random"
    }

    [Fact]
    public void EncodeSetMap_TagsAndEmitsTheCommandJson()
    {
        var message = LobbyProtocol.EncodeSetMap("DesertWar");

        Assert.Equal(LobbyProtocol.MsgLobbyCmd, message[0]);
        using var doc = JsonDocument.Parse(message.AsSpan(1).ToArray());
        Assert.Equal("setMap", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("DesertWar", doc.RootElement.GetProperty("map").GetString());
    }

    [Fact]
    public void EncodeStart_IsATaggedStartCommand()
    {
        var message = LobbyProtocol.EncodeStart();

        Assert.Equal(LobbyProtocol.MsgLobbyCmd, message[0]);
        using var doc = JsonDocument.Parse(message.AsSpan(1).ToArray());
        Assert.Equal("start", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void EncodeLoaded_IsATaggedLoadedCommand()
    {
        var message = LobbyProtocol.EncodeLoaded();

        Assert.Equal(LobbyProtocol.MsgLobbyCmd, message[0]);
        using var doc = JsonDocument.Parse(message.AsSpan(1).ToArray());
        Assert.Equal("loaded", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void EncodeRematch_IsATaggedRematchCommand()
    {
        var message = LobbyProtocol.EncodeRematch();

        Assert.Equal(LobbyProtocol.MsgLobbyCmd, message[0]);
        using var doc = JsonDocument.Parse(message.AsSpan(1).ToArray());
        Assert.Equal("rematch", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void ParseState_ReadsTheLoadingPhaseSeedAndPerPlayerLoaded()
    {
        var view = LobbyProtocol.ParseState(StateMessage(
            "{\"mode\":\"ffa\",\"phase\":\"loading\",\"hostSlot\":0,\"countdown\":0,\"seed\":12345," +
            "\"players\":[{\"slot\":0,\"name\":\"Ada\",\"team\":0,\"ready\":true,\"loaded\":true}," +
            "{\"slot\":1,\"name\":\"Bea\",\"team\":1,\"ready\":true,\"loaded\":false}]}"));

        Assert.Equal(LobbyPhase.Loading, view.Phase);
        Assert.Equal(12345, view.Seed);
        Assert.True(view.Players[0].Loaded);
        Assert.False(view.Players[1].Loaded);
    }

    [Fact]
    public void ParseState_DefaultsSeedAndLoadedWhenAbsent()
    {
        var view = LobbyProtocol.ParseState(StateMessage(
            "{\"mode\":\"ffa\",\"phase\":\"waiting\",\"hostSlot\":0,\"countdown\":0," +
            "\"players\":[{\"slot\":0,\"name\":\"Ada\",\"team\":0,\"ready\":false}]}"));

        Assert.Equal(0, view.Seed); // older server without the field
        Assert.False(view.Players[0].Loaded);
    }
}
