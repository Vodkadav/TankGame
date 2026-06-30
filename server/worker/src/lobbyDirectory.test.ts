import { describe, it, expect } from "vitest";
import {
  summarize,
  publishLobby,
  listOpenLobbies,
  OPEN_PREFIX,
  type LobbyMetaStore,
  type OpenLobby,
} from "./lobbyDirectory";
import { emptyLobby, reduce, MAX_PLAYERS, type LobbyState } from "./lobbyState";

class FakeMetaStore implements LobbyMetaStore {
  readonly map = new Map<string, OpenLobby>();
  async list(options: { prefix: string }) {
    return {
      keys: [...this.map.entries()]
        .filter(([name]) => name.startsWith(options.prefix))
        .map(([name, metadata]) => ({ name, metadata })),
    };
  }
  async put(key: string, _value: string, options: { metadata: OpenLobby }) {
    this.map.set(key, options.metadata);
  }
  async delete(key: string) {
    this.map.delete(key);
  }
}

function lobbyWith(players: number, mode: "ffa" | "team" = "ffa"): LobbyState {
  let state = emptyLobby(mode);
  for (let slot = 0; slot < players; slot++) {
    state = reduce(state, { type: "join", slot, name: `P${slot}` });
  }
  return state;
}

describe("lobby directory", () => {
  it("summarizes a joinable waiting lobby", () => {
    expect(summarize(lobbyWith(2, "team"), "ABCDEF")).toEqual({
      code: "ABCDEF",
      mode: "team",
      players: 2,
      phase: "waiting",
    });
  });

  it("does not list an empty lobby", () => {
    expect(summarize(emptyLobby(), "EMPTY1")).toBeNull();
  });

  it("does not list a full lobby", () => {
    expect(summarize(lobbyWith(MAX_PLAYERS), "FULL01")).toBeNull();
  });

  it("does not list a lobby that has started counting down", () => {
    const counting = reduce(lobbyWith(2), { type: "start", slot: 0 });
    expect(summarize(counting, "GO0001")).toBeNull();
  });

  it("publishes a joinable lobby and withdraws it once it is no longer joinable", async () => {
    const store = new FakeMetaStore();

    await publishLobby(store, lobbyWith(2), "ROOM01");
    expect(store.map.has(OPEN_PREFIX + "ROOM01")).toBe(true);

    // Filling the room withdraws it from the browser.
    await publishLobby(store, lobbyWith(MAX_PLAYERS), "ROOM01");
    expect(store.map.has(OPEN_PREFIX + "ROOM01")).toBe(false);
  });

  it("lists every open lobby's summary from a single metadata read", async () => {
    const store = new FakeMetaStore();
    await publishLobby(store, lobbyWith(2, "ffa"), "AAAAAA");
    await publishLobby(store, lobbyWith(3, "team"), "BBBBBB");
    store.map.set("lobby:CCCCCC", { code: "x", mode: "ffa", players: 0, phase: "waiting" }); // non-open key ignored

    const open = await listOpenLobbies(store);
    expect(open.map((l) => l.code).sort()).toEqual(["AAAAAA", "BBBBBB"]);
    expect(open.find((l) => l.code === "BBBBBB")).toMatchObject({ mode: "team", players: 3 });
  });
});
