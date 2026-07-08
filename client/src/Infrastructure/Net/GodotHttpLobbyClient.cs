using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Godot;
using TankGame.Domain.Net;

namespace TankGame.Infrastructure.Net;

/// <summary>The web build's <see cref="ILobbyClient"/>: the same Worker lobby routes, but over
/// Godot's <c>HttpRequest</c> node instead of .NET's <c>HttpClient</c> — on the multi-threaded WASM
/// runtime the .NET browser HTTP interop dies with a <c>NullReferenceException</c> inside
/// <c>BrowserHttpInterop.CreateController</c> (observed live on the arcade, 2026-07-06), so the web
/// path must go through the engine. Must sit in the scene tree (the browser scene parents it) or
/// the requests never pump. Failures surface as the contract's null/false, matching
/// <see cref="HttpLobbyClient"/>.</summary>
public sealed partial class GodotHttpLobbyClient : Node, ILobbyClient
{
    /// <summary>The Worker origin, e.g. <c>https://tankgame-worker…workers.dev</c>.</summary>
    public string BaseUrl { get; init; } = "";

    public async Task<string?> CreateLobbyAsync()
    {
        var (ok, status, body) = await RequestAsync(HttpClient.Method.Post, "/lobby");
        return ok && IsSuccess(status) ? LobbyWire.ParseCreatedCode(body) : null;
    }

    public async Task<bool> JoinLobbyAsync(string code)
    {
        var (ok, status, _) = await RequestAsync(
            HttpClient.Method.Post, $"/lobby/{Uri.EscapeDataString(code)}/join");
        return ok && IsSuccess(status);
    }

    public async Task<IReadOnlyList<OpenLobbyInfo>?> ListOpenLobbiesAsync()
    {
        var (ok, status, body) = await RequestAsync(HttpClient.Method.Get, "/lobbies");
        return ok && IsSuccess(status) ? LobbyWire.ParseOpenLobbies(body) : null;
    }

    private static bool IsSuccess(long status) => status is >= 200 and < 300;

    // One throwaway HttpRequest child per call — they're cheap, and it keeps concurrent calls
    // (a refresh racing a join) from sharing a node that only supports one request at a time.
    private async Task<(bool Ok, long Status, string Body)> RequestAsync(
        HttpClient.Method method, string path)
    {
        var request = new HttpRequest { Timeout = 10 }; // else a stalled request awaits forever and leaks the node
        AddChild(request);
        try
        {
            if (request.Request(BaseUrl + path, method: method) != Error.Ok)
            {
                return (false, 0, "");
            }

            var signal = await ToSignal(request, HttpRequest.SignalName.RequestCompleted);
            if ((long)signal[0] != (long)HttpRequest.Result.Success)
            {
                return (false, 0, "");
            }

            return (true, (long)signal[1], Encoding.UTF8.GetString((byte[])signal[3]));
        }
        finally
        {
            request.QueueFree();
        }
    }
}
