// The lobby browser (multiplayer milestone, web-export.md §5): a directory of OPEN lobbies a player
// can pick from, separate from the per-code MatchRoom DOs that own live lobby state. Each room
// publishes a tiny summary of itself here; a room that fills, starts, or empties withdraws itself.
//
// The directory is a single Durable Object (one global instance) rather than KV: KV `list()` is
// eventually consistent (up to ~60s to propagate a write), which was long enough that a peer's
// just-created game wasn't visible and the Refresh button looked dead until a player fully left and
// rejoined. A DO gives read-your-writes consistency, so a publish shows on the very next list.
// ponytail: single hot DO instance — fine at family-arcade scale (a handful of concurrent lobbies);
// shard by region only if concurrent-lobby volume ever makes this instance the bottleneck.

import { MAX_PLAYERS, type GameMode, type LobbyPhase, type LobbyState } from "./lobbyState";

export interface OpenLobby {
  code: string;
  mode: GameMode;
  players: number;
  phase: LobbyPhase;
  map: string; // "" = random
}

// The narrow slice of a DurableObjectStub the directory needs, so callers are unit-testable with a
// fake that obeys the same /publish + /list contract as the real DO below.
export interface DirectoryStub {
  fetch(input: string, init?: RequestInit): Promise<Response>;
}

// Any origin works — a DO stub ignores the host and routes on path only; this keeps the URL valid.
const DIRECTORY_ORIGIN = "http://directory";

/** A room's browser summary, or null when it should NOT be listed (empty, full, or already playing —
 * only a waiting lobby with a free seat is joinable from the browser). */
export function summarize(state: LobbyState, code: string): OpenLobby | null {
  const joinable = state.phase === "waiting" && state.players.length > 0 && state.players.length < MAX_PLAYERS;
  return joinable
    ? { code, mode: state.mode, players: state.players.length, phase: state.phase, map: state.map }
    : null;
}

/** Publish (or withdraw) a room's summary so the browser reflects its current joinability. */
export async function publishToDirectory(dir: DirectoryStub, state: LobbyState, code: string): Promise<void> {
  const summary = summarize(state, code);
  await dir.fetch(`${DIRECTORY_ORIGIN}/publish`, {
    method: "POST",
    body: JSON.stringify({ code, summary }),
  });
}

/** Every currently-open lobby, in one strongly-consistent read. */
export async function listOpenLobbies(dir: DirectoryStub): Promise<OpenLobby[]> {
  const response = await dir.fetch(`${DIRECTORY_ORIGIN}/list`);
  return (await response.json()) as OpenLobby[];
}

const ENTRIES_KEY = "entries";

/** The single directory instance. MatchRooms POST /publish their summary on every state change; the
 * /lobbies route GETs /list. Storage survives hibernation, so the browser stays populated across
 * evictions. */
export class LobbyDirectory implements DurableObject {
  private readonly state: DurableObjectState;
  private entries: Record<string, OpenLobby> = {};

  constructor(state: DurableObjectState) {
    this.state = state;
    this.state.blockConcurrencyWhile(async () => {
      this.entries = (await this.state.storage.get<Record<string, OpenLobby>>(ENTRIES_KEY)) ?? {};
    });
  }

  async fetch(request: Request): Promise<Response> {
    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname === "/list") {
      return Response.json(Object.values(this.entries));
    }

    if (request.method === "POST" && url.pathname === "/publish") {
      const { code, summary } = (await request.json()) as { code: string; summary: OpenLobby | null };
      if (summary) {
        this.entries[code] = summary;
      } else {
        delete this.entries[code];
      }
      await this.state.storage.put(ENTRIES_KEY, this.entries);
      return new Response(null, { status: 204 });
    }

    return new Response("not found", { status: 404 });
  }
}
