using System;
using TankGame.Domain.Net;
using TankGame.Infrastructure.Net;

namespace TankGame.Presentation;

/// <summary>The networked-match entry point behind the title screen's "Join TEST01" button (M3-T8).
/// The transport is built through a swappable <see cref="TransportFactory"/> so the join click path
/// is testable against a mock without opening a real socket; the default factory builds the live
/// <see cref="WebSocketTransport"/> to the hardcoded dev lobby on the deployed Worker (the two-device
/// playtest in <c>docs/setup/m3-go-live.md</c>).</summary>
public static class NetworkSession
{
    /// <summary>The hardcoded dev lobby code for the M3 go-live two-device playtest.</summary>
    public const string TestLobbyCode = "TEST01";

    private const string WorkerHost = "tankgame-worker.vodkadav.workers.dev";

    /// <summary>Builds the transport for a lobby code. Swap in tests to assert the click path without
    /// opening a socket; the default connects a live WebSocket to <c>/room/{code}</c>.</summary>
    public static Func<string, IMatchTransport> TransportFactory { get; set; } =
        code => new WebSocketTransport(new GodotWebSocket($"wss://{WorkerHost}/room/{code}"));

    /// <summary>The transport for the joined match, or null before a join. The networked play scene
    /// reads this once that wiring lands.</summary>
    public static IMatchTransport? Active { get; private set; }

    /// <summary>Joins <paramref name="code"/> by building (and, for the live factory, connecting) its
    /// transport, and records it as <see cref="Active"/>.</summary>
    public static IMatchTransport Join(string code) => Active = TransportFactory(code);
}
