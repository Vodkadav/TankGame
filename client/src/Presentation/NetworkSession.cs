using System;
using TankGame.Domain.Net;
using TankGame.Infrastructure.Net;

namespace TankGame.Presentation;

/// <summary>The networked-match entry point behind the lobby screen (ADR-0019 step 2): the lobby
/// directory mints/validates the shareable codes, the transport carries the match. Both are built
/// through swappable factories so the Host/Join click paths are testable without HTTP or a socket;
/// the defaults talk to the deployed Worker.</summary>
public static class NetworkSession
{
    private const string WorkerHost = "tankgame-worker.vodkadav.workers.dev";

    /// <summary>Builds the lobby directory client. Swap in tests; the default hits the live Worker's
    /// lobby routes.</summary>
    public static Func<ILobbyClient> LobbyFactory { get; set; } =
        () => new HttpLobbyClient($"https://{WorkerHost}");

    /// <summary>Builds the transport for a lobby code. Swap in tests to assert the click path without
    /// opening a socket; the default connects a live WebSocket to <c>/room/{code}</c>.</summary>
    public static Func<string, IMatchTransport> TransportFactory { get; set; } =
        code => new WebSocketTransport(new GodotWebSocket($"wss://{WorkerHost}/room/{code}"));

    /// <summary>The transport for the joined match, or null before a join. The networked play scene
    /// reads this.</summary>
    public static IMatchTransport? Active { get; private set; }

    /// <summary>Joins <paramref name="code"/> by building (and, for the live factory, connecting) its
    /// transport, and records it as <see cref="Active"/>.</summary>
    public static IMatchTransport Join(string code) => Active = TransportFactory(code);

    /// <summary>Drops the active transport — leaving a match, or a test resetting the seam.</summary>
    public static void Reset() => Active = null;
}
