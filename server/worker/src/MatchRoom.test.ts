import { SELF } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import {
  encodeInput,
  encodeSnapshotMessage,
  decodeInput,
  decodeSnapshot,
  MSG_WELCOME,
  MSG_SNAPSHOT,
  type InputFrame,
  type SnapshotFrame,
} from "./protocol/codec";

// A joined connection that buffers every inbound message from accept time. In relay mode the DO
// only ever sends a client (a) its welcome on connect and (b) bytes a peer relays to it, so a read
// may have to wait for a peer to act. Buffering from accept (synchronously, before any await) keeps
// arrival order and means no relayed frame is missed.
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

// The first server→client message is always the welcome (the slot assignment).
async function welcomeOf(conn: Conn): Promise<number> {
  const message = await conn.next();
  expect(message[0]).toBe(MSG_WELCOME);
  return message[1];
}

describe("MatchRoom (relay)", () => {
  it("welcomes the host as slot 0 and the guest as slot 1", async () => {
    const host = await joinRoom("WEL001");
    expect(await welcomeOf(host)).toBe(0);

    const guest = await joinRoom("WEL001");
    expect(await welcomeOf(guest)).toBe(1);

    host.ws.close();
    guest.ws.close();
  });

  it("forwards a guest InputFrame to the host (slot 0)", async () => {
    const host = await joinRoom("REL001");
    const guest = await joinRoom("REL001");
    expect(await welcomeOf(host)).toBe(0);
    expect(await welcomeOf(guest)).toBe(1);

    const frame: InputFrame = { seq: 7, moveX: 1, moveY: 0, aim: 0.5, buttons: 1 };
    guest.ws.send(encodeInput(frame));

    const received = await host.next();
    expect(decodeInput(received)).toEqual(frame);

    host.ws.close();
    guest.ws.close();
  });

  it("forwards a host SnapshotFrame to the guests", async () => {
    const host = await joinRoom("REL002");
    const guest = await joinRoom("REL002");
    expect(await welcomeOf(host)).toBe(0);
    expect(await welcomeOf(guest)).toBe(1);

    const snapshot: SnapshotFrame = {
      tick: 3,
      ackSeq: 2,
      tanks: [{ slot: 0, x: 64, y: 128, rotation: 0, turretRotation: 0.5, hp: 8, team: 0 }],
      wallDeltas: [],
      projectiles: [],
    };
    host.ws.send(encodeSnapshotMessage(snapshot));

    const received = await guest.next();
    expect(received[0]).toBe(MSG_SNAPSHOT);
    expect(decodeSnapshot(received.subarray(1))).toEqual(snapshot);

    host.ws.close();
    guest.ws.close();
  });

  it("does not relay a guest's bytes back to that guest (host-only relay)", async () => {
    const host = await joinRoom("REL003");
    const guest = await joinRoom("REL003");
    expect(await welcomeOf(host)).toBe(0);
    expect(await welcomeOf(guest)).toBe(1);

    guest.ws.send(encodeInput({ seq: 1, moveX: 1, moveY: 0, aim: 0, buttons: 0 }));

    // The host receives the input; the guest must NOT receive its own frame echoed back. Race the
    // guest's next read against the host's: the host's relayed input must arrive first.
    const firstToHost = host.next();
    const sentinel = Symbol("no-echo");
    const guestEcho = guest.next().then(() => "echo");
    const winner = await Promise.race([
      firstToHost.then(() => "host"),
      guestEcho,
      new Promise<typeof sentinel>((resolve) => setTimeout(() => resolve(sentinel), 100)),
    ]);
    expect(winner).toBe("host");

    host.ws.close();
    guest.ws.close();
  });

  it("rejects a non-WebSocket request to a room", async () => {
    const response = await SELF.fetch("http://room.test/room/REL004");
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
