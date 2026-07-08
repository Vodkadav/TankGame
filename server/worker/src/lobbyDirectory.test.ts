import { env } from "cloudflare:test";
import { describe, it, expect } from "vitest";
import {
  summarize,
  publishToDirectory,
  listOpenLobbies,
  type DirectoryStub,
  type OpenLobby,
} from "./lobbyDirectory";
import { emptyLobby, reduce, MAX_PLAYERS, type LobbyState } from "./lobbyState";

// An honest fake of the LobbyDirectory DO: same /publish + /list contract as the real class, so a
// test that passes against it would pass against the deployed DO. (Rule: honest fakes over mocks.)
class FakeDirectory implements DirectoryStub {
  readonly entries = new Map<string, OpenLobby>();
  async fetch(input: string, init?: RequestInit): Promise<Response> {
    const url = new URL(input);
    if (init?.method === "POST" && url.pathname === "/publish") {
      const { code, summary } = JSON.parse(init.body as string) as { code: string; summary: OpenLobby | null };
      if (summary) {
        this.entries.set(code, summary);
      } else {
        this.entries.delete(code);
      }
      return new Response(null, { status: 204 });
    }
    if (url.pathname === "/list") {
      return Response.json([...this.entries.values()]);
    }
    return new Response("not found", { status: 404 });
  }
}

function lobbyWith(players: number, mode: "ffa" | "team" = "ffa"): LobbyState {
  let state = emptyLobby(mode);
  for (let slot = 0; slot < players; slot++) {
    state = reduce(state, { type: "join", slot, name: `P${slot}` });
  }
  return state;
}

describe("lobby directory summary", () => {
  it("summarizes a joinable waiting lobby", () => {
    expect(summarize(lobbyWith(2, "team"), "ABCDEF")).toEqual({
      code: "ABCDEF",
      mode: "team",
      players: 2,
      phase: "waiting",
      map: "",
    });
  });

  it("the summary carries the room's picked map for the browser row", () => {
    const state = reduce(lobbyWith(2), { type: "setMap", slot: 0, map: "DesertWar" });
    expect(summarize(state, "MAP001")?.map).toBe("DesertWar");
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
});

describe("directory publish/list against a fake stub", () => {
  it("publishes a joinable lobby and withdraws it once it is no longer joinable", async () => {
    const dir = new FakeDirectory();

    await publishToDirectory(dir, lobbyWith(2), "ROOM01");
    expect(dir.entries.has("ROOM01")).toBe(true);

    // Filling the room withdraws it from the browser.
    await publishToDirectory(dir, lobbyWith(MAX_PLAYERS), "ROOM01");
    expect(dir.entries.has("ROOM01")).toBe(false);
  });

  it("lists every open lobby's summary", async () => {
    const dir = new FakeDirectory();
    await publishToDirectory(dir, lobbyWith(2, "ffa"), "AAAAAA");
    await publishToDirectory(dir, lobbyWith(3, "team"), "BBBBBB");

    const open = await listOpenLobbies(dir);
    expect(open.map((l) => l.code).sort()).toEqual(["AAAAAA", "BBBBBB"]);
    expect(open.find((l) => l.code === "BBBBBB")).toMatchObject({ mode: "team", players: 3 });
  });
});

describe("LobbyDirectory Durable Object (real, strongly consistent)", () => {
  function directoryStub(): DirectoryStub {
    return env.LOBBY_DIRECTORY.get(env.LOBBY_DIRECTORY.idFromName("test-global")) as unknown as DirectoryStub;
  }

  it("a published lobby is visible on the very next list (read-your-writes, unlike KV)", async () => {
    const dir = directoryStub();
    await publishToDirectory(dir, lobbyWith(2, "team"), "LIVE01");

    const open = await listOpenLobbies(dir);
    expect(open.find((l) => l.code === "LIVE01")).toMatchObject({ mode: "team", players: 2 });
  });
});
