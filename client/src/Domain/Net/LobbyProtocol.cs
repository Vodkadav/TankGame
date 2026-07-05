using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TankGame.Domain.Net;

/// <summary>Which way the match is scored.</summary>
public enum GameMode
{
    /// <summary>Free-for-all — every tank is its own team.</summary>
    Ffa,

    /// <summary>Two teams.</summary>
    Team,
}

/// <summary>The lobby's lifecycle stage (mirrors the worker's LobbyPhase).</summary>
public enum LobbyPhase
{
    Waiting,
    Countdown,
    Started,
}

/// <summary>One seat in the lobby.</summary>
public readonly record struct LobbyPlayer(int Slot, string Name, int Team, bool Ready);

/// <summary>The client's view of a lobby — the C# mirror of the worker's <c>LobbyState</c> JSON,
/// pushed over the socket as a <see cref="LobbyProtocol.MsgLobbyState"/> message. Read-only: the
/// server is the authority, the client renders this and sends commands.</summary>
/// <param name="Map">The map id the host picked for the room; "" means random, resolved by the
/// host client at launch. Defaults to "" so a push from a server without the field still parses.</param>
public sealed record LobbyView(
    GameMode Mode,
    LobbyPhase Phase,
    int HostSlot,
    int Countdown,
    IReadOnlyList<LobbyPlayer> Players,
    string Map = "");

/// <summary>The lobby control protocol — the C# mirror of the worker's JSON lobby channel
/// (<c>codec.ts</c> / <c>lobbyState.ts</c>). The server pushes <see cref="MsgLobbyState"/> snapshots
/// and accepts <see cref="MsgLobbyCmd"/> commands (it stamps the sender's slot, so commands carry no
/// slot). JSON, not the fixed binary game protocol, because the lobby is low-rate and shape-churning.
/// Pure — no Godot — so the parse/encode round-trips are unit-tested without a runtime.</summary>
public static class LobbyProtocol
{
    /// <summary>Seats per room — mirrors the worker's <c>MAX_PLAYERS</c> (owner ask: 4, extendable
    /// later). The room UI, placeholder cast, and spawn table all size off this.</summary>
    public const int MaxPlayers = 4;

    /// <summary>Leading kind byte of a server→client lobby-state push.</summary>
    public const byte MsgLobbyState = 0x10;

    /// <summary>Leading kind byte of a client→server lobby command.</summary>
    public const byte MsgLobbyCmd = 0x11;

    private static readonly JsonSerializerOptions CommandOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Parses a <see cref="MsgLobbyState"/> message (tag byte then UTF-8 JSON) into a view.</summary>
    public static LobbyView ParseState(ReadOnlySpan<byte> message)
    {
        using var doc = JsonDocument.Parse(message[1..].ToArray());
        var root = doc.RootElement;

        var players = new List<LobbyPlayer>();
        foreach (var p in root.GetProperty("players").EnumerateArray())
        {
            players.Add(new LobbyPlayer(
                p.GetProperty("slot").GetInt32(),
                p.GetProperty("name").GetString() ?? "Player",
                p.GetProperty("team").GetInt32(),
                p.GetProperty("ready").GetBoolean()));
        }

        return new LobbyView(
            ParseMode(root.GetProperty("mode").GetString()),
            ParsePhase(root.GetProperty("phase").GetString()),
            root.GetProperty("hostSlot").GetInt32(),
            root.GetProperty("countdown").GetInt32(),
            players,
            root.TryGetProperty("map", out var map) ? map.GetString() ?? "" : "");
    }

    public static byte[] EncodeSetName(string name) => Tag(new { type = "setName", name });

    public static byte[] EncodeSetReady(bool ready) => Tag(new { type = "setReady", ready });

    public static byte[] EncodeSetTeam(int team) => Tag(new { type = "setTeam", team });

    public static byte[] EncodeSetMode(GameMode mode) => Tag(new { type = "setMode", mode = ModeString(mode) });

    public static byte[] EncodeSetMap(string map) => Tag(new { type = "setMap", map });

    public static byte[] EncodeStart() => Tag(new { type = "start" });

    private static byte[] Tag(object command)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(command, CommandOptions);
        var message = new byte[json.Length + 1];
        message[0] = MsgLobbyCmd;
        json.CopyTo(message, 1);
        return message;
    }

    private static GameMode ParseMode(string? mode) => mode == "team" ? GameMode.Team : GameMode.Ffa;

    private static string ModeString(GameMode mode) => mode == GameMode.Team ? "team" : "ffa";

    private static LobbyPhase ParsePhase(string? phase) => phase switch
    {
        "countdown" => LobbyPhase.Countdown,
        "started" => LobbyPhase.Started,
        _ => LobbyPhase.Waiting,
    };
}
