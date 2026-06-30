// The lobby browser (multiplayer milestone, web-export.md §5): a directory of OPEN lobbies a player
// can pick from, separate from the per-code MatchRoom DOs that own live lobby state. Each room
// publishes a tiny summary of itself into KV (as list metadata, so the browser is a single list call
// with no per-key fetch); a room that fills, starts, or empties withdraws itself. KV is eventually
// consistent (~seconds), which is fine for a browser the player refreshes.

import { MAX_PLAYERS, type GameMode, type LobbyPhase, type LobbyState } from "./lobbyState";

export const OPEN_PREFIX = "open:";

export interface OpenLobby {
  code: string;
  mode: GameMode;
  players: number;
  phase: LobbyPhase;
}

// The narrow slice of KVNamespace the directory needs, so the logic is unit-testable with a fake.
export interface LobbyMetaStore {
  list(options: { prefix: string }): Promise<{ keys: { name: string; metadata?: OpenLobby }[] }>;
  put(key: string, value: string, options: { metadata: OpenLobby }): Promise<void>;
  delete(key: string): Promise<void>;
}

/** A room's browser summary, or null when it should NOT be listed (empty, full, or already playing —
 * only a waiting lobby with a free seat is joinable from the browser). */
export function summarize(state: LobbyState, code: string): OpenLobby | null {
  const joinable = state.phase === "waiting" && state.players.length > 0 && state.players.length < MAX_PLAYERS;
  return joinable
    ? { code, mode: state.mode, players: state.players.length, phase: state.phase }
    : null;
}

/** Publish (or withdraw) a room's summary so the browser reflects its current joinability. */
export async function publishLobby(store: LobbyMetaStore, state: LobbyState, code: string): Promise<void> {
  const summary = summarize(state, code);
  if (summary) {
    await store.put(OPEN_PREFIX + code, "1", { metadata: summary });
  } else {
    await store.delete(OPEN_PREFIX + code);
  }
}

/** Every currently-open lobby, newest-room ordering left to KV. Reads the summaries straight from the
 * list metadata — one call, no per-key gets. */
export async function listOpenLobbies(store: LobbyMetaStore): Promise<OpenLobby[]> {
  const listed = await store.list({ prefix: OPEN_PREFIX });
  return listed.keys.map((k) => k.metadata).filter((m): m is OpenLobby => m !== undefined);
}
