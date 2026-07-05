using System.Collections.Generic;
using System.Linq;
using TankGame.Domain.Net;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class NetRosterTests
{
    private const string Code = "ABC123";

    private static LobbyView View(GameMode mode, params LobbyPlayer[] players) =>
        new(mode, LobbyPhase.Started, HostSlot: 0, Countdown: 0, players);

    [Fact]
    public void AFullHouse_SeatsEveryHuman_AndFlagsTheLocalOne()
    {
        var lobby = View(GameMode.Ffa,
            new LobbyPlayer(0, "Ada", 0, true),
            new LobbyPlayer(1, "Bea", 1, true),
            new LobbyPlayer(2, "Cid", 2, true),
            new LobbyPlayer(3, "Dot", 3, true));

        var plan = NetRoster.Build(lobby, localSlot: 2, Code, seats: 4);

        Assert.Equal(4, plan.Count);
        Assert.Equal(new[] { "Ada", "Bea", "Cid", "Dot" }, plan.Select(s => s.Name));
        Assert.Equal(NetRoster.SeatKind.LocalHuman, plan[2].Kind);
        Assert.All(new[] { plan[0], plan[1], plan[3] },
            seat => Assert.Equal(NetRoster.SeatKind.RemoteHuman, seat.Kind));
    }

    [Fact]
    public void ASoloStart_FillsTheEmptySeats_WithTheRoomsPlaceholderCast()
    {
        var lobby = View(GameMode.Ffa, new LobbyPlayer(0, "Ada", 0, true));

        var plan = NetRoster.Build(lobby, localSlot: 0, Code, seats: 4);

        var placeholders = LobbySeats.PlaceholderNames(Code, 4);
        Assert.Equal(NetRoster.SeatKind.LocalHuman, plan[0].Kind);
        for (var slot = 1; slot < 4; slot++)
        {
            Assert.Equal(NetRoster.SeatKind.Ai, plan[slot].Kind);
            Assert.Equal(placeholders[slot], plan[slot].Name); // the gray name the room promised
            Assert.Equal(slot, plan[slot].Team); // FFA: every seat its own team
        }
    }

    [Fact]
    public void TeamMode_BalancesTheAiFill_AcrossTheTwoTeams()
    {
        var lobby = View(GameMode.Team, new LobbyPlayer(0, "Ada", 0, true));

        var plan = NetRoster.Build(lobby, localSlot: 0, Code, seats: 4);

        var teams = plan.Select(s => s.Team).ToList();
        Assert.Equal(2, teams.Count(t => t == 0));
        Assert.Equal(2, teams.Count(t => t == 1));
    }

    [Fact]
    public void ANullLobby_IsTheTwoPlayerEra_LocalPlusOneRemote()
    {
        var plan = NetRoster.Build(null, localSlot: 1, Code, seats: 4);

        Assert.Equal(2, plan.Count);
        Assert.Equal(NetRoster.SeatKind.RemoteHuman, plan[0].Kind);
        Assert.Equal(NetRoster.SeatKind.LocalHuman, plan[1].Kind);
        Assert.Equal(new[] { 0, 1 }, plan.Select(s => s.Team));
    }
}
