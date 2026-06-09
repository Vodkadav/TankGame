// MatchRoom — one lobby's match, as a Durable Object (one DO per lobby code). Per ADR-0019 the DO is
// a PURE RELAY + lobby directory: it assigns each connecting client a slot (host = 0, guests = 1+),
// tells the client its slot with a WelcomeFrame, and forwards bytes between peers. It does NOT
// simulate — authority is the host client running the real C# World. Routing is by sender slot:
//   • the host (slot 0) broadcasts SnapshotFrames → every guest;
//   • a guest (slot 1+) sends InputFrames → the host.
// The WebSocket hibernation API keeps an idle room free; the slot survives hibernation via the
// socket's serialized attachment. The lobby-code routes and the request-budget alarm live elsewhere.

import { encodeWelcome } from "./protocol/codec";

const HOST_SLOT = 0;
const MAX_PLAYERS = 2;

interface Attachment {
  slot: number;
}

export class MatchRoom implements DurableObject {
  private readonly state: DurableObjectState;

  constructor(state: DurableObjectState, _env: unknown) {
    this.state = state;
  }

  async fetch(request: Request): Promise<Response> {
    if (request.headers.get("Upgrade") !== "websocket") {
      return new Response("expected a websocket upgrade", { status: 426 });
    }
    if (this.state.getWebSockets().length >= MAX_PLAYERS) {
      return new Response("room full", { status: 503 });
    }

    const slot = this.state.getWebSockets().length; // first joiner → 0 (host), second → 1 (guest)
    const pair = new WebSocketPair();
    const client = pair[0];
    const server = pair[1];

    this.state.acceptWebSocket(server); // hibernation API
    server.serializeAttachment({ slot } satisfies Attachment); // survives hibernation
    server.send(encodeWelcome(slot)); // first message: tell the client which slot it drives

    return new Response(null, { status: 101, webSocket: client });
  }

  webSocketMessage(sender: WebSocket, message: ArrayBuffer | string): void {
    if (typeof message === "string") {
      return; // the protocol is binary; ignore anything else
    }
    for (const target of this.relayTargets(slotOf(sender))) {
      try {
        target.send(message);
      } catch {
        // target socket closing mid-relay — its close handler will clean it up
      }
    }
  }

  webSocketClose(ws: WebSocket, code: number, _reason: string, _wasClean: boolean): void {
    try {
      ws.close(code);
    } catch {
      // already closing
    }
    // No sim to stop; an empty room simply hibernates until the next joiner.
  }

  // Where bytes from a given sender slot go: the host (slot 0) broadcasts to all guests; a guest
  // sends to the host. Returns the live peer sockets (the sender itself is always excluded).
  private relayTargets(senderSlot: number): WebSocket[] {
    const peers = this.state.getWebSockets().filter((ws) => slotOf(ws) !== senderSlot);
    if (senderSlot === HOST_SLOT) {
      return peers; // host → every guest
    }
    return peers.filter((ws) => slotOf(ws) === HOST_SLOT); // guest → host only
  }
}

function slotOf(ws: WebSocket): number {
  return (ws.deserializeAttachment() as Attachment | null)?.slot ?? 0;
}
