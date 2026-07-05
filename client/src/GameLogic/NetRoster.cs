using System.Collections.Generic;
using TankGame.Domain.Net;

namespace TankGame.GameLogic;

/// <summary>Turns the lobby's final <see cref="LobbyView"/> into the host's seating plan: one seat
/// per room slot, humans where players sat, AI tanks (carrying the seat's placeholder name — the
/// same one the room showed in gray) everywhere else. Teams: humans keep their lobby team; an AI
/// seat is its own team in FFA (team = slot) and joins the smaller side in Team mode. Pure C# so
/// the whole plan is unit-tested without a scene.</summary>
public static class NetRoster
{
    public enum SeatKind
    {
        /// <summary>The host's own tank, on local input.</summary>
        LocalHuman,

        /// <summary>Another player's tank, on relayed input.</summary>
        RemoteHuman,

        /// <summary>An un-joined seat filled by an AI tank on Start (owner ask).</summary>
        Ai,
    }

    public readonly record struct Seat(byte Slot, string Name, int Team, SeatKind Kind);

    /// <summary>The seating plan for a started lobby. A null <paramref name="lobby"/> is the
    /// 2-player era (a session started without the room flow, e.g. old tests): the local slot plus
    /// one remote human, teams 0/1.</summary>
    public static IReadOnlyList<Seat> Build(LobbyView? lobby, byte localSlot, string lobbyCode, int seats)
    {
        var placeholders = LobbySeats.PlaceholderNames(lobbyCode, seats);
        var plan = new List<Seat>(seats);
        if (lobby is null)
        {
            plan.Add(new Seat(0, placeholders[0], 0,
                localSlot == 0 ? SeatKind.LocalHuman : SeatKind.RemoteHuman));
            plan.Add(new Seat(1, placeholders[1], 1,
                localSlot == 1 ? SeatKind.LocalHuman : SeatKind.RemoteHuman));
            return plan;
        }

        var teamCounts = new int[2];
        foreach (var player in lobby.Players)
        {
            if (lobby.Mode == GameMode.Team && player.Team is 0 or 1)
            {
                teamCounts[player.Team]++;
            }
        }

        for (byte slot = 0; slot < seats; slot++)
        {
            if (PlayerAt(lobby, slot) is { } player)
            {
                plan.Add(new Seat(slot, player.Name, player.Team,
                    slot == localSlot ? SeatKind.LocalHuman : SeatKind.RemoteHuman));
                continue;
            }

            var team = slot; // FFA: every seat its own team, exactly like the lobby reducer
            if (lobby.Mode == GameMode.Team)
            {
                team = (byte)(teamCounts[0] <= teamCounts[1] ? 0 : 1); // AI joins the smaller side
                teamCounts[team]++;
            }

            plan.Add(new Seat(slot, placeholders[slot], team, SeatKind.Ai));
        }

        return plan;
    }

    private static LobbyPlayer? PlayerAt(LobbyView lobby, byte slot)
    {
        foreach (var player in lobby.Players)
        {
            if (player.Slot == slot)
            {
                return player;
            }
        }

        return null;
    }
}
