import { SELF } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import { encodeInput, decodeInput } from "./protocol/codec";

// Opens a WebSocket to a lobby through the Worker → MatchRoom DO and returns the accepted client.
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

function nextBinaryMessage(ws: WebSocket): Promise<Uint8Array> {
  return new Promise((resolve) => {
    ws.addEventListener(
      "message",
      (event: MessageEvent) => resolve(new Uint8Array(event.data as ArrayBuffer)),
      { once: true },
    );
  });
}

describe("MatchRoom", () => {
  it("broadcasts one peer's input frame to the other peer", async () => {
    const host = await joinRoom("TEST01");
    const guest = await joinRoom("TEST01");

    const guestReceives = nextBinaryMessage(guest);
    host.send(encodeInput({ seq: 7, moveX: 1, moveY: 0, aim: 0, buttons: 1 }));

    const decoded = decodeInput(await guestReceives);
    expect(decoded.seq).toBe(7);
    expect(decoded.moveX).toBe(1);
  });

  it("rejects a non-WebSocket request to a room", async () => {
    const response = await SELF.fetch("http://room.test/room/TEST01");
    expect(response.status).toBe(426);
  });
});
