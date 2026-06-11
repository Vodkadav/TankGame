using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TankGame.Domain.Net;

namespace TankGame.Infrastructure.Net;

/// <summary>The live <see cref="ILobbyClient"/>: the Worker's lobby routes over HTTP
/// (<c>POST /lobby</c> mints a code, <c>POST /lobby/:code/join</c> validates one — M3-T4,
/// kept by ADR-0019). Any failure to reach or parse the service surfaces as the contract's
/// null/false, never an exception — losing wifi mid-menu is a normal path for a phone game.</summary>
public sealed class HttpLobbyClient : ILobbyClient
{
    private readonly HttpClient _http;

    /// <param name="baseUrl">The Worker origin, e.g. <c>https://tankgame-worker…workers.dev</c>.</param>
    /// <param name="handler">Test seam: a canned <see cref="HttpMessageHandler"/>; null = live.</param>
    public HttpLobbyClient(string baseUrl, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(baseUrl);
    }

    public async Task<string?> CreateLobbyAsync()
    {
        try
        {
            var response = await _http.PostAsync("/lobby", content: null);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("code").GetString();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException
            or System.Collections.Generic.KeyNotFoundException)
        {
            return null;
        }
    }

    public async Task<bool> JoinLobbyAsync(string code)
    {
        try
        {
            var response = await _http.PostAsync($"/lobby/{Uri.EscapeDataString(code)}/join", content: null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
