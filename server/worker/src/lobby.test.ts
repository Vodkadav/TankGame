import { SELF } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import { createLobby, joinLobby, randomLobbyCode, type LobbyStore, type RoomIdSource } from "./lobby";

class FakeStore implements LobbyStore {
  readonly map = new Map<string, string>();
  async get(key: string): Promise<string | null> {
    return this.map.get(key) ?? null;
  }
  async put(key: string, value: string): Promise<void> {
    this.map.set(key, value);
  }
}

const fakeRooms: RoomIdSource = { idFromName: (name) => ({ toString: () => `do-${name}` }) };

describe("lobby registry", () => {
  it("retries on a code collision until it finds a free code", async () => {
    const store = new FakeStore();
    store.map.set("lobby:AAAAAA", "do-AAAAAA"); // already taken
    const codes = ["AAAAAA", "BBBBBB"];
    let i = 0;

    const result = await createLobby(store, fakeRooms, () => codes[i++]);

    expect(result.code).toBe("BBBBBB");
    expect(result.doId).toBe("do-BBBBBB");
  });

  it("stores the new lobby so it can be joined", async () => {
    const store = new FakeStore();

    await createLobby(store, fakeRooms, () => "CDEFGH");

    expect(await joinLobby(store, "CDEFGH")).toEqual({ code: "CDEFGH", wsUrl: "/room/CDEFGH" });
  });

  it("returns null when joining an unknown lobby", async () => {
    expect(await joinLobby(new FakeStore(), "ZZZZZZ")).toBeNull();
  });

  it("throws if no free code is found within the attempt budget", async () => {
    const store = new FakeStore();
    store.map.set("lobby:AAAAAA", "x");
    await expect(createLobby(store, fakeRooms, () => "AAAAAA", 3)).rejects.toThrow();
  });

  it("generates 6-character codes with no ambiguous characters", () => {
    const code = randomLobbyCode();
    expect(code).toHaveLength(6);
    expect(code).not.toMatch(/[OI01]/);
  });

  it("POST /lobby returns a code, and that lobby can then be joined", async () => {
    const created = await SELF.fetch("http://lobby.test/lobby", { method: "POST" });
    expect(created.status).toBe(201);
    const body = (await created.json()) as { code: string; doId: string };
    expect(body.code).toHaveLength(6);

    const joined = await SELF.fetch(`http://lobby.test/lobby/${body.code}/join`, { method: "POST" });
    expect(joined.status).toBe(200);
    expect((await joined.json()) as { wsUrl: string }).toEqual({ code: body.code, wsUrl: `/room/${body.code}` });
  });

  it("POST /lobby/:code/join 404s for an unknown lobby", async () => {
    const res = await SELF.fetch("http://lobby.test/lobby/ZZZZZZ/join", { method: "POST" });
    expect(res.status).toBe(404);
  });
});
