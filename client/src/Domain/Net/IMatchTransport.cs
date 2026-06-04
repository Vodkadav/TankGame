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

    /// <summary>Raised when an authoritative snapshot arrives from the server.</summary>
    event Action<SnapshotFrame> SnapshotReceived;
}
