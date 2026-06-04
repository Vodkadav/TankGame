// MatchRoom — the authoritative match for one lobby, as a Durable Object (one DO per lobby code).
// Holds a MatchSim, assigns each of the (up to two) players a slot, applies their decoded inputs,
// and runs a 20 Hz tick loop that steps the sim and broadcasts a per-client snapshot. The loop runs
// only while players are connected; an empty room drops the sim and lets the DO hibernate. See
// ADR-0005.

import { MatchSim } from "./sim/matchSim";
import { decodeInput, encodeSnapshot } from "./protocol/codec";

const TICK_MS = 50; // 20 Hz
const MAX_PLAYERS = 2;

interface Attachment {
  slot: number;
}

export class MatchRoom implements DurableObject {
  private readonly state: DurableObjectState;
  private sim: MatchSim | null = null;
  private loop: ReturnType<typeof setInterval> | null = null;

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
    this.ensureRunning();

    return new Response(null, { status: 101, webSocket: client });
  }

  webSocketMessage(sender: WebSocket, message: ArrayBuffer | string): void {
    if (this.sim === null || typeof message === "string") {
      return; // the protocol is binary; ignore anything else
    }
    this.sim.applyInput(slotOf(sender), decodeInput(new Uint8Array(message)));
  }

  webSocketClose(ws: WebSocket, code: number, _reason: string, _wasClean: boolean): void {
    try {
      ws.close(code);
    } catch {
      // already closing
    }
    // Once the last player leaves, stop ticking and drop the sim so the DO can hibernate.
    const remaining = this.state.getWebSockets().filter((s) => s !== ws);
    if (remaining.length === 0) {
      this.stop();
    }
  }

  private ensureRunning(): void {
    this.sim ??= new MatchSim();
    this.loop ??= setInterval(() => this.tick(), TICK_MS);
  }

  private stop(): void {
    if (this.loop !== null) {
      clearInterval(this.loop);
      this.loop = null;
    }
    this.sim = null;
  }

  private tick(): void {
    if (this.sim === null) {
      return;
    }
    this.sim.step();
    for (const ws of this.state.getWebSockets()) {
      try {
        ws.send(encodeSnapshot(this.sim.snapshotFor(slotOf(ws))));
      } catch {
        // socket closing mid-tick — the close handler will clean it up
      }
    }
  }
}

function slotOf(ws: WebSocket): number {
  return (ws.deserializeAttachment() as Attachment | null)?.slot ?? 0;
}
