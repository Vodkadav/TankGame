// MatchRoom — the authoritative match for one lobby, as a Durable Object (one DO per lobby code).
// M3-T3 skeleton: it accepts WebSocket upgrades using the hibernation API and broadcasts each
// peer's message to the others. The 20 Hz simulation tick, input application, and snapshot
// broadcast (M3-T5) build on top of this. See ADR-0005.

export class MatchRoom implements DurableObject {
  private readonly state: DurableObjectState;

  constructor(state: DurableObjectState, _env: unknown) {
    this.state = state;
  }

  async fetch(request: Request): Promise<Response> {
    if (request.headers.get("Upgrade") !== "websocket") {
      return new Response("expected a websocket upgrade", { status: 426 });
    }

    const pair = new WebSocketPair();
    const client = pair[0];
    const server = pair[1];

    // Hibernation API: the runtime can evict the DO from memory between messages and rehydrate
    // it on the next one, so an idle lobby costs nothing while keeping the socket open.
    this.state.acceptWebSocket(server);

    return new Response(null, { status: 101, webSocket: client });
  }

  webSocketMessage(sender: WebSocket, message: ArrayBuffer | string): void {
    // Skeleton relay: echo each peer's frame to every other peer. The server-authoritative
    // MatchSim (M3-T5) replaces this with apply-input → tick → broadcast-snapshot.
    for (const peer of this.state.getWebSockets()) {
      if (peer !== sender) {
        peer.send(message);
      }
    }
  }

  webSocketClose(ws: WebSocket, code: number, _reason: string, _wasClean: boolean): void {
    // Close our end so the socket is fully torn down; peer cleanup is implicit via getWebSockets.
    try {
      ws.close(code);
    } catch {
      // already closing — nothing to do
    }
  }
}
