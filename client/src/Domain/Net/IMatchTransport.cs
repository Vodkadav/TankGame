using System;

namespace TankGame.Domain.Net;

/// <summary>The client's link to the authoritative match: it sends local input up and raises
/// server snapshots down. The concrete implementation (a WebSocket transport in Infrastructure,
/// M3-T6) and a loopback fake for tests both satisfy this; nothing above this interface knows
/// whether the peer is a real server or a stub. "Client sends intent, server resolves outcome"
/// made concrete — the same seam the local <c>IInputSource</c> established (ADR-0011).</summary>
public interface IMatchTransport
{
    /// <summary>Sends one input frame to the server. Fire-and-forget; ordering/delivery is the
    /// transport's concern.</summary>
    void SendInput(InputFrame input);

    /// <summary>Drives the transport once per frame: pumps the underlying socket and raises
    /// <see cref="WelcomeReceived"/> / <see cref="SnapshotReceived"/> for any messages that have
    /// arrived. A polled (rather than callback-threaded) model so delivery stays on the game loop —
    /// the Godot <c>WebSocketPeer</c> must be pumped from <c>_Process</c>; test fakes raise the
    /// events directly and leave this a no-op.</summary>
    void Poll();

    /// <summary>Raised once on connect with the slot the server assigned this client (0 host /
    /// 1 guest) — the slot it predicts and renders as its own.</summary>
    event Action<byte> WelcomeReceived;

    /// <summary>Raised when an authoritative snapshot arrives from the server.</summary>
    event Action<SnapshotFrame> SnapshotReceived;
}
