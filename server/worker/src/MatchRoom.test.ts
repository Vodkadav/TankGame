import { SELF } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import { encodeInput, decodeSnapshot, type SnapshotFrame } from "./protocol/codec";

async function joinRoom(code: string): Promise<WebSocket> {
  const response = await SELF.fetch(`http://room.test/room/${code}`, {
    headers: { Upgrade: "websocket" },
  });
  expect(response.status).toBe(101);
  const ws = response.webSocket;
  expect(ws).toBeTruthy();
  ws!.accept();
  return ws!;
}

function nextSnapshot(ws: WebSocket): Promise<SnapshotFrame> {
  return new Promise((resolve) => {
    ws.addEventListener(
      "message",
      (event: MessageEvent) => resolve(decodeSnapshot(new Uint8Array(event.data as ArrayBuffer))),
      { once: true },
    );
  });
}

describe("MatchRoom", () => {
  it("runs the sim and broadcasts snapshots that reflect a player's input", async () => {
    const host = await joinRoom("SIM001");
    const guest = await joinRoom("SIM001");

    // One input frame sets a persistent move intent (the sim keeps applying it each tick).
    host.send(encodeInput({ seq: 1, moveX: 1, moveY: 0, aim: 0, buttons: 0 }));

    let last: SnapshotFrame | null = null;
    for (let i = 0; i < 16; i++) {
      last = await nextSnapshot(host);
      if (last.tanks.length > 0 && last.tanks[0].x > 170) break;
    }

    expect(last).not.toBeNull();
    expect(last!.tick).toBeGreaterThan(0);
    expect(last!.tanks[0].slot).toBe(0);
    expect(last!.tanks[0].x).toBeGreaterThan(170); // moved east from the spawn (x≈160)

    host.close();
    guest.close();
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

    a.close();
    b.close();
  });
});
