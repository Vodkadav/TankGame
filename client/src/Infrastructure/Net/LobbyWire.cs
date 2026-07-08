using System;
using System.Collections.Generic;
using System.Text.Json;
using TankGame.Domain.Net;

namespace TankGame.Infrastructure.Net;

/// <summary>One reading of the Worker's lobby-route JSON, shared by the .NET
/// (<see cref="HttpLobbyClient"/>) and Godot (<see cref="GodotHttpLobbyClient"/>) clients so the
/// two can never disagree about the wire. Garbage parses to null — a lobby service hiccup is a
/// normal path, never an exception.</summary>
public static class LobbyWire
{
    public static string? ParseCreatedCode(string json)
    {
        try
        {
            using var body = JsonDocument.Parse(json);
            return body.RootElement.GetProperty("code").GetString();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    public static IReadOnlyList<OpenLobbyInfo>? ParseOpenLobbies(string json)
    {
        try
        {
            using var body = JsonDocument.Parse(json);
            var open = new List<OpenLobbyInfo>();
            foreach (var lobby in body.RootElement.EnumerateArray())
            {
                open.Add(new OpenLobbyInfo(
                    lobby.GetProperty("code").GetString() ?? "",
                    lobby.GetProperty("mode").GetString() == "team" ? GameMode.Team : GameMode.Ffa,
                    lobby.GetProperty("players").GetInt32(),
                    lobby.TryGetProperty("map", out var map) ? map.GetString() ?? "" : ""));
            }

            return open;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
