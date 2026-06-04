using System;
using TankGame.Domain.Net;

namespace TankGame.Infrastructure.Net;

/// <summary>The client's WebSocket <see cref="IMatchTransport"/> (M3-T6): encodes each
/// <see cref="InputFrame"/> onto an <see cref="IMatchSocket"/> and decodes inbound
/// <see cref="SnapshotFrame"/>s back up as <see cref="SnapshotReceived"/>. All the wire framing is
/// delegated to <see cref="ProtocolCodec"/> (mirrored byte-for-byte by the server), so this class is
/// only the routing between frames and bytes — kept free of Godot so it unit-tests against a fake
/// socket. The concrete engine socket is injected, matching the local <c>IInputSource</c> seam.</summary>
public sealed class WebSocketTransport : IMatchTransport
{
    private readonly IMatchSocket _socket;

    public WebSocketTransport(IMatchSocket socket) => _socket = socket;

    public event Action<byte>? WelcomeReceived;
    public event Action<SnapshotFrame>? SnapshotReceived;

    public void SendInput(InputFrame input) => _socket.Send(ProtocolCodec.EncodeInput(input));

    /// <summary>Pumps the socket and raises the matching event for every server message that arrived
    /// since the last call, dispatching on its leading kind byte (welcome vs snapshot). Drive it from
    /// the game loop (<c>_Process</c>); it is decoupled from <see cref="SendInput"/> so input can be
    /// sent at any time independent of snapshot cadence.</summary>
    public void Poll()
    {
        foreach (var message in _socket.Poll())
        {
            if (message.Length == 0)
            {
                continue;
            }

            switch (message[0])
            {
                case ProtocolCodec.MsgWelcome:
                    WelcomeReceived?.Invoke(ProtocolCodec.DecodeWelcome(message));
                    break;
                case ProtocolCodec.MsgSnapshot:
                    SnapshotReceived?.Invoke(ProtocolCodec.DecodeSnapshot(message.AsSpan(1)));
                    break;
            }
        }
    }
}
