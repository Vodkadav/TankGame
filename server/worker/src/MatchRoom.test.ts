import { SELF } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import {
  encodeInput,
  decodeSnapshot,
  MSG_WELCOME,
  MSG_SNAPSHOT,
  type SnapshotFrame,
} from "./protocol/codec";

// A joined connection that buffers every inbound message from accept time. The room runs a 20 Hz
// tick the moment the host connects, so a second joiner's welcome can be immediately chased by a
// snapshot; buffering from accept (synchronously, before any await) means no message is missed or
// reordered, and reads drain in arrival order.
interface Conn {
  ws: WebSocket;
  next(): Promise<Uint8Array>;
}

async function joinRoom(code: string): Promise<Conn> {
  const response = await SELF.fetch(`http://room.test/room/${code}`, {
    headers: { Upgrade: "websocket" },
  });
  expect(response.status).toBe(101);
  const ws = response.webSocket;
  expect(ws).toBeTruthy();

  const buffered: Uint8Array[] = [];
  const waiters: ((message: Uint8Array) => void)[] = [];
  ws!.accept();
  ws!.addEventListener("message", (event: MessageEvent) => {
    const message = new Uint8Array(event.data as ArrayBuffer);
    const waiter = waiters.shift();
    if (waiter) {
      waiter(message);
    } else {
      buffered.push(message);
    }
  });

  return {
    ws: ws!,
    next: () =>
      buffered.length > 0
        ? Promise.resolve(buffered.shift()!)
        : new Promise<Uint8Array>((resolve) => waiters.push(resolve)),
  };
}

// The first server→client message is the welcome (the slot assignment); snapshots follow, tagged.
async function nextSnapshot(conn: Conn): Promise<SnapshotFrame> {
  const message = await conn.next();
  expect(message[0]).toBe(MSG_SNAPSHOT);
  return decodeSnapshot(message.subarray(1));
}

describe("MatchRoom", () => {
  it("welcomes the host as slot 0 and the guest as slot 1", async () => {
    const host = await joinRoom("WEL001");
    const hostWelcome = await host.next();
    expect(hostWelcome[0]).toBe(MSG_WELCOME);
    expect(hostWelcome[1]).toBe(0);

    const guest = await joinRoom("WEL001");
    const guestWelcome = await guest.next();
    expect(guestWelcome[0]).toBe(MSG_WELCOME);
    expect(guestWelcome[1]).toBe(1);

    host.ws.close();
    guest.ws.close();
  });

  it("runs the sim and broadcasts snapshots that reflect a player's input", async () => {
    const host = await joinRoom("SIM001");
    const guest = await joinRoom("SIM001");

    const welcome = await host.next(); // first message is the slot assignment
    expect(welcome[0]).toBe(MSG_WELCOME);

    // One input frame sets a persistent move intent (the sim keeps applying it each tick).
    host.ws.send(encodeInput({ seq: 1, moveX: 1, moveY: 0, aim: 0, buttons: 0 }));

    let last: SnapshotFrame | null = null;
    for (let i = 0; i < 16; i++) {
      last = await nextSnapshot(host);
      if (last.tanks.length > 0 && last.tanks[0].x > 170) break;
    }

    expect(last).not.toBeNull();
    expect(last!.tick).toBeGreaterThan(0);
    expect(last!.tanks[0].slot).toBe(0);
    expect(last!.tanks[0].x).toBeGreaterThan(170); // moved east from the spawn (x≈160)

    host.ws.close();
    guest.ws.close();
  });

  it("rejects a non-WebSocket request to a room", async () => {
    const response = await SELF.fetch("http://room.test/room/SIM002");
    expect(response.status).toBe(426);
  });

  it("refuses a third player in a full room", async () => {
    const a = await joinRoom("FULL01");
    const b = await joinRoom("FULL01");

    const third = await SELF.fetch("http://room.test/room/FULL01", {
      headers: { Upgrade: "websocket" },
    });
    expect(third.status).toBe(503);

    a.ws.close();
    b.ws.close();
  });
});
